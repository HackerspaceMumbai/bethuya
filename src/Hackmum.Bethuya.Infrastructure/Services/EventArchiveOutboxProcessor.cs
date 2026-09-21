using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;
using ArchiveEventId = Hackmum.Bethuya.Core.ValueObjects.EventId;

namespace Hackmum.Bethuya.Infrastructure.Services;

internal sealed partial class EventArchiveOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<EventArchiveOutboxProcessor> logger) : BackgroundService
{
    private const int MaxAttemptsBeforeFailureIsTerminal = 8;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessOneAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogArchiveProjectionFailed(logger, ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogOutboxProcessorStopped(logger);
        }
    }

    private async Task ProcessOneAsync(CancellationToken ct)
    {
        EventArchiveOutboxWorkItem? workItem = null;
        using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task? heartbeatTask = null;
        var claimLost = false;
        try
        {
            workItem = await ClaimNextAsync(ct);
            if (workItem is null)
            {
                return;
            }

            // Renew the claim lease periodically while the external publish is in flight.
            // If renewal ever finds the claim has been taken over (lease expired and another
            // worker reclaimed the message), cancel the publish so a slow, stale writer can
            // never overwrite content a newer claimant has already published.
            heartbeatTask = RunClaimHeartbeatAsync(workItem, publishCts, () => claimLost = true);

            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGitHubEventRepository>();
            var result = await repository.PublishEventAsync(
                new(
                    workItem.EventId,
                    workItem.FolderPath,
                    workItem.FolderPath,
                    workItem.ReadmeMarkdown,
                    workItem.MetadataJson,
                    workItem.IdempotencyKey),
                publishCts.Token);

            await StopHeartbeatAsync(publishCts, heartbeatTask);
            heartbeatTask = null;

            if (claimLost)
            {
                return;
            }

            var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
            var strategy = db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                // Atomically mark the message processed only if the claim token still matches and
                // the message is not yet processed. This prevents a stale publish from overwriting
                // a newer claimant's completion state.
                var updatedRows = await db.EventArchiveOutboxMessages
                    .Where(item => item.Id == workItem.Id
                        && item.ClaimToken == workItem.ClaimToken
                        && item.ProcessedAt == null)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(item => item.ProcessedAt, DateTimeOffset.UtcNow)
                            .SetProperty(item => item.LockedUntil, (DateTimeOffset?)null)
                            .SetProperty(item => item.LastError, (string?)null),
                        ct);

                if (updatedRows == 0)
                {
                    // Claim was lost during publication; another worker has reclaimed it.
                    return;
                }

                // Update the event's archive folder URL. This must happen after the conditional
                // message update succeeds, within the same execution strategy transaction.
                var evt = await db.Events.FirstOrDefaultAsync(item => item.Id == workItem.EventId.Value, ct);
                if (evt is not null)
                {
                    evt.GitHubFolderUrl = result.FolderUrl;
                    await db.SaveChangesAsync(ct);
                }

                LogArchiveProjectionCompleted(logger, workItem.EventId.Value, result.FolderUrl);
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (claimLost)
        {
            // The publish was deliberately cancelled because another worker reclaimed the
            // message; that worker owns the outcome, so there is nothing further to record.
        }
        catch (Exception ex)
        {
            if (workItem is not null && !claimLost)
            {
                try
                {
                    await RecordFailureAsync(workItem, ex, ct);
                }
                catch (Exception recordEx)
                {
                    LogArchiveProjectionFailed(logger, recordEx);
                }
            }

            LogArchiveProjectionFailed(logger, ex);
        }
        finally
        {
            await StopHeartbeatAsync(publishCts, heartbeatTask);
        }
    }

    private async Task RunClaimHeartbeatAsync(
        EventArchiveOutboxWorkItem workItem,
        CancellationTokenSource publishCts,
        Action onClaimLost)
    {
        try
        {
            using var timer = new PeriodicTimer(HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(publishCts.Token))
            {
                var renewed = await TryRenewClaimAsync(workItem, publishCts.Token);
                if (!renewed)
                {
                    onClaimLost();
                    await publishCts.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown of the heartbeat once the publish completes or is cancelled.
        }
        catch (Exception ex)
        {
            // A transient failure renewing the lease should not crash the publish in flight or
            // the outer processing loop; worst case the lease can expire and be reclaimed,
            // which is the pre-heartbeat behavior this method exists to reduce, not eliminate.
            LogClaimHeartbeatFailed(logger, ex);
            onClaimLost();
            await publishCts.CancelAsync();
        }
    }

    /// <summary>
    /// Attempts to extend the lease for the message identified by <paramref name="workItem"/>, but only while
    /// its claim token still matches and it has not been processed by another worker. Returns <see langword="false"/>
    /// when the claim has been lost (the lease was not renewed because another worker already reclaimed or completed it).
    /// </summary>
    private async Task<bool> TryRenewClaimAsync(EventArchiveOutboxWorkItem workItem, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
        var renewedRows = await db.EventArchiveOutboxMessages
            .Where(item => item.Id == workItem.Id
                && item.ClaimToken == workItem.ClaimToken
                && item.ProcessedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.LockedUntil, DateTimeOffset.UtcNow.Add(LeaseDuration)),
                ct);

        return renewedRows > 0;
    }

    private static async Task StopHeartbeatAsync(CancellationTokenSource publishCts, Task? heartbeatTask)
    {
        if (heartbeatTask is null)
        {
            return;
        }

        if (!publishCts.IsCancellationRequested)
        {
            await publishCts.CancelAsync();
        }

        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
            // Expected once cancellation is requested to stop the heartbeat loop.
        }
    }

    private async Task<EventArchiveOutboxWorkItem?> ClaimNextAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
        var strategy = db.Database.CreateExecutionStrategy();
        EventArchiveOutboxWorkItem? result = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var now = DateTimeOffset.UtcNow;
            
            // Fetch unprocessed messages (server-side only, simpler predicate).
            // SQLite LINQ translation is limited, so we'll filter all other conditions on the client.
            // AsNoTracking for performance; we'll update messages explicitly if claimed.
            var candidates = await db.EventArchiveOutboxMessages
                .AsNoTracking()
                .Where(m => m.ProcessedAt == null)
                .Take(100)
                .ToListAsync(ct);

            // Filter for available and non-locked messages, and order by creation time on the client.
            candidates = candidates
                .Where(m => m.AvailableAt <= now && (m.LockedUntil == null || m.LockedUntil < now))
                .OrderBy(m => m.CreatedAt)
                .ToList();

            if (candidates.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                result = null;
                return;
            }

            // Fetch only ordering fields once to check for ordering constraints within the transaction.
            // This must be done within the same transaction at Serializable isolation to ensure we don't claim
            // while an older message for the same event is still pending.
            var oldestPendingByEvent = await db.EventArchiveOutboxMessages
                .AsNoTracking()
                .Where(m => m.ProcessedAt == null)
                .Select(m => new { m.EventId, m.CreatedAt })
                .ToListAsync(ct);

            var oldestCreatedAtByEvent = oldestPendingByEvent
                .GroupBy(m => m.EventId.Value)
                .ToDictionary(group => group.Key, group => group.Min(m => m.CreatedAt));

            // Try each candidate in order, skipping those that have an older unprocessed message for the same event.
            // This prevents a single event's backoff from starving other events' work and improves throughput.
            EventArchiveOutboxMessage? selectedMessage = null;
            foreach (var candidate in candidates)
            {
                var olderExists = oldestCreatedAtByEvent[candidate.EventId.Value] < candidate.CreatedAt;

                if (olderExists)
                {
                    continue; // Try next candidate; this event has older pending work
                }

                selectedMessage = candidate;
                break;
            }

            if (selectedMessage is null)
            {
                // All candidates have older messages pending for their respective events.
                await transaction.RollbackAsync(ct);
                result = null;
                return;
            }

            var claimToken = Guid.NewGuid().ToString("N");
            var newAttemptCount = selectedMessage.AttemptCount + 1;
            var newLockedUntil = now.Add(LeaseDuration);
            
            // Use ExecuteUpdateAsync for atomic claim update (message was fetched as AsNoTracking)
            var updatedRows = await db.EventArchiveOutboxMessages
                .Where(m => m.Id == selectedMessage.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(m => m.ClaimToken, claimToken)
                        .SetProperty(m => m.LockedUntil, newLockedUntil)
                        .SetProperty(m => m.AttemptCount, newAttemptCount),
                    ct);

            await transaction.CommitAsync(ct);

            if (updatedRows == 0)
            {
                result = null;
                return;
            }

            result = new(
                selectedMessage.Id,
                selectedMessage.EventId,
                selectedMessage.FolderPath,
                selectedMessage.ReadmeMarkdown,
                selectedMessage.MetadataJson,
                selectedMessage.IdempotencyKey,
                newAttemptCount,
                claimToken);
        });

        return result;
    }

    private async Task RecordFailureAsync(EventArchiveOutboxWorkItem workItem, Exception exception, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
        var errorMessage = exception.Message[..Math.Min(exception.Message.Length, 4000)];
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Check if claim is still valid and message is not yet processed.
            var message = await db.EventArchiveOutboxMessages
                .FirstOrDefaultAsync(item => item.Id == workItem.Id, ct);
            if (message is null || message.ProcessedAt is not null || message.ClaimToken != workItem.ClaimToken)
            {
                return;
            }

            // Check if there's a newer projection for the same event (created after this message).
            // Use client-side evaluation to work around Vogen value-object LINQ translation limitations.
            var newerEventIdValue = message.EventId.Value;
            var newerMessageIdValue = message.Id.Value;
            var newerCreatedAtValue = message.CreatedAt;
            
            var allMessages = await db.EventArchiveOutboxMessages
                .AsNoTracking()
                .ToListAsync(ct);  // Force client evaluation
            
            var hasNewerProjection = allMessages.Any(m => 
                m.EventId.Value == newerEventIdValue && 
                m.Id.Value != newerMessageIdValue && 
                m.CreatedAt > newerCreatedAtValue);

            if (workItem.AttemptCount >= MaxAttemptsBeforeFailureIsTerminal && hasNewerProjection)
            {
                // Terminal failure with a newer projection: mark this message processed
                // using an atomic update that checks both the claim token and processed state.
                var terminalUpdateRows = await db.EventArchiveOutboxMessages
                    .Where(item => item.Id == workItem.Id
                        && item.ClaimToken == workItem.ClaimToken
                        && item.ProcessedAt == null)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(item => item.ProcessedAt, DateTimeOffset.UtcNow)
                            .SetProperty(item => item.LockedUntil, (DateTimeOffset?)null)
                            .SetProperty(item => item.ClaimToken, string.Empty)
                            .SetProperty(item => item.LastError, errorMessage)
                            .SetProperty(item => item.AvailableAt, DateTimeOffset.UtcNow),
                        ct);

                // Ignore if no rows updated; another worker may have reclaimed or completed the message.
                return;
            }

            // Retry path: schedule the next attempt with exponential backoff.
            var delay = TimeSpan.FromMinutes(Math.Min(Math.Pow(2, Math.Min(workItem.AttemptCount, 6)), 60));
            var retryUpdateRows = await db.EventArchiveOutboxMessages
                .Where(item => item.Id == workItem.Id
                    && item.ClaimToken == workItem.ClaimToken
                    && item.ProcessedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.AvailableAt, DateTimeOffset.UtcNow.Add(delay))
                        .SetProperty(item => item.LockedUntil, (DateTimeOffset?)null)
                        .SetProperty(item => item.ClaimToken, string.Empty)
                        .SetProperty(item => item.LastError, errorMessage),
                    ct);

            // Ignore if no rows updated; another worker may have reclaimed or completed the message.
        });
    }

    private sealed record EventArchiveOutboxWorkItem(
        EventArchiveOutboxMessageId Id,
        ArchiveEventId EventId,
        string FolderPath,
        string ReadmeMarkdown,
        string MetadataJson,
        string IdempotencyKey,
        int AttemptCount,
        string ClaimToken);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Archive projection completed for event {EventId}; folder {FolderUrl}.")]
    private static partial void LogArchiveProjectionCompleted(ILogger logger, Guid eventId, string folderUrl);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Archive projection attempt failed; the outbox message will be retried.")]
    private static partial void LogArchiveProjectionFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Event archive outbox processor stopped due to cancellation.")]
    private static partial void LogOutboxProcessorStopped(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Failed to renew the outbox claim lease; the publish will continue without further lease renewal.")]
    private static partial void LogClaimHeartbeatFailed(ILogger logger, Exception exception);
}

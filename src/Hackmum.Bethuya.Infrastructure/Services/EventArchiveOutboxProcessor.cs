using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Enums;
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
                    workItem.Title,
                    workItem.FolderPath,
                    workItem.ReadmeMarkdown,
                    workItem.MetadataJson,
                    workItem.IdempotencyKey,
                    workItem.ConfirmedPreviewToken),
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
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                // Atomically mark the message processed only if the claim token still matches and
                // update the event's PR state in the same transaction. This prevents a stale
                // publish from overwriting a newer claimant's completion state.
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

                await db.Events.Where(
                    item => item.Id == workItem.EventId.Value
                        && item.ArchivePublicationIdempotencyKey == workItem.IdempotencyKey)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(item => item.ArchivePublicationStatus, EventArchivePublicationStatus.DraftPrPending), ct);

                // Retain successful PR discovery independently of newer pending/failed state, in confirmation order.
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "Events"
                    SET "ArchiveBranchName" = {result.BranchName},
                        "ArchivePullRequestNumber" = {result.PullRequestNumber},
                        "ArchivePullRequestUrl" = {result.PullRequestUrl}
                    WHERE "Id" = {workItem.EventId.Value}
                        AND NOT EXISTS (
                            SELECT 1
                            FROM "EventArchiveOutboxMessages" AS newer
                            JOIN "EventArchiveOutboxMessages" AS completed ON completed."Id" = {workItem.Id.Value}
                            WHERE newer."EventId" = completed."EventId"
                                AND newer."Id" <> completed."Id"
                                AND newer."ProcessedAt" IS NOT NULL
                                AND newer."LastError" IS NULL
                                AND newer."ConfirmedPreviewToken" IS NOT NULL
                                AND (
                                    newer."CreatedAt" > completed."CreatedAt"
                                    OR (
                                        newer."CreatedAt" = completed."CreatedAt"
                                        AND newer."Id" > completed."Id"
                                    )
                                ))
                    """, ct);

                await transaction.CommitAsync(ct);
                LogArchiveProjectionCompleted(logger, workItem.EventId, result.FolderUrl);
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
        catch (ArchivePreviewMismatchException ex)
        {
            if (workItem is not null && !claimLost)
            {
                await RecordFailureAsync(workItem, ex, ct, terminal: true);
            }

            LogArchiveProjectionFailed(logger, ex);
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
            
            // SQLite cannot translate the DateTimeOffset availability comparisons, so load the lightweight
            // ordering fields first and filter eligibility on the client in the database's stable ID order.
            var orderedPending = await db.EventArchiveOutboxMessages
                .AsNoTracking()
                .Where(m => m.ProcessedAt == null && m.ConfirmedBy != string.Empty)
                .OrderBy(m => m.Id)
                .Select(m => new { m.EventId, m.Id, m.CreatedAt, m.AvailableAt, m.LockedUntil })
                .ToListAsync(ct);

            var identifierOrder = orderedPending
                .Select((message, index) => new { message.Id, Index = index })
                .ToDictionary(message => message.Id, message => message.Index);
            orderedPending = orderedPending
                .OrderBy(message => message.CreatedAt)
                .ThenBy(message => identifierOrder[message.Id])
                .ToList();

            var oldestMessageIdByEvent = orderedPending
                .GroupBy(m => m.EventId.Value)
                .ToDictionary(group => group.Key, group => group.First().Id);

            var candidateIds = orderedPending
                .Where(m => m.AvailableAt <= now && (m.LockedUntil == null || m.LockedUntil < now))
                .Take(100)
                .Select(m => m.Id)
                .ToArray();
            if (candidateIds.Length == 0)
            {
                await transaction.RollbackAsync(ct);
                result = null;
                return;
            }

            var candidatesById = await db.EventArchiveOutboxMessages
                .AsNoTracking()
                .Where(m => candidateIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, ct);
            var candidates = candidateIds.Select(id => candidatesById[id]).ToList();

            // Try each candidate in database order, skipping messages that are not oldest for their event.
            // This prevents a single event's backoff from starving other events' work and improves throughput.
            EventArchiveOutboxMessage? selectedMessage = null;
            foreach (var candidate in candidates)
            {
                if (oldestMessageIdByEvent[candidate.EventId.Value] != candidate.Id)
                {
                    continue; // Try next event; this event has older pending work.
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
                selectedMessage.Title,
                selectedMessage.ReadmeMarkdown,
                selectedMessage.MetadataJson,
                selectedMessage.IdempotencyKey,
                selectedMessage.ConfirmedPreviewToken,
                newAttemptCount,
                claimToken);
        });

        return result;
    }

    private async Task RecordFailureAsync(
        EventArchiveOutboxWorkItem workItem,
        Exception exception,
        CancellationToken ct,
        bool terminal = false)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
        var errorMessage = exception.Message[..Math.Min(exception.Message.Length, 4000)];
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // Check if claim is still valid and message is not yet processed.
            var message = await db.EventArchiveOutboxMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == workItem.Id, ct);
            if (message is null || message.ProcessedAt is not null || message.ClaimToken != workItem.ClaimToken)
            {
                await transaction.RollbackAsync(ct);
                return;
            }

            // Check if there's a later projection for the same event using the same total order as claims.
            // Use client-side evaluation to work around Vogen value-object LINQ translation limitations.
            var newerEventIdValue = message.EventId.Value;
            var messageId = message.Id;
            var messageCreatedAt = message.CreatedAt;
            
            var hasNewerProjection = false;
            if (!terminal && workItem.AttemptCount >= MaxAttemptsBeforeFailureIsTerminal)
            {
                var allMessages = await db.EventArchiveOutboxMessages
                    .AsNoTracking()
                    .OrderBy(item => item.Id)
                    .ToListAsync(ct);  // Force client evaluation
                var identifierOrder = allMessages
                    .Select((item, index) => new { item.Id, Index = index })
                    .ToDictionary(item => item.Id, item => item.Index);

                hasNewerProjection = allMessages.Any(m =>
                    m.EventId.Value == newerEventIdValue &&
                    m.Id != messageId
                    && (m.CreatedAt > messageCreatedAt
                        || (m.CreatedAt == messageCreatedAt
                            && identifierOrder[m.Id] > identifierOrder[messageId])));
            }

            if (terminal || (workItem.AttemptCount >= MaxAttemptsBeforeFailureIsTerminal && hasNewerProjection))
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

                if (terminalUpdateRows > 0)
                {
                    await db.Events.Where(
                        item => item.Id == workItem.EventId.Value
                            && item.ArchivePublicationIdempotencyKey == workItem.IdempotencyKey)
                        .ExecuteUpdateAsync(update => update
                            .SetProperty(item => item.ArchivePublicationStatus, EventArchivePublicationStatus.Failed), ct);
                }

                await transaction.CommitAsync(ct);
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
            await transaction.CommitAsync(ct);
        });
    }

    private sealed record EventArchiveOutboxWorkItem(
        EventArchiveOutboxMessageId Id,
        ArchiveEventId EventId,
        string FolderPath,
        string Title,
        string ReadmeMarkdown,
        string MetadataJson,
        string IdempotencyKey,
        string? ConfirmedPreviewToken,
        int AttemptCount,
        string ClaimToken);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Archive projection completed for event {EventId}; folder {FolderUrl}.")]
    private static partial void LogArchiveProjectionCompleted(ILogger logger, ArchiveEventId eventId, string folderUrl);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Archive projection attempt failed.")]
    private static partial void LogArchiveProjectionFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Event archive outbox processor stopped due to cancellation.")]
    private static partial void LogOutboxProcessorStopped(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Failed to renew the outbox claim lease; the publish will continue without further lease renewal.")]
    private static partial void LogClaimHeartbeatFailed(ILogger logger, Exception exception);
}

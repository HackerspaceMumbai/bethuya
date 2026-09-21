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
        try
        {
            workItem = await ClaimNextAsync(ct);
            if (workItem is null)
            {
                return;
            }

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
                ct);

            var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
            var message = await db.EventArchiveOutboxMessages
                .FirstAsync(item => item.Id == workItem.Id, ct);
            message.ProcessedAt = DateTimeOffset.UtcNow;
            message.LockedUntil = null;
            message.LastError = null;
            var evt = await db.Events.FirstOrDefaultAsync(item => item.Id == message.EventId.Value, ct);
            if (evt is not null)
            {
                evt.GitHubFolderUrl = result.FolderUrl;
            }

            await db.SaveChangesAsync(ct);
            LogArchiveProjectionCompleted(logger, workItem.EventId.Value, result.FolderUrl);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (workItem is not null)
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
            var message = await db.EventArchiveOutboxMessages
                .Where(item => item.ProcessedAt == null
                    && item.AvailableAt <= now
                    && (item.LockedUntil == null || item.LockedUntil < now)
                    && !db.EventArchiveOutboxMessages.Any(older =>
                        older.EventId == item.EventId
                        && older.ProcessedAt == null
                        && older.CreatedAt < item.CreatedAt))
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (message is null)
            {
                await transaction.RollbackAsync(ct);
                result = null;
                return;
            }

            message.LockedUntil = now.Add(LeaseDuration);
            message.AttemptCount++;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            result = new(
                message.Id,
                message.EventId,
                message.FolderPath,
                message.ReadmeMarkdown,
                message.MetadataJson,
                message.IdempotencyKey,
                message.AttemptCount);
        });

        return result;
    }

    private async Task RecordFailureAsync(EventArchiveOutboxWorkItem workItem, Exception exception, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
        var message = await db.EventArchiveOutboxMessages
            .FirstOrDefaultAsync(item => item.Id == workItem.Id, ct);
        if (message is null || message.ProcessedAt is not null)
        {
            return;
        }

        message.LastError = exception.Message[..Math.Min(exception.Message.Length, 4000)];
        if (workItem.AttemptCount >= MaxAttemptsBeforeFailureIsTerminal)
        {
            message.ProcessedAt = DateTimeOffset.UtcNow;
            message.LockedUntil = null;
            message.AvailableAt = message.ProcessedAt.Value;
            await db.SaveChangesAsync(ct);
            return;
        }

        var delay = TimeSpan.FromMinutes(Math.Min(Math.Pow(2, Math.Min(workItem.AttemptCount, 6)), 60));
        message.AvailableAt = DateTimeOffset.UtcNow.Add(delay);
        message.LockedUntil = null;
        await db.SaveChangesAsync(ct);
    }

    private sealed record EventArchiveOutboxWorkItem(
        EventArchiveOutboxMessageId Id,
        ArchiveEventId EventId,
        string FolderPath,
        string ReadmeMarkdown,
        string MetadataJson,
        string IdempotencyKey,
        int AttemptCount);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Archive projection completed for event {EventId}; folder {FolderUrl}.")]
    private static partial void LogArchiveProjectionCompleted(ILogger logger, Guid eventId, string folderUrl);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Archive projection attempt failed; the outbox message will be retried.")]
    private static partial void LogArchiveProjectionFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Event archive outbox processor stopped due to cancellation.")]
    private static partial void LogOutboxProcessorStopped(ILogger logger);
}

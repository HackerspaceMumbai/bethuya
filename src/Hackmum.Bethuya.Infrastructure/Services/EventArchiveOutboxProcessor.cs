using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Hackmum.Bethuya.Infrastructure.Services;

internal sealed partial class EventArchiveOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<EventArchiveOutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessOneAsync(stoppingToken);
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
            var evt = await db.Events.FirstOrDefaultAsync(item => item.Id == message.EventId, ct);
            if (evt is not null)
            {
                evt.GitHubFolderUrl = result.FolderUrl;
            }

            await db.SaveChangesAsync(ct);
            LogArchiveProjectionCompleted(logger, workItem.EventId, result.FolderUrl);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or InvalidOperationException or TimeoutException or DbUpdateException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            if (workItem is not null)
            {
                await RecordFailureAsync(workItem, ex, ct);
            }

            LogArchiveProjectionFailed(logger, ex);
        }
    }

    private async Task<EventArchiveOutboxWorkItem?> ClaimNextAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = DateTimeOffset.UtcNow;
        var message = await db.EventArchiveOutboxMessages
            .Where(item => item.ProcessedAt == null
                && item.AvailableAt <= now
                && (item.LockedUntil == null || item.LockedUntil < now))
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (message is null)
        {
            await transaction.RollbackAsync(ct);
            return null;
        }

        message.LockedUntil = now.Add(LeaseDuration);
        message.AttemptCount++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new(
            message.Id,
            message.EventId,
            message.FolderPath,
            message.ReadmeMarkdown,
            message.MetadataJson,
            message.IdempotencyKey,
            message.AttemptCount);
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

        var delay = TimeSpan.FromMinutes(Math.Min(Math.Pow(2, Math.Min(workItem.AttemptCount, 6)), 60));
        message.AvailableAt = DateTimeOffset.UtcNow.Add(delay);
        message.LockedUntil = null;
        message.LastError = exception.Message[..Math.Min(exception.Message.Length, 4000)];
        await db.SaveChangesAsync(ct);
    }

    private sealed record EventArchiveOutboxWorkItem(
        Guid Id,
        Guid EventId,
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

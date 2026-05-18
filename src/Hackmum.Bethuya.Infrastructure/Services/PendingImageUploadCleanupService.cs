using Hackmum.Bethuya.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>Deletes direct uploads that were never attached to a saved event.</summary>
public sealed partial class PendingImageUploadCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<CloudinaryOptions> options,
    ILogger<PendingImageUploadCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(options.Value.PendingUploadCleanupIntervalMinutes, 5));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var imageUploadService = scope.ServiceProvider.GetRequiredService<IImageUploadService>();
                var cleanedCount = await imageUploadService.CleanupExpiredPendingUploadsAsync(stoppingToken);

                if (cleanedCount > 0)
                {
                    LogDeletedExpiredPendingUploads(logger, cleanedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogPendingUploadCleanupIterationFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Deleted {Count} expired pending image upload(s).")]
    private static partial void LogDeletedExpiredPendingUploads(ILogger logger, int count);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Pending image upload cleanup iteration failed; will retry on next interval.")]
    private static partial void LogPendingUploadCleanupIterationFailed(ILogger logger, Exception exception);
}

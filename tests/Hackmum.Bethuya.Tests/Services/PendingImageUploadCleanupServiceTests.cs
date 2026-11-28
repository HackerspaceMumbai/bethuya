using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class PendingImageUploadCleanupServiceTests
{
    [Test]
    public async Task ExecuteAsync_CleanupThrowsDatabaseError_KeepsRunningOnLaterTicks()
    {
        var calls = 0;
        var thirdCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var imageUploadService = Substitute.For<IImageUploadService>();
        imageUploadService.CleanupExpiredPendingUploadsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
            {
                if (Interlocked.Increment(ref calls) == 3)
                {
                    thirdCall.TrySetResult();
                }

                return Task.FromException<int>(new DbUpdateException("Simulated database failure."));
            });
        var services = new ServiceCollection();
        services.AddScoped(_ => imageUploadService);
        await using var provider = services.BuildServiceProvider();
        using var service = new PendingImageUploadCleanupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CloudinaryOptions()),
            new FastTimeProvider(),
            NullLogger<PendingImageUploadCleanupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.WhenAny(thirdCall.Task, service.ExecuteTask!, Task.Delay(TimeSpan.FromSeconds(10)));
        var faulted = service.ExecuteTask!.IsFaulted;
        await service.StopAsync(CancellationToken.None);

        await Assert.That(faulted).IsFalse();
        await Assert.That(Volatile.Read(ref calls)).IsGreaterThanOrEqualTo(3);
    }

    /// <summary>Fires every timer after a few milliseconds so the cleanup loop does not wait for its real interval.</summary>
    private sealed class FastTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => new FastTimer(base.CreateTimer(callback, state, Shorten(dueTime), Shorten(period)));

        private static TimeSpan Shorten(TimeSpan value)
            => value == Timeout.InfiniteTimeSpan || value == TimeSpan.Zero ? value : TimeSpan.FromMilliseconds(10);

        private sealed class FastTimer(ITimer inner) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(Shorten(dueTime), Shorten(period));

            public void Dispose() => inner.Dispose();

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}

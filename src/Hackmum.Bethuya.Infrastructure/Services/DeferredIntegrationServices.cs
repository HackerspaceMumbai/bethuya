using Hackmum.Bethuya.Core.Services;

namespace Hackmum.Bethuya.Infrastructure.Services;

public sealed class NoOpTeamsNotificationService : ITeamsNotificationService
{
    public Task NotifyScheduleChangedAsync(Guid eventId, string reason, CancellationToken ct = default)
        => Task.CompletedTask;
}

public sealed class NoOpLumaRegistrationService : ILumaRegistrationService
{
    public Task<string?> GetRegistrationUrlAsync(Guid eventId, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}

public sealed class NoOpWebCacheInvalidationService : IWebCacheInvalidationService
{
    public Task InvalidateEventAsync(Guid eventId, CancellationToken ct = default)
        => Task.CompletedTask;
}

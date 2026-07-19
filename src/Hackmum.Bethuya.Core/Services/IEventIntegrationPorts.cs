namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Sends organizer notifications for lifecycle operations.
/// </summary>
public interface ITeamsNotificationService
{
    Task NotifyScheduleChangedAsync(Guid eventId, string reason, CancellationToken ct = default);
}

/// <summary>
/// Synchronizes registration platform state for published events.
/// </summary>
public interface ILumaRegistrationService
{
    Task<string?> GetRegistrationUrlAsync(Guid eventId, CancellationToken ct = default);
}

/// <summary>
/// Invalidates public event cache entries.
/// </summary>
public interface IWebCacheInvalidationService
{
    Task InvalidateEventAsync(Guid eventId, CancellationToken ct = default);
}

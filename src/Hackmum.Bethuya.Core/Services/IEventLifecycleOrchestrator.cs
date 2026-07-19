using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Coordinates event lifecycle changes and external side effects.
/// </summary>
public interface IEventLifecycleOrchestrator
{
    Task<EventLifecycleOperationResult> TransitionAsync(
        Guid eventId,
        MeetupLifecycleState targetState,
        string actor,
        CancellationToken ct = default);

    Task<EventLifecycleOperationResult> PublishAsync(
        Guid eventId,
        string actor,
        string? registrationUrl,
        CancellationToken ct = default);

    Task<EventLifecycleOperationResult> AlterScheduleAsync(
        Guid eventId,
        string actor,
        string reason,
        CancellationToken ct = default);

    Task<EventLifecycleOperationResult> CompleteAsync(
        Guid eventId,
        string actor,
        DateTimeOffset? assetDueAt,
        CancellationToken ct = default);

    Task<EventLifecycleOperationResult> ArchiveAsync(
        Guid eventId,
        string actor,
        bool overrideMissingAssets,
        CancellationToken ct = default);
}

/// <summary>
/// Result returned by lifecycle orchestration operations.
/// </summary>
public sealed record EventLifecycleOperationResult(
    Guid EventId,
    MeetupLifecycleState LifecycleState,
    string? GitHubFolderUrl,
    string? RegistrationUrl,
    string Message);

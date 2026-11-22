using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

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

    /// <summary>Reads archive content and previews a candidate without queuing or remote writes.</summary>
    Task<EventArchivePublicationPreview> PreviewArchivePublicationAsync(
        Guid eventId,
        CancellationToken ct = default);

    /// <summary>Verifies the preview and queues a durable projection attributed to the authenticated actor.</summary>
    Task<EventLifecycleOperationResult> ConfirmArchivePublicationAsync(
        Guid eventId,
        string actor,
        string previewToken,
        CancellationToken ct = default);

    /// <summary>Validates identity and previews the exact supplied archive path without adopting it.</summary>
    Task<EventArchivePublicationPreview> PreviewArchiveAdoptionAsync(
        Guid eventId,
        string folderPath,
        CancellationToken ct = default);

    /// <summary>Confirms the reviewed path and saves adoption provenance without queuing publication.</summary>
    Task<EventArchiveAdoptionResult> ConfirmArchiveAdoptionAsync(
        Guid eventId,
        string folderPath,
        string previewToken,
        string actor,
        CancellationToken ct = default);

    /// <summary>Reads PR state on demand and atomically saves it only if the publication identity is unchanged.</summary>
    Task<EventArchivePullRequestStatus> RefreshArchivePublicationStatusAsync(
        Guid eventId,
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

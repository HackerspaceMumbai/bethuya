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

    Task<EventArchivePublicationPreview> PreviewArchivePublicationAsync(
        Guid eventId,
        CancellationToken ct = default);

    Task<EventLifecycleOperationResult> ConfirmArchivePublicationAsync(
        Guid eventId,
        string actor,
        string previewToken,
        CancellationToken ct = default);

    Task<EventArchivePublicationPreview> PreviewArchiveAdoptionAsync(
        Guid eventId,
        string folderPath,
        CancellationToken ct = default);

    Task<EventArchiveAdoptionResult> ConfirmArchiveAdoptionAsync(
        Guid eventId,
        string folderPath,
        string previewToken,
        string actor,
        CancellationToken ct = default);

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

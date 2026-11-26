using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Publishes event artifacts to a GitHub-backed repository.
/// </summary>
public interface IGitHubEventRepository
{
    Task<EventArchiveEntry?> GetArchiveEntryAsync(string folderPath, CancellationToken ct = default);

    Task<EventArchivePreview> PreviewEventAsync(EventPublicationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a preview from an archive entry already fetched at a specific repository revision.
    /// </summary>
    Task<EventArchivePreview> PreviewEventWithEntryAsync(
        EventPublicationRequest request,
        EventArchiveEntry entry,
        CancellationToken ct = default);

    Task<EventPublicationResult> PublishEventAsync(EventPublicationRequest request, CancellationToken ct = default);

    Task<EventArchivePullRequestStatus> GetPublicationStatusAsync(
        int pullRequestNumber,
        string folderPath,
        CancellationToken ct = default);
}

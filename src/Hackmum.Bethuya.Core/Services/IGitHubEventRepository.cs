using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Publishes event artifacts to a GitHub-backed repository.
/// </summary>
public interface IGitHubEventRepository
{
    Task<EventPublicationResult> PublishEventAsync(EventPublicationRequest request, CancellationToken ct = default);
}

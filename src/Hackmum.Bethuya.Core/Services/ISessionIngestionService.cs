using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Imports normalized sessions into an event agenda.
/// </summary>
public interface ISessionIngestionService
{
    Task<IReadOnlyList<NormalizedSession>> PreviewSessionizeAsync(Guid eventId, CancellationToken ct = default);

    Task<int> ImportSessionizeAsync(Guid eventId, CancellationToken ct = default);
}

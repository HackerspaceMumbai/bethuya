using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Retrieves sessions from Sessionize for a configured external event.
/// </summary>
public interface ISessionizeService
{
    Task<IReadOnlyList<NormalizedSession>> GetSessionsAsync(string sessionizeEventId, CancellationToken ct = default);
}

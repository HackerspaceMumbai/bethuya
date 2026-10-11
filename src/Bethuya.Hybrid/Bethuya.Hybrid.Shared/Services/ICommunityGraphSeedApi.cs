using Refit;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Development-only seed operations used by the Aspire dashboard.</summary>
public interface ICommunityGraphSeedApi
{
    /// <summary>Provisions the canonical development personas before graph exploration.</summary>
    [Post("/api/dev/community-simulation/seed")]
    Task SeedPersonasAsync([Header("X-Bethuya-Dev-Persona")] string persona, CancellationToken ct = default);

    /// <summary>Seeds repeatable fictional verified graph participation.</summary>
    [Post("/api/dev/community-graph/seed")]
    Task SeedGraphAsync([Header("X-Bethuya-Dev-Persona")] string persona, CancellationToken ct = default);
}

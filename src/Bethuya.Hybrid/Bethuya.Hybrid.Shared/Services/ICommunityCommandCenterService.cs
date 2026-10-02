using Bethuya.Hybrid.Shared.Models.CommandCenter;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Provides the command-center projection for a resolved display audience and mode.</summary>
public interface ICommunityCommandCenterService
{
    /// <summary>Gets a cohesive, display-ready command-center projection.</summary>
    Task<CommunityCommandCenter> GetAsync(
        CommunityRole role,
        CommandCenterMode? modeOverride = null,
        CancellationToken cancellationToken = default);
}

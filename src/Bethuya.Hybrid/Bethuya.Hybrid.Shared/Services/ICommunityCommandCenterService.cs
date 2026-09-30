using Bethuya.Hybrid.Shared.Models.CommandCenter;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Provides the organizer command-center projection for a selected role and mode.</summary>
public interface ICommunityCommandCenterService
{
    /// <summary>Gets a cohesive, display-ready command-center projection.</summary>
    Task<CommunityCommandCenter> GetAsync(
        CommunityRole role,
        CommandCenterMode? modeOverride = null,
        CancellationToken cancellationToken = default);
}

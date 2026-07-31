namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Sensitive-data routing strategy for a member profile.
/// </summary>
public enum SensitiveDataResidencyMode
{
    /// <summary>
    /// Data remains in the community's configured sovereign region.
    /// </summary>
    SovereignRegion,
    /// <summary>
    /// Data is constrained to the member's jurisdiction.
    /// </summary>
    JurisdictionLocked,
    /// <summary>
    /// Data is processed only on local/offline infrastructure.
    /// </summary>
    LocalOnly
}

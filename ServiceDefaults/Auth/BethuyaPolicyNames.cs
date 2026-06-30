namespace ServiceDefaults.Auth;

/// <summary>
/// Canonical authorization policy names. Single source of truth consumed by the backend
/// authorization registration. Mirrored by the UI-facing
/// <c>Bethuya.Hybrid.Shared.Auth.BethuyaAuthorizationPolicies</c> (guarded by a parity test).
/// </summary>
public static class BethuyaPolicyNames
{
    /// <summary>Requires the <see cref="BethuyaRoleNames.Admin"/> role.</summary>
    public const string RequireAdmin = nameof(RequireAdmin);

    /// <summary>Requires <see cref="BethuyaRoleNames.Organizer"/> (or <see cref="BethuyaRoleNames.Admin"/>).</summary>
    public const string RequireOrganizer = nameof(RequireOrganizer);

    /// <summary>Requires <see cref="BethuyaRoleNames.Curator"/> (or <see cref="BethuyaRoleNames.Admin"/>).</summary>
    public const string RequireCurator = nameof(RequireCurator);

    /// <summary>Requires any authenticated platform role.</summary>
    public const string RequireAttendee = nameof(RequireAttendee);
}

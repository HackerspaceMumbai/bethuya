namespace Bethuya.Hybrid.Shared.Auth;

/// <summary>Authorization policy name constants shared across Web and Backend projects.</summary>
public static class BethuyaAuthorizationPolicies
{
    /// <summary>Requires the user to be in the <see cref="BethuyaRoles.Organizer"/> role.</summary>
    public const string RequireOrganizer = nameof(RequireOrganizer);

    /// <summary>Requires the user to be in the <see cref="BethuyaRoles.Curator"/> role.</summary>
    public const string RequireCurator = nameof(RequireCurator);

    /// <summary>Requires the user to be in the <see cref="BethuyaRoles.Organizer"/> or <see cref="BethuyaRoles.Curator"/> role.</summary>
    public const string RequireOrganizerOrCurator = nameof(RequireOrganizerOrCurator);

    /// <summary>Requires connector-ingestion authority via role or explicit ingestion claim.</summary>
    public const string RequireConnectorIngestion = nameof(RequireConnectorIngestion);

    /// <summary>Requires the user to be in the <see cref="BethuyaRoles.Attendee"/> role.</summary>
    public const string RequireAttendee = nameof(RequireAttendee);

    /// <summary>Requires the user to be in the <see cref="BethuyaRoles.Admin"/> role.</summary>
    public const string RequireAdmin = nameof(RequireAdmin);
}

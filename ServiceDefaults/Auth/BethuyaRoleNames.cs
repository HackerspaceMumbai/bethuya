namespace ServiceDefaults.Auth;

/// <summary>
/// Canonical platform role names. This is the single source of truth consumed by the backend
/// authorization registration. The UI-facing <c>Bethuya.Hybrid.Shared.Auth.BethuyaRoles</c>
/// mirrors these values (guarded by a parity test) because the Blazor client cannot reference
/// the server-side ServiceDefaults project.
/// </summary>
public static class BethuyaRoleNames
{
    /// <summary>Platform administrator with full access.</summary>
    public const string Admin = "Admin";

    /// <summary>Event organizer who manages their own events.</summary>
    public const string Organizer = "Organizer";

    /// <summary>Curator who reviews and selects attendees.</summary>
    public const string Curator = "Curator";

    /// <summary>Attendee who registers for events.</summary>
    public const string Attendee = "Attendee";
}

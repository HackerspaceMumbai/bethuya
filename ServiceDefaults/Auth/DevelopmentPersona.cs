namespace ServiceDefaults.Auth;

/// <summary>
/// An immutable development-only persona entry from the allowlisted catalog.
/// Only the opaque <see cref="Key"/> crosses the wire; the Backend constructs
/// claims exclusively from this record — the caller never supplies claims directly.
/// </summary>
public sealed record DevelopmentPersona(
    string Key,
    string Subject,
    string DisplayName,
    string Email,
    IReadOnlyList<string> Roles);

using System.Security.Claims;

namespace ServiceDefaults.Auth;

/// <summary>Shared development authentication defaults used when <c>Authentication:Provider=None</c>.</summary>
public static class DevelopmentAuthenticationDefaults
{
    /// <summary>The authentication scheme used for local development.</summary>
    public const string SchemeName = "Development";

    /// <summary>
    /// Creates the legacy fixed development principal (all four roles).
    /// Used when no persona is selected (backward-compatible default — case 1 of the
    /// three-way persona-resolution logic).
    /// </summary>
    public static ClaimsPrincipal CreatePrincipal()
    {
        var claims = new List<Claim>
        {
            new("sub", "dev-user-001"),
            new("name", "Dev User"),
            new("email", "dev@bethuya.local"),
            new("role", BethuyaRoleNames.Admin),
            new("role", BethuyaRoleNames.Organizer),
            new("role", BethuyaRoleNames.Curator),
            new("role", BethuyaRoleNames.Attendee),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: SchemeName, nameType: "name", roleType: "role");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a principal for a known allowlisted persona (case 2 of the three-way logic).
    /// Claims are derived strictly from the catalog entry — callers never supply roles or
    /// profile data directly. Uses the same claim-type conventions as <see cref="CreatePrincipal"/>.
    /// </summary>
    public static ClaimsPrincipal CreatePersonaPrincipal(DevelopmentPersona persona)
    {
        var claims = new List<Claim>
        {
            new("sub", persona.Subject),
            new("name", persona.DisplayName),
            new("email", persona.Email),
        };

        foreach (var role in persona.Roles)
        {
            claims.Add(new Claim("role", role));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: SchemeName, nameType: "name", roleType: "role");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a fail-closed principal for an unrecognized persona key (case 3 of the three-way logic).
    /// The principal is authenticated (preserving the Development scheme's "never anonymous" contract)
    /// but carries ZERO roles, so every role-gated policy (<c>RequireAdmin</c>, <c>RequireOrganizer</c>,
    /// etc.) will evaluate to 403. This prevents a tampered or unknown key from silently escalating
    /// to the fixed admin identity.
    /// </summary>
    /// <param name="suppliedKey">
    /// The unrecognized key supplied by the caller. Treated as diagnostic-only data (not markup);
    /// it is safe to include in a claim value but must only be logged via structured fields — never
    /// via string interpolation into a log message template.
    /// </param>
    public static ClaimsPrincipal CreateUnknownPersonaPrincipal(string suppliedKey)
    {
        // Do not echo the raw key into sub/email where it could be mistaken for a real identity.
        // Keep it out of claims altogether; the handler logs it as a structured field separately.
        _ = suppliedKey; // acknowledged; not placed in claims to avoid identity confusion

        var claims = new List<Claim>
        {
            new("sub", "dev-persona-unknown"),
            new("name", "Unknown Dev Persona"),
            new("email", "unknown-persona@bethuya.dev"),
            // Intentionally zero role claims — fail closed.
        };

        var identity = new ClaimsIdentity(claims, authenticationType: SchemeName, nameType: "name", roleType: "role");
        return new ClaimsPrincipal(identity);
    }
}

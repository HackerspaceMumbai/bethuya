using System.Security.Claims;

namespace ServiceDefaults.Auth;

/// <summary>Shared development authentication defaults used when <c>Authentication:Provider=None</c>.</summary>
public static class DevelopmentAuthenticationDefaults
{
    /// <summary>The authentication scheme used for local development.</summary>
    public const string SchemeName = "Development";

    /// <summary>Creates the shared development principal for local auth-disabled flows.</summary>
    public static ClaimsPrincipal CreatePrincipal()
    {
        var claims = new List<Claim>
        {
            new("sub", "dev-user-001"),
            new("name", "Dev User"),
            new("email", "dev@bethuya.local"),
            new("role", "Admin"),
            new("role", "Organizer"),
            new("role", "Curator"),
            new("role", "Attendee"),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: SchemeName, nameType: "name", roleType: "role");
        return new ClaimsPrincipal(identity);
    }
}

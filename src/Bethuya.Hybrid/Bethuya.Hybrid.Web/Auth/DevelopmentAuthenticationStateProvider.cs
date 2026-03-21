using System.Security.Claims;
using Bethuya.Hybrid.Shared.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Development-only <see cref="AuthenticationStateProvider"/> used when
/// <see cref="AuthProviderType.None"/> is configured. Returns an authenticated
/// admin user so pages with <c>[Authorize]</c> attributes render in dev mode.
/// </summary>
internal sealed class DevelopmentAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState DevState = CreateDevState();

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(DevState);

    private static AuthenticationState CreateDevState()
    {
        var claims = new List<Claim>
        {
            new("sub", "dev-user-001"),
            new("name", "Dev User"),
            new("email", "dev@bethuya.local"),
            new("role", BethuyaRoles.Admin),
            new("role", BethuyaRoles.Organizer),
            new("role", BethuyaRoles.Curator),
            new("role", BethuyaRoles.Attendee),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "Development", nameType: "name", roleType: "role");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}

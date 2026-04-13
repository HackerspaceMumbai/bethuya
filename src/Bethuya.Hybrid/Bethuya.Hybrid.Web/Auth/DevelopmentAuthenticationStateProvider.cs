using Microsoft.AspNetCore.Components.Authorization;
using ServiceDefaults.Auth;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Development-only <see cref="AuthenticationStateProvider"/> used when
/// <see cref="AuthProviderType.None"/> is configured. Returns an authenticated
/// admin user so pages with <c>[Authorize]</c> attributes render in dev mode.
/// </summary>
internal sealed class DevelopmentAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState DevState = new(DevelopmentAuthenticationDefaults.CreatePrincipal());

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(DevState);
}

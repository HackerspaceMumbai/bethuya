using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using ServiceDefaults.Auth;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Development-only <see cref="AuthenticationStateProvider"/> used when
/// <see cref="AuthProviderType.None"/> is configured. Returns the current HTTP request's
/// already-resolved <see cref="System.Security.Claims.ClaimsPrincipal"/> (populated by
/// <c>DevelopmentAuthenticationHandler</c> via the ASP.NET Core authentication middleware),
/// so Blazor circuits reflect whichever persona was selected for this request rather than a
/// shared static instance.
/// </summary>
/// <remarks>
/// Falls back to the legacy fixed development principal when no <see cref="HttpContext"/> is
/// available (e.g. during Blazor circuit initialization before the request is bound).
/// </remarks>
internal sealed class DevelopmentAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    : AuthenticationStateProvider
{
    // Cached fallback — used only when no HttpContext is available.
    private static readonly AuthenticationState DefaultDevState =
        new(DevelopmentAuthenticationDefaults.CreatePrincipal());

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = httpContextAccessor.HttpContext?.User;

        // No HttpContext bound (circuit not yet associated with an HTTP request) →
        // fall back to legacy fixed dev principal to keep all [Authorize] UI rendering.
        if (user is null)
        {
            return Task.FromResult(DefaultDevState);
        }

        return Task.FromResult(new AuthenticationState(user));
    }
}

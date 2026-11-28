using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using ServiceDefaults.Auth;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Development-only <see cref="AuthenticationStateProvider"/> used when
/// <see cref="AuthProviderType.None"/> is configured. Captures the Blazor circuit's
/// host-authenticated <see cref="System.Security.Claims.ClaimsPrincipal"/> via
/// <see cref="IHostEnvironmentAuthenticationStateProvider"/> (preserving the selected persona
/// across SignalR circuit callbacks when <c>HttpContext</c> is null) and falls back to
/// <see cref="IHttpContextAccessor"/> during SSR prerendering or unit tests.
/// </summary>
internal sealed class DevelopmentAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    : AuthenticationStateProvider, IHostEnvironmentAuthenticationStateProvider
{
    // Cached fallback — used only when neither host circuit state nor HttpContext is available.
    private static readonly AuthenticationState DefaultDevState =
        new(DevelopmentAuthenticationDefaults.CreatePrincipal());

    private Task<AuthenticationState>? _hostAuthenticationStateTask;

    public void SetAuthenticationState(Task<AuthenticationState> authenticationStateTask)
    {
        _hostAuthenticationStateTask = authenticationStateTask
            ?? throw new ArgumentNullException(nameof(authenticationStateTask));
        NotifyAuthenticationStateChanged(_hostAuthenticationStateTask);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_hostAuthenticationStateTask is not null)
        {
            return _hostAuthenticationStateTask;
        }

        var user = httpContextAccessor.HttpContext?.User;

        // No HttpContext bound (SSR/test without host state) → fall back to legacy fixed dev principal.
        if (user is null)
        {
            return Task.FromResult(DefaultDevState);
        }

        return Task.FromResult(new AuthenticationState(user));
    }
}

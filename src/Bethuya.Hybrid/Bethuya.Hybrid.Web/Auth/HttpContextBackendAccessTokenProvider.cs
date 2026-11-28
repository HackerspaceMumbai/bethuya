using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using ServiceDefaults.Auth;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Default <see cref="IBackendAccessTokenProvider"/> that resolves the signed-in user's
/// backend access token from the server-side <see cref="IBackendTokenStore"/> (supporting both
/// SSR HTTP requests and Blazor <c>InteractiveServer</c> SignalR circuits with refresh-token
/// renewal) and falls back to <c>HttpContext.GetTokenAsync("access_token")</c> when present.
/// </summary>
public sealed class HttpContextBackendAccessTokenProvider(
    IHttpContextAccessor httpContextAccessor,
    ICircuitPrincipalAccessor? circuitPrincipalAccessor = null,
    IBackendTokenStore? tokenStore = null)
    : IBackendAccessTokenProvider
{
    /// <inheritdoc />
    public async ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var principal = httpContext?.User ?? circuitPrincipalAccessor?.CurrentPrincipal;

        var sessionId = principal?.FindFirst(BackendTokenClaimTypes.TokenSessionId)?.Value;
        if (!string.IsNullOrWhiteSpace(sessionId) && tokenStore is not null)
        {
            var storedToken = await tokenStore
                .GetValidAccessTokenAsync(sessionId, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(storedToken))
            {
                return storedToken;
            }
        }

        if (httpContext is null)
        {
            return null;
        }

        return await httpContext.GetTokenAsync("access_token").ConfigureAwait(false);
    }
}

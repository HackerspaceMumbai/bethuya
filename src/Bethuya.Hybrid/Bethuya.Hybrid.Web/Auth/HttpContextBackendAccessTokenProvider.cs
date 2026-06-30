using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Default <see cref="IBackendAccessTokenProvider"/> that reads the <c>access_token</c> stored on
/// the current request's authentication ticket (<c>SaveTokens = true</c> on the OIDC handler).
/// </summary>
public sealed class HttpContextBackendAccessTokenProvider(IHttpContextAccessor httpContextAccessor)
    : IBackendAccessTokenProvider
{
    /// <inheritdoc />
    public async ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        return await httpContext.GetTokenAsync("access_token").ConfigureAwait(false);
    }
}

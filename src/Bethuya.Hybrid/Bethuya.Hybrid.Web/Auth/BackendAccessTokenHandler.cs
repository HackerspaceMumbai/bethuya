using System.Net.Http.Headers;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Propagates the signed-in user's access token to the Backend API as a <c>Bearer</c>
/// <c>Authorization</c> header. This is the BFF token-forwarding seam that lets the backend
/// enforce authorization against a real identity rather than trusting the Web tier.
/// </summary>
/// <remarks>
/// The handler is a no-op when no token is available (e.g. anonymous requests) and never
/// overwrites an <c>Authorization</c> header that a caller already set.
/// </remarks>
public sealed class BackendAccessTokenHandler(IBackendAccessTokenProvider tokenProvider) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            var token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Supplies the access token for the currently signed-in user so it can be forwarded to the
/// Backend API. Implementations isolate the token source (e.g. the OIDC authentication ticket
/// stored on the <see cref="Microsoft.AspNetCore.Http.HttpContext"/>) from the HTTP plumbing.
/// </summary>
public interface IBackendAccessTokenProvider
{
    /// <summary>Gets the current user's access token, or <c>null</c> when none is available.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

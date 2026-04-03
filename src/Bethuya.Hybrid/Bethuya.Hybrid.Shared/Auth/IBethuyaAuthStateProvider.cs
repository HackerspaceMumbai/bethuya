using System.Security.Claims;

namespace Bethuya.Hybrid.Shared.Auth;

public interface IBethuyaAuthStateProvider {
    /// <summary>
    /// Asynchronously gets the current authenticated user principal.
    /// </summary>
    /// <returns>
    /// A task that resolves to the current <see cref="ClaimsPrincipal"/> representing the authenticated user.
    /// </returns>
    Task<ClaimsPrincipal> GetCurrentUserAsync();

    /// <summary>
    /// Notifies the provider that a user has logged in and updates the authentication state.
    /// </summary>
    /// <param name="user">
    /// The <see cref="ClaimsPrincipal"/> for the user who has successfully logged in.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous notification operation.
    /// </returns>
    Task NotifyUserLoggedIn(ClaimsPrincipal user);

    /// <summary>
    /// Notifies the provider that the current user has logged out and updates the authentication state.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous notification operation.
    /// </returns>
    Task NotifyUserLoggedOut();
}
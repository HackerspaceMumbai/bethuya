using System.Security.Claims;

namespace Bethuya.Hybrid.Shared.Auth;

/// <summary>
/// Provides the current authentication state for the hybrid application and exposes
/// notifications for authentication state transitions.
/// </summary>
/// <remarks>
/// Implementations should ensure that consumers always observe a principal that accurately
/// reflects the current authentication state, including anonymous or signed-out states.
/// </remarks>
public interface IBethuyaAuthStateProvider {
    /// <summary>
    /// Gets the principal that represents the current user authentication state.
    /// </summary>
    /// <returns>
    /// A task that resolves to the current <see cref="ClaimsPrincipal"/>. Implementations should
    /// return a principal representing the current state, including an unauthenticated principal
    /// when no user is signed in.
    /// </returns>
    Task<ClaimsPrincipal> GetCurrentUserAsync();

    /// <summary>
    /// Notifies the provider that a user has successfully logged in and that the supplied
    /// principal should become the current authentication state.
    /// </summary>
    /// <param name="user">
    /// The authenticated <see cref="ClaimsPrincipal"/> to publish as the current user.
    /// </param>
    Task NotifyUserLoggedIn(ClaimsPrincipal user);

    /// <summary>
    /// Notifies the provider that the current user has logged out and that the authentication
    /// state should transition to a signed-out or anonymous principal.
    /// </summary>
    Task NotifyUserLoggedOut();
}
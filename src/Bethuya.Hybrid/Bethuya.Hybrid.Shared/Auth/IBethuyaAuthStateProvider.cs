using System.Security.Claims;
using System.Threading;

namespace Bethuya.Hybrid.Shared.Auth;

public interface IBethuyaAuthStateProvider
{
    /// <summary>
    /// Asynchronously gets the current authenticated user principal.
    /// </summary>
    /// <param name="ct">A cancellation token for the operation.</param>
    /// <returns>
    /// A task that resolves to the current <see cref="ClaimsPrincipal"/>
    /// representing the authenticated user, or an unauthenticated principal
    /// if no user is currently signed in.
    /// </returns>
    Task<ClaimsPrincipal> GetCurrentUserAsync(CancellationToken ct = default);

    /// <summary>
    /// Notifies the provider that a user has logged in and updates the
    /// authentication state.
    /// </summary>
    /// <param name="user">
    /// The <see cref="ClaimsPrincipal"/> for the user who has successfully
    /// logged in.
    /// </param>
    /// <param name="ct">A cancellation token for the operation.</param>
    Task NotifyUserLoggedInAsync(ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>
    /// Notifies the provider that the current user has logged out and updates
    /// the authentication state.
    /// </summary>
    /// <param name="ct">A cancellation token for the operation.</param>
    Task NotifyUserLoggedOutAsync(CancellationToken ct = default);
}
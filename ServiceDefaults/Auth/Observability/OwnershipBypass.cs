using System.Security.Claims;

namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// Shared logic for the resource-ownership model: which roles may bypass ownership and how the caller's
/// subject is resolved from the validated principal. Centralised so the
/// <see cref="ResourceOwnerHandler"/> (policy evaluation) and the audit classification
/// (<see cref="AuthorizationAuditorExtensions"/>) agree on a single definition of "bypass".
/// </summary>
public static class OwnershipBypass
{
    /// <summary>
    /// Roles that may act on a resource they do not own (operational bypass): Admin, Organizer, Curator.
    /// </summary>
    public static readonly IReadOnlyList<string> Roles =
    [
        BethuyaRoleNames.Admin,
        BethuyaRoleNames.Organizer,
        BethuyaRoleNames.Curator
    ];

    /// <summary>Whether <paramref name="user"/> holds any ownership-bypass role.</summary>
    public static bool IsBypassRole(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        for (var i = 0; i < Roles.Count; i++)
        {
            if (user.IsInRole(Roles[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the caller's authentication subject (JWT <c>sub</c>, falling back to
    /// <c>nameidentifier</c>), or <see langword="null"/> when unauthenticated/absent.
    /// </summary>
    public static string? ResolveSubject(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Identity?.IsAuthenticated is not true)
        {
            return null;
        }

        return user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}

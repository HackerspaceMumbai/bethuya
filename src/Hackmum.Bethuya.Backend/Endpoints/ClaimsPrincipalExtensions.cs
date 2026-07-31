using System.Security.Claims;
using Hackmum.Bethuya.Backend.Services;

namespace Hackmum.Bethuya.Backend.Endpoints;

/// <summary>
/// Extension methods on <see cref="ClaimsPrincipal"/> for endpoint use.
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts a <see cref="CommunitySubjectContext"/> from the authenticated principal.
    /// Returns <see langword="null"/> when the principal carries no usable subject identifier.
    /// </summary>
    /// <param name="user">The authenticated <see cref="ClaimsPrincipal"/>.</param>
    /// <returns>
    /// A populated <see cref="CommunitySubjectContext"/> when the principal has a
    /// <c>sub</c> or <see cref="ClaimTypes.NameIdentifier"/> claim; otherwise <see langword="null"/>.
    /// </returns>
    internal static CommunitySubjectContext? GetSubject(this ClaimsPrincipal user)
    {
        var userId = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var displayName = user.FindFirst("name")?.Value ?? user.Identity?.Name;
        var email = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value;

        return new CommunitySubjectContext(userId, displayName, email);
    }
}

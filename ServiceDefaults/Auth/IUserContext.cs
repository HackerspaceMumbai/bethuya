namespace ServiceDefaults.Auth;

/// <summary>
/// Abstraction over the validated server-side caller identity. Endpoints depend on this instead of
/// hand-rolling <c>ClaimsPrincipal</c> claim lookups, so identity resolution stays consistent and the
/// authoritative id/email come from the validated principal — never from request bodies.
/// </summary>
public interface IUserContext
{
    /// <summary>Whether the current request carries an authenticated principal.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The authentication subject identifier (JWT <c>sub</c> claim, falling back to
    /// <c>nameidentifier</c>), or <see langword="null"/> when unauthenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>The caller's email claim, or <see langword="null"/> when unavailable.</summary>
    string? Email { get; }

    /// <summary>The caller's display name claim, or <see langword="null"/> when unavailable.</summary>
    string? Name { get; }
}

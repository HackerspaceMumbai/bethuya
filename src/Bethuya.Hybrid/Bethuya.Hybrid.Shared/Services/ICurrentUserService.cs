namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Provides identity information for the current user within the Bethuya platform.</summary>
public interface ICurrentUserService
{
    /// <summary>Gets the unique identifier for the current user, or <see langword="null"/> if unauthenticated.</summary>
    string? UserId { get; }

    /// <summary>Gets the email address of the current user, or <see langword="null"/> if unauthenticated.</summary>
    string? Email { get; }

    /// <summary>Gets a value indicating whether the current request is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Determines whether the current user is in the specified role.</summary>
    bool IsInRole(string role);
}

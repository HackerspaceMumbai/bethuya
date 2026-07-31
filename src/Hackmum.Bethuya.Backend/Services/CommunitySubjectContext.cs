namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Authenticated community subject details derived from the current principal.
/// </summary>
/// <param name="UserId">Stable identity subject identifier.</param>
/// <param name="DisplayName">Optional display name from claims.</param>
/// <param name="Email">Optional email from claims.</param>
public sealed record CommunitySubjectContext(string UserId, string? DisplayName, string? Email);

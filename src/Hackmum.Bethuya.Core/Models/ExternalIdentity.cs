using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// A linked external identity that strengthens trust and reuse across community workflows.
/// </summary>
public sealed class ExternalIdentity
{
    /// <summary>
    /// Unique external identity identifier.
    /// </summary>
    public ExternalIdentityId Id { get; init; } = ExternalIdentityId.From(Guid.CreateVersion7());
    /// <summary>
    /// Owning community member identifier.
    /// </summary>
    public CommunityMemberId CommunityMemberId { get; init; }
    /// <summary>
    /// Identity provider kind.
    /// </summary>
    public IdentityProviderKind Provider { get; set; }
    /// <summary>
    /// Provider subject identifier.
    /// </summary>
    public required string Subject { get; set; }
    /// <summary>
    /// Optional provider username.
    /// </summary>
    public string? Username { get; set; }
    /// <summary>
    /// Optional public profile URL.
    /// </summary>
    public string? ProfileUrl { get; set; }
    /// <summary>
    /// Whether provider link has been verified.
    /// </summary>
    public bool IsVerified { get; set; } = true;
    /// <summary>
    /// Timestamp when the identity was first linked.
    /// </summary>
    public DateTimeOffset LinkedAt { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Last verification timestamp.
    /// </summary>
    public DateTimeOffset? LastVerifiedAt { get; set; }

    /// <summary>
    /// Navigation to owning community member.
    /// </summary>
    public CommunityMember? CommunityMember { get; init; }
}

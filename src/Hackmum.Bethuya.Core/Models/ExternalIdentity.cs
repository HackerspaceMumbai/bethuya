using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// A linked external identity that strengthens trust and reuse across community workflows.
/// </summary>
public sealed class ExternalIdentity
{
    public ExternalIdentityId Id { get; init; } = ExternalIdentityId.From(Guid.CreateVersion7());
    public CommunityMemberId CommunityMemberId { get; init; }
    public IdentityProviderKind Provider { get; set; }
    public required string Subject { get; set; }
    public string? Username { get; set; }
    public string? ProfileUrl { get; set; }
    public bool IsVerified { get; set; } = true;
    public DateTimeOffset LinkedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastVerifiedAt { get; set; }

    public CommunityMember? CommunityMember { get; init; }
}

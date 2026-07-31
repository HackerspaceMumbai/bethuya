using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Canonical community identity reused across event, passport, and future opportunity workflows.
/// </summary>
public sealed class CommunityMember
{
    /// <summary>
    /// Unique community member identifier.
    /// </summary>
    public CommunityMemberId Id { get; init; } = CommunityMemberId.From(Guid.CreateVersion7());
    /// <summary>
    /// Stable identity subject from the authentication provider.
    /// </summary>
    public required string UserId { get; set; }
    /// <summary>
    /// Preferred member display name.
    /// </summary>
    public required string DisplayName { get; set; }
    /// <summary>
    /// Primary member email used for projection joins.
    /// </summary>
    public required string Email { get; set; }
    /// <summary>
    /// Community slug this member belongs to.
    /// </summary>
    public string CommunitySlug { get; set; } = "hackerspace-mumbai";
    /// <summary>
    /// Optional occupation status text.
    /// </summary>
    public string? OccupationStatus { get; set; }
    /// <summary>
    /// Optional company affiliation.
    /// </summary>
    public string? CompanyName { get; set; }
    /// <summary>
    /// Optional education affiliation.
    /// </summary>
    public string? EducationInstitute { get; set; }
    /// <summary>
    /// Profile visibility scope for this member.
    /// </summary>
    public ProfileVisibilityScope Visibility { get; set; } = ProfileVisibilityScope.CommunityOnly;
    /// <summary>
    /// Whether organizers can access participation signals.
    /// </summary>
    public bool ShareParticipationWithOrganizers { get; set; } = true;
    /// <summary>
    /// Whether discoverable in community opportunity flows.
    /// </summary>
    public bool IsDiscoverableToCommunity { get; set; } = true;
    /// <summary>
    /// Residency region label used for policy routing.
    /// </summary>
    public string ResidencyRegion { get; set; } = "South India";
    /// <summary>
    /// Residency mode used for sensitive data workflows.
    /// </summary>
    public SensitiveDataResidencyMode ResidencyMode { get; set; } = SensitiveDataResidencyMode.SovereignRegion;
    /// <summary>
    /// Compliance profile label for auditing and policy checks.
    /// </summary>
    public string ComplianceProfile { get; set; } = "DPDP-ready";
    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Linked external identities associated with this member.
    /// </summary>
    public List<ExternalIdentity> ExternalIdentities { get; init; } = [];
}

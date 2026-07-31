using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Canonical community identity reused across event, passport, and future opportunity workflows.
/// </summary>
public sealed class CommunityMember
{
    public CommunityMemberId Id { get; init; } = CommunityMemberId.From(Guid.CreateVersion7());
    public required string UserId { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public string CommunitySlug { get; set; } = "hackerspace-mumbai";
    public string? OccupationStatus { get; set; }
    public string? CompanyName { get; set; }
    public string? EducationInstitute { get; set; }
    public ProfileVisibilityScope Visibility { get; set; } = ProfileVisibilityScope.CommunityOnly;
    public bool ShareParticipationWithOrganizers { get; set; } = true;
    public bool IsDiscoverableToCommunity { get; set; } = true;
    public string ResidencyRegion { get; set; } = "South India";
    public SensitiveDataResidencyMode ResidencyMode { get; set; } = SensitiveDataResidencyMode.SovereignRegion;
    public string ComplianceProfile { get; set; } = "DPDP-ready";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ExternalIdentity> ExternalIdentities { get; init; } = [];
}

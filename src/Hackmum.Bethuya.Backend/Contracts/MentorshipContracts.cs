using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Backend.Contracts;

// ─── Opt-in ───────────────────────────────────────────────────────────────────

/// <summary>Request to opt in or update an existing mentor profile.</summary>
public sealed record MentorOptInRequest(
    IReadOnlyList<MentorExpertiseArea> ExpertiseAreas,
    string? IntroductionBio = null,
    int AvailabilityHoursPerMonth = 2,
    bool IsDiscoverable = true);

/// <summary>Request to pause or withdraw from the mentorship programme.</summary>
public sealed record MentorStatusUpdateRequest(
    MentorshipStatus Status);

// ─── Responses ────────────────────────────────────────────────────────────────

/// <summary>Public surface returned from all mentor profile reads.</summary>
public sealed record MentorProfileResponse(
    Guid MentorProfileId,
    string MemberDisplayName,
    string MemberEmail,
    MentorshipStatus Status,
    IReadOnlyList<MentorExpertiseArea> ExpertiseAreas,
    string? IntroductionBio,
    int AvailabilityHoursPerMonth,
    bool IsDiscoverable,
    DateTimeOffset OptedInAt,
    DateTimeOffset UpdatedAt);

/// <summary>Trimmed public entry returned from the community discovery directory (no email).</summary>
public sealed record MentorDiscoveryEntryResponse(
    Guid MentorProfileId,
    string DisplayName,
    string? OccupationStatus,
    string? CompanyName,
    IReadOnlyList<MentorExpertiseArea> ExpertiseAreas,
    string? IntroductionBio,
    int AvailabilityHoursPerMonth);

// ─── Recommendation ────────────────────────────────────────────────────────────

/// <summary>Organizer-scoped request to generate a mentor-pairing recommendation draft.</summary>
public sealed record DraftMentorPairingSuggestionRequest(
    int LookbackDays = 90,
    IReadOnlyList<MentorExpertiseArea>? FocusAreas = null,
    string? RequestedBy = null);

using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Backend.Contracts;

/// <summary>
/// Member-facing Community Passport projection.
/// </summary>
public sealed record CommunityPassportResponse(
    string DisplayName,
    string Email,
    string? OccupationStatus,
    string? CompanyName,
    string? EducationInstitute,
    string CurrentTier,
    PassportMetricsResponse Metrics,
    PassportPrivacyResponse Privacy,
    PassportResidencyResponse Residency,
    IReadOnlyList<PassportIdentityResponse> LinkedIdentities,
    IReadOnlyList<PassportTimelineEntryResponse> Timeline);

/// <summary>
/// Summary metrics for the current member.
/// </summary>
public sealed record PassportMetricsResponse(
    int EventsRegistered,
    int EventsAttended,
    int EventsWaitlisted,
    int VolunteerSignals,
    int MilestonesEarned);

/// <summary>
/// Privacy controls visible and editable by the member.
/// </summary>
public sealed record PassportPrivacyResponse(
    ProfileVisibilityScope Visibility,
    bool ShareParticipationWithOrganizers,
    bool IsDiscoverableToCommunity);

/// <summary>
/// Jurisdiction-aware residency policy applied to the current member's sensitive data.
/// </summary>
public sealed record PassportResidencyResponse(
    string Region,
    SensitiveDataResidencyMode Mode,
    string ComplianceProfile);

/// <summary>
/// One linked external identity.
/// </summary>
public sealed record PassportIdentityResponse(
    IdentityProviderKind Provider,
    string Subject,
    string? Username,
    string? ProfileUrl,
    bool IsVerified,
    DateTimeOffset LinkedAt);

/// <summary>
/// A timeline entry shown in the Community Passport.
/// </summary>
public sealed record PassportTimelineEntryResponse(
    Guid EventId,
    string EventTitle,
    string Status,
    DateTimeOffset OccurredAt,
    string Evidence);

/// <summary>
/// Request payload for updating Community Passport privacy settings.
/// </summary>
public sealed record UpdateCommunityPassportPrivacyRequest(
    ProfileVisibilityScope Visibility,
    bool ShareParticipationWithOrganizers,
    bool IsDiscoverableToCommunity);

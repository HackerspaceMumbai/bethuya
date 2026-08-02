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
/// Lifecycle-aware journey projection for the authenticated community member.
/// </summary>
public sealed record CommunityJourneyProjectionResponse(
    string CurrentStage,
    int JourneyScore,
    double StageCompletionPercent,
    JourneyStageProgressResponse StageProgress,
    IReadOnlyList<JourneyTimelineEntryResponse> Timeline,
    IReadOnlyList<JourneyTimelineProjectionResponse> Projections,
    IReadOnlyList<EventLifecycleJourneyProgressResponse> LifecycleProgression);

/// <summary>
/// Progress details for the current and next journey stages.
/// </summary>
public sealed record JourneyStageProgressResponse(
    string CurrentStage,
    string? NextStage,
    int CurrentStageMinScore,
    int CurrentStageMaxScore,
    int NextStageScoreThreshold,
    int PointsToNextStage);

/// <summary>
/// One chronological journey event for member progression.
/// </summary>
public sealed record JourneyTimelineEntryResponse(
    DateTimeOffset OccurredAt,
    string Source,
    string Activity,
    int Points,
    string Evidence,
    Guid? EventId,
    string? EventTitle);

/// <summary>
/// Forecasted journey milestones based on recent activity velocity.
/// </summary>
public sealed record JourneyTimelineProjectionResponse(
    string Milestone,
    DateTimeOffset ProjectedAt,
    int PointsRemaining,
    double MonthlyVelocityPoints,
    string Confidence,
    string Rationale);

/// <summary>
/// Event lifecycle progression projected from current lifecycle state.
/// </summary>
public sealed record EventLifecycleJourneyProgressResponse(
    Guid EventId,
    string EventTitle,
    string CurrentState,
    string? NextState,
    DateTimeOffset? ProjectedNextTransitionAt);

/// <summary>
/// Organizer-facing read models for community lifecycle health.
/// </summary>
public sealed record CommunityHealthDashboardReadModelResponse(
    DateTimeOffset AsOfUtc,
    int LookbackDays,
    RetentionReadModelResponse Retention,
    AttendanceReadModelResponse Attendance,
    VolunteerGrowthReadModelResponse VolunteerGrowth,
    LeadershipFunnelReadModelResponse LeadershipFunnel);

/// <summary>
/// Member retention trend over the configured lookback windows.
/// </summary>
public sealed record RetentionReadModelResponse(
    int PreviouslyActiveMembers,
    int CurrentlyActiveMembers,
    int RetainedMembers,
    double RetentionRatePercent);

/// <summary>
/// Attendance distribution and conversion for recent events.
/// </summary>
public sealed record AttendanceReadModelResponse(
    int RegisteredCount,
    int AcceptedCount,
    int AttendedCount,
    int WaitlistedCount,
    double AttendanceRatePercent);

/// <summary>
/// Volunteer signal growth compared to the prior lookback window.
/// </summary>
public sealed record VolunteerGrowthReadModelResponse(
    int PreviousWindowSignals,
    int CurrentWindowSignals,
    int DeltaSignals,
    double GrowthRatePercent);

/// <summary>
/// Leadership funnel stages derived from discoverability, signals, and participation history.
/// </summary>
public sealed record LeadershipFunnelReadModelResponse(
    int DiscoverableMembers,
    int VolunteerInterestedMembers,
    int ActiveVolunteers,
    int LeadershipCandidates);

/// <summary>
/// Request payload for updating Community Passport privacy settings.
/// </summary>
public sealed record UpdateCommunityPassportPrivacyRequest(
    ProfileVisibilityScope Visibility,
    bool ShareParticipationWithOrganizers,
    bool IsDiscoverableToCommunity);

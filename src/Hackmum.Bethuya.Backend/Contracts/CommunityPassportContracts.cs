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

/// <summary>
/// Versioned aggregate used by the unified Community Passport experience.
/// </summary>
public sealed record CommunityPassportExperienceResponse(
    string SchemaVersion,
    PassportViewerResponse Viewer,
    PassportIdentitySummaryResponse Identity,
    CommunityStoryResponse CommunityStory,
    IReadOnlyList<CommunitySignalResponse> Signals,
    IReadOnlyList<GrowthPathwayResponse> Journey,
    IReadOnlyList<PassportContributionResponse> Contributions,
    IReadOnlyList<ActivityDayResponse> Activity,
    PassportPortfolioResponse Portfolio,
    IReadOnlyList<PassportConnectionResponse> Connections,
    IReadOnlyList<PassportOpportunityResponse> Opportunities,
    PassportPrivacyPreferencesResponse Privacy,
    PassportResidencyResponse Residency);

/// <summary>
/// Describes how the current viewer may interact with the passport.
/// </summary>
public sealed record PassportViewerResponse(
    bool IsOwner,
    bool IsOrganizer,
    bool CanEdit,
    bool IsParticipationRedacted);

/// <summary>
/// Member identity fields shown in the passport hero.
/// </summary>
public sealed record PassportIdentitySummaryResponse(
    Guid MemberId,
    string DisplayName,
    string Email,
    string Initials,
    string CommunityName,
    DateTimeOffset MemberSince,
    string? OccupationStatus,
    string? Affiliation);

/// <summary>
/// Evidence-backed member journey narrative.
/// </summary>
public sealed record CommunityStoryResponse(
    string Narrative,
    IReadOnlyList<string> Evidence);

/// <summary>
/// Explainable community participation signal.
/// </summary>
public sealed record CommunitySignalResponse(
    CommunitySignalKind Kind,
    string Label,
    bool IsOrganizerAwarded,
    string Explanation,
    IReadOnlyList<string> Evidence);

/// <summary>
/// One evidence-based growth pathway and its milestones.
/// </summary>
public sealed record GrowthPathwayResponse(
    string Name,
    string CurrentStage,
    IReadOnlyList<GrowthMilestoneResponse> Milestones,
    string Attestation = "Progress derived from verified participation evidence");

/// <summary>
/// One pathway milestone.
/// </summary>
public sealed record GrowthMilestoneResponse(
    string Name,
    string State,
    string Explanation);

/// <summary>
/// Human-friendly normalized participation record.
/// </summary>
public sealed record PassportContributionResponse(
    Guid Id,
    string Type,
    string Title,
    string Description,
    string Community,
    string ImpactArea,
    DateTimeOffset OccurredAt,
    bool IsVerified,
    bool IsLedgerEvidence,
    Guid? EventId = null,
    string Attestation = "Recorded participation evidence");

/// <summary>
/// Daily contribution count used by the accessible activity graph.
/// </summary>
public sealed record ActivityDayResponse(
    DateOnly Date,
    int Count,
    IReadOnlyList<string> Activities);

/// <summary>
/// Verified and member-curated portfolio projection.
/// </summary>
public sealed record PassportPortfolioResponse(
    IReadOnlyList<VerifiedPortfolioHighlightResponse> VerifiedHighlights,
    IReadOnlyList<PortfolioEntryResponse> Entries);

/// <summary>
/// Automatically projected portfolio highlight.
/// </summary>
public sealed record VerifiedPortfolioHighlightResponse(
    string Title,
    string Description,
    string Evidence);

/// <summary>
/// Member-curated portfolio entry.
/// </summary>
public sealed record PortfolioEntryResponse(
    Guid Id,
    string Title,
    string Description,
    bool IsFeatured,
    int DisplayOrder,
    IReadOnlyList<PortfolioLinkResponse> Links,
    IReadOnlyList<Guid> EvidenceEntryIds);

/// <summary>
/// Typed external portfolio link.
/// </summary>
public sealed record PortfolioLinkResponse(
    PortfolioLinkKind Kind,
    string Url,
    string? Label);

/// <summary>
/// Contextual relationship summary.
/// </summary>
public sealed record PassportConnectionResponse(
    Guid MemberId,
    string DisplayName,
    CommunityRelationshipKind Kind,
    string Context,
    string Explanation);

/// <summary>
/// Opportunity history with lifecycle and outcome.
/// </summary>
public sealed record PassportOpportunityResponse(
    Guid Id,
    MemberOpportunityKind Kind,
    string Title,
    string Description,
    MemberOpportunityStatus CurrentStatus,
    string? Outcome,
    DateTimeOffset OfferedAt,
    IReadOnlyList<OpportunityLifecycleEventResponse> Lifecycle);

/// <summary>
/// One opportunity lifecycle transition.
/// </summary>
public sealed record OpportunityLifecycleEventResponse(
    MemberOpportunityStatus Status,
    DateTimeOffset OccurredAt,
    string Explanation);

/// <summary>
/// Complete member privacy, consent, and discovery preferences.
/// </summary>
public sealed record PassportPrivacyPreferencesResponse(
    ProfileVisibilityScope Visibility,
    bool ShareParticipationWithOrganizers,
    bool AppearInMentorshipRecommendations,
    bool AppearInOpportunityRecommendations,
    bool AppearInCollaboratorDiscovery,
    bool AppearInSpeakerRecommendations,
    bool AppearInVolunteerLeadershipRecommendations,
    bool EnableRelationshipInsights,
    bool ReceiveOpportunityRecommendations);

/// <summary>
/// Updates the complete privacy and discovery preference set.
/// </summary>
public sealed record UpdatePassportPrivacyPreferencesRequest(
    ProfileVisibilityScope Visibility,
    bool ShareParticipationWithOrganizers,
    bool AppearInMentorshipRecommendations,
    bool AppearInOpportunityRecommendations,
    bool AppearInCollaboratorDiscovery,
    bool AppearInSpeakerRecommendations,
    bool AppearInVolunteerLeadershipRecommendations,
    bool EnableRelationshipInsights,
    bool ReceiveOpportunityRecommendations);

/// <summary>
/// Creates or updates a member-curated portfolio entry.
/// </summary>
public sealed record UpsertPortfolioEntryRequest(
    string Title,
    string Description,
    bool IsFeatured,
    int DisplayOrder,
    IReadOnlyList<PortfolioLinkResponse> Links,
    IReadOnlyList<Guid> EvidenceEntryIds);

/// <summary>
/// Reorders the current member's portfolio entries.
/// </summary>
public sealed record ReorderPortfolioEntriesRequest(
    IReadOnlyList<Guid> EntryIds);

/// <summary>
/// Organizer request to award Champion recognition.
/// </summary>
public sealed record AwardChampionSignalRequest(
    string Rationale,
    IReadOnlyList<Guid> EvidenceEntryIds);

/// <summary>
/// Organizer request to revoke Champion recognition.
/// </summary>
public sealed record RevokeChampionSignalRequest(
    string Reason);

/// <summary>
/// Organizer directory response.
/// </summary>
public sealed record CommunityPassportDirectoryResponse(
    int TotalCount,
    IReadOnlyList<CommunityPassportDirectoryEntryResponse> Entries);

/// <summary>
/// One privacy-filtered organizer directory entry.
/// </summary>
public sealed record CommunityPassportDirectoryEntryResponse(
    Guid MemberId,
    string DisplayName,
    string Community,
    string? OccupationStatus,
    IReadOnlyList<string> Signals,
    DateTimeOffset MemberSince,
    bool IsParticipationShared);

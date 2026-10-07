using Refit;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>
/// Refit-generated typed client for the Community Passport API.
/// </summary>
public interface ICommunityPassportApi
{
    /// <summary>Gets the versioned unified passport experience.</summary>
    [Get("/api/community/passport/experience")]
    Task<CommunityPassportExperienceDto> GetExperienceAsync(CancellationToken ct = default);

    /// <summary>Updates complete passport privacy and discovery preferences.</summary>
    [Put("/api/community/passport/experience/privacy")]
    Task<PassportPrivacyPreferencesDto> SavePrivacyPreferencesAsync(
        [Body] UpdatePassportPrivacyPreferencesDto request,
        CancellationToken ct = default);

    /// <summary>Creates a member-curated portfolio entry.</summary>
    [Post("/api/community/passport/portfolio")]
    Task<PortfolioEntryDto> CreatePortfolioEntryAsync(
        [Body] UpsertPortfolioEntryDto request,
        CancellationToken ct = default);

    /// <summary>Updates a member-curated portfolio entry.</summary>
    [Put("/api/community/passport/portfolio/{entryId}")]
    Task<PortfolioEntryDto> UpdatePortfolioEntryAsync(
        Guid entryId,
        [Body] UpsertPortfolioEntryDto request,
        CancellationToken ct = default);

    /// <summary>Deletes a member-curated portfolio entry.</summary>
    [Delete("/api/community/passport/portfolio/{entryId}")]
    Task DeletePortfolioEntryAsync(Guid entryId, CancellationToken ct = default);

    /// <summary>Reorders member-curated portfolio entries.</summary>
    [Put("/api/community/passport/portfolio/order")]
    Task ReorderPortfolioEntriesAsync(
        [Body] ReorderPortfolioEntriesDto request,
        CancellationToken ct = default);

    /// <summary>Downloads the canonical passport portability archive.</summary>
    [Get("/api/community/passport/export")]
    Task<HttpResponseMessage> ExportPassportAsync(CancellationToken ct = default);

    /// <summary>Reads the organizer-scoped member directory.</summary>
    [Get("/api/community/passport/members")]
    Task<CommunityPassportDirectoryDto> GetDirectoryAsync(
        [AliasAs("search")] string? search = null,
        [AliasAs("participationShared")] bool? participationShared = null,
        [AliasAs("skip")] int skip = 0,
        [AliasAs("take")] int take = 25,
        CancellationToken ct = default);

    /// <summary>Reads one permission-filtered passport for an organizer.</summary>
    [Get("/api/community/passport/members/{memberId}")]
    Task<CommunityPassportExperienceDto> GetMemberExperienceAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Awards Champion recognition to a member.</summary>
    [Post("/api/community/passport/members/{memberId}/signals/champion")]
    Task<CommunitySignalDto> AwardChampionAsync(
        Guid memberId,
        [Body] AwardChampionSignalDto request,
        CancellationToken ct = default);

    /// <summary>Revokes Champion recognition from a member.</summary>
    [Delete("/api/community/passport/members/{memberId}/signals/champion")]
    Task RevokeChampionAsync(
        Guid memberId,
        [Body] RevokeChampionSignalDto request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current user's Community Passport projection.
    /// </summary>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>Current Community Passport DTO.</returns>
    [Get("/api/community/passport")]
    Task<CommunityPassportDto> GetPassportAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves updated privacy controls for the current user's Community Passport.
    /// </summary>
    /// <param name="request">Updated privacy payload.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>Persisted privacy view.</returns>
    [Post("/api/community/passport/privacy")]
    Task<PassportPrivacyDto> SavePrivacyAsync([Body] UpdateCommunityPassportPrivacyDto request, CancellationToken ct = default);

    /// <summary>
    /// Writes normalized participation entries into the member ledger.
    /// </summary>
    [Post("/api/community/passport/participation")]
    Task<ParticipationEntryWriteResultDto> WriteParticipationAsync([Body] UpsertParticipationEntriesDto request, CancellationToken ct = default);

    /// <summary>
    /// Reads the current member timeline projection backed by the participation ledger.
    /// </summary>
    [Get("/api/community/passport/participation/timeline")]
    Task<MemberParticipationTimelineDto> GetParticipationTimelineAsync([AliasAs("limit")] int? limit = null, CancellationToken ct = default);

    /// <summary>
    /// Reads lifecycle journey progression and projected member milestones.
    /// </summary>
    [Get("/api/community/passport/journey")]
    Task<CommunityJourneyProjectionDto> GetJourneyProjectionAsync([AliasAs("timelineLimit")] int? timelineLimit = null, CancellationToken ct = default);

    /// <summary>
    /// Reads organizer-facing dashboard projection models.
    /// </summary>
    [Get("/api/community/passport/dashboard/read-model")]
    Task<CommunityHealthDashboardReadModelDto> GetDashboardReadModelAsync([AliasAs("lookbackDays")] int? lookbackDays = null, CancellationToken ct = default);

    /// <summary>
    /// Creates a human-review-gated member-growth opportunity recommendation draft.
    /// </summary>
    [Post("/api/community/passport/recommendations/member-growth")]
    Task<RecommendationDraftDto> DraftMemberGrowthRecommendationAsync([Body] DraftMemberGrowthRecommendationDto request, CancellationToken ct = default);

    /// <summary>
    /// Creates a human-review-gated weekly community briefing draft.
    /// </summary>
    [Post("/api/community/passport/recommendations/weekly-briefing")]
    Task<RecommendationDraftDto> DraftWeeklyCommunityBriefingAsync([Body] DraftWeeklyCommunityBriefingDto request, CancellationToken ct = default);

    /// <summary>
    /// Approves an existing recommendation draft.
    /// </summary>
    [Post("/api/community/passport/recommendations/{draftId}/approve")]
    Task<RecommendationDraftDto> ApproveRecommendationDraftAsync(Guid draftId, [Body] ApproveRecommendationDraftDto request, CancellationToken ct = default);
}

/// <summary>
/// Member-facing Community Passport projection.
/// </summary>
/// <param name="DisplayName">Display name shown in the passport.</param>
/// <param name="Email">Primary member email.</param>
/// <param name="OccupationStatus">Optional occupation status.</param>
/// <param name="CompanyName">Optional company affiliation.</param>
/// <param name="EducationInstitute">Optional education affiliation.</param>
/// <param name="CurrentTier">Computed community tier label.</param>
/// <param name="Metrics">Aggregated participation metrics.</param>
/// <param name="Privacy">Current privacy controls.</param>
/// <param name="Residency">Applied residency policy.</param>
/// <param name="LinkedIdentities">Linked external identities.</param>
/// <param name="Timeline">Recent activity timeline.</param>
public sealed record CommunityPassportDto(
    string DisplayName,
    string Email,
    string? OccupationStatus,
    string? CompanyName,
    string? EducationInstitute,
    string CurrentTier,
    PassportMetricsDto Metrics,
    PassportPrivacyDto Privacy,
    PassportResidencyDto Residency,
    IReadOnlyList<PassportIdentityDto> LinkedIdentities,
    IReadOnlyList<PassportTimelineEntryDto> Timeline);

/// <summary>
/// Summary participation metrics.
/// </summary>
/// <param name="EventsRegistered">Total registrations.</param>
/// <param name="EventsAttended">Total attended events.</param>
/// <param name="EventsWaitlisted">Total waitlisted events.</param>
/// <param name="VolunteerSignals">Total volunteer intent signals.</param>
/// <param name="MilestonesEarned">Total milestones earned.</param>
public sealed record PassportMetricsDto(
    int EventsRegistered,
    int EventsAttended,
    int EventsWaitlisted,
    int VolunteerSignals,
    int MilestonesEarned);

/// <summary>
/// Privacy controls for the current member.
/// </summary>
/// <param name="Visibility">Visibility scope value.</param>
/// <param name="ShareParticipationWithOrganizers">Whether organizers can use participation signals.</param>
/// <param name="IsDiscoverableToCommunity">Whether discoverable in community opportunity flows.</param>
public sealed record PassportPrivacyDto(
    string Visibility,
    bool ShareParticipationWithOrganizers,
    bool IsDiscoverableToCommunity);

/// <summary>
/// Residency and compliance policy view.
/// </summary>
/// <param name="Region">Residency region label.</param>
/// <param name="Mode">Residency mode value.</param>
/// <param name="ComplianceProfile">Compliance profile label.</param>
public sealed record PassportResidencyDto(
    string Region,
    string Mode,
    string ComplianceProfile);

/// <summary>
/// Linked identity descriptor.
/// </summary>
/// <param name="Provider">Identity provider name.</param>
/// <param name="Subject">Provider subject value.</param>
/// <param name="Username">Optional provider username.</param>
/// <param name="ProfileUrl">Optional provider profile URL.</param>
/// <param name="IsVerified">Whether this identity is verified.</param>
/// <param name="LinkedAt">Timestamp when identity was linked.</param>
public sealed record PassportIdentityDto(
    string Provider,
    string Subject,
    string? Username,
    string? ProfileUrl,
    bool IsVerified,
    DateTimeOffset LinkedAt);

/// <summary>
/// Activity timeline entry for the passport.
/// </summary>
/// <param name="EventId">Event identifier.</param>
/// <param name="EventTitle">Event title.</param>
/// <param name="Status">Mapped participation status.</param>
/// <param name="OccurredAt">When this timeline event occurred.</param>
/// <param name="Evidence">Human-readable supporting evidence.</param>
public sealed record PassportTimelineEntryDto(
    Guid EventId,
    string EventTitle,
    string Status,
    DateTimeOffset OccurredAt,
    string Evidence);

/// <summary>
/// Privacy update payload for the Community Passport.
/// </summary>
/// <param name="Visibility">Requested visibility scope value.</param>
/// <param name="ShareParticipationWithOrganizers">Whether organizers may use participation signals.</param>
/// <param name="IsDiscoverableToCommunity">Whether discoverable in community opportunity flows.</param>
public sealed record UpdateCommunityPassportPrivacyDto(
    string Visibility,
    bool ShareParticipationWithOrganizers,
    bool IsDiscoverableToCommunity);

/// <summary>
/// Batched participation write payload.
/// </summary>
public sealed record UpsertParticipationEntriesDto(
    IReadOnlyList<ParticipationEntryWriteDto> Entries);

/// <summary>
/// One normalized participation entry submitted by orchestration flows.
/// </summary>
public sealed record ParticipationEntryWriteDto(
    string Connector,
    string ExternalMemberKey,
    string Activity,
    DateTimeOffset OccurredAt,
    string Evidence,
    string ProvenanceKey,
    Guid? EventId = null,
    string? ExternalEventId = null,
    string? ExternalRecordId = null,
    string? SourceCorrelationId = null);

/// <summary>
/// Participation write result summary.
/// </summary>
public sealed record ParticipationEntryWriteResultDto(
    int ReceivedCount,
    int StoredCount,
    int DuplicateCount);

/// <summary>
/// Member participation timeline projection DTO.
/// </summary>
public sealed record MemberParticipationTimelineDto(
    IReadOnlyList<MemberParticipationTimelineEntryDto> Entries);

/// <summary>
/// One timeline item from the unified participation ledger.
/// </summary>
public sealed record MemberParticipationTimelineEntryDto(
    Guid EntryId,
    string Connector,
    string Activity,
    DateTimeOffset OccurredAt,
    string Evidence,
    string ProvenanceKey,
    Guid? EventId,
    string? EventTitle);

/// <summary>
/// Lifecycle-aware journey projection for the current member.
/// </summary>
public sealed record CommunityJourneyProjectionDto(
    string CurrentStage,
    int JourneyScore,
    double StageCompletionPercent,
    JourneyStageProgressDto StageProgress,
    IReadOnlyList<JourneyTimelineEntryDto> Timeline,
    IReadOnlyList<JourneyTimelineProjectionDto> Projections,
    IReadOnlyList<EventLifecycleJourneyProgressDto> LifecycleProgression);

/// <summary>
/// Current and next stage journey details.
/// </summary>
public sealed record JourneyStageProgressDto(
    string CurrentStage,
    string? NextStage,
    int CurrentStageMinScore,
    int CurrentStageMaxScore,
    int NextStageScoreThreshold,
    int PointsToNextStage);

/// <summary>
/// One timeline event contributing to journey progression.
/// </summary>
public sealed record JourneyTimelineEntryDto(
    DateTimeOffset OccurredAt,
    string Source,
    string Activity,
    int Points,
    string Evidence,
    Guid? EventId,
    string? EventTitle);

/// <summary>
/// One projected future journey milestone.
/// </summary>
public sealed record JourneyTimelineProjectionDto(
    string Milestone,
    DateTimeOffset ProjectedAt,
    int PointsRemaining,
    double MonthlyVelocityPoints,
    string Confidence,
    string Rationale);

/// <summary>
/// Event lifecycle progression and next-state projection.
/// </summary>
public sealed record EventLifecycleJourneyProgressDto(
    Guid EventId,
    string EventTitle,
    string CurrentState,
    string? NextState,
    DateTimeOffset? ProjectedNextTransitionAt);

/// <summary>
/// Organizer-facing dashboard read models.
/// </summary>
public sealed record CommunityHealthDashboardReadModelDto(
    DateTimeOffset AsOfUtc,
    int LookbackDays,
    RetentionReadModelDto Retention,
    AttendanceReadModelDto Attendance,
    VolunteerGrowthReadModelDto VolunteerGrowth,
    LeadershipFunnelReadModelDto LeadershipFunnel);

/// <summary>
/// Member retention trend metrics.
/// </summary>
public sealed record RetentionReadModelDto(
    int PreviouslyActiveMembers,
    int CurrentlyActiveMembers,
    int RetainedMembers,
    double RetentionRatePercent);

/// <summary>
/// Attendance distribution and conversion metrics.
/// </summary>
public sealed record AttendanceReadModelDto(
    int RegisteredCount,
    int AcceptedCount,
    int AttendedCount,
    int WaitlistedCount,
    double AttendanceRatePercent);

/// <summary>
/// Volunteer signal growth metrics.
/// </summary>
public sealed record VolunteerGrowthReadModelDto(
    int PreviousWindowSignals,
    int CurrentWindowSignals,
    int DeltaSignals,
    double GrowthRatePercent);

/// <summary>
/// Leadership funnel metrics from discoverability and contribution signals.
/// </summary>
public sealed record LeadershipFunnelReadModelDto(
    int DiscoverableMembers,
    int VolunteerInterestedMembers,
    int ActiveVolunteers,
    int LeadershipCandidates);

/// <summary>
/// Request payload for drafting member-growth opportunity recommendations.
/// </summary>
public sealed record DraftMemberGrowthRecommendationDto(
    int LookbackDays = 90,
    string? RequestedBy = null);

/// <summary>
/// Request payload for drafting weekly community briefings.
/// </summary>
public sealed record DraftWeeklyCommunityBriefingDto(
    int LookbackDays = 90,
    string? RequestedBy = null);

/// <summary>
/// Request payload for approving a recommendation draft.
/// </summary>
public sealed record ApproveRecommendationDraftDto(
    string ApprovedBy,
    string? ApprovalNotes = null);

/// <summary>
/// Shared recommendation/evidence envelope DTO.
/// </summary>
public sealed record RecommendationEnvelopeDto(
    string SchemaVersion,
    string RecommendationKind,
    string Audience,
    string Headline,
    string Summary,
    IReadOnlyList<RecommendationActionDto> Actions,
    IReadOnlyList<RecommendationEvidenceDto> Evidence,
    bool RequiresHumanApproval);

/// <summary>
/// One recommendation action item.
/// </summary>
public sealed record RecommendationActionDto(
    string ActionKey,
    string Title,
    string Rationale,
    string Priority);

/// <summary>
/// One recommendation evidence item.
/// </summary>
public sealed record RecommendationEvidenceDto(
    string EvidenceKey,
    string Observation,
    string Source,
    double? MetricValue,
    string? MetricUnit,
    string Confidence);

/// <summary>
/// Recommendation draft audit metadata DTO.
/// </summary>
public sealed record RecommendationAuditMetadataDto(
    string InputHash,
    string ResponseId,
    string AgentName,
    string AgentVersionTag,
    string TraceParent,
    string? CorrelationId);

/// <summary>
/// Recommendation draft response DTO.
/// </summary>
public sealed record RecommendationDraftDto(
    Guid DraftId,
    string DraftKind,
    RecommendationEnvelopeDto Recommendation,
    bool RequiresHumanApproval,
    string HumanReviewPolicy,
    bool IsApproved,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    RecommendationAuditMetadataDto Audit);

/// <summary>Versioned aggregate for the unified Community Passport UI.</summary>
public sealed record CommunityPassportExperienceDto(
    string SchemaVersion,
    PassportViewerDto Viewer,
    PassportIdentitySummaryDto Identity,
    CommunityStoryDto CommunityStory,
    IReadOnlyList<CommunitySignalDto> Signals,
    IReadOnlyList<GrowthPathwayDto> Journey,
    IReadOnlyList<PassportContributionDto> Contributions,
    IReadOnlyList<ActivityDayDto> Activity,
    PassportPortfolioDto Portfolio,
    IReadOnlyList<PassportConnectionDto> Connections,
    IReadOnlyList<PassportOpportunityDto> Opportunities,
    PassportPrivacyPreferencesDto Privacy,
    PassportResidencyDto Residency);

/// <summary>Current viewer capabilities.</summary>
public sealed record PassportViewerDto(bool IsOwner, bool IsOrganizer, bool CanEdit, bool IsParticipationRedacted);

/// <summary>Passport hero identity.</summary>
public sealed record PassportIdentitySummaryDto(
    Guid MemberId,
    string DisplayName,
    string Email,
    string Initials,
    string CommunityName,
    DateTimeOffset MemberSince,
    string? OccupationStatus,
    string? Affiliation);

/// <summary>Evidence-backed member story.</summary>
public sealed record CommunityStoryDto(string Narrative, IReadOnlyList<string> Evidence);

/// <summary>Explainable community participation signal.</summary>
public sealed record CommunitySignalDto(
    string Kind,
    string Label,
    bool IsOrganizerAwarded,
    string Explanation,
    IReadOnlyList<string> Evidence);

/// <summary>Evidence-based journey pathway.</summary>
public sealed record GrowthPathwayDto(
    string Name,
    string CurrentStage,
    IReadOnlyList<GrowthMilestoneDto> Milestones,
    string Attestation = "Progress derived from verified participation evidence");

/// <summary>One journey milestone.</summary>
public sealed record GrowthMilestoneDto(string Name, string State, string Explanation);

/// <summary>Normalized contribution entry.</summary>
public sealed record PassportContributionDto(
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

/// <summary>Daily contribution activity.</summary>
public sealed record ActivityDayDto(DateOnly Date, int Count, IReadOnlyList<string> Activities);

/// <summary>Verified and curated portfolio projection.</summary>
public sealed record PassportPortfolioDto(
    IReadOnlyList<VerifiedPortfolioHighlightDto> VerifiedHighlights,
    IReadOnlyList<PortfolioEntryDto> Entries);

/// <summary>Verified ledger-backed portfolio highlight.</summary>
public sealed record VerifiedPortfolioHighlightDto(string Title, string Description, string Evidence);

/// <summary>Member-curated portfolio entry.</summary>
public sealed record PortfolioEntryDto(
    Guid Id,
    string Title,
    string Description,
    bool IsFeatured,
    int DisplayOrder,
    IReadOnlyList<PortfolioLinkDto> Links,
    IReadOnlyList<Guid> EvidenceEntryIds);

/// <summary>Typed external portfolio link.</summary>
public sealed record PortfolioLinkDto(string Kind, string Url, string? Label);

/// <summary>Portfolio create/update payload.</summary>
public sealed record UpsertPortfolioEntryDto(
    string Title,
    string Description,
    bool IsFeatured,
    int DisplayOrder,
    IReadOnlyList<PortfolioLinkDto> Links,
    IReadOnlyList<Guid> EvidenceEntryIds);

/// <summary>Portfolio reorder payload.</summary>
public sealed record ReorderPortfolioEntriesDto(IReadOnlyList<Guid> EntryIds);

/// <summary>Contextual member relationship.</summary>
public sealed record PassportConnectionDto(
    Guid MemberId,
    string DisplayName,
    string Kind,
    string Context,
    string Explanation);

/// <summary>Member opportunity history.</summary>
public sealed record PassportOpportunityDto(
    Guid Id,
    string Kind,
    string Title,
    string Description,
    string CurrentStatus,
    string? Outcome,
    DateTimeOffset OfferedAt,
    IReadOnlyList<OpportunityLifecycleEventDto> Lifecycle);

/// <summary>One opportunity lifecycle event.</summary>
public sealed record OpportunityLifecycleEventDto(string Status, DateTimeOffset OccurredAt, string Explanation);

/// <summary>Complete privacy and discovery preferences.</summary>
public sealed record PassportPrivacyPreferencesDto(
    string Visibility,
    bool ShareParticipationWithOrganizers,
    bool AppearInMentorshipRecommendations,
    bool AppearInOpportunityRecommendations,
    bool AppearInCollaboratorDiscovery,
    bool AppearInSpeakerRecommendations,
    bool AppearInVolunteerLeadershipRecommendations,
    bool EnableRelationshipInsights,
    bool ReceiveOpportunityRecommendations);

/// <summary>Privacy and discovery update payload.</summary>
public sealed record UpdatePassportPrivacyPreferencesDto(
    string Visibility,
    bool ShareParticipationWithOrganizers,
    bool AppearInMentorshipRecommendations,
    bool AppearInOpportunityRecommendations,
    bool AppearInCollaboratorDiscovery,
    bool AppearInSpeakerRecommendations,
    bool AppearInVolunteerLeadershipRecommendations,
    bool EnableRelationshipInsights,
    bool ReceiveOpportunityRecommendations);

/// <summary>Organizer directory result.</summary>
public sealed record CommunityPassportDirectoryDto(
    int TotalCount,
    IReadOnlyList<CommunityPassportDirectoryEntryDto> Entries);

/// <summary>One organizer directory entry.</summary>
public sealed record CommunityPassportDirectoryEntryDto(
    Guid MemberId,
    string DisplayName,
    string Community,
    string? OccupationStatus,
    IReadOnlyList<string> Signals,
    DateTimeOffset MemberSince,
    bool IsParticipationShared);

/// <summary>Champion award payload.</summary>
public sealed record AwardChampionSignalDto(string Rationale, IReadOnlyList<Guid> EvidenceEntryIds);

/// <summary>Champion revocation payload.</summary>
public sealed record RevokeChampionSignalDto(string Reason);

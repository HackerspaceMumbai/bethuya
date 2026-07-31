namespace Hackmum.Bethuya.Backend.Contracts;

public sealed record CurationDashboardResponse(
    Guid EventId,
    string EventTitle,
    int Capacity,
    int Applicants,
    EventFairnessTargetsContract Targets,
    FairnessDimensionProgressResponse GenderProgress,
    IReadOnlyList<FairnessDimensionProgressResponse> Dimensions,
    IReadOnlyList<CurationRegistrantResponse> Registrants,
    IReadOnlyList<string> CurationInsights,
    OpportunityEngineResponse OpportunityEngine);

public sealed record FairnessDimensionProgressResponse(
    string Dimension,
    double CurrentPercent,
    double TargetPercent,
    double DeficitPercent,
    int NeededCount,
    bool IsSuppressed,
    string? Alert);

public sealed record ImpactPreviewResponse(
    IReadOnlyDictionary<string, double> DeltaPercentByDimension,
    string Explanation,
    bool IsSuppressed);

public sealed record CurationRegistrantResponse(
    Guid RegistrationId,
    string FullName,
    string Email,
    string Status,
    DateTimeOffset RegisteredAt,
    string? Bio,
    IReadOnlyList<string> Interests,
    CurationProfileSummaryResponse Profile,
    CurationReliabilityResponse Reliability,
    CurationIntentInsightResponse Intent,
    CurationRecommendationResponse Recommendation,
    ImpactPreviewResponse Impact);

public sealed record GenerateCurationProposalRequest(
    string? RequestedBy = null);

public sealed record CurationProfileSummaryResponse(
    string Headline,
    string Organization,
    string HistoryLabel,
    bool IsFirstTimer,
    int PastAcceptedCount,
    int PastAttendedCount,
    bool HasOrganizerStandoutContribution,
    int? GitHubRepoCount,
    bool IsGitHubLinked,
    bool IsLinkedInVerified,
    int MemberSinceYear,
    IReadOnlyList<string> Tags);

public sealed record CurationReliabilityResponse(
    bool HasHistory,
    int Score,
    string Label,
    string Summary);

public sealed record CurationIntentInsightResponse(
    string Summary,
    string Specificity,
    string Evidence,
    string Authenticity,
    IReadOnlyList<string> Signals,
    string Interpretation);

public sealed record CurationRecommendationResponse(
    string Label,
    string Tone,
    string Summary,
    IReadOnlyList<string> Highlights,
    string? AssessmentText = null);

public sealed record OpportunityEngineResponse(
    IReadOnlyList<VolunteerRoleDefinitionResponse> VolunteerRoles,
    IReadOnlyList<VolunteerShiftDefinitionResponse> VolunteerShifts,
    IReadOnlyList<ShiftAssignmentRuleResponse> ShiftAssignmentRules,
    IReadOnlyList<OpportunityCandidateResponse> Candidates,
    IReadOnlyList<OpportunityConflictResponse> Conflicts,
    IReadOnlyList<string> OrganizerWorkflow);

public sealed record VolunteerRoleDefinitionResponse(
    string RoleKey,
    string Label,
    string Summary,
    int RequiredVolunteersPerShift,
    IReadOnlyList<string> SupportedShiftKeys,
    IReadOnlyList<string> PreferredDimensionKeys);

public sealed record VolunteerShiftDefinitionResponse(
    string ShiftKey,
    string Label,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int RequiredVolunteers);

public sealed record ShiftAssignmentRuleResponse(
    string RuleKey,
    string Description,
    string Severity);

public sealed record OpportunityCandidateResponse(
    Guid RegistrationId,
    string FullName,
    string SuggestedRoleKey,
    string SuggestedShiftKey,
    double Score,
    IReadOnlyList<string> Rationale);

public sealed record OpportunityConflictResponse(
    string ConflictKey,
    Guid? RegistrationId,
    string? RoleKey,
    string? ShiftKey,
    string Severity,
    string Message);

public sealed record ApplyCurationDecisionRequest(
    string Action,
    string? Reason = null);

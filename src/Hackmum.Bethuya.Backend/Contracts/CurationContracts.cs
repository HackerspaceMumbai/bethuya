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
    IReadOnlyList<string> CurationInsights);

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
    IReadOnlyList<string> Highlights);

public sealed record ApplyCurationDecisionRequest(
    string Action,
    string? Reason = null);

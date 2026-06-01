using Refit;

namespace Bethuya.Hybrid.Shared.Services;

public interface ICurationApi
{
    [Get("/api/curation/{eventId}")]
    Task<CurationDashboardDto> GetDashboardAsync(Guid eventId, CancellationToken ct = default);

    [Post("/api/curation/{eventId}/proposal")]
    Task<CurationDashboardDto> GenerateProposalAsync(Guid eventId, [Body] GenerateCurationProposalDto request, CancellationToken ct = default);

    [Post("/api/curation/{eventId}/registrants/{registrationId}/decision")]
    Task<CurationDashboardDto> ApplyDecisionAsync(
        Guid eventId,
        Guid registrationId,
        [Body] ApplyCurationDecisionDto request,
        CancellationToken ct = default);
}

public sealed record GenerateCurationProposalDto(string? RequestedBy = null);

public sealed record CurationDashboardDto(
    Guid EventId,
    string EventTitle,
    int Capacity,
    int Applicants,
    EventFairnessTargetsDto Targets,
    FairnessDimensionProgressDto GenderProgress,
    IReadOnlyList<FairnessDimensionProgressDto> Dimensions,
    IReadOnlyList<CurationRegistrantDto> Registrants,
    IReadOnlyList<string> CurationInsights);

public sealed record FairnessDimensionProgressDto(
    string Dimension,
    double CurrentPercent,
    double TargetPercent,
    double DeficitPercent,
    int NeededCount,
    bool IsSuppressed,
    string? Alert);

public sealed record CurationRegistrantDto(
    Guid RegistrationId,
    string FullName,
    string Email,
    string Status,
    DateTimeOffset RegisteredAt,
    string? Bio,
    IReadOnlyList<string> Interests,
    CurationProfileSummaryDto Profile,
    CurationReliabilityDto Reliability,
    CurationIntentInsightDto Intent,
    CurationRecommendationDto Recommendation,
    ImpactPreviewDto Impact);

public sealed record ImpactPreviewDto(
    IReadOnlyDictionary<string, double> DeltaPercentByDimension,
    string Explanation,
    bool IsSuppressed);

public sealed record CurationProfileSummaryDto(
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

public sealed record CurationReliabilityDto(
    bool HasHistory,
    int Score,
    string Label,
    string Summary);

public sealed record CurationIntentInsightDto(
    string Summary,
    string Specificity,
    string Evidence,
    string Authenticity,
    IReadOnlyList<string> Signals,
    string Interpretation);

public sealed record CurationRecommendationDto(
    string Label,
    string Tone,
    string Summary,
    IReadOnlyList<string> Highlights,
    string? AssessmentText = null);

public sealed record ApplyCurationDecisionDto(
    string Action,
    string? Reason = null);

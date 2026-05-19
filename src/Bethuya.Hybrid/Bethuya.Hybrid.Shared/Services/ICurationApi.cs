using Refit;

namespace Bethuya.Hybrid.Shared.Services;

public interface ICurationApi
{
    [Get("/api/curation/{eventId}")]
    Task<CurationDashboardDto> GetDashboardAsync(Guid eventId, CancellationToken ct = default);

    [Post("/api/curation/{eventId}/proposal")]
    Task<CurationDashboardDto> GenerateProposalAsync(Guid eventId, [Body] GenerateCurationProposalDto request, CancellationToken ct = default);
}

public sealed record GenerateCurationProposalDto(string? RequestedBy = null);

public sealed record CurationDashboardDto(
    Guid EventId,
    int Capacity,
    int Applicants,
    EventFairnessTargetsDto Targets,
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
    IReadOnlyList<string> Interests,
    ImpactPreviewDto Impact);

public sealed record ImpactPreviewDto(
    IReadOnlyDictionary<string, double> DeltaPercentByDimension,
    string Explanation,
    bool IsSuppressed);

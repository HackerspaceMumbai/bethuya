namespace Hackmum.Bethuya.Backend.Contracts;

public sealed record CurationDashboardResponse(
    Guid EventId,
    int Capacity,
    int Applicants,
    EventFairnessTargetsContract Targets,
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
    IReadOnlyList<string> Interests,
    ImpactPreviewResponse Impact);

public sealed record GenerateCurationProposalRequest(
    string? RequestedBy = null);

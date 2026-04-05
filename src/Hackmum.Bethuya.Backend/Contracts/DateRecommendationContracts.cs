namespace Hackmum.Bethuya.Backend.Contracts;

/// <summary>API request for date recommendation.</summary>
public sealed record RecommendDatesApiRequest(
    string? Title = null,
    string? Type = null,
    string? Description = null,
    string? Location = null,
    int? Capacity = null);

/// <summary>API response with recommended event dates.</summary>
public sealed record RecommendDatesApiResponse(
    string StartDate,
    string StartTime,
    string EndDate,
    string EndTime,
    string? Reasoning = null);

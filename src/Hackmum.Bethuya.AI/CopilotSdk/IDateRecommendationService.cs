namespace Hackmum.Bethuya.AI.CopilotSdk;

/// <summary>Context about the event being created, used to guide date recommendations.</summary>
public sealed record DateRecommendationContext(
    string? Title = null,
    string? Type = null,
    string? Description = null,
    string? Location = null,
    int? Capacity = null);

/// <summary>AI-recommended date and time for an event.</summary>
public sealed record DateRecommendation(
    DateOnly StartDate,
    TimeOnly StartTime,
    DateOnly EndDate,
    TimeOnly EndTime,
    string? Reasoning = null);

/// <summary>Recommends optimal event dates using the GitHub Copilot SDK.</summary>
public interface IDateRecommendationService
{
    /// <summary>
    /// Requests an AI-powered date recommendation based on community event patterns
    /// and the provided event context.
    /// </summary>
    Task<DateRecommendation> RecommendAsync(
        DateRecommendationContext context,
        CancellationToken ct = default);
}

using Hackmum.Bethuya.Backend.Contracts;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Generates a member story from an evidence-backed passport projection.
/// </summary>
public interface ICommunityStoryGenerator
{
    CommunityStoryResponse Generate(CommunityStoryInput input);
}

/// <summary>
/// Bounded, non-sensitive inputs used to generate a Community Story.
/// </summary>
public sealed record CommunityStoryInput(
    string DisplayName,
    int MemberSinceYear,
    IReadOnlyList<CommunitySignalResponse> Signals,
    IReadOnlyList<PassportContributionResponse> Contributions,
    IReadOnlyList<PassportOpportunityResponse> Opportunities);

/// <summary>
/// Deterministic Phase 1 story generator. A later AI implementation can replace this service.
/// </summary>
public sealed class DeterministicCommunityStoryGenerator : ICommunityStoryGenerator
{
    public CommunityStoryResponse Generate(CommunityStoryInput input)
    {
        var evidence = input.Contributions
            .OrderByDescending(contribution => contribution.OccurredAt)
            .Take(3)
            .Select(contribution => contribution.Title)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var signalLabels = input.Signals
            .Take(3)
            .Select(signal => signal.Label)
            .ToArray();

        var opening = $"{input.DisplayName} joined the community in {input.MemberSinceYear}.";
        var participation = signalLabels.Length == 0
            ? " Their passport is beginning to record a community journey grounded in verified participation."
            : $" Their participation reflects the {JoinNaturalLanguage(signalLabels)} signals.";
        var outcome = input.Opportunities.Any(opportunity =>
            opportunity.CurrentStatus == Hackmum.Bethuya.Core.Enums.MemberOpportunityStatus.Completed)
            ? " Completed opportunities show how that participation is turning into community outcomes."
            : evidence.Count > 0
                ? $" Recent contributions include {JoinNaturalLanguage(evidence)}."
                : string.Empty;

        return new CommunityStoryResponse(opening + participation + outcome, evidence);
    }

    private static string JoinNaturalLanguage(IReadOnlyList<string> values)
        => values.Count switch
        {
            0 => string.Empty,
            1 => values[0],
            2 => $"{values[0]} and {values[1]}",
            _ => $"{string.Join(", ", values.Take(values.Count - 1))}, and {values[^1]}"
        };
}

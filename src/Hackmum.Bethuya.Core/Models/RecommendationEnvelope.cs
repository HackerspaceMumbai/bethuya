namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Shared recommendation and evidence schema used by planner, curator, reporter, and recommendation services.
/// </summary>
public sealed record RecommendationEnvelope(
    string SchemaVersion,
    string RecommendationKind,
    string Audience,
    string Headline,
    string Summary,
    IReadOnlyList<RecommendationAction> Actions,
    IReadOnlyList<RecommendationEvidence> Evidence,
    bool RequiresHumanApproval = true);

/// <summary>
/// One actionable recommendation item.
/// </summary>
public sealed record RecommendationAction(
    string ActionKey,
    string Title,
    string Rationale,
    string Priority);

/// <summary>
/// One evidence artifact supporting a recommendation.
/// </summary>
public sealed record RecommendationEvidence(
    string EvidenceKey,
    string Observation,
    string Source,
    double? MetricValue = null,
    string? MetricUnit = null,
    string Confidence = "medium");

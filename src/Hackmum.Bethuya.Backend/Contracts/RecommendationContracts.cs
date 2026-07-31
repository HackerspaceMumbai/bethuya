using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Backend.Contracts;

public sealed record DraftMemberGrowthRecommendationRequest(
    int LookbackDays = 90,
    string? RequestedBy = null);

public sealed record DraftWeeklyCommunityBriefingRequest(
    int LookbackDays = 90,
    string? RequestedBy = null);

public sealed record ApproveRecommendationDraftRequest(
    string ApprovedBy,
    string? ApprovalNotes = null);

public sealed record RecommendationAuditMetadataResponse(
    string InputHash,
    string ResponseId,
    string AgentName,
    string AgentVersionTag,
    string TraceParent,
    string? CorrelationId);

public sealed record RecommendationDraftResponse(
    Guid DraftId,
    string DraftKind,
    RecommendationEnvelope Recommendation,
    bool RequiresHumanApproval,
    string HumanReviewPolicy,
    bool IsApproved,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    RecommendationAuditMetadataResponse Audit);

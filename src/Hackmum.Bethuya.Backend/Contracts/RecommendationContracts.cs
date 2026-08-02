using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Backend.Contracts;

/// <summary>Request to draft member growth recommendations using the Reporter Agent.</summary>
public sealed record DraftMemberGrowthRecommendationRequest(
    int LookbackDays = 90,
    string? RequestedBy = null);

/// <summary>Request to draft a weekly community briefing using the Reporter Agent.</summary>
public sealed record DraftWeeklyCommunityBriefingRequest(
    int LookbackDays = 90,
    string? RequestedBy = null);

/// <summary>Request to approve a recommendation draft by an authorised human reviewer.</summary>
public sealed record ApproveRecommendationDraftRequest(
    /// <summary>Identity of the approver (display name, email, or user id).</summary>
    string ApprovedBy,
    /// <summary>Optional free-text notes recorded alongside the approval decision.</summary>
    string? ApprovalNotes = null);

/// <summary>Immutable audit record for a single agent invocation that produced a recommendation draft.</summary>
public sealed record RecommendationAuditMetadataResponse(
    /// <summary>SHA-256 hash of the serialised agent request payload.</summary>
    string InputHash,
    /// <summary>Agent-assigned response identifier for correlation with agent logs.</summary>
    string ResponseId,
    /// <summary>Logical name of the agent that produced the draft.</summary>
    string AgentName,
    /// <summary>Version tag of the agent at invocation time.</summary>
    string AgentVersionTag,
    /// <summary>W3C traceparent header value linking to the distributed trace.</summary>
    string TraceParent,
    /// <summary>Optional application-level correlation id propagated through the request.</summary>
    string? CorrelationId);

/// <summary>Human-reviewable recommendation draft returned by the Reporter Agent pipeline.</summary>
public sealed record RecommendationDraftResponse(
    /// <summary>Unique draft identifier used to approve or retrieve this draft.</summary>
    Guid DraftId,
    /// <summary>Discriminator for the draft kind (e.g. <c>MemberGrowth</c>, <c>WeeklyCommunityBriefing</c>).</summary>
    string DraftKind,
    /// <summary>Structured recommendation content produced by the agent.</summary>
    RecommendationEnvelope Recommendation,
    /// <summary>Indicates that a human must explicitly approve this draft before it may be actioned.</summary>
    bool RequiresHumanApproval,
    /// <summary>Name of the policy that governs human-in-the-loop review for this draft kind.</summary>
    string HumanReviewPolicy,
    /// <summary>Whether a human has already approved this draft.</summary>
    bool IsApproved,
    /// <summary>UTC timestamp when the draft was created.</summary>
    DateTimeOffset CreatedAt,
    /// <summary>UTC timestamp when the draft was approved, or <see langword="null"/> if not yet approved.</summary>
    DateTimeOffset? ApprovedAt,
    /// <summary>Audit metadata linking this draft to the agent invocation that produced it.</summary>
    RecommendationAuditMetadataResponse Audit);

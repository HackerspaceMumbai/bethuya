using System.Diagnostics;
using System.Text.Json;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Creates human-review-gated recommendation drafts backed by auditable invocation metadata.
/// </summary>
public sealed class CommunityRecommendationService(
    CommunityJourneyReadModelService journeyReadModelService,
    IDecisionRepository decisionRepository)
{
    private const string RecommendationSchemaVersion = "1.0";
    private const string RecommendationAgentName = "community-recommendation-engine";
    private const string RecommendationAgentVersionTag = "v1";
    private const string HumanReviewPolicy = "explicit-human-approval-required";

    public async Task<RecommendationDraftResponse> DraftMemberGrowthOpportunityAsync(
        DraftMemberGrowthRecommendationRequest request,
        string requestedBy,
        CancellationToken ct = default)
    {
        var dashboard = await journeyReadModelService.GetDashboardReadModelAsync(request.LookbackDays, ct);
        var recommendation = BuildMemberGrowthRecommendation(dashboard);
        return await PersistDraftAsync(
            draftKind: "member-growth-opportunity",
            recommendation,
            request,
            requestedBy,
            ct);
    }

    public async Task<RecommendationDraftResponse> DraftWeeklyBriefingAsync(
        DraftWeeklyCommunityBriefingRequest request,
        string requestedBy,
        CancellationToken ct = default)
    {
        var dashboard = await journeyReadModelService.GetDashboardReadModelAsync(request.LookbackDays, ct);
        var recommendation = BuildWeeklyBriefingRecommendation(dashboard);
        return await PersistDraftAsync(
            draftKind: "weekly-community-briefing",
            recommendation,
            request,
            requestedBy,
            ct);
    }

    public async Task<RecommendationDraftResponse> ApproveDraftAsync(
        Guid draftId,
        ApproveRecommendationDraftRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApprovedBy))
        {
            throw new InvalidOperationException("ApprovedBy is required.");
        }

        var decision = await decisionRepository.GetByIdAsync(draftId, ct)
            ?? throw new KeyNotFoundException("Recommendation draft not found.");

        if (!IsSupportedDraftKind(decision.EntityType))
        {
            throw new InvalidOperationException("Decision is not a recommendation draft.");
        }

        var payload = DeserializePayload(decision.Diff)
            ?? throw new InvalidOperationException("Recommendation draft payload is invalid.");

        if (decision.Status == DecisionStatus.Applied)
        {
            return ToDraftResponse(decision.Id, decision.EntityType, payload);
        }

        decision.Status = DecisionStatus.Applied;
        decision.Type = DecisionType.Approve;
        decision.Reason = string.IsNullOrWhiteSpace(request.ApprovalNotes)
            ? $"Approved by {request.ApprovedBy}"
            : $"Approved by {request.ApprovedBy}: {request.ApprovalNotes}";

        var approvedPayload = payload with
        {
            ApprovedAt = DateTimeOffset.UtcNow,
            ApprovedBy = request.ApprovedBy,
            ApprovalNotes = request.ApprovalNotes
        };
        decision.Diff = JsonSerializer.Serialize(approvedPayload);

        await decisionRepository.UpdateAsync(decision, ct);
        return ToDraftResponse(decision.Id, decision.EntityType, approvedPayload);
    }

    private async Task<RecommendationDraftResponse> PersistDraftAsync<TRequest>(
        string draftKind,
        RecommendationEnvelope recommendation,
        TRequest request,
        string requestedBy,
        CancellationToken ct)
    {
        var traceParent = AiAuditMetadata.NormalizeOptionalTraceMetadata(Activity.Current?.Id);
        var correlationId = AiAuditMetadata.NormalizeRequiredTraceMetadata(Activity.Current?.TraceId.ToString() ?? Guid.CreateVersion7().ToString("N"));
        var createdAt = DateTimeOffset.UtcNow;
        var inputHash = AiAuditMetadata.ComputeInputHash(request);

        var draftId = Guid.CreateVersion7();
        var payload = new RecommendationDraftPayload(
            SchemaVersion: RecommendationSchemaVersion,
            Recommendation: recommendation,
            Audit: new RecommendationAuditPayload(
                InputHash: inputHash,
                ResponseId: AiAuditMetadata.NormalizeRequiredPersistedProviderMetadata($"rec-{draftId:N}", "rec-unavailable"),
                AgentName: AiAuditMetadata.NormalizeRequiredPersistedProviderMetadata(RecommendationAgentName, "community-recommendation-engine"),
                AgentVersionTag: AiAuditMetadata.NormalizeRequiredPersistedProviderMetadata(RecommendationAgentVersionTag, "unknown"),
                TraceParent: AiAuditMetadata.BuildAuditTraceParent(traceParent, correlationId),
                CorrelationId: correlationId),
            HumanReviewPolicy: HumanReviewPolicy,
            CreatedAt: createdAt,
            ApprovedAt: null,
            ApprovedBy: null,
            ApprovalNotes: null);

        var decision = new Decision
        {
            Id = draftId,
            EntityType = draftKind,
            EntityId = draftId,
            Type = DecisionType.Approve,
            Status = DecisionStatus.Pending,
            DecidedBy = requestedBy,
            Reason = recommendation.Headline,
            Diff = JsonSerializer.Serialize(payload)
        };

        await decisionRepository.CreateAsync(decision, ct);
        return ToDraftResponse(decision.Id, decision.EntityType, payload);
    }

    private static RecommendationEnvelope BuildMemberGrowthRecommendation(CommunityHealthDashboardReadModelResponse dashboard)
    {
        var retentionGap = Math.Max(0d, 100d - dashboard.Retention.RetentionRatePercent);
        var volunteerDelta = dashboard.VolunteerGrowth.DeltaSignals;
        var growthDirection = volunteerDelta >= 0 ? "upward" : "downward";

        return new RecommendationEnvelope(
            SchemaVersion: RecommendationSchemaVersion,
            RecommendationKind: "member-growth-opportunity",
            Audience: "organizer",
            Headline: "Strengthen member-growth opportunity flow for the next cycle.",
            Summary: $"Volunteer signal trend is {growthDirection} ({volunteerDelta:+#;-#;0}) and retention gap is {retentionGap:0.##}% across the active lookback window.",
            Actions:
            [
                new RecommendationAction(
                    ActionKey: "target-discoverable-members",
                    Title: "Engage discoverable members with volunteer opportunities.",
                    Rationale: "Converts discoverability into active participation and leadership readiness.",
                    Priority: "high"),
                new RecommendationAction(
                    ActionKey: "follow-up-waitlisted-members",
                    Title: "Follow up with waitlisted and accepted members for next-event conversion.",
                    Rationale: "Improves continuity and lifts retention/attendance conversion.",
                    Priority: "medium")
            ],
            Evidence:
            [
                new RecommendationEvidence(
                    EvidenceKey: "retention-rate",
                    Observation: "Current retention rate across comparable windows.",
                    Source: "community-health-read-model",
                    MetricValue: dashboard.Retention.RetentionRatePercent,
                    MetricUnit: "percent",
                    Confidence: "high"),
                new RecommendationEvidence(
                    EvidenceKey: "volunteer-growth-delta",
                    Observation: "Volunteer signals delta between current and previous windows.",
                    Source: "community-health-read-model",
                    MetricValue: volunteerDelta,
                    MetricUnit: "signals",
                    Confidence: "high"),
                new RecommendationEvidence(
                    EvidenceKey: "leadership-candidates",
                    Observation: "Members currently matching leadership-candidate criteria.",
                    Source: "community-health-read-model",
                    MetricValue: dashboard.LeadershipFunnel.LeadershipCandidates,
                    MetricUnit: "members",
                    Confidence: "medium")
            ]);
    }

    private static RecommendationEnvelope BuildWeeklyBriefingRecommendation(CommunityHealthDashboardReadModelResponse dashboard)
    {
        var attendanceRate = dashboard.Attendance.AttendanceRatePercent;
        var retentionRate = dashboard.Retention.RetentionRatePercent;
        var activeVolunteers = dashboard.LeadershipFunnel.ActiveVolunteers;

        return new RecommendationEnvelope(
            SchemaVersion: RecommendationSchemaVersion,
            RecommendationKind: "weekly-community-briefing",
            Audience: "organizer",
            Headline: "Weekly community briefing draft ready for organizer review.",
            Summary: $"Attendance conversion is {attendanceRate:0.##}%, retention is {retentionRate:0.##}%, and active volunteers are {activeVolunteers}.",
            Actions:
            [
                new RecommendationAction(
                    ActionKey: "publish-weekly-briefing",
                    Title: "Review and publish this weekly briefing with edits.",
                    Rationale: "Human-reviewed communication keeps the briefing accurate and community-aligned.",
                    Priority: "high"),
                new RecommendationAction(
                    ActionKey: "prioritize-volunteer-nudges",
                    Title: "Prioritize volunteer re-engagement nudges this week.",
                    Rationale: "Sustains leadership funnel momentum between events.",
                    Priority: "medium")
            ],
            Evidence:
            [
                new RecommendationEvidence(
                    EvidenceKey: "attendance-rate",
                    Observation: "Attendance conversion over accepted registrations.",
                    Source: "community-health-read-model",
                    MetricValue: attendanceRate,
                    MetricUnit: "percent",
                    Confidence: "high"),
                new RecommendationEvidence(
                    EvidenceKey: "retention-rate",
                    Observation: "Retained member ratio for active windows.",
                    Source: "community-health-read-model",
                    MetricValue: retentionRate,
                    MetricUnit: "percent",
                    Confidence: "high"),
                new RecommendationEvidence(
                    EvidenceKey: "active-volunteers",
                    Observation: "Members currently classified as active volunteers.",
                    Source: "community-health-read-model",
                    MetricValue: activeVolunteers,
                    MetricUnit: "members",
                    Confidence: "medium")
            ]);
    }

    private static RecommendationDraftResponse ToDraftResponse(
        Guid draftId,
        string draftKind,
        RecommendationDraftPayload payload)
        => new(
            DraftId: draftId,
            DraftKind: draftKind,
            Recommendation: payload.Recommendation,
            RequiresHumanApproval: payload.Recommendation.RequiresHumanApproval,
            HumanReviewPolicy: payload.HumanReviewPolicy,
            IsApproved: payload.ApprovedAt.HasValue,
            CreatedAt: payload.CreatedAt,
            ApprovedAt: payload.ApprovedAt,
            Audit: new RecommendationAuditMetadataResponse(
                InputHash: payload.Audit.InputHash,
                ResponseId: payload.Audit.ResponseId,
                AgentName: payload.Audit.AgentName,
                AgentVersionTag: payload.Audit.AgentVersionTag,
                TraceParent: payload.Audit.TraceParent,
                CorrelationId: payload.Audit.CorrelationId));

    private static RecommendationDraftPayload? DeserializePayload(string? payload)
        => string.IsNullOrWhiteSpace(payload)
            ? null
            : JsonSerializer.Deserialize<RecommendationDraftPayload>(payload);

    private static bool IsSupportedDraftKind(string entityType)
        => entityType is "member-growth-opportunity" or "weekly-community-briefing";

    private sealed record RecommendationDraftPayload(
        string SchemaVersion,
        RecommendationEnvelope Recommendation,
        RecommendationAuditPayload Audit,
        string HumanReviewPolicy,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ApprovedAt,
        string? ApprovedBy,
        string? ApprovalNotes);

    private sealed record RecommendationAuditPayload(
        string InputHash,
        string ResponseId,
        string AgentName,
        string AgentVersionTag,
        string TraceParent,
        string? CorrelationId);
}

using System.Diagnostics;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Manages mentor opt-in lifecycle, community discovery, and pairing recommendation drafts.
/// Reuses Passport identity badges, participation ledger signals, and the shared
/// <see cref="RecommendationEnvelope"/> schema from earlier slices.
/// </summary>
public sealed class MentorshipService(
    CommunityPassportService passportService,
    IMentorProfileRepository mentorRepo,
    IDecisionRepository decisionRepository,
    CommunityJourneyReadModelService journeyReadModelService)
{
    private const string RecommendationSchemaVersion = "1.0";
    private const string RecommendationAgentName = "mentorship-recommendation-engine";
    private const string RecommendationAgentVersionTag = "v1";
    private const string HumanReviewPolicy = "explicit-human-approval-required";
    private const string MentorPairingDraftKind = "mentor-pairing-suggestion";

    // ─── Opt-in ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates or updates a mentor profile for the authenticated community member.
    /// Auto-provisions the Community Passport member record if not yet present.
    /// </summary>
    public async Task<MentorProfileResponse> OptInAsync(
        CommunitySubjectContext subject,
        MentorOptInRequest request,
        CancellationToken ct = default)
    {
        var member = await passportService.EnsureMemberProvisionedAsync(subject, ct);
        return await UpsertProfileAsync(member, request, MentorshipStatus.OptedIn, ct);
    }

    /// <summary>
    /// Transitions a mentor profile to <see cref="MentorshipStatus.Paused"/> or
    /// <see cref="MentorshipStatus.OptedOut"/> and persists the change.
    /// </summary>
    public async Task<MentorProfileResponse> UpdateStatusAsync(
        CommunitySubjectContext subject,
        MentorStatusUpdateRequest request,
        CancellationToken ct = default)
    {
        var member = await passportService.EnsureMemberProvisionedAsync(subject, ct);
        var existing = await mentorRepo.GetByMemberIdAsync(member.Id, ct)
            ?? throw new InvalidOperationException("No mentor profile found. Opt in first.");

        existing.Status = request.Status;
        existing.IsDiscoverable = request.Status == MentorshipStatus.OptedIn;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await mentorRepo.UpsertAsync(existing, ct);
        return ToResponse(updated, member);
    }

    /// <summary>
    /// Returns the calling member's own mentor profile, or <see langword="null"/> if they have not opted in.
    /// </summary>
    public async Task<MentorProfileResponse?> GetMyProfileAsync(
        CommunitySubjectContext subject,
        CancellationToken ct = default)
    {
        var member = await passportService.EnsureMemberProvisionedAsync(subject, ct);
        var profile = await mentorRepo.GetByMemberIdAsync(member.Id, ct);
        return profile is null ? null : ToResponse(profile, member);
    }

    // ─── Discovery ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns community members currently opted in and discoverable as mentors,
    /// optionally filtered by expertise area.  Respects community-level privacy settings.
    /// </summary>
    public async Task<IReadOnlyList<MentorDiscoveryEntryResponse>> DiscoverMentorsAsync(
        IReadOnlyList<MentorExpertiseArea>? filterAreas,
        int limit,
        CancellationToken ct = default)
    {
        if (limit is <= 0 or > 100)
        {
            limit = 20;
        }

        var profiles = await mentorRepo.GetDiscoverableMentorsAsync(filterAreas, limit, ct);

        return profiles.Select(profile => new MentorDiscoveryEntryResponse(
            MentorProfileId: profile.Id.Value,
            DisplayName: profile.Member?.DisplayName ?? "Community Member",
            OccupationStatus: profile.Member?.OccupationStatus,
            CompanyName: profile.Member?.CompanyName,
            ExpertiseAreas: profile.ExpertiseAreas,
            IntroductionBio: profile.IntroductionBio,
            AvailabilityHoursPerMonth: profile.AvailabilityHoursPerMonth)).ToList();
    }

    // ─── Recommendation ───────────────────────────────────────────────────────

    /// <summary>
    /// Generates an organizer-facing mentor-pairing suggestion draft backed by community journey signals.
    /// The draft requires human approval before any action is taken on it.
    /// </summary>
    public async Task<RecommendationDraftResponse> DraftMentorPairingSuggestionAsync(
        DraftMentorPairingSuggestionRequest request,
        string requestedBy,
        CancellationToken ct = default)
    {
        var dashboard = await journeyReadModelService.GetDashboardReadModelAsync(request.LookbackDays, ct);
        var discoverableMentors = await mentorRepo.GetDiscoverableMentorsAsync(request.FocusAreas, 50, ct);

        var recommendation = BuildMentorPairingRecommendation(dashboard, discoverableMentors, request.FocusAreas);
        return await PersistDraftAsync(recommendation, request, requestedBy, ct);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task<MentorProfileResponse> UpsertProfileAsync(
        CommunityMember member,
        MentorOptInRequest request,
        MentorshipStatus status,
        CancellationToken ct)
    {
        var profile = new MentorProfile
        {
            MemberId = member.Id,
            Status = status,
            IntroductionBio = string.IsNullOrWhiteSpace(request.IntroductionBio) ? null : request.IntroductionBio.Trim(),
            AvailabilityHoursPerMonth = Math.Clamp(request.AvailabilityHoursPerMonth, 1, 40),
            IsDiscoverable = request.IsDiscoverable && status == MentorshipStatus.OptedIn
        };
        profile.ExpertiseAreas = request.ExpertiseAreas;

        var persisted = await mentorRepo.UpsertAsync(profile, ct);
        return ToResponse(persisted, member);
    }

    private static RecommendationEnvelope BuildMentorPairingRecommendation(
        CommunityHealthDashboardReadModelResponse dashboard,
        List<MentorProfile> discoverableMentors,
        IReadOnlyList<MentorExpertiseArea>? focusAreas)
    {
        var mentorCount = discoverableMentors.Count;
        var leadershipCandidates = dashboard.LeadershipFunnel.LeadershipCandidates;
        var focusLabel = focusAreas is { Count: > 0 }
            ? string.Join(", ", focusAreas)
            : "all areas";

        return new RecommendationEnvelope(
            SchemaVersion: RecommendationSchemaVersion,
            RecommendationKind: MentorPairingDraftKind,
            Audience: "organizer",
            Headline: "Mentor-pairing opportunity: connect leadership-ready members with active mentors.",
            Summary: $"{mentorCount} discoverable mentor(s) available across {focusLabel}. " +
                     $"{leadershipCandidates} member(s) match leadership-candidate criteria for potential pairing.",
            Actions:
            [
                new RecommendationAction(
                    ActionKey: "initiate-mentor-pairing",
                    Title: "Review suggested mentor-mentee pairings and send invitations.",
                    Rationale: "Structured mentorship accelerates leadership pipeline growth and community retention.",
                    Priority: "high"),
                new RecommendationAction(
                    ActionKey: "recruit-additional-mentors",
                    Title: "Reach out to Volunteer Track members to expand the mentor pool.",
                    Rationale: $"With {mentorCount} active mentor(s), expanding capacity ensures all interested mentees are matched.",
                    Priority: "medium")
            ],
            Evidence:
            [
                new RecommendationEvidence(
                    EvidenceKey: "active-mentor-count",
                    Observation: "Community members currently opted in as discoverable mentors.",
                    Source: "mentorship-directory",
                    MetricValue: mentorCount,
                    MetricUnit: "mentors",
                    Confidence: "high"),
                new RecommendationEvidence(
                    EvidenceKey: "leadership-candidates",
                    Observation: "Members meeting the leadership-candidate threshold in the current lookback window.",
                    Source: "community-health-read-model",
                    MetricValue: leadershipCandidates,
                    MetricUnit: "members",
                    Confidence: "medium"),
                new RecommendationEvidence(
                    EvidenceKey: "volunteer-growth-delta",
                    Observation: "Net volunteer signal delta between current and previous lookback windows.",
                    Source: "community-health-read-model",
                    MetricValue: dashboard.VolunteerGrowth.DeltaSignals,
                    MetricUnit: "signals",
                    Confidence: "high")
            ]);
    }

    private async Task<RecommendationDraftResponse> PersistDraftAsync<TRequest>(
        RecommendationEnvelope recommendation,
        TRequest request,
        string requestedBy,
        CancellationToken ct)
    {
        var traceParent = AiAuditMetadata.NormalizeOptionalTraceMetadata(Activity.Current?.Id);
        var correlationId = AiAuditMetadata.NormalizeRequiredTraceMetadata(
            Activity.Current?.TraceId.ToString() ?? Guid.CreateVersion7().ToString("N"));
        var inputHash = AiAuditMetadata.ComputeInputHash(request);
        var createdAt = DateTimeOffset.UtcNow;
        var draftId = Guid.CreateVersion7();

        var payload = new System.Text.Json.Nodes.JsonObject
        {
            ["SchemaVersion"] = RecommendationSchemaVersion,
            ["Recommendation"] = System.Text.Json.JsonSerializer.SerializeToNode(recommendation),
            ["Audit"] = new System.Text.Json.Nodes.JsonObject
            {
                ["InputHash"] = inputHash,
                ["ResponseId"] = AiAuditMetadata.NormalizeRequiredPersistedProviderMetadata($"mrec-{draftId:N}", "mrec-unavailable"),
                ["AgentName"] = AiAuditMetadata.NormalizeRequiredPersistedProviderMetadata(RecommendationAgentName, "mentorship-recommendation-engine"),
                ["AgentVersionTag"] = AiAuditMetadata.NormalizeRequiredPersistedProviderMetadata(RecommendationAgentVersionTag, "unknown"),
                ["TraceParent"] = AiAuditMetadata.BuildAuditTraceParent(traceParent, correlationId),
                ["CorrelationId"] = correlationId
            },
            ["HumanReviewPolicy"] = HumanReviewPolicy,
            ["CreatedAt"] = createdAt,
            ["ApprovedAt"] = null,
            ["ApprovedBy"] = null,
            ["ApprovalNotes"] = null
        };

        var decision = new Core.Models.Decision
        {
            Id = draftId,
            EntityType = MentorPairingDraftKind,
            EntityId = draftId,
            Type = Core.Enums.DecisionType.Approve,
            Status = Core.Enums.DecisionStatus.Pending,
            DecidedBy = requestedBy,
            Reason = recommendation.Headline,
            Diff = payload.ToJsonString()
        };

        await decisionRepository.CreateAsync(decision, ct);

        return new RecommendationDraftResponse(
            DraftId: draftId,
            DraftKind: MentorPairingDraftKind,
            Recommendation: recommendation,
            RequiresHumanApproval: recommendation.RequiresHumanApproval,
            HumanReviewPolicy: HumanReviewPolicy,
            IsApproved: false,
            CreatedAt: createdAt,
            ApprovedAt: null,
            Audit: new RecommendationAuditMetadataResponse(
                InputHash: inputHash,
                ResponseId: $"mrec-{draftId:N}",
                AgentName: RecommendationAgentName,
                AgentVersionTag: RecommendationAgentVersionTag,
                TraceParent: AiAuditMetadata.BuildAuditTraceParent(traceParent, correlationId),
                CorrelationId: correlationId));
    }

    private static MentorProfileResponse ToResponse(MentorProfile profile, CommunityMember member)
        => new(
            MentorProfileId: profile.Id.Value,
            MemberDisplayName: member.DisplayName,
            MemberEmail: member.Email,
            Status: profile.Status,
            ExpertiseAreas: profile.ExpertiseAreas,
            IntroductionBio: profile.IntroductionBio,
            AvailabilityHoursPerMonth: profile.AvailabilityHoursPerMonth,
            IsDiscoverable: profile.IsDiscoverable,
            OptedInAt: profile.OptedInAt,
            UpdatedAt: profile.UpdatedAt);
}

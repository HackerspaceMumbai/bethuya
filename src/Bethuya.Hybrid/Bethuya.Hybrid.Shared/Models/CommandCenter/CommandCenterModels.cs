namespace Bethuya.Hybrid.Shared.Models.CommandCenter;

/// <summary>
/// The two homepage surfaces. The distinction is operations versus participation,
/// not individual roles: every operating role shares one command center layout.
/// </summary>
public enum CommandCenterSurface
{
    /// <summary>Community Operations Command Center — shared by all operating roles.</summary>
    Operations,

    /// <summary>Community Participation homepage — journey oriented.</summary>
    Participation
}

public enum CommunityRole
{
    CommunityAdministrator,
    EventOrganizer,
    VolunteerLead,
    MentorshipLead,
    CommunityMember,
    EmergingContributor,

    /// <summary>Registered for an event with minimal community history; sees the onboarding journey.</summary>
    EventParticipant
}

/// <summary>Retained for service signature compatibility; V9 no longer switches homepage layout by mode.</summary>
public enum CommandCenterMode
{
    Strategic,
    Event
}

public enum DecisionKind
{
    WaitlistAdmission,
    VolunteerAssignment,
    SpeakerConfirmation,
    MentorNomination,
    SponsorApproval
}

public enum SignalUrgency
{
    Recommendation,
    Intervention
}

public enum JourneyStageStatus
{
    Completed,
    Current,
    Recommended,
    Future
}

/// <summary>Narrative foresight for the Strategic Community Insight hero.</summary>
public sealed record StrategicInsight(
    string Observation,
    string Risk,
    string RecommendedAction,
    string ActionLabel,
    string ActionRoute);

/// <summary>A single human-reviewed decision in the Immediate Decisions inbox.</summary>
public sealed record ImmediateDecision(
    string DecisionId,
    DecisionKind Kind,
    string Title,
    string Summary,
    IReadOnlyList<string> Evidence,
    string PrimaryVerb,
    string SubjectName);

/// <summary>Program outcome card — produced leaders, not session counts.</summary>
public sealed record CommunityProgram(
    string ProgramId,
    string Name,
    string Tagline,
    IReadOnlyList<string> ProducedOutcomes,
    string WorkspaceRoute);

/// <summary>Operational event workspace with readiness-first health and impact lines.</summary>
public sealed record EventWorkspace(
    string WorkspaceId,
    string Title,
    string DateLabel,
    bool IsHero,
    int ReadinessPercent,
    string ReadinessLabel,
    IReadOnlyList<string> Operations,
    IReadOnlyList<string> CommunityImpact,
    string EventRoute,
    string StatusLabel);

/// <summary>Leadership recommendation with journey placement and ledger evidence.</summary>
public sealed record LeadershipSignal(
    string SignalId,
    string SignalTitle,
    string MemberName,
    string MemberInitials,
    IReadOnlyList<string> JourneyStages,
    string NextStage,
    string RecommendedFuture,
    IReadOnlyList<string> Evidence,
    string ActionLabel,
    SignalUrgency Urgency,
    string ActionRoute);

/// <summary>One stage on a member growth path.</summary>
public sealed record JourneyStage(string Name, JourneyStageStatus Status);

/// <summary>Member growth journey with evidence for the slide-over drawer.</summary>
public sealed record GrowthJourney(
    string MemberId,
    string MemberName,
    string MemberInitials,
    IReadOnlyList<JourneyStage> Stages,
    string Recommendation,
    IReadOnlyList<string> LedgerTimeline,
    IReadOnlyList<string> ProgramHistory,
    IReadOnlyList<string> EventHistory,
    string GraphNote,
    string Rationale);

/// <summary>Strategic chapter investment recommendation.</summary>
public sealed record ChapterRecommendation(
    string RecommendationId,
    string Title,
    string ExpectedOutcome,
    IReadOnlyList<string> SupportingEvidence,
    string ActionLabel,
    string ActionRoute);

/// <summary>Telemetry converted into a next action.</summary>
public sealed record ChapterHealthItem(
    string HealthId,
    string NextAction,
    string SupportingContext);

/// <summary>Display context derived from an authenticated principal without granting authorization.</summary>
public sealed record CommandCenterAudience(
    CommunityRole Role,
    CommandCenterSurface Surface,
    string DisplayName,
    string ExperienceLabel,
    string ExperienceSummary);

/// <summary>V9 Community Command Center projection — nine fixed sections for operating personas.</summary>
public sealed record CommunityCommandCenter(
    CommunityRole Role,
    StrategicInsight Insight,
    IReadOnlyList<ImmediateDecision> Decisions,
    IReadOnlyList<CommunityProgram> Programs,
    IReadOnlyList<EventWorkspace> EventWorkspaces,
    IReadOnlyList<LeadershipSignal> LeadershipSignals,
    IReadOnlyList<GrowthJourney> GrowthJourneys,
    IReadOnlyList<ChapterRecommendation> ChapterRecommendations,
    IReadOnlyList<ChapterHealthItem> ChapterHealth);

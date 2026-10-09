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

public enum CommandCenterMode
{
    Strategic,
    Event
}

public enum AttentionSeverity
{
    Advisory,
    Required,
    Critical
}

/// <summary>Non-event touchpoints are first-class alongside events on the operations homepage.</summary>
public enum TouchpointKind
{
    Event,
    MentorshipSession,
    VolunteerOrientation,
    WorkingGroup
}

public sealed record SnapshotMetric(string Label, string Value, string Detail, string Trend, bool IsPositive);

public sealed record WeeklyInsight(
    string Eyebrow,
    string Headline,
    IReadOnlyList<string> Narrative,
    IReadOnlyList<string> Signals);

/// <summary>Awareness-only signal about a person's trajectory. Never a task or an approval.</summary>
public sealed record PersonToWatch(
    string Name,
    string Initials,
    string Signal,
    string JourneyFrom,
    string JourneyTo,
    string WatchReason);

public sealed record AttentionItem(
    string Category,
    string Title,
    string Summary,
    string ActionLabel,
    string Route,
    AttentionSeverity Severity,
    int Priority);

public sealed record ReviewItem(string Label, int Count, string Summary, string Route);

public sealed record TouchpointMetric(string Label, string Value);

public sealed record TouchpointItem(
    string Title,
    TouchpointKind Kind,
    string KindLabel,
    string DateLabel,
    string OperationalSummary,
    string Route,
    bool IsAtRisk,
    string ReadinessLabel,
    IReadOnlyList<TouchpointMetric> Metrics,
    string RiskLabel);

public sealed record DeadlineItem(string Label, string DueLabel, string Route);

public sealed record QuickAction(string Label, string Route, string Icon);

public sealed record WorkspaceLink(string Label, string Route, string Icon);

/// <summary>Display context derived from an authenticated principal without granting authorization.</summary>
public sealed record CommandCenterAudience(
    CommunityRole Role,
    CommandCenterSurface Surface,
    string DisplayName,
    string ExperienceLabel,
    string ExperienceSummary);

public sealed record CommunityCommandCenter(
    CommunityRole Role,
    CommandCenterMode RecommendedMode,
    CommandCenterMode EffectiveMode,
    string ModeReason,
    string ModeQuestion,
    IReadOnlyList<SnapshotMetric> Snapshot,
    WeeklyInsight Insight,
    IReadOnlyList<PersonToWatch> PeopleToWatch,
    IReadOnlyList<AttentionItem> AttentionItems,
    IReadOnlyList<ReviewItem> Reviews,
    IReadOnlyList<TouchpointItem> Touchpoints,
    IReadOnlyList<DeadlineItem> Deadlines,
    IReadOnlyList<QuickAction> QuickActions,
    IReadOnlyList<WorkspaceLink> Workspaces);

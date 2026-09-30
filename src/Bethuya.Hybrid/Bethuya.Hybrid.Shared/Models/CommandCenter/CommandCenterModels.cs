namespace Bethuya.Hybrid.Shared.Models.CommandCenter;

public enum CommunityRole
{
    CommunityAdministrator,
    EventOrganizer,
    VolunteerLead,
    MentorshipLead
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

public sealed record SnapshotMetric(string Label, string Value, string Detail, string Trend, bool IsPositive);

public sealed record WeeklyInsight(
    string Eyebrow,
    string Headline,
    IReadOnlyList<string> Narrative,
    IReadOnlyList<string> Signals);

public sealed record MomentumPerson(
    string Name,
    string Initials,
    string Signal,
    string JourneyFrom,
    string JourneyTo);

public sealed record AttentionItem(
    string Category,
    string Title,
    string Summary,
    string ActionLabel,
    string Route,
    AttentionSeverity Severity,
    int Priority);

public sealed record ReviewItem(string Label, int Count, string Summary, string Route);

public sealed record UpcomingEventItem(
    string Title,
    string DateLabel,
    string OperationalSummary,
    string Route,
    bool IsAtRisk);

public sealed record DeadlineItem(string Label, string DueLabel, string Route);

public sealed record WorkspaceLink(string Label, string Route, string Icon);

public sealed record CommunityCommandCenter(
    CommunityRole Role,
    CommandCenterMode RecommendedMode,
    CommandCenterMode EffectiveMode,
    string ModeReason,
    IReadOnlyList<SnapshotMetric> Snapshot,
    WeeklyInsight Insight,
    IReadOnlyList<MomentumPerson> Momentum,
    IReadOnlyList<AttentionItem> AttentionItems,
    IReadOnlyList<ReviewItem> Reviews,
    IReadOnlyList<UpcomingEventItem> UpcomingEvents,
    IReadOnlyList<DeadlineItem> Deadlines,
    IReadOnlyList<WorkspaceLink> Workspaces);

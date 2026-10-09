namespace Bethuya.Hybrid.Shared.Models.Participation;

/// <summary>A single step in the getting-started checklist shown to event participants.</summary>
public sealed record OnboardingStep(string Label, string Detail, string ActionLabel, string Route, bool IsComplete);

/// <summary>An entry in the member's participation timeline.</summary>
public sealed record ParticipationMoment(string DateLabel, string Title, string Detail);

/// <summary>A summarised contribution statistic for the member's own history.</summary>
public sealed record ContributionHighlight(string Label, string Value, string Detail);

/// <summary>A recommended way for the member to participate more deeply.</summary>
public sealed record ParticipationOpportunity(
    string Title,
    string Summary,
    string MatchReason,
    string ActionLabel,
    string Route);

/// <summary>An upcoming activity the member is personally part of.</summary>
public sealed record ParticipationActivity(string Title, string DateLabel, string Detail, string Route);

/// <summary>
/// A section unlocked by earned participation (volunteering, mentoring, working groups,
/// recognition). Members only see the sections they have earned.
/// </summary>
public sealed record EarnedSection(
    string Title,
    string Summary,
    string DataTest,
    IReadOnlyList<ParticipationActivity> Items);

/// <summary>The member's community passport: where they are in their journey.</summary>
public sealed record CommunityPassport(
    string Headline,
    string StageLabel,
    string StageDetail,
    string NextStageLabel,
    int ProgressPercent,
    IReadOnlyList<string> Badges);

/// <summary>
/// The Community Participation homepage model. It deliberately carries no operational
/// state: no snapshot metrics, attention queue, review queue, approvals, or deadlines.
/// </summary>
public sealed record CommunityParticipationHome(
    string Headline,
    string Subhead,
    bool IsOnboarding,
    CommunityPassport Passport,
    IReadOnlyList<OnboardingStep> OnboardingSteps,
    IReadOnlyList<ParticipationActivity> UpcomingActivities,
    IReadOnlyList<ParticipationMoment> Timeline,
    IReadOnlyList<ContributionHighlight> Contributions,
    IReadOnlyList<ParticipationOpportunity> Opportunities,
    IReadOnlyList<EarnedSection> EarnedSections);

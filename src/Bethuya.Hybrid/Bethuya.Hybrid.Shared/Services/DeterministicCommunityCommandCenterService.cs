using Bethuya.Hybrid.Shared.Models.CommandCenter;
using System.Globalization;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Supplies stable, rule-driven data until live community intelligence providers are available.</summary>
public sealed class DeterministicCommunityCommandCenterService(TimeProvider timeProvider) : ICommunityCommandCenterService
{
    private static readonly IReadOnlyList<SnapshotMetric> Snapshot =
    [
        new("Community health", "Healthy", "Steady participation across 6 active circles", "+4% this month", true),
        new("Active participants", "428", "67% participated in the last 30 days", "+23 people", true),
        new("Open opportunities", "18", "9 volunteer · 5 mentorship · 4 leadership", "6 need owners", false),
        new("Retention", "74%", "38 members returned this quarter", "+7% quarter over quarter", true)
    ];

    private static readonly IReadOnlyList<MomentumPerson> Momentum =
    [
        new("Priya Menon", "PM", "First volunteer contribution in four months.", "Attendee", "Volunteer"),
        new("Jordan Blake", "JB", "Coordinated logistics across three community events.", "Volunteer", "Organizer candidate"),
        new("Rina Shah", "RS", "Completed three consecutive workshops.", "Attendee", "Consistent contributor"),
        new("Akash Kumar", "AK", "Completed a first mentorship milestone.", "Member", "Mentee")
    ];

    private static readonly IReadOnlyList<AttentionItem> OngoingAttention =
    [
        new("Community health", "Akash has been waitlisted repeatedly", "Three consecutive waitlists are increasing disengagement risk.", "Review journey", "/community-health", AttentionSeverity.Required, 92),
        new("Mentorship", "Mentorship cohort pairing is delayed", "Two additional mentors are required before matching can finish.", "Find mentors", "/mentorship", AttentionSeverity.Required, 88),
        new("Volunteer network", "Returning contributor needs a follow-up", "A contributor returned after six months and has not received outreach.", "Draft outreach", "/volunteers", AttentionSeverity.Advisory, 76)
    ];

    private static readonly IReadOnlyList<ReviewItem> Reviews =
    [
        new("Waitlist adjustments", 14, "Capacity-aware recommendations ready for a human decision.", "/events"),
        new("Volunteer promotions", 3, "Contributors showing sustained organizer readiness.", "/volunteers"),
        new("Mentor pairings", 2, "Suggested matches based on stated goals and availability.", "/mentorship"),
        new("Outreach drafts", 5, "Re-engagement messages awaiting an organizer edit.", "/intelligence")
    ];

    private static readonly IReadOnlyList<DeadlineItem> Deadlines =
    [
        new("Hacktoberfest volunteer assignment", "Due in 2 days", "/volunteers"),
        new("Mentorship intake closes", "Due in 4 days", "/mentorship"),
        new("Speaker confirmation deadline", "Due in 5 days", "/events")
    ];

    private static readonly IReadOnlyList<WorkspaceLink> Workspaces =
    [
        new("Community Graph", "/community-graph", "network"),
        new("Opportunity Engine", "/opportunities", "sparkles"),
        new("Events", "/events", "calendar-days"),
        new("Community Health", "/community-health", "heart-pulse"),
        new("Volunteers", "/volunteers", "hand-heart"),
        new("Mentorship", "/mentorship", "graduation-cap"),
        new("Intelligence", "/intelligence", "brain-circuit")
    ];

    /// <inheritdoc />
    public Task<CommunityCommandCenter> GetAsync(
        CommunityRole role,
        CommandCenterMode? modeOverride = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var hacktoberfestDate = GetNextHacktoberfestDate(today);
        var daysUntilEvent = hacktoberfestDate.DayNumber - today.DayNumber;
        var hasEventPressure = daysUntilEvent <= 21;
        var recommendedMode = hasEventPressure ? CommandCenterMode.Event : CommandCenterMode.Strategic;
        var modeReason = hasEventPressure
            ? $"Hacktoberfest Mumbai is {FormatDayDistance(daysUntilEvent)} and volunteer coverage remains incomplete."
            : $"Hacktoberfest Mumbai is {daysUntilEvent} days away, so strategic community priorities remain in focus.";
        var effectiveMode = modeOverride ?? recommendedMode;
        AttentionItem[] attention =
        [
            new(
                "Event operations",
                "Hacktoberfest Mumbai",
                $"Volunteer coverage is short by two people with the event {FormatDayDistance(daysUntilEvent)}.",
                "Assign volunteers",
                "/volunteers",
                hasEventPressure ? AttentionSeverity.Critical : AttentionSeverity.Advisory,
                hasEventPressure ? 100 : 70),
            .. OngoingAttention
        ];

        var attentionItems = attention
            .OrderByDescending(item => effectiveMode == CommandCenterMode.Event && item.Category == "Event operations")
            .ThenByDescending(item => GetRolePriority(item, role))
            .ThenByDescending(item => item.Priority)
            .ToArray();

        var insight = role switch
        {
            CommunityRole.EventOrganizer => CreateInsight(
                "Operations are tightening",
                "Hacktoberfest is drawing strong demand while volunteer coverage needs intervention."),
            CommunityRole.VolunteerLead => CreateInsight(
                "Volunteer leadership is compounding",
                "Three contributors are showing the consistency needed for greater responsibility."),
            CommunityRole.MentorshipLead => CreateInsight(
                "Mentorship demand is outpacing supply",
                "Participation is growing steadily, but two mentor seats are blocking new pairings."),
            _ => CreateInsight(
                "Community momentum is becoming leadership",
                "Volunteer participation continues to accelerate while previously inactive members re-engage.")
        };

        return Task.FromResult(new CommunityCommandCenter(
            role,
            recommendedMode,
            effectiveMode,
            modeReason,
            Snapshot,
            insight,
            Momentum,
            attentionItems,
            Reviews,
            CreateUpcomingEvents(hacktoberfestDate),
            Deadlines,
            Workspaces));
    }

    private static DateOnly GetNextHacktoberfestDate(DateOnly today)
    {
        var eventDate = new DateOnly(today.Year, 10, 17);
        return eventDate < today ? eventDate.AddYears(1) : eventDate;
    }

    private static string FormatDayDistance(int daysUntilEvent) => daysUntilEvent switch
    {
        0 => "today",
        1 => "1 day away",
        _ => $"{daysUntilEvent} days away"
    };

    private static IReadOnlyList<UpcomingEventItem> CreateUpcomingEvents(DateOnly hacktoberfestDate) =>
    [
        new(
            "Hacktoberfest Mumbai",
            hacktoberfestDate.ToString("dd MMM", CultureInfo.InvariantCulture),
            "150 confirmed · 18 waitlisted · volunteer gap: 2",
            "/events",
            true),
        new(
            "Platform Engineering Night",
            hacktoberfestDate.AddDays(7).ToString("dd MMM", CultureInfo.InvariantCulture),
            "84 confirmed · volunteer gap: 1",
            "/events",
            true),
        new(
            "AgentCamp Mangaluru",
            hacktoberfestDate.AddDays(16).ToString("dd MMM", CultureInfo.InvariantCulture),
            "Capacity at 95% · speaker lineup ready",
            "/events",
            false)
    ];

    private static WeeklyInsight CreateInsight(string headline, string opening) => new(
        "Community insight of the week",
        headline,
        [opening, "Three contributors appear ready for increased responsibility.", "Two previously inactive members have re-engaged."],
        ["Participation history", "Volunteer activity", "Mentorship activity", "Community relationships", "Contribution history"]);

    private static int GetRolePriority(AttentionItem item, CommunityRole role)
    {
        var preferredCategory = role switch
        {
            CommunityRole.EventOrganizer => "Event operations",
            CommunityRole.VolunteerLead => "Volunteer network",
            CommunityRole.MentorshipLead => "Mentorship",
            _ => "Community health"
        };

        return item.Priority + (item.Category == preferredCategory ? 1_000 : 0);
    }
}

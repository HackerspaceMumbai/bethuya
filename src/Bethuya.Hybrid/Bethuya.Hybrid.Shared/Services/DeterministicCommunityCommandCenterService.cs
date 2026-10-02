using Bethuya.Hybrid.Shared.Models.CommandCenter;
using System.Globalization;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Supplies stable, rule-driven data until live community intelligence providers are available.</summary>
public sealed class DeterministicCommunityCommandCenterService(TimeProvider timeProvider) : ICommunityCommandCenterService
{
    private static readonly IReadOnlyList<SnapshotMetric> StrategicSnapshot =
    [
        new("Community health", "Healthy", "Steady participation across 6 active circles", "+4% this month", true),
        new("Active participants", "428", "67% participated in the last 30 days", "+23 people", true),
        new("Open opportunities", "18", "9 volunteer · 5 mentorship · 4 leadership", "6 need owners", false),
        new("Retention", "74%", "38 members returned this quarter", "+7% quarter over quarter", true)
    ];

    private static readonly IReadOnlyList<SnapshotMetric> EventSnapshot =
    [
        new("Event readiness", "82%", "Core program ready; volunteer coverage remains open", "+9% this week", true),
        new("Capacity", "150 / 160", "94% of attendee capacity confirmed", "10 seats remain", true),
        new("Waitlist pressure", "18", "Decisions needed before attendee communications", "6 high-priority", false),
        new("Volunteer coverage", "12 / 14", "Check-in and accessibility support need owners", "2 shifts open", false)
    ];

    private static readonly IReadOnlyList<MomentumPerson> StrategicMomentum =
    [
        new("Priya Menon", "PM", "First volunteer contribution in four months.", "Attendee", "Volunteer"),
        new("Jordan Blake", "JB", "Coordinated logistics across three community events.", "Volunteer", "Organizer candidate"),
        new("Rina Shah", "RS", "Completed three consecutive workshops.", "Attendee", "Consistent contributor"),
        new("Akash Kumar", "AK", "Completed a first mentorship milestone.", "Member", "Mentee")
    ];

    private static readonly IReadOnlyList<MomentumPerson> EventMomentum =
    [
        new("Jordan Blake", "JB", "Owns the run-of-show and has resolved three cross-team blockers.", "Volunteer", "Event lead"),
        new("Priya Menon", "PM", "Completed check-in training and can close the arrival coverage gap.", "Attendee", "Check-in lead"),
        new("Rina Shah", "RS", "Facilitated three workshops and can support the overflow room.", "Contributor", "Room host"),
        new("Akash Kumar", "AK", "Responded quickly to attendee questions and can support the help desk.", "Member", "Attendee support")
    ];

    private static readonly IReadOnlyList<AttentionItem> OngoingAttention =
    [
        new("Community health", "Akash has been waitlisted repeatedly", "Three consecutive waitlists are increasing disengagement risk.", "Review journey", "/community-health", AttentionSeverity.Required, 92),
        new("Mentorship", "Mentorship cohort pairing is delayed", "Two additional mentors are required before matching can finish.", "Find mentors", "/mentorship", AttentionSeverity.Required, 88),
        new("Volunteer network", "Returning contributor needs a follow-up", "A contributor returned after six months and has not received outreach.", "Draft outreach", "/volunteers", AttentionSeverity.Advisory, 76),
        new("Member journey", "Choose your next community step", "Two welcoming opportunities match recent attendee interests and availability.", "Explore opportunities", "/opportunities", AttentionSeverity.Advisory, 74),
        new("Contributor opportunity", "Your session contribution has momentum", "A submitted session idea is ready for the next human-reviewed community step.", "Review opportunity", "/opportunities", AttentionSeverity.Advisory, 78)
    ];

    private static readonly IReadOnlyList<ReviewItem> Reviews =
    [
        new("Waitlist adjustments", 14, "Capacity-aware recommendations ready for a human decision.", "/events"),
        new("Volunteer promotions", 3, "Contributors showing sustained organizer readiness.", "/volunteers"),
        new("Mentor pairings", 2, "Suggested matches based on stated goals and availability.", "/mentorship"),
        new("Outreach drafts", 5, "Re-engagement messages awaiting an organizer edit.", "/intelligence")
    ];

    private static readonly IReadOnlyList<ReviewItem> EventReviews =
    [
        new("Waitlist decisions", 14, "Capacity-aware recommendations ready before attendee communications.", "/events"),
        new("Volunteer assignments", 3, "Critical check-in and accessibility shifts need approval.", "/volunteers"),
        new("Speaker confirmations", 2, "Session changes need an organizer decision.", "/events"),
        new("Accessibility requests", 5, "Accommodation plans are ready for human review.", "/events")
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
        var effectiveMode = modeOverride ?? recommendedMode;
        var modeReason = CreateModeReason(effectiveMode, recommendedMode, daysUntilEvent);
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

        var isEventMode = effectiveMode == CommandCenterMode.Event;
        var insight = isEventMode ? CreateEventInsight(role) : CreateStrategicInsight(role);

        return Task.FromResult(new CommunityCommandCenter(
            role,
            recommendedMode,
            effectiveMode,
            modeReason,
            isEventMode ? EventSnapshot : StrategicSnapshot,
            insight,
            CreateMomentum(role, isEventMode),
            attentionItems,
            isEventMode ? EventReviews : Reviews,
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

    private static string CreateModeReason(
        CommandCenterMode effectiveMode,
        CommandCenterMode recommendedMode,
        int daysUntilEvent)
    {
        if (effectiveMode != recommendedMode)
        {
            return effectiveMode == CommandCenterMode.Strategic
                ? $"Strategic view selected manually; Hacktoberfest remains {FormatDayDistance(daysUntilEvent)}."
                : $"Event operations selected manually with Hacktoberfest {FormatDayDistance(daysUntilEvent)}.";
        }

        return effectiveMode == CommandCenterMode.Event
            ? $"Hacktoberfest Mumbai is {FormatDayDistance(daysUntilEvent)} and volunteer coverage remains incomplete."
            : $"Hacktoberfest Mumbai is {daysUntilEvent} days away, so strategic community priorities remain in focus.";
    }

    private static IReadOnlyList<UpcomingEventItem> CreateUpcomingEvents(DateOnly hacktoberfestDate) =>
    [
        new(
            "Hacktoberfest Mumbai",
            hacktoberfestDate.ToString("dd MMM", CultureInfo.InvariantCulture),
            "150 confirmed · 18 waitlisted · volunteer gap: 2",
            "/events",
            true,
            "82% ready",
            "150 / 160",
            "18 waiting",
            "12 / 14",
            "Volunteer gap"),
        new(
            "Platform Engineering Night",
            hacktoberfestDate.AddDays(7).ToString("dd MMM", CultureInfo.InvariantCulture),
            "84 confirmed · volunteer gap: 1",
            "/events",
            true,
            "76% ready",
            "84 / 100",
            "9 waiting",
            "7 / 8",
            "Host coverage"),
        new(
            "AgentCamp Mangaluru",
            hacktoberfestDate.AddDays(16).ToString("dd MMM", CultureInfo.InvariantCulture),
            "Capacity at 95% · speaker lineup ready",
            "/events",
            false,
            "91% ready",
            "114 / 120",
            "4 waiting",
            "10 / 10",
            "On track")
    ];

    private static WeeklyInsight CreateStrategicInsight(CommunityRole role)
    {
        var (headline, opening) = role switch
        {
            CommunityRole.EventOrganizer => (
                "Community participation is widening beyond events",
                "Recent events are converting attendees into recurring contributors."),
            CommunityRole.VolunteerLead => (
                "Volunteer leadership is compounding",
                "Three contributors are showing the consistency needed for greater responsibility."),
            CommunityRole.MentorshipLead => (
                "Mentorship demand is outpacing supply",
                "Participation is growing steadily, but two mentor seats are blocking new pairings."),
            CommunityRole.CommunityMember => (
                "A consistent next step matters more than a crowded calendar",
                "Two welcoming opportunities match recent interests without requiring an existing leadership role."),
            CommunityRole.EmergingContributor => (
                "Your contribution is opening a leadership path",
                "A recent session proposal creates a clear route from participation into visible community ownership."),
            _ => (
                "Community momentum is becoming leadership",
                "Volunteer participation continues to accelerate while previously inactive members re-engage.")
        };

        return new WeeklyInsight(
            "Community insight of the week",
            headline,
            [opening, "Three contributors appear ready for increased responsibility.", "Two previously inactive members have re-engaged."],
            ["Participation history", "Volunteer activity", "Mentorship activity", "Community relationships", "Contribution history"]);
    }

    private static WeeklyInsight CreateEventInsight(CommunityRole role)
    {
        var (headline, opening) = role switch
        {
            CommunityRole.VolunteerLead => (
                "Event execution depends on two coverage decisions",
                "Check-in and accessibility support are the only critical volunteer gaps."),
            CommunityRole.MentorshipLead => (
                "Event execution can create new mentor pathways",
                "Four experienced contributors can be paired with first-time volunteers during delivery."),
            CommunityRole.CommunityAdministrator => (
                "Event execution risk is concentrated, not systemic",
                "Capacity is healthy; volunteer coverage and waitlist decisions need intervention."),
            CommunityRole.CommunityMember => (
                "Your clearest event contribution is attendee support",
                "A lightweight welcome-desk opportunity fits your current community journey."),
            CommunityRole.EmergingContributor => (
                "Your session can unlock event momentum",
                "Confirming the proposed format would close a program gap and create a first facilitation opportunity."),
            _ => (
                "Event execution is recoverable with two decisions",
                "Volunteer ownership and waitlist approvals are the remaining critical path.")
        };

        return new WeeklyInsight(
            "Event intelligence brief",
            headline,
            [opening, "Attendee demand remains strong with ten seats still available.", "Three emerging contributors can directly improve event readiness."],
            ["Registration velocity", "Waitlist history", "Volunteer shifts", "Session readiness", "Attendee support requests"]);
    }

    private static IReadOnlyList<MomentumPerson> CreateMomentum(CommunityRole role, bool isEventMode)
    {
        var people = isEventMode ? EventMomentum : StrategicMomentum;
        var priorityName = role switch
        {
            CommunityRole.EventOrganizer => "Jordan Blake",
            CommunityRole.VolunteerLead => "Priya Menon",
            CommunityRole.MentorshipLead or CommunityRole.CommunityMember => "Akash Kumar",
            CommunityRole.EmergingContributor => "Rina Shah",
            _ => "Jordan Blake"
        };

        return [.. people.OrderByDescending(person => person.Name == priorityName)];
    }

    private static int GetRolePriority(AttentionItem item, CommunityRole role)
    {
        var preferredCategory = role switch
        {
            CommunityRole.EventOrganizer => "Event operations",
            CommunityRole.VolunteerLead => "Volunteer network",
            CommunityRole.MentorshipLead => "Mentorship",
            CommunityRole.CommunityMember => "Member journey",
            CommunityRole.EmergingContributor => "Contributor opportunity",
            _ => "Community health"
        };

        return item.Priority + (item.Category == preferredCategory ? 1_000 : 0);
    }
}

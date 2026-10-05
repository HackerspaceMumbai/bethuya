using Bethuya.Hybrid.Shared.Models.CommandCenter;
using System.Globalization;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>
/// Supplies stable, rule-driven operations data until live Community Intelligence,
/// Community Graph, and Opportunity Engine providers are available. Every operating role
/// receives the same module set; only the content inside the modules changes.
/// </summary>
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

    private static readonly IReadOnlyList<PersonToWatch> StrategicPeopleToWatch =
    [
        new("Jordan Blake", "JB", "Coordinated logistics across three community events.", "Volunteer", "Organizer candidate", "Sustained ownership without being asked."),
        new("Rina Shah", "RS", "Completed three consecutive workshops and answered newcomer questions.", "Contributor", "Mentor candidate", "Teaching naturally emerged from contributing."),
        new("Priya Menon", "PM", "First contribution in four months after a quiet period.", "Returning member", "Re-engaging contributor", "Returning members need a warm, timely next step."),
        new("Akash Kumar", "AK", "High engagement, but waitlisted for three consecutive events.", "Member", "At-risk participant", "Repeat waitlisting can quietly end a journey.")
    ];

    private static readonly IReadOnlyList<PersonToWatch> EventPeopleToWatch =
    [
        new("Jordan Blake", "JB", "Owns the run-of-show and has resolved three cross-team blockers.", "Volunteer", "Event lead", "Directly affects whether this event succeeds."),
        new("Priya Menon", "PM", "Completed check-in training and can close the arrival coverage gap.", "Returning member", "Check-in lead", "One of two people who can close the coverage gap."),
        new("Rina Shah", "RS", "Facilitated three workshops and can support the overflow room.", "Contributor", "Room host", "Reduces single-point-of-failure risk in the program."),
        new("Akash Kumar", "AK", "Responded quickly to attendee questions in the last two events.", "Member", "Attendee support", "Can absorb help-desk load during peak arrival.")
    ];

    private static readonly IReadOnlyList<AttentionItem> AdministratorAttention =
    [
        new("Retention risk", "Repeat waitlisting is driving disengagement", "Akash Kumar and four others were waitlisted three times in a row.", "Review retention", "/community-health", AttentionSeverity.Required, 94),
        new("Leadership pipeline", "Two leadership roles have no successor", "Program ownership is concentrated in three people across six circles.", "Review pipeline", "/community-graph", AttentionSeverity.Required, 90),
        new("Health trend", "New-member second-touch rate is slipping", "Second participation within 30 days fell from 48% to 39%.", "Open health report", "/community-health", AttentionSeverity.Advisory, 84),
        new("Community opportunity", "Six opportunities have no owner", "Unowned opportunities expire before anyone can act on them.", "Assign owners", "/opportunities", AttentionSeverity.Advisory, 78)
    ];

    private static readonly IReadOnlyList<AttentionItem> OrganizerAttention =
    [
        new("Capacity risk", "Hacktoberfest is at 94% of capacity", "Ten seats remain while registrations continue at twelve per day.", "Review capacity", "/events", AttentionSeverity.Required, 94),
        new("Waitlist", "Eighteen waitlist decisions are pending", "Attendee communications are blocked until decisions are made.", "Decide waitlist", "/events", AttentionSeverity.Required, 92),
        new("Volunteer coverage", "Two critical shifts have no owner", "Check-in and accessibility support remain unassigned.", "Assign volunteers", "/volunteers", AttentionSeverity.Required, 88),
        new("Speaker readiness", "Two sessions are unconfirmed", "Format and AV requirements are still missing for the afternoon track.", "Confirm speakers", "/events", AttentionSeverity.Advisory, 82)
    ];

    private static readonly IReadOnlyList<AttentionItem> VolunteerLeadAttention =
    [
        new("Coverage gap", "Check-in has no confirmed owner", "Arrival coverage is the highest-impact unfilled role.", "Fill coverage", "/volunteers", AttentionSeverity.Required, 94),
        new("Open shifts", "Four shifts remain unclaimed", "Accessibility support and teardown are the longest-standing gaps.", "Open shift board", "/volunteers", AttentionSeverity.Required, 88),
        new("Recognition", "Three volunteers are overdue for recognition", "Consistent contributors have not been acknowledged this quarter.", "Recognise volunteers", "/volunteers", AttentionSeverity.Advisory, 82),
        new("Burnout risk", "Two volunteers worked five consecutive events", "Sustained load without a break precedes most volunteer drop-off.", "Review workload", "/community-health", AttentionSeverity.Advisory, 80)
    ];

    private static readonly IReadOnlyList<AttentionItem> MentorshipLeadAttention =
    [
        new("Mentor supply", "Two mentor seats are blocking the cohort", "Six mentees cannot be paired until mentors are confirmed.", "Find mentors", "/mentorship", AttentionSeverity.Required, 94),
        new("Pairing delay", "Five pairings have waited over ten days", "Pairing latency is the strongest predictor of mentee drop-off.", "Review pairings", "/mentorship", AttentionSeverity.Required, 90),
        new("Graduation", "Three mentees are ready to graduate", "Completed milestones are awaiting a human review decision.", "Review graduations", "/mentorship", AttentionSeverity.Advisory, 84),
        new("At-risk mentee", "Two mentees have missed consecutive sessions", "Missed sessions without follow-up usually end the pairing.", "Open mentee journeys", "/community-health", AttentionSeverity.Advisory, 80)
    ];

    private static readonly IReadOnlyList<ReviewItem> StrategicReviews =
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
        var isEventMode = effectiveMode == CommandCenterMode.Event;
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
            .. GetRoleAttention(role)
        ];

        var attentionItems = attention
            .OrderByDescending(item => isEventMode && item.Category == "Event operations")
            .ThenByDescending(item => item.Priority)
            .ToArray();

        return Task.FromResult(new CommunityCommandCenter(
            role,
            recommendedMode,
            effectiveMode,
            modeReason,
            isEventMode ? "Can this event succeed?" : "How is the community evolving?",
            isEventMode ? EventSnapshot : StrategicSnapshot,
            isEventMode ? CreateEventInsight(role) : CreateStrategicInsight(role),
            CreatePeopleToWatch(role, isEventMode),
            attentionItems,
            isEventMode ? EventReviews : StrategicReviews,
            CreateTouchpoints(hacktoberfestDate),
            Deadlines,
            CreateQuickActions(role, isEventMode),
            Workspaces));
    }

    private static IReadOnlyList<AttentionItem> GetRoleAttention(CommunityRole role) => role switch
    {
        CommunityRole.EventOrganizer => OrganizerAttention,
        CommunityRole.VolunteerLead => VolunteerLeadAttention,
        CommunityRole.MentorshipLead => MentorshipLeadAttention,
        _ => AdministratorAttention
    };

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

    private static IReadOnlyList<QuickAction> CreateQuickActions(CommunityRole role, bool isEventMode)
    {
        if (isEventMode)
        {
            return
            [
                new("Assign a volunteer shift", "/volunteers", "hand-heart"),
                new("Decide waitlist entries", "/events", "list-checks"),
                new("Message confirmed attendees", "/events", "send"),
                new("Review accessibility requests", "/events", "accessibility")
            ];
        }

        return role switch
        {
            CommunityRole.EventOrganizer =>
            [
                new("Draft the next event", "/events", "calendar-plus"),
                new("Open the opportunity engine", "/opportunities", "sparkles"),
                new("Review volunteer coverage", "/volunteers", "hand-heart")
            ],
            CommunityRole.VolunteerLead =>
            [
                new("Open the shift board", "/volunteers", "hand-heart"),
                new("Recognise a volunteer", "/volunteers", "award"),
                new("Review workload balance", "/community-health", "heart-pulse")
            ],
            CommunityRole.MentorshipLead =>
            [
                new("Invite a mentor", "/mentorship", "graduation-cap"),
                new("Review pending pairings", "/mentorship", "users"),
                new("Open mentee journeys", "/community-health", "heart-pulse")
            ],
            _ =>
            [
                new("Review community health", "/community-health", "heart-pulse"),
                new("Open the community graph", "/community-graph", "network"),
                new("Assign opportunity owners", "/opportunities", "sparkles")
            ]
        };
    }

    private static IReadOnlyList<TouchpointItem> CreateTouchpoints(DateOnly hacktoberfestDate) =>
    [
        new(
            "Hacktoberfest Mumbai",
            TouchpointKind.Event,
            "Event",
            hacktoberfestDate.ToString("dd MMM", CultureInfo.InvariantCulture),
            "150 confirmed · 18 waitlisted · volunteer gap: 2",
            "/events",
            true,
            "82% ready",
            [
                new("Capacity", "150 / 160"),
                new("Waitlist", "18 waiting"),
                new("Volunteers", "12 / 14")
            ],
            "Volunteer gap"),
        new(
            "Mentorship cohort kickoff",
            TouchpointKind.MentorshipSession,
            "Mentorship session",
            hacktoberfestDate.AddDays(3).ToString("dd MMM", CultureInfo.InvariantCulture),
            "6 mentees ready · 2 mentor seats open",
            "/mentorship",
            true,
            "70% ready",
            [
                new("Pairings", "6 / 8"),
                new("Mentees waiting", "2"),
                new("Mentors", "6 / 8")
            ],
            "Mentor shortfall"),
        new(
            "Volunteer orientation",
            TouchpointKind.VolunteerOrientation,
            "Volunteer orientation",
            hacktoberfestDate.AddDays(5).ToString("dd MMM", CultureInfo.InvariantCulture),
            "11 registered · check-in training required",
            "/volunteers",
            false,
            "88% ready",
            [
                new("Registered", "11 / 14"),
                new("Training pending", "3"),
                new("Facilitators", "2 / 2")
            ],
            "On track"),
        new(
            "Platform Engineering Night",
            TouchpointKind.Event,
            "Event",
            hacktoberfestDate.AddDays(7).ToString("dd MMM", CultureInfo.InvariantCulture),
            "84 confirmed · volunteer gap: 1",
            "/events",
            true,
            "76% ready",
            [
                new("Capacity", "84 / 100"),
                new("Waitlist", "9 waiting"),
                new("Volunteers", "7 / 8")
            ],
            "Host coverage"),
        new(
            "Community health working group",
            TouchpointKind.WorkingGroup,
            "Working group",
            hacktoberfestDate.AddDays(12).ToString("dd MMM", CultureInfo.InvariantCulture),
            "Retention review · 5 standing members",
            "/community-health",
            false,
            "95% ready",
            [
                new("Members", "5 / 6"),
                new("Agenda items", "4"),
                new("Owners confirmed", "4 / 4")
            ],
            "On track"),
        new(
            "AgentCamp Mangaluru",
            TouchpointKind.Event,
            "Event",
            hacktoberfestDate.AddDays(16).ToString("dd MMM", CultureInfo.InvariantCulture),
            "Capacity at 95% · speaker lineup ready",
            "/events",
            false,
            "91% ready",
            [
                new("Capacity", "114 / 120"),
                new("Waitlist", "4 waiting"),
                new("Volunteers", "10 / 10")
            ],
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

    private static IReadOnlyList<PersonToWatch> CreatePeopleToWatch(CommunityRole role, bool isEventMode)
    {
        var people = isEventMode ? EventPeopleToWatch : StrategicPeopleToWatch;
        var priorityName = role switch
        {
            CommunityRole.EventOrganizer => "Jordan Blake",
            CommunityRole.VolunteerLead => "Priya Menon",
            CommunityRole.MentorshipLead => "Rina Shah",
            _ => "Akash Kumar"
        };

        return [.. people.OrderByDescending(person => person.Name == priorityName)];
    }
}

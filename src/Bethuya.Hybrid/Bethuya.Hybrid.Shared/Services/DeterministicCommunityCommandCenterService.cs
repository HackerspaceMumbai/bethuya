using Bethuya.Hybrid.Shared.Models.CommandCenter;
using System.Globalization;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>
/// Supplies stable, rule-driven V9 operations data until live Community Intelligence,
/// Community Graph, and Opportunity Engine providers are available. Every operating role
/// receives the same nine-section layout; role only reorders decision priority.
/// </summary>
public sealed class DeterministicCommunityCommandCenterService(TimeProvider timeProvider) : ICommunityCommandCenterService
{
    private static readonly string[] JourneyPath =
    [
        "Participant",
        "Volunteer",
        "Mentor",
        "Track Lead",
        "Chapter Champion"
    ];

    /// <inheritdoc />
    public Task<CommunityCommandCenter> GetAsync(
        CommunityRole role,
        CommandCenterMode? modeOverride = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = modeOverride;

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var hacktoberfestDate = GetNextHacktoberfestDate(today);

        return Task.FromResult(new CommunityCommandCenter(
            role,
            CreateInsight(role),
            CreateDecisions(role),
            CreatePrograms(),
            CreateEventWorkspaces(hacktoberfestDate),
            CreateLeadershipSignals(role),
            CreateGrowthJourneys(),
            CreateChapterRecommendations(),
            CreateChapterHealth()));
    }

    private static StrategicInsight CreateInsight(CommunityRole role) => role switch
    {
        CommunityRole.MentorshipLead => new(
            "The Open Source Mentorship Circle is producing future tech leads at 2.4x the rate of event-only attendance.",
            "Mentor capacity will be exhausted within 3 weeks if intake continues at the current pace.",
            "Promote additional mentors from the candidate pool and expand mentor capacity for the next cohort.",
            "Review Promotion Candidates",
            "/programs"),
        CommunityRole.VolunteerLead => new(
            "Volunteer ownership is compounding: consistent shift leads convert into event leads at twice the rate of one-off helpers.",
            "Two critical coverage gaps remain before Hacktoberfest delivery.",
            "Assign check-in and accessibility leads from the ready volunteer pool.",
            "Review Coverage Gaps",
            "/volunteers"),
        CommunityRole.EventOrganizer => new(
            "Hacktoberfest Mumbai is converting waitlisted members into sustained contributors when admissions are timely.",
            "Eighteen waitlist decisions are blocking attendee communications.",
            "Review waitlist admissions with Participation Ledger evidence before the next communication window.",
            "Review Waitlist Admissions",
            "/events"),
        _ => new(
            "The Open Source Mentorship Circle is producing future tech leads at 2.4x the rate of event-only attendance.",
            "We are reaching a mentorship bottleneck — mentor capacity will be exhausted within 3 weeks.",
            "Review the 12 mentor candidates and expand mentor capacity before the next cohort opens.",
            "Review Promotion Candidates",
            "/programs")
    };

    private static ImmediateDecision[] CreateDecisions(CommunityRole role)
    {
        ImmediateDecision[] decisions =
        [
            new(
                "dec-waitlist-01",
                DecisionKind.WaitlistAdmission,
                "3 Waitlist Admissions Ready",
                "Capacity-aware recommendations for Hacktoberfest Mumbai with ledger attendance history.",
                ["Attended 4 prior events", "100% check-in history", "Volunteered once at Platform Engineering Night"],
                "Approve",
                "Waitlist cohort A"),
            new(
                "dec-speaker-01",
                DecisionKind.SpeakerConfirmation,
                "1 Speaker At Risk (Confirmation)",
                "Review speaker availability for the afternoon track before attendee communications.",
                ["Session brief submitted", "AV requirements incomplete", "Prior talk delivered at AgentCamp"],
                "Confirm",
                "DevFest afternoon track"),
            new(
                "dec-volunteer-01",
                DecisionKind.VolunteerAssignment,
                "1 Volunteer Role Conflict",
                "Priya Menon is nominated for both check-in lead and accessibility support.",
                ["Completed check-in training", "Led accessibility at 2 events", "Availability confirmed for 17 Oct"],
                "Assign",
                "Priya Menon"),
            new(
                "dec-mentor-01",
                DecisionKind.MentorNomination,
                "2 Track Lead Nominations Ready",
                "Rina Shah and Jordan Blake meet sustained contribution thresholds for Track Lead roles.",
                ["Mentored informally across 3 workshops", "Sustained contributions for 8 months", "Passport pathway: Mentor → Track Lead"],
                "Nominate",
                "Track Lead candidate pool"),
            new(
                "dec-sponsor-01",
                DecisionKind.SponsorApproval,
                "1 Sponsor Approval Pending",
                "Community partner ask requires organizer review before publishing on the event workspace.",
                ["Partner contributed venue credits last year", "No conflicting exclusivity terms", "Impact report on file"],
                "Approve",
                "Community partner")
        ];

        return role switch
        {
            CommunityRole.VolunteerLead =>
            [
                .. decisions.Where(d => d.Kind == DecisionKind.VolunteerAssignment),
                .. decisions.Where(d => d.Kind != DecisionKind.VolunteerAssignment)
            ],
            CommunityRole.MentorshipLead =>
            [
                .. decisions.Where(d => d.Kind == DecisionKind.MentorNomination),
                .. decisions.Where(d => d.Kind != DecisionKind.MentorNomination)
            ],
            CommunityRole.EventOrganizer =>
            [
                .. decisions.Where(d => d.Kind is DecisionKind.WaitlistAdmission or DecisionKind.SpeakerConfirmation),
                .. decisions.Where(d => d.Kind is not (DecisionKind.WaitlistAdmission or DecisionKind.SpeakerConfirmation))
            ],
            _ => [.. decisions]
        };
    }

    private static IReadOnlyList<CommunityProgram> CreatePrograms() =>
    [
        new(
            "prog-mentorship",
            "Open Source Mentorship Circle",
            "Produce future tech leads through sustained pairing.",
            ["6 Future Tech Leads", "12 Speaker Candidates", "4 Mentors graduated to Track Lead"],
            "/programs"),
        new(
            "prog-maintainers",
            "Maintainers & Contributors Guild",
            "Grow maintainers from first-time contributors.",
            ["3 New Maintainers", "8 Future Mentors", "2 Governance Leads"],
            "/programs"),
        new(
            "prog-hosts",
            "Chapter Hosts Pathway",
            "Develop regional hosts from event volunteers.",
            ["2 Regional Hosts", "1 Chapter Director", "5 Event Leads"],
            "/programs"),
        new(
            "prog-speakers",
            "Speaker Development Track",
            "Convert workshop facilitators into conference speakers.",
            ["7 First-time Speakers", "3 Panel Moderators", "2 Track Leads"],
            "/programs")
    ];

    private static IReadOnlyList<EventWorkspace> CreateEventWorkspaces(DateOnly hacktoberfestDate) =>
    [
        new(
            "evt-hacktoberfest",
            $"Hacktoberfest Mumbai {hacktoberfestDate.Year}",
            hacktoberfestDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            true,
            92,
            "Ready for execution",
            [
                "Capacity: 186 / 200",
                "Staffing: 12 / 14 shifts filled",
                "Speaker Confirmation: 14 / 15",
                "Venue Readiness: Confirmed"
            ],
            [
                "Verified Attendance: 120+ Evidence Signals",
                "Leadership Candidates: 8 surfaced",
                "Champions Surfaced: 3",
                "Connections Formed: 47 ledger-backed"
            ],
            "/events",
            "Operations active"),
        new(
            "evt-platform",
            "Platform Engineering Night",
            hacktoberfestDate.AddDays(7).ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            false,
            76,
            "Operational risk detected",
            ["Capacity: 84 / 100", "Staffing: 7 / 8", "Speakers: Confirmed"],
            ["Leadership Candidates: 2", "Connections Formed: 12"],
            "/events",
            "Host coverage needed"),
        new(
            "evt-agentcamp",
            "AgentCamp Mangaluru",
            hacktoberfestDate.AddDays(16).ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            false,
            91,
            "Ready for execution",
            ["Capacity: 114 / 120", "Staffing: Full", "Speakers: Ready"],
            ["Leadership Candidates: 4", "Champions Surfaced: 1"],
            "/events",
            "On track")
    ];

    private static IReadOnlyList<LeadershipSignal> CreateLeadershipSignals(CommunityRole role)
    {
        LeadershipSignal[] signals =
        [
            new(
                "sig-maya",
                "Emerging Chapter Champion",
                "Maya Sen",
                "MS",
                ["Participant", "Volunteer", "Mentor"],
                "Track Lead",
                "Chapter Champion",
                [
                    "Mentored 12 participants",
                    "Organized 2 workshops",
                    "Sustained contributions for 8 months",
                    "Delivered 3 talks"
                ],
                "Nominate as Chapter Champion",
                SignalUrgency.Recommendation,
                "/opportunities"),
            new(
                "sig-akash",
                "At-Risk Contributor",
                "Akash Kumar",
                "AK",
                ["Participant", "Volunteer"],
                "Volunteer",
                "Mentor",
                [
                    "Waitlisted for 3 consecutive events",
                    "Previously volunteered twice",
                    "Last verified attendance 4 months ago"
                ],
                "Send Outreach",
                SignalUrgency.Intervention,
                "/community-health"),
            new(
                "sig-jordan",
                "High-Velocity Contributor",
                "Jordan Blake",
                "JB",
                ["Participant", "Volunteer", "Mentor"],
                "Track Lead",
                "Chapter Champion",
                [
                    "Coordinated logistics across 3 events",
                    "Resolved 3 cross-team blockers",
                    "Mentored 4 first-time volunteers"
                ],
                "Approve as Track Lead",
                SignalUrgency.Recommendation,
                "/opportunities"),
            new(
                "sig-rina",
                "Cross-Chapter Bridge",
                "Rina Shah",
                "RS",
                ["Participant", "Volunteer", "Mentor"],
                "Track Lead",
                "Regional Host",
                [
                    "Facilitated workshops in 2 chapters",
                    "Introduced 6 newcomers across communities",
                    "Shared attendance with Mangaluru cohort"
                ],
                "Invite to Host Pathway",
                SignalUrgency.Recommendation,
                "/programs")
        ];

        var priority = role switch
        {
            CommunityRole.VolunteerLead => "sig-akash",
            CommunityRole.MentorshipLead => "sig-rina",
            CommunityRole.EventOrganizer => "sig-jordan",
            _ => "sig-maya"
        };

        return [.. signals.OrderByDescending(s => s.SignalId == priority)];
    }

    private static IReadOnlyList<GrowthJourney> CreateGrowthJourneys() =>
    [
        CreateJourney(
            "member-maya",
            "Maya Sen",
            "MS",
            current: "Mentor",
            recommended: "Track Lead",
            recommendation: "Ready for Track Lead — evidence supports Chapter Champion path.",
            ledger: ["Mentored 12 participants", "Organized 2 workshops", "Delivered 3 talks"],
            programs: ["Open Source Mentorship Circle — Mentor", "Speaker Development Track — Facilitator"],
            events: ["Hacktoberfest Mumbai 2025 — Volunteer Lead", "Platform Engineering Night — Speaker"],
            graph: "Connected to 18 members through mentorship and shared attendance.",
            rationale: "Sustained mentoring plus workshop ownership matches Track Lead criteria."),
        CreateJourney(
            "member-jordan",
            "Jordan Blake",
            "JB",
            current: "Mentor",
            recommended: "Track Lead",
            recommendation: "Nominate for Track Lead after Hacktoberfest delivery.",
            ledger: ["Coordinated 3 events", "Mentored 4 volunteers", "Resolved 3 blockers"],
            programs: ["Chapter Hosts Pathway — Candidate"],
            events: ["Hacktoberfest Mumbai 2026 — Run-of-show owner"],
            graph: "Bridge node between volunteer leads and speaker track.",
            rationale: "Operational ownership plus mentoring signals Track Lead readiness."),
        CreateJourney(
            "member-rina",
            "Rina Shah",
            "RS",
            current: "Mentor",
            recommended: "Track Lead",
            recommendation: "Invite to Host Pathway — cross-chapter facilitation supports Track Lead.",
            ledger: ["Facilitated workshops in 2 chapters", "Introduced 6 newcomers across communities", "Shared attendance with Mangaluru cohort"],
            programs: ["Open Source Mentorship Circle — Mentee graduate", "Chapter Hosts Pathway — Candidate"],
            events: ["AgentCamp Mangaluru — Room host"],
            graph: "Cross-chapter ties with Mumbai and Mangaluru cohorts.",
            rationale: "Teaching and cross-chapter bridging match Track Lead criteria."),
        CreateJourney(
            "member-akash",
            "Akash Kumar",
            "AK",
            current: "Participant",
            recommended: "Volunteer",
            recommendation: "Offer a low-friction volunteer shift after outreach.",
            ledger: ["Waitlisted 3 times", "Attended 2 events before waitlist streak"],
            programs: [],
            events: ["Platform Engineering Night — Waitlisted", "Hacktoberfest Mumbai 2026 — Waitlisted"],
            graph: "Sparse recent connections; prior volunteer edges still present.",
            rationale: "Repeat waitlisting precedes disengagement without a warm next step.")
    ];

    private static GrowthJourney CreateJourney(
        string memberId,
        string name,
        string initials,
        string current,
        string recommended,
        string recommendation,
        IReadOnlyList<string> ledger,
        IReadOnlyList<string> programs,
        IReadOnlyList<string> events,
        string graph,
        string rationale)
    {
        var stages = JourneyPath.Select(stage =>
        {
            var currentIndex = Array.IndexOf(JourneyPath, current);
            var recommendedIndex = Array.IndexOf(JourneyPath, recommended);
            var stageIndex = Array.IndexOf(JourneyPath, stage);

            var status = stageIndex switch
            {
                _ when stageIndex < currentIndex => JourneyStageStatus.Completed,
                _ when stageIndex == currentIndex => JourneyStageStatus.Current,
                _ when stageIndex == recommendedIndex => JourneyStageStatus.Recommended,
                _ => JourneyStageStatus.Future
            };

            return new JourneyStage(stage, status);
        }).ToArray();

        return new GrowthJourney(
            memberId,
            name,
            initials,
            stages,
            recommendation,
            ledger,
            programs,
            events,
            graph,
            rationale);
    }

    private static IReadOnlyList<ChapterRecommendation> CreateChapterRecommendations() =>
    [
        new(
            "rec-promote",
            "Promote leadership candidates",
            "Fill 2 Track Lead seats before the next quarter.",
            ["3 members at Mentor stage with strong ledger evidence", "1 Chapter Champion nomination ready"],
            "Open Leadership Signals",
            "#leadership-signals"),
        new(
            "rec-mentors",
            "Expand mentorship capacity",
            "Add 2 mentors within 21 days to clear the pairing queue.",
            ["6 mentees waiting", "Open Source Mentorship Circle producing 2.4x tech-lead outcomes"],
            "Open Mentorship Circle",
            "/programs"),
        new(
            "rec-cohort",
            "Launch new contributor cohort",
            "Convert 15 first-time attendees into verified volunteers.",
            ["Hacktoberfest waitlist pressure", "Guild maintainer pipeline has open seats"],
            "Open Programs Workspace",
            "/programs"),
        new(
            "rec-pipeline",
            "Create volunteer pipelines",
            "Publish a check-in → accessibility → event-lead ladder.",
            ["2 critical shifts unfilled", "High-velocity volunteers available"],
            "Review Assignments",
            "#immediate-decisions")
    ];

    private static IReadOnlyList<ChapterHealthItem> CreateChapterHealth() =>
    [
        new("health-promote", "2 leadership positions are ready for promotion.", "Mentor → Track Lead evidence is complete for Maya Sen and Jordan Blake."),
        new("health-mentors", "2 mentors required within 21 days.", "Pairing latency is the strongest predictor of mentee drop-off."),
        new("health-waitlist", "18 waitlist decisions need human review before communications.", "Capacity remains at 186 / 200 for Hacktoberfest Mumbai."),
        new("health-coverage", "2 volunteer shifts still need owners.", "Check-in and accessibility are the highest-impact unfilled roles.")
    ];

    private static DateOnly GetNextHacktoberfestDate(DateOnly today)
    {
        var eventDate = new DateOnly(today.Year, 10, 17);
        return eventDate < today ? eventDate.AddYears(1) : eventDate;
    }
}

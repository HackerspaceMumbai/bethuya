using Bethuya.Hybrid.Shared.Models.CommandCenter;
using Bethuya.Hybrid.Shared.Models.Participation;
using System.Globalization;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>
/// Stable provider boundary for the Community Participation homepage. Deterministic today,
/// replaceable by live journey and opportunity services without changing the homepage.
/// </summary>
public interface ICommunityParticipationService
{
    /// <summary>Gets the participation homepage for the supplied role.</summary>
    Task<CommunityParticipationHome> GetAsync(
        CommunityRole role,
        string displayName,
        CancellationToken cancellationToken = default);
}

/// <summary>Rule-driven participation data for Homepage v3.</summary>
public sealed class DeterministicCommunityParticipationService(TimeProvider timeProvider) : ICommunityParticipationService
{
    private static readonly IReadOnlyList<ParticipationOpportunity> ParticipantOpportunities =
    [
        new("Welcome desk helper", "Greet attendees for the first hour of Hacktoberfest.", "Matches your event and a low time commitment.", "Express interest", "/opportunities"),
        new("Beginner workshop track", "Join a guided track built for first-time contributors.", "Matches your stated interest in getting started.", "Explore track", "/opportunities")
    ];

    private static readonly IReadOnlyList<ContributionHighlight> MemberContributions =
    [
        new("Events attended", "7", "Across the last 12 months"),
        new("Volunteer shifts", "4", "Check-in, AV, and attendee support"),
        new("Workshops facilitated", "3", "Beginner and intermediate tracks"),
        new("People helped", "21", "Based on sessions you supported")
    ];

    private static readonly IReadOnlyList<ParticipationOpportunity> MemberOpportunities =
    [
        new("Become a mentor", "Guide one mentee through a three-month cohort.", "You have facilitated three workshops and answered newcomer questions.", "Express interest", "/mentorship"),
        new("Lead a working group", "Co-own the community health working group.", "Matches your consistent participation and interest in retention.", "Learn more", "/community-graph"),
        new("Host the overflow room", "Support the second room at Hacktoberfest.", "Matches your facilitation history and availability.", "Express interest", "/opportunities")
    ];

    /// <inheritdoc />
    public Task<CommunityParticipationHome> GetAsync(
        CommunityRole role,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var isOnboarding = role == CommunityRole.EventParticipant;
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var hacktoberfestDate = GetNextHacktoberfestDate(today);

        if (isOnboarding)
        {
            return Task.FromResult(new CommunityParticipationHome(
                $"Welcome, {displayName}",
                "You are registered for Hacktoberfest Mumbai. Here is how to get the most from it.",
                true,
                new CommunityPassport(
                    "Your community passport",
                    "Event participant",
                    "You have one upcoming event and no community history yet.",
                    "Community member",
                    20,
                    ["Registered"]),
                CreateOnboardingSteps(hacktoberfestDate),
                CreateParticipantActivities(hacktoberfestDate, today),
                [],
                [],
                ParticipantOpportunities,
                []));
        }

        var isContributor = role == CommunityRole.EmergingContributor;
        var earnedSections = CreateEarnedSections(hacktoberfestDate, today);

        return Task.FromResult(new CommunityParticipationHome(
            $"Your journey, {displayName}",
            "Where you are, what you can do next, and how to participate more deeply.",
            false,
            new CommunityPassport(
                "Your community passport",
                isContributor ? "Emerging contributor" : "Community member",
                isContributor
                    ? "Three facilitated workshops and a session proposal in review."
                    : "Seven events attended and four volunteer shifts completed.",
                isContributor ? "Mentor" : "Contributor",
                isContributor ? 72 : 55,
                isContributor
                    ? ["Facilitator", "Volunteer", "Session author"]
                    : ["Volunteer", "Regular attendee"]),
            [],
            CreateMemberActivities(hacktoberfestDate),
            CreateMemberTimeline(today),
            MemberContributions,
            MemberOpportunities,
            isContributor ? earnedSections : [earnedSections[0], earnedSections[3]]));
    }

    private static DateOnly GetNextHacktoberfestDate(DateOnly today)
    {
        var eventDate = new DateOnly(today.Year, 10, 17);
        return eventDate < today ? eventDate.AddYears(1) : eventDate;
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    private static string FormatMonth(DateOnly date) =>
        date.ToString("MMM yyyy", CultureInfo.InvariantCulture);

    private static IReadOnlyList<OnboardingStep> CreateOnboardingSteps(DateOnly eventDate) =>
    [
        new("Confirm your event registration", $"Hacktoberfest Mumbai on {FormatDate(eventDate)} · your seat is confirmed.", "View event", "/events", true),
        new("Complete your profile", "Add your interests so recommendations match what you care about.", "Complete profile", "/profile", false),
        new("Join the community", "Introduce yourself and get access to community channels.", "Join community", "/community-graph", false),
        new("Discover opportunities", "Find a first, low-commitment way to contribute.", "Discover opportunities", "/opportunities", false)
    ];

    private static IReadOnlyList<ParticipationActivity> CreateParticipantActivities(
        DateOnly eventDate,
        DateOnly today)
    {
        var eventActivity = new ParticipationActivity(
            "Hacktoberfest Mumbai",
            FormatDate(eventDate),
            "You are confirmed. Doors open at 09:30.",
            "/events");
        var welcomeDate = eventDate.AddDays(-2);
        return welcomeDate >= today
            ?
            [
                new("Newcomer welcome call", FormatDate(welcomeDate), "Optional 20-minute orientation before the event.", "/events"),
                eventActivity
            ]
            : [eventActivity];
    }

    private static IReadOnlyList<ParticipationActivity> CreateMemberActivities(DateOnly eventDate) =>
    [
        new("Hacktoberfest Mumbai", FormatDate(eventDate), "You are confirmed and signed up for the overflow room.", "/events"),
        new("Mentorship cohort kickoff", FormatDate(eventDate.AddDays(3)), "Your mentor pairing starts with an intro session.", "/mentorship"),
        new("Community health working group", FormatDate(eventDate.AddDays(12)), "Standing invitation as a contributing member.", "/community-health")
    ];

    private static IReadOnlyList<ParticipationMoment> CreateMemberTimeline(DateOnly today) =>
    [
        new(FormatMonth(today.AddMonths(-1)), "Facilitated a workshop breakout", "Supported eight first-time contributors."),
        new(FormatMonth(today.AddMonths(-2)), "Volunteered at Platform Engineering Night", "Check-in and attendee support."),
        new(FormatMonth(today.AddMonths(-3)), "Submitted a session proposal", "Currently in community review."),
        new(FormatMonth(today.AddMonths(-5)), "Attended your first event", "AgentCamp Mangaluru.")
    ];

    private static IReadOnlyList<EarnedSection> CreateEarnedSections(DateOnly eventDate, DateOnly today) =>
    [
        new("Volunteer assignments", "Shifts you have accepted.", "earned-volunteer",
        [
            new("Overflow room host", FormatDate(eventDate), "Hacktoberfest Mumbai · 13:00 – 16:00", "/volunteers")
        ]),
        new("Mentorship activities", "Your pairings and sessions.", "earned-mentorship",
        [
            new("Mentee intro session", FormatDate(eventDate.AddDays(3)), "First session of the upcoming cohort.", "/mentorship")
        ]),
        new("Working groups", "Groups you participate in.", "earned-working-groups",
        [
            new("Community health working group", FormatDate(eventDate.AddDays(12)), "Retention review agenda.", "/community-health")
        ]),
        new("Recognition", "How the community has acknowledged you.", "earned-recognition",
        [
            new("Workshop facilitator badge", FormatMonth(today.AddMonths(-1)), "Awarded after three facilitated sessions.", "/community-graph")
        ])
    ];
}

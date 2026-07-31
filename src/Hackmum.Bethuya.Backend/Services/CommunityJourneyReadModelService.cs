using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Builds read-heavy lifecycle journey and community health projections from existing event, registration, passport, and ledger foundations.
/// </summary>
public sealed partial class CommunityJourneyReadModelService(
    BethuyaDbContext db,
    CommunityPassportService communityPassportService,
    ILogger<CommunityJourneyReadModelService> logger)
{
    private static readonly JourneyStageDefinition[] JourneyStages =
    [
        new("Explorer", 0, 7),
        new("Builder", 8, 17),
        new("Contributor", 18, 29),
        new("Community Leader", 30, int.MaxValue)
    ];

    private const int DefaultTimelineLimit = 20;
    private const int MaxTimelineLimit = 100;
    private const int MinLookbackDays = 30;
    private const int MaxLookbackDays = 365;

    /// <summary>
    /// Normalizes email for consistent comparison across all code paths.
    /// Uses invariant culture to avoid issues with region-specific char mappings (e.g., Turkish 'i' → 'İ').
    /// </summary>
    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    public async Task<CommunityJourneyProjectionResponse> GetJourneyProjectionAsync(
        CommunitySubjectContext subject,
        int timelineLimit = DefaultTimelineLimit,
        CancellationToken ct = default)
    {
        var member = await communityPassportService.EnsureMemberProvisionedAsync(subject, ct);

        var registrations = await QueryRegistrationsByEmail(member.Email)
            .OrderByDescending(registration => registration.UpdatedAt)
            .ToListAsync(ct);

        var ledgerEntries = await db.ParticipationLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.CommunityMemberId == member.Id)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.IngestedAt)
            .ToListAsync(ct);

        var eventsById = await LoadEventsByIdAsync(
            [
                .. registrations.Select(registration => registration.EventId),
                .. ledgerEntries.Where(entry => entry.EventId.HasValue).Select(entry => entry.EventId!.Value)
            ],
            ct);

        var allTimelineEntries = BuildTimeline(registrations, ledgerEntries, eventsById)
            .OrderByDescending(entry => entry.OccurredAt)
            .ToList();
        var timelineEntries = allTimelineEntries
            .Take(Math.Clamp(timelineLimit, 1, MaxTimelineLimit))
            .ToList();

        var journeyScore = allTimelineEntries.Sum(entry => entry.Points);
        var stageProgress = BuildStageProgress(journeyScore);
        var currentStage = JourneyStages.First(stage => stage.Name == stageProgress.CurrentStage);

        var projections = BuildJourneyProjections(journeyScore, allTimelineEntries, currentStage);
        var lifecycleProgression = BuildLifecycleProgression(eventsById);
        var stageCompletion = CalculateStageCompletionPercent(journeyScore, currentStage);

        return new CommunityJourneyProjectionResponse(
            CurrentStage: stageProgress.CurrentStage,
            JourneyScore: journeyScore,
            StageCompletionPercent: stageCompletion,
            StageProgress: stageProgress,
            Timeline: timelineEntries,
            Projections: projections,
            LifecycleProgression: lifecycleProgression);
    }

    public async Task<CommunityHealthDashboardReadModelResponse> GetDashboardReadModelAsync(
        int lookbackDays = 90,
        CancellationToken ct = default)
    {
        var boundedLookbackDays = Math.Clamp(lookbackDays, MinLookbackDays, MaxLookbackDays);
        var now = DateTimeOffset.UtcNow;
        var currentWindowStart = now.AddDays(-boundedLookbackDays);
        var previousWindowStart = currentWindowStart.AddDays(-boundedLookbackDays);

        var registrations = await db.Registrations
            .AsNoTracking()
            .Where(registration => registration.UpdatedAt >= previousWindowStart && registration.UpdatedAt <= now)
            .ToListAsync(ct);

        var ledgerEntries = await db.ParticipationLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.OccurredAt >= previousWindowStart && entry.OccurredAt <= now)
            .ToListAsync(ct);

        var communityMembers = await db.CommunityMembers
            .AsNoTracking()
            .Select(member => new
            {
                member.Id,
                member.Email,
                member.IsDiscoverableToCommunity
            })
            .ToListAsync(ct);

        var currentRegistrations = registrations
            .Where(registration => registration.UpdatedAt >= currentWindowStart)
            .ToArray();
        var previousRegistrations = registrations
            .Where(registration => registration.UpdatedAt >= previousWindowStart && registration.UpdatedAt < currentWindowStart)
            .ToArray();

        var previousActiveMembers = previousRegistrations
            .Where(registration => registration.Status == RegistrationStatus.CheckedIn)
            .Select(registration => registration.Email.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var currentActiveMembers = currentRegistrations
            .Where(registration => registration.Status == RegistrationStatus.CheckedIn)
            .Select(registration => registration.Email.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var retainedMembers = previousActiveMembers.Intersect(currentActiveMembers, StringComparer.Ordinal).Count();
        var retention = new RetentionReadModelResponse(
            PreviouslyActiveMembers: previousActiveMembers.Count,
            CurrentlyActiveMembers: currentActiveMembers.Count,
            RetainedMembers: retainedMembers,
            RetentionRatePercent: ToPercent(retainedMembers, previousActiveMembers.Count));

        int acceptedCount = 0, attendedCount = 0, waitlistedCount = 0;
        foreach (var registration in currentRegistrations)
        {
            if (registration.Status is RegistrationStatus.Accepted or RegistrationStatus.CheckedIn)
                acceptedCount++;
            if (registration.Status == RegistrationStatus.CheckedIn)
                attendedCount++;
            if (registration.Status == RegistrationStatus.Waitlisted)
                waitlistedCount++;
        }
        var attendance = new AttendanceReadModelResponse(
            RegisteredCount: currentRegistrations.Length,
            AcceptedCount: acceptedCount,
            AttendedCount: attendedCount,
            WaitlistedCount: waitlistedCount,
            AttendanceRatePercent: ToPercent(attendedCount, acceptedCount));

        var currentVolunteerSignals = CountVolunteerSignals(currentRegistrations, ledgerEntries, currentWindowStart, now);
        var previousVolunteerSignals = CountVolunteerSignals(previousRegistrations, ledgerEntries, previousWindowStart, currentWindowStart);
        var volunteerGrowth = new VolunteerGrowthReadModelResponse(
            PreviousWindowSignals: previousVolunteerSignals,
            CurrentWindowSignals: currentVolunteerSignals,
            DeltaSignals: currentVolunteerSignals - previousVolunteerSignals,
            GrowthRatePercent: previousVolunteerSignals == 0
                ? (currentVolunteerSignals > 0 ? 100d : 0d)
                : Math.Round(((double)(currentVolunteerSignals - previousVolunteerSignals) / previousVolunteerSignals) * 100d, 2));

        var membersByEmail = communityMembers
            .GroupBy(member => NormalizeEmail(member.Email))
            .ToList();

        // Log warning if duplicate emails detected, then take the newest (highest ID, which reflects insertion order)
        var duplicateEmails = membersByEmail.Where(g => g.Count() > 1).ToList();
        if (duplicateEmails.Count > 0)
        {
            var emailList = string.Join(", ", duplicateEmails.Select(g => g.Key));
            LogDuplicateEmails(duplicateEmails.Count, emailList);
        }

        var membersByEmailDict = membersByEmail
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var newestMember = group.OrderByDescending(m => m.Id).First();
                    return new CommunityMemberLookup(newestMember.Id, newestMember.IsDiscoverableToCommunity);
                },
                StringComparer.Ordinal);
        var interestedMemberIds = ResolveVolunteerInterestedMemberIds(registrations, ledgerEntries, membersByEmailDict);
        var activeVolunteerIds = ResolveActiveVolunteerIds(currentWindowStart, now, registrations, ledgerEntries, membersByEmailDict);
        var leadershipCandidateIds = ResolveLeadershipCandidateIds(registrations, ledgerEntries, membersByEmailDict);

        var funnel = new LeadershipFunnelReadModelResponse(
            DiscoverableMembers: communityMembers.Count(member => member.IsDiscoverableToCommunity),
            VolunteerInterestedMembers: interestedMemberIds.Count,
            ActiveVolunteers: activeVolunteerIds.Count,
            LeadershipCandidates: leadershipCandidateIds.Count);

        return new CommunityHealthDashboardReadModelResponse(
            AsOfUtc: now,
            LookbackDays: boundedLookbackDays,
            Retention: retention,
            Attendance: attendance,
            VolunteerGrowth: volunteerGrowth,
            LeadershipFunnel: funnel);
    }

    private IQueryable<Registration> QueryRegistrationsByEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var query = db.Registrations.AsNoTracking();
        if (db.Database.IsNpgsql())
        {
            var pattern = EscapeLikePattern(email.Trim());
            return query.Where(registration => EF.Functions.ILike(registration.Email, pattern));
        }

        // CA1862: We use ToUpperInvariant() comparison instead of string.Equals(..., StringComparison.OrdinalIgnoreCase)
        // because the latter is not translatable by EF Core on non-Npgsql relational providers (e.g., SQLite, SQL Server).
        // ToUpperInvariant() is consistently translatable across all EF Core providers to SQL UPPER() function.
#pragma warning disable CA1862
        return query.Where(registration => registration.Email.ToUpperInvariant() == normalizedEmail);
#pragma warning restore CA1862
    }

    private static List<JourneyTimelineEntryResponse> BuildTimeline(
        List<Registration> registrations,
        List<ParticipationLedgerEntry> ledgerEntries,
        Dictionary<Guid, Event> eventsById)
    {
        var timeline = new List<JourneyTimelineEntryResponse>(registrations.Count + ledgerEntries.Count);

        foreach (var registration in registrations)
        {
            var status = registration.Status.ToString();
            var points = PointsForRegistration(registration.Status);
            var eventTitle = eventsById.TryGetValue(registration.EventId, out var evt)
                ? evt.Title
                : "Unknown event";

            timeline.Add(new JourneyTimelineEntryResponse(
                OccurredAt: registration.UpdatedAt,
                Source: "Registration",
                Activity: status,
                Points: points,
                Evidence: $"Registration status moved to {status}.",
                EventId: registration.EventId,
                EventTitle: eventTitle));
        }

        foreach (var entry in ledgerEntries)
        {
            var eventTitle = entry.EventId.HasValue && eventsById.TryGetValue(entry.EventId.Value, out var evt)
                ? evt.Title
                : entry.EventId.HasValue ? "Unknown event" : null;

            timeline.Add(new JourneyTimelineEntryResponse(
                OccurredAt: entry.OccurredAt,
                Source: entry.Connector.ToString(),
                Activity: entry.Activity.ToString(),
                Points: PointsForActivity(entry.Activity),
                Evidence: entry.Evidence,
                EventId: entry.EventId,
                EventTitle: eventTitle));
        }

        return timeline;
    }

    private static JourneyStageProgressResponse BuildStageProgress(int score)
    {
        var current = JourneyStages.First(stage => score >= stage.MinScore && score <= stage.MaxScore);
        var currentIndex = Array.IndexOf(JourneyStages, current);
        var hasNext = currentIndex < JourneyStages.Length - 1;
        var next = hasNext ? JourneyStages[currentIndex + 1] : default;

        return new JourneyStageProgressResponse(
            CurrentStage: current.Name,
            NextStage: hasNext ? next.Name : null,
            CurrentStageMinScore: current.MinScore,
            CurrentStageMaxScore: current.MaxScore,
            NextStageScoreThreshold: hasNext ? next.MinScore : current.MinScore,
            PointsToNextStage: hasNext ? Math.Max(0, next.MinScore - score) : 0);
    }

    private static List<JourneyTimelineProjectionResponse> BuildJourneyProjections(
        int score,
        List<JourneyTimelineEntryResponse> timeline,
        JourneyStageDefinition currentStage)
    {
        var recentWindowStart = DateTimeOffset.UtcNow.AddDays(-90);
        var recentPoints = timeline
            .Where(entry => entry.OccurredAt >= recentWindowStart)
            .Sum(entry => entry.Points);
        var monthlyVelocity = Math.Round(recentPoints / 3d, 2);

        if (monthlyVelocity <= 0)
        {
            return [];
        }

        var projections = new List<JourneyTimelineProjectionResponse>();
        var currentIndex = Array.IndexOf(JourneyStages, currentStage);

        for (var index = currentIndex + 1; index < JourneyStages.Length; index++)
        {
            var next = JourneyStages[index];
            var pointsRemaining = Math.Max(0, next.MinScore - score);
            if (pointsRemaining == 0)
            {
                continue;
            }

            var monthsToMilestone = Math.Ceiling(pointsRemaining / monthlyVelocity);
            var projectedAt = DateTimeOffset.UtcNow.AddDays(monthsToMilestone * 30d);
            var confidence = monthlyVelocity >= 8 ? "High" : monthlyVelocity >= 4 ? "Medium" : "Low";

            projections.Add(new JourneyTimelineProjectionResponse(
                Milestone: next.Name,
                ProjectedAt: projectedAt,
                PointsRemaining: pointsRemaining,
                MonthlyVelocityPoints: monthlyVelocity,
                Confidence: confidence,
                Rationale: $"Projected using trailing 90-day activity velocity of {monthlyVelocity:0.##} points/month."));
        }

        return projections;
    }

    private static List<EventLifecycleJourneyProgressResponse> BuildLifecycleProgression(
        Dictionary<Guid, Event> eventsById)
    {
        var progression = new List<EventLifecycleJourneyProgressResponse>(eventsById.Count);

        foreach (var evt in eventsById.Values.OrderByDescending(evt => evt.StartDate))
        {
            var nextState = GetProjectedNextState(evt.LifecycleState);
            progression.Add(new EventLifecycleJourneyProgressResponse(
                EventId: evt.Id,
                EventTitle: evt.Title,
                CurrentState: evt.LifecycleState.ToString(),
                NextState: nextState?.ToString(),
                ProjectedNextTransitionAt: nextState.HasValue
                    ? ProjectLifecycleTransitionAt(evt, nextState.Value)
                    : null));
        }

        return progression;
    }

    private static MeetupLifecycleState? GetProjectedNextState(MeetupLifecycleState currentState)
        => currentState switch
        {
            MeetupLifecycleState.Drafted => MeetupLifecycleState.VenueLocked,
            MeetupLifecycleState.VenueLocked => MeetupLifecycleState.CfpOpen,
            MeetupLifecycleState.CfpOpen => MeetupLifecycleState.ReviewAndPlanning,
            MeetupLifecycleState.CfpExtended => MeetupLifecycleState.ReviewAndPlanning,
            MeetupLifecycleState.ReviewAndPlanning => MeetupLifecycleState.AgendaApproved,
            MeetupLifecycleState.AgendaApproved => MeetupLifecycleState.Published,
            MeetupLifecycleState.Published => MeetupLifecycleState.Completed,
            MeetupLifecycleState.ScheduleAltered => MeetupLifecycleState.Published,
            MeetupLifecycleState.Delayed => MeetupLifecycleState.Published,
            MeetupLifecycleState.Completed => MeetupLifecycleState.Archived,
            MeetupLifecycleState.Archived => null,
            _ => null
        };

    private static DateTimeOffset ProjectLifecycleTransitionAt(Event evt, MeetupLifecycleState targetState)
        => targetState switch
        {
            MeetupLifecycleState.VenueLocked => evt.StartDate.AddDays(-42),
            MeetupLifecycleState.CfpOpen => evt.StartDate.AddDays(-35),
            MeetupLifecycleState.ReviewAndPlanning => evt.StartDate.AddDays(-21),
            MeetupLifecycleState.AgendaApproved => evt.StartDate.AddDays(-14),
            MeetupLifecycleState.Published => evt.StartDate.AddDays(-7),
            MeetupLifecycleState.Completed => evt.EndDate.AddHours(4),
            MeetupLifecycleState.Archived => evt.EndDate.AddDays(14),
            _ => evt.StartDate
        };

    private static double CalculateStageCompletionPercent(int score, JourneyStageDefinition stage)
    {
        if (stage.MaxScore == int.MaxValue)
        {
            return 100d;
        }

        var span = Math.Max(1, stage.MaxScore - stage.MinScore);
        var normalized = Math.Clamp(score - stage.MinScore, 0, span);
        return Math.Round((double)normalized / span * 100d, 2);
    }

    private static int PointsForRegistration(RegistrationStatus status)
        => status switch
        {
            RegistrationStatus.CheckedIn => 4,
            RegistrationStatus.Accepted => 2,
            RegistrationStatus.Waitlisted => 1,
            _ => 0
        };

    private static int PointsForActivity(ParticipationActivityKind activity)
        => activity switch
        {
            ParticipationActivityKind.JoinedCommunity => 2,
            ParticipationActivityKind.MessageEngaged => 1,
            ParticipationActivityKind.Registered => 1,
            ParticipationActivityKind.Waitlisted => 1,
            ParticipationActivityKind.Attended => 4,
            ParticipationActivityKind.Volunteered => 5,
            ParticipationActivityKind.SubmittedSession => 6,
            _ => 1
        };

    private static int CountVolunteerSignals(
        IReadOnlyCollection<Registration> registrations,
        IReadOnlyCollection<ParticipationLedgerEntry> ledgerEntries,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        var registrationSignals = registrations.Count(HasVolunteerSignal);
        var ledgerSignals = ledgerEntries.Count(entry =>
            entry.OccurredAt >= windowStart
            && entry.OccurredAt < windowEnd
            && entry.Activity is ParticipationActivityKind.Volunteered or ParticipationActivityKind.SubmittedSession);
        return registrationSignals + ledgerSignals;
    }

    private static HashSet<Hackmum.Bethuya.Core.ValueObjects.CommunityMemberId> ResolveVolunteerInterestedMemberIds(
        IReadOnlyCollection<Registration> registrations,
        IReadOnlyCollection<ParticipationLedgerEntry> ledgerEntries,
        IReadOnlyDictionary<string, CommunityMemberLookup> membersByEmail)
    {
        var interested = new HashSet<Hackmum.Bethuya.Core.ValueObjects.CommunityMemberId>();

        foreach (var registration in registrations.Where(HasVolunteerSignal))
        {
            var email = NormalizeEmail(registration.Email);
            if (membersByEmail.TryGetValue(email, out var member))
            {
                interested.Add(member.Id);
            }
        }

        foreach (var memberId in ledgerEntries
                     .Where(entry => entry.Activity is ParticipationActivityKind.Volunteered or ParticipationActivityKind.SubmittedSession)
                     .Select(entry => entry.CommunityMemberId))
        {
            interested.Add(memberId);
        }

        return interested;
    }

    private static HashSet<Hackmum.Bethuya.Core.ValueObjects.CommunityMemberId> ResolveActiveVolunteerIds(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        IReadOnlyCollection<Registration> registrations,
        IReadOnlyCollection<ParticipationLedgerEntry> ledgerEntries,
        IReadOnlyDictionary<string, CommunityMemberLookup> membersByEmail)
    {
        var active = new HashSet<Hackmum.Bethuya.Core.ValueObjects.CommunityMemberId>();

        foreach (var entry in ledgerEntries.Where(entry =>
                     entry.OccurredAt >= windowStart
                     && entry.OccurredAt < windowEnd
                     && entry.Activity is ParticipationActivityKind.Volunteered or ParticipationActivityKind.SubmittedSession))
        {
            active.Add(entry.CommunityMemberId);
        }

        foreach (var registration in registrations.Where(registration =>
                     registration.UpdatedAt >= windowStart
                     && registration.UpdatedAt < windowEnd
                     && HasVolunteerSignal(registration)))
        {
            var email = NormalizeEmail(registration.Email);
            if (membersByEmail.TryGetValue(email, out var member))
            {
                active.Add(member.Id);
            }
        }

        return active;
    }

    private static HashSet<Hackmum.Bethuya.Core.ValueObjects.CommunityMemberId> ResolveLeadershipCandidateIds(
        IReadOnlyCollection<Registration> registrations,
        IReadOnlyCollection<ParticipationLedgerEntry> ledgerEntries,
        IReadOnlyDictionary<string, CommunityMemberLookup> membersByEmail)
    {
        var attendedCountsByMemberId = new Dictionary<Hackmum.Bethuya.Core.ValueObjects.CommunityMemberId, int>();
        foreach (var registration in registrations.Where(registration => registration.Status == RegistrationStatus.CheckedIn))
        {
            var email = NormalizeEmail(registration.Email);
            if (!membersByEmail.TryGetValue(email, out var member))
            {
                continue;
            }

            attendedCountsByMemberId.TryGetValue(member.Id, out var currentCount);
            attendedCountsByMemberId[member.Id] = currentCount + 1;
        }

        var leadershipSignals = ledgerEntries
            .Where(entry => entry.Activity is ParticipationActivityKind.Volunteered or ParticipationActivityKind.SubmittedSession)
            .Select(entry => entry.CommunityMemberId)
            .ToHashSet();

        return attendedCountsByMemberId
            .Where(pair => pair.Value >= 2 && leadershipSignals.Contains(pair.Key))
            .Select(pair => pair.Key)
            .ToHashSet();
    }

    private static bool HasVolunteerSignal(Registration registration)
        => registration.ContributionPreferences.Any(preference =>
               preference.Contains("volunteer", StringComparison.OrdinalIgnoreCase))
           || (!string.IsNullOrWhiteSpace(registration.Intent)
               && registration.Intent.Contains("volunteer", StringComparison.OrdinalIgnoreCase))
           || (!string.IsNullOrWhiteSpace(registration.Goals)
               && registration.Goals.Contains("volunteer", StringComparison.OrdinalIgnoreCase));

    private async Task<Dictionary<Guid, Event>> LoadEventsByIdAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken ct)
    {
        var ids = eventIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await db.Events
            .AsNoTracking()
            .Where(evt => ids.Contains(evt.Id))
            .ToDictionaryAsync(evt => evt.Id, ct);
    }

    private static double ToPercent(int numerator, int denominator)
        => denominator <= 0
            ? 0d
            : Math.Round(((double)numerator / denominator) * 100d, 2);

    private static string EscapeLikePattern(string value)
        => value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    private readonly record struct CommunityMemberLookup(
        Hackmum.Bethuya.Core.ValueObjects.CommunityMemberId Id,
        bool IsDiscoverableToCommunity);

    private readonly record struct JourneyStageDefinition(string Name, int MinScore, int MaxScore);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Warning,
        Message = "Found {DuplicateCount} duplicate emails in community members: {Emails} (taking newest by ID for each)")]
    private partial void LogDuplicateEmails(int duplicateCount, string emails);
}

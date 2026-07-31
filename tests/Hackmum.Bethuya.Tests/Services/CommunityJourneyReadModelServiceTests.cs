using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class CommunityJourneyReadModelServiceTests
{
    [Test]
    public async Task GetJourneyProjectionAsync_BuildsLifecycleProgressAndMilestoneForecasts()
    {
        await using var db = CreateDbContext();
        var passportService = new CommunityPassportService(db);
        var subject = new CommunitySubjectContext("journey-member-1", "Journey Tester", "journey1@example.com");

        db.AttendeeProfiles.Add(new AttendeeProfile
        {
            UserId = subject.UserId,
            FirstName = "Journey",
            LastName = "Tester",
            Email = subject.Email!,
            GovernmentPhotoIdType = "PAN",
            GovernmentIdLastFour = "1234",
            LinkedInMemberId = "journey-linkedin",
            GitHubLogin = "journey-github",
            GitHubProfileUrl = "https://github.com/journey-github",
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        });

        var lifecycleEvent = new Event
        {
            Title = "Lifecycle Event",
            Type = EventType.Meetup,
            Capacity = 80,
            StartDate = DateTimeOffset.UtcNow.AddDays(10),
            EndDate = DateTimeOffset.UtcNow.AddDays(10).AddHours(3),
            CreatedBy = "organizer"
        };
        lifecycleEvent.TransitionLifecycleTo(MeetupLifecycleState.VenueLocked, DateTimeOffset.UtcNow.AddDays(-20));
        lifecycleEvent.TransitionLifecycleTo(MeetupLifecycleState.CfpOpen, DateTimeOffset.UtcNow.AddDays(-18));
        lifecycleEvent.TransitionLifecycleTo(MeetupLifecycleState.ReviewAndPlanning, DateTimeOffset.UtcNow.AddDays(-16));
        lifecycleEvent.TransitionLifecycleTo(MeetupLifecycleState.AgendaApproved, DateTimeOffset.UtcNow.AddDays(-14));
        lifecycleEvent.TransitionLifecycleTo(MeetupLifecycleState.Published, DateTimeOffset.UtcNow.AddDays(-12));

        db.Events.Add(lifecycleEvent);
        db.Registrations.AddRange(
            new Registration
            {
                EventId = lifecycleEvent.Id,
                FullName = "Journey Tester",
                Email = subject.Email!,
                Status = RegistrationStatus.CheckedIn,
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-5)
            },
            new Registration
            {
                EventId = lifecycleEvent.Id,
                FullName = "Journey Tester",
                Email = subject.Email!,
                Status = RegistrationStatus.Accepted,
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-8)
            });

        await db.SaveChangesAsync();
        _ = await passportService.GetPassportAsync(subject);
        var memberId = await db.CommunityMembers
            .Where(member => member.UserId == subject.UserId)
            .Select(member => member.Id)
            .SingleAsync();

        db.ParticipationLedgerEntries.AddRange(
            new ParticipationLedgerEntry
            {
                Id = ParticipationLedgerEntryId.From(Guid.CreateVersion7()),
                CommunityMemberId = memberId,
                Connector = ParticipationConnectorKind.Discord,
                ExternalMemberKey = "discord:journey:1",
                Activity = ParticipationActivityKind.JoinedCommunity,
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-25),
                Evidence = "Joined #welcome",
                ProvenanceKey = "journey:discord:join:1"
            },
            new ParticipationLedgerEntry
            {
                Id = ParticipationLedgerEntryId.From(Guid.CreateVersion7()),
                CommunityMemberId = memberId,
                Connector = ParticipationConnectorKind.GitHub,
                ExternalMemberKey = "github:journey:1",
                Activity = ParticipationActivityKind.Volunteered,
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-4),
                Evidence = "Volunteered for registration desk",
                ProvenanceKey = "journey:github:volunteer:1",
                EventId = lifecycleEvent.Id
            },
            new ParticipationLedgerEntry
            {
                Id = ParticipationLedgerEntryId.From(Guid.CreateVersion7()),
                CommunityMemberId = memberId,
                Connector = ParticipationConnectorKind.GitHub,
                ExternalMemberKey = "github:journey:1",
                Activity = ParticipationActivityKind.SubmittedSession,
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-2),
                Evidence = "Submitted lightning talk",
                ProvenanceKey = "journey:github:session:1",
                EventId = lifecycleEvent.Id
            });

        await db.SaveChangesAsync();
        var service = new CommunityJourneyReadModelService(db, passportService);

        var projection = await service.GetJourneyProjectionAsync(subject, timelineLimit: 50);

        await Assert.That(projection.CurrentStage).IsEqualTo("Contributor");
        await Assert.That(projection.JourneyScore).IsGreaterThanOrEqualTo(18);
        await Assert.That(projection.Projections.Count).IsGreaterThan(0);
        await Assert.That(projection.LifecycleProgression.Select(item => item.CurrentState)).Contains("Published");
        await Assert.That(projection.LifecycleProgression.Select(item => item.NextState)).Contains("Completed");
    }

    [Test]
    public async Task GetDashboardReadModelAsync_ComputesRetentionVolunteerGrowthAndFunnel()
    {
        await using var db = CreateDbContext();
        var passportService = new CommunityPassportService(db);
        var now = DateTimeOffset.UtcNow;

        var subjects = new[]
        {
            new CommunitySubjectContext("member-a", "Member A", "member-a@example.com"),
            new CommunitySubjectContext("member-b", "Member B", "member-b@example.com"),
            new CommunitySubjectContext("member-c", "Member C", "member-c@example.com")
        };

        var namesByUserId = new Dictionary<string, (string First, string Last)>
        {
            ["member-a"] = ("Member", "A"),
            ["member-b"] = ("Member", "B"),
            ["member-c"] = ("Member", "C")
        };

        foreach (var subject in subjects)
        {
            var names = namesByUserId[subject.UserId];
            db.AttendeeProfiles.Add(new AttendeeProfile
            {
                UserId = subject.UserId,
                FirstName = names.First,
                LastName = names.Last,
                Email = subject.Email!,
                GovernmentPhotoIdType = "PAN",
                GovernmentIdLastFour = "9999",
                LinkedInMemberId = $"{subject.UserId}-linkedin",
                GitHubLogin = $"{subject.UserId}-github",
                GitHubProfileUrl = $"https://github.com/{subject.UserId}-github",
                IsProfileComplete = true,
                ProfileCompletedAt = now
            });
        }

        await db.SaveChangesAsync();
        foreach (var subject in subjects)
        {
            _ = await passportService.GetPassportAsync(subject);
        }

        var memberIdsByEmail = await db.CommunityMembers
            .ToDictionaryAsync(member => member.Email, member => member.Id);

        var evt = new Event
        {
            Title = "Read Model Event",
            Type = EventType.Meetup,
            Capacity = 120,
            StartDate = now.AddDays(7),
            EndDate = now.AddDays(7).AddHours(4),
            CreatedBy = "organizer"
        };
        db.Events.Add(evt);

        db.Registrations.AddRange(
            new Registration
            {
                EventId = evt.Id,
                FullName = "Member A",
                Email = "member-a@example.com",
                Status = RegistrationStatus.CheckedIn,
                UpdatedAt = now.AddDays(-120)
            },
            new Registration
            {
                EventId = evt.Id,
                FullName = "Member B",
                Email = "member-b@example.com",
                Status = RegistrationStatus.CheckedIn,
                UpdatedAt = now.AddDays(-118),
                ContributionPreferences = ["Volunteer logistics"]
            },
            new Registration
            {
                EventId = evt.Id,
                FullName = "Member A",
                Email = "member-a@example.com",
                Status = RegistrationStatus.CheckedIn,
                UpdatedAt = now.AddDays(-12)
            },
            new Registration
            {
                EventId = evt.Id,
                FullName = "Member C",
                Email = "member-c@example.com",
                Status = RegistrationStatus.Accepted,
                UpdatedAt = now.AddDays(-10)
            });

        db.ParticipationLedgerEntries.AddRange(
            new ParticipationLedgerEntry
            {
                Id = ParticipationLedgerEntryId.From(Guid.CreateVersion7()),
                CommunityMemberId = memberIdsByEmail["member-a@example.com"],
                Connector = ParticipationConnectorKind.GitHub,
                ExternalMemberKey = "github:member-a",
                Activity = ParticipationActivityKind.Volunteered,
                OccurredAt = now.AddDays(-8),
                Evidence = "Mentored first-timers",
                ProvenanceKey = "dashboard:volunteer:current:a"
            },
            new ParticipationLedgerEntry
            {
                Id = ParticipationLedgerEntryId.From(Guid.CreateVersion7()),
                CommunityMemberId = memberIdsByEmail["member-b@example.com"],
                Connector = ParticipationConnectorKind.GitHub,
                ExternalMemberKey = "github:member-b",
                Activity = ParticipationActivityKind.Volunteered,
                OccurredAt = now.AddDays(-140),
                Evidence = "Managed check-in desk",
                ProvenanceKey = "dashboard:volunteer:previous:b"
            });

        await db.SaveChangesAsync();
        var service = new CommunityJourneyReadModelService(db, passportService);

        CommunityHealthDashboardReadModelResponse readModel = await service.GetDashboardReadModelAsync(lookbackDays: 90);

        await Assert.That(readModel.Retention.PreviouslyActiveMembers).IsEqualTo(2);
        await Assert.That(readModel.Retention.CurrentlyActiveMembers).IsEqualTo(1);
        await Assert.That(readModel.Retention.RetainedMembers).IsEqualTo(1);
        await Assert.That(readModel.Attendance.RegisteredCount).IsEqualTo(2);
        await Assert.That(readModel.Attendance.AcceptedCount).IsEqualTo(2);
        await Assert.That(readModel.Attendance.AttendedCount).IsEqualTo(1);
        await Assert.That(readModel.LeadershipFunnel.LeadershipCandidates).IsEqualTo(1);
        await Assert.That(readModel.VolunteerGrowth.CurrentWindowSignals).IsEqualTo(1);
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"community-journey-read-model-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }
}

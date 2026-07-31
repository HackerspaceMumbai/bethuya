using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class ParticipationLedgerServiceTests
{
    [Test]
    public async Task WriteAsync_DeduplicatesByProvenanceKey()
    {
        await using var db = CreateDbContext();
        var passportService = new CommunityPassportService(db);
        var ledgerService = new ParticipationLedgerService(db, passportService);
        var subject = new CommunitySubjectContext("member-ledger-1", "Ledger Tester", "ledger1@example.com");

        var occurredAt = new DateTimeOffset(2026, 7, 31, 9, 0, 0, TimeSpan.Zero);
        var request = new UpsertParticipationEntriesRequest(
        [
            new ParticipationEntryWriteRequest(
                Connector: ParticipationConnectorKind.Meetup,
                ExternalMemberKey: "meetup:member:1",
                Activity: ParticipationActivityKind.Registered,
                OccurredAt: occurredAt,
                Evidence: "RSVP confirmed",
                ProvenanceKey: "meetup:rsvp:1"),
            new ParticipationEntryWriteRequest(
                Connector: ParticipationConnectorKind.Meetup,
                ExternalMemberKey: "meetup:member:1",
                Activity: ParticipationActivityKind.Registered,
                OccurredAt: occurredAt,
                Evidence: "RSVP confirmed duplicate",
                ProvenanceKey: "meetup:rsvp:1")
        ]);

        var result = await ledgerService.WriteAsync(subject, request);

        await Assert.That(result.ReceivedCount).IsEqualTo(2);
        await Assert.That(result.StoredCount).IsEqualTo(1);
        await Assert.That(result.DuplicateCount).IsEqualTo(1);
    }

    [Test]
    public async Task ReadTimelineAsync_ReturnsNewestEntriesWithEventTitles()
    {
        await using var db = CreateDbContext();
        var passportService = new CommunityPassportService(db);
        var ledgerService = new ParticipationLedgerService(db, passportService);
        var subject = new CommunitySubjectContext("member-ledger-2", "Timeline Tester", "ledger2@example.com");

        var firstEvent = new Event
        {
            Title = "Community Hack Night",
            Type = EventType.Meetup,
            Capacity = 60,
            StartDate = new DateTimeOffset(2026, 7, 10, 18, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 7, 10, 21, 0, 0, TimeSpan.Zero),
            CreatedBy = "organizer"
        };
        var secondEvent = new Event
        {
            Title = "Lightning Demos",
            Type = EventType.Meetup,
            Capacity = 40,
            StartDate = new DateTimeOffset(2026, 7, 20, 18, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 7, 20, 20, 0, 0, TimeSpan.Zero),
            CreatedBy = "organizer"
        };

        db.Events.AddRange(firstEvent, secondEvent);
        await db.SaveChangesAsync();

        _ = await ledgerService.WriteAsync(subject, new UpsertParticipationEntriesRequest(
        [
            new ParticipationEntryWriteRequest(
                Connector: ParticipationConnectorKind.Discord,
                ExternalMemberKey: "discord:user:42",
                Activity: ParticipationActivityKind.JoinedCommunity,
                OccurredAt: new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
                Evidence: "Joined #introductions",
                ProvenanceKey: "discord:join:42"),
            new ParticipationEntryWriteRequest(
                Connector: ParticipationConnectorKind.GitHub,
                ExternalMemberKey: "github:octocat",
                Activity: ParticipationActivityKind.Volunteered,
                OccurredAt: new DateTimeOffset(2026, 7, 25, 11, 30, 0, TimeSpan.Zero),
                Evidence: "Opened volunteer issue",
                ProvenanceKey: "github:issue:1001",
                EventId: secondEvent.Id),
            new ParticipationEntryWriteRequest(
                Connector: ParticipationConnectorKind.Eventbrite,
                ExternalMemberKey: "eventbrite:member:7",
                Activity: ParticipationActivityKind.Attended,
                OccurredAt: new DateTimeOffset(2026, 7, 12, 19, 10, 0, TimeSpan.Zero),
                Evidence: "Checked in at venue",
                ProvenanceKey: "eventbrite:checkin:77",
                EventId: firstEvent.Id)
        ]));

        var timeline = await ledgerService.ReadTimelineAsync(subject, limit: 2);

        await Assert.That(timeline.Entries.Count).IsEqualTo(2);
        await Assert.That(timeline.Entries[0].ProvenanceKey).IsEqualTo("github:issue:1001");
        await Assert.That(timeline.Entries[0].EventTitle).IsEqualTo("Lightning Demos");
        await Assert.That(timeline.Entries[1].ProvenanceKey).IsEqualTo("eventbrite:checkin:77");
        await Assert.That(timeline.Entries[1].EventTitle).IsEqualTo("Community Hack Night");
    }

    [Test]
    public async Task WriteAsync_ResolvesMemberByExternalIdentityInsteadOfCallerSubject()
    {
        await using var db = CreateDbContext();
        var passportService = new CommunityPassportService(db);
        var ledgerService = new ParticipationLedgerService(db, passportService);
        var callerSubject = new CommunitySubjectContext("connector-caller", "Connector Caller", "connector@example.com");

        var linkedMember = await SeedMemberWithIdentityAsync(
            db,
            userId: "linked-discord-user",
            email: "linked-discord@example.com",
            provider: IdentityProviderKind.Discord,
            subject: "discord:user:42");

        var writeResult = await ledgerService.WriteAsync(callerSubject, new UpsertParticipationEntriesRequest(
        [
            new ParticipationEntryWriteRequest(
                Connector: ParticipationConnectorKind.Discord,
                ExternalMemberKey: "discord:user:42",
                Activity: ParticipationActivityKind.JoinedCommunity,
                OccurredAt: new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero),
                Evidence: "Joined #welcome",
                ProvenanceKey: "discord:join:42")
        ]));

        await Assert.That(writeResult.StoredCount).IsEqualTo(1);

        var entry = await db.ParticipationLedgerEntries.SingleAsync();
        await Assert.That(entry.CommunityMemberId).IsEqualTo(linkedMember.Id);
    }

    [Test]
    public async Task WriteAsync_DoesNotCrossDeduplicateDifferentMembersForSameProvenance()
    {
        await using var db = CreateDbContext();
        var passportService = new CommunityPassportService(db);
        var ledgerService = new ParticipationLedgerService(db, passportService);
        var callerSubject = new CommunitySubjectContext("connector-caller-2", "Connector Caller Two", "connector2@example.com");

        _ = await SeedMemberWithIdentityAsync(
            db,
            userId: "member-one",
            email: "member-one@example.com",
            provider: IdentityProviderKind.GitHub,
            subject: "github:alice");
        _ = await SeedMemberWithIdentityAsync(
            db,
            userId: "member-two",
            email: "member-two@example.com",
            provider: IdentityProviderKind.GitHub,
            subject: "github:bob");

        var result = await ledgerService.WriteAsync(callerSubject, new UpsertParticipationEntriesRequest(
        [
            new ParticipationEntryWriteRequest(
                Connector: ParticipationConnectorKind.GitHub,
                ExternalMemberKey: "github:alice",
                Activity: ParticipationActivityKind.Volunteered,
                OccurredAt: new DateTimeOffset(2026, 7, 31, 8, 30, 0, TimeSpan.Zero),
                Evidence: "Volunteered for docs",
                ProvenanceKey: "github:volunteer:issue-99"),
            new ParticipationEntryWriteRequest(
                Connector: ParticipationConnectorKind.GitHub,
                ExternalMemberKey: "github:bob",
                Activity: ParticipationActivityKind.Volunteered,
                OccurredAt: new DateTimeOffset(2026, 7, 31, 8, 35, 0, TimeSpan.Zero),
                Evidence: "Volunteered for moderation",
                ProvenanceKey: "github:volunteer:issue-99")
        ]));

        await Assert.That(result.ReceivedCount).IsEqualTo(2);
        await Assert.That(result.StoredCount).IsEqualTo(2);
        await Assert.That(result.DuplicateCount).IsEqualTo(0);
    }

    private static async Task<CommunityMember> SeedMemberWithIdentityAsync(
        BethuyaDbContext db,
        string userId,
        string email,
        IdentityProviderKind provider,
        string subject)
    {
        var member = new CommunityMember
        {
            Id = CommunityMemberId.From(Guid.CreateVersion7()),
            UserId = userId,
            DisplayName = userId,
            Email = email
        };

        db.CommunityMembers.Add(member);
        db.ExternalIdentities.Add(new ExternalIdentity
        {
            Id = ExternalIdentityId.From(Guid.CreateVersion7()),
            CommunityMemberId = member.Id,
            Provider = provider,
            Subject = subject,
            IsVerified = true
        });

        await db.SaveChangesAsync();
        return member;
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"participation-ledger-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }
}

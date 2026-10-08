using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class CommunityGraphServiceTests
{
    [Test]
    public async Task Graph_UsesOnlyVerifiedLedgerAndRespectsDiscoveryPrivacy()
    {
        await using var db = CreateDatabase();
        var viewer = Member("viewer");
        var visible = Member("visible");
        var hidden = Member("hidden");
        hidden.IsDiscoverableToCommunity = false;
        var outsider = Member("outsider");
        outsider.CommunitySlug = "another-community";
        db.CommunityMembers.AddRange(viewer, visible, hidden, outsider);
        db.ParticipationLedgerEntries.AddRange(
            Entry(visible, ParticipationActivityKind.JoinedCommunity),
            Entry(hidden, ParticipationActivityKind.JoinedCommunity),
            Entry(outsider, ParticipationActivityKind.JoinedCommunity),
            Entry(viewer, ParticipationActivityKind.JoinedCommunity, verified: false));
        await db.SaveChangesAsync();

        var graph = await new CommunityGraphService(db).ReadAsync("viewer");

        await Assert.That(graph.Nodes.Any(n => n.Label == "visible")).IsTrue();
        await Assert.That(graph.Nodes.Any(n => n.Label == "hidden" || n.Label == "outsider")).IsFalse();
        await Assert.That(graph.Relationships.Count).IsEqualTo(1);
        await Assert.That(graph.Relationships[0].Evidence.Count).IsEqualTo(1);
        await Assert.That(graph.Relationships[0].Evidence[0].Summary).IsEqualTo("Confirmed participation");
    }

    [Test]
    public async Task Graph_PreservesStructuredTargetsAndExplainsSuggestionsWithoutScores()
    {
        await using var db = CreateDatabase();
        var viewer = Member("viewer");
        db.CommunityMembers.Add(viewer);
        db.ParticipationLedgerEntries.AddRange(
            Entry(viewer, ParticipationActivityKind.ContributedProject, "Project", "community-portal", "Community Portal"),
            Entry(viewer, ParticipationActivityKind.JoinedChapter, "Chapter", "bengaluru", "Bengaluru Chapter"),
            Entry(viewer, ParticipationActivityKind.Spoke, "Event", "session-one", "Cloud Native Day"),
            Entry(viewer, ParticipationActivityKind.Spoke, "Event", "session-two", "Design in the Open"));
        await db.SaveChangesAsync();

        var graph = await new CommunityGraphService(db).ReadAsync("viewer");

        await Assert.That(graph.Nodes.Any(n => n.Kind == "Chapter")).IsTrue();
        await Assert.That(graph.Nodes.Any(n => n.Kind == "Project")).IsTrue();
        await Assert.That(graph.Opportunities.Single().Title).IsEqualTo("Workshop Speaker");
        await Assert.That(graph.Opportunities.Single().Evidence.Count).IsEqualTo(2);
        await Assert.That(graph.Relationships.All(r => r.Evidence.Count > 0)).IsTrue();
    }

    [Test]
    public async Task Graph_DoesNotInventMentorshipFromSharedAttendanceOrIncludeFutureRecords()
    {
        await using var db = CreateDatabase();
        var viewer = Member("viewer");
        var peer = Member("peer");
        db.CommunityMembers.AddRange(viewer, peer);
        db.ParticipationLedgerEntries.AddRange(
            Entry(viewer, ParticipationActivityKind.Attended, "Event", "shared", "Shared meetup"),
            Entry(peer, ParticipationActivityKind.Attended, "Event", "shared", "Shared meetup"),
            new ParticipationLedgerEntry
            {
                CommunityMemberId = viewer.Id, ExternalMemberKey = "viewer", Evidence = "Future",
                ProvenanceKey = "future", Activity = ParticipationActivityKind.JoinedCommunity,
                IsVerified = true, OccurredAt = DateTimeOffset.UtcNow.AddDays(2)
            });
        await db.SaveChangesAsync();

        var graph = await new CommunityGraphService(db).ReadAsync("viewer");
        await Assert.That(graph.Relationships.Any(r => r.Kind == "Mentors")).IsFalse();
        await Assert.That(graph.Relationships.Single(r => r.Kind == "Shared attendance").Evidence.Count).IsEqualTo(2);
        await Assert.That(graph.Relationships.Any(r => r.Evidence.Any(e => e.Summary == "Future"))).IsFalse();
    }

    private static BethuyaDbContext CreateDatabase() => new(new DbContextOptionsBuilder<BethuyaDbContext>()
        .UseInMemoryDatabase($"graph-{Guid.NewGuid()}").Options);

    private static CommunityMember Member(string name) => new()
    {
        UserId = name, DisplayName = name, Email = $"{name}@example.com"
    };

    private static ParticipationLedgerEntry Entry(CommunityMember member, ParticipationActivityKind activity,
        string? kind = null, string? key = null, string? label = null, bool verified = true) => new()
    {
        CommunityMemberId = member.Id, Connector = ParticipationConnectorKind.Meetup,
        ExternalMemberKey = member.UserId, Activity = activity, Evidence = "Confirmed participation",
        ProvenanceKey = Guid.NewGuid().ToString(), OccurredAt = DateTimeOffset.UtcNow.AddDays(-1),
        IsVerified = verified, TargetKind = kind, TargetKey = key, TargetLabel = label
    };
}

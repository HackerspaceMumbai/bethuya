using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class CommunityGraphServiceTests
{
    [Test]
    public async Task Graph_RespectsGranularPrivacyWhenOtherDiscoveryRemainsEnabled()
    {
        await using var db = CreateDatabase();
        var viewer = Member("viewer");
        var speaker = Member("speaker");
        speaker.AppearInSpeakerRecommendations = false;
        var hidden = Member("no-insights");
        hidden.EnableRelationshipInsights = false;
        var peer = Member("no-collaboration");
        peer.AppearInCollaboratorDiscovery = false;
        db.CommunityMembers.AddRange(viewer, speaker, hidden, peer);
        db.ParticipationLedgerEntries.AddRange(
            Entry(speaker, ParticipationActivityKind.Spoke, "Event", "one", "One"),
            Entry(speaker, ParticipationActivityKind.Spoke, "Event", "two", "Two"),
            Entry(hidden, ParticipationActivityKind.JoinedCommunity),
            Entry(viewer, ParticipationActivityKind.Attended, "Event", "one", "One"),
            Entry(peer, ParticipationActivityKind.Attended, "Event", "one", "One"));
        await db.SaveChangesAsync();
        var graph = await new CommunityGraphService(db).ReadAsync(viewer.UserId);
        await Assert.That(graph.Nodes.Any(n => n.Label == "speaker")).IsTrue();
        await Assert.That(graph.Nodes.Any(n => n.Label == "no-insights")).IsFalse();
        await Assert.That(graph.Opportunities.Count).IsEqualTo(0);
        await Assert.That(graph.Relationships.Any(r => r.Kind == "Shared attendance")).IsFalse();
    }

    [Test]
    public async Task Graph_DiscoveryDoesNotRequireOrganizerSharing()
    {
        await using var db = CreateDatabase();
        var viewer = Member("viewer");
        var peer = Member("peer");
        peer.ShareParticipationWithOrganizers = false;
        db.CommunityMembers.AddRange(viewer, peer);
        db.ParticipationLedgerEntries.Add(Entry(peer, ParticipationActivityKind.JoinedCommunity));
        await db.SaveChangesAsync();
        var graph = await new CommunityGraphService(db).ReadAsync(viewer.UserId);
        await Assert.That(graph.Nodes.Any(n => n.Label == "peer")).IsTrue();
    }

    [Test]
    public async Task Graph_DenseAttendanceSerializesEvidenceOnceWithinPayloadBudget()
    {
        await using var db = CreateDatabase();
        var members = Enumerable.Range(0, 80).Select(i => Member($"member-{i:D2}")).ToArray();
        db.CommunityMembers.AddRange(members);
        var first = members.OrderBy(m => m.Id.Value.ToString(), StringComparer.Ordinal).First();
        foreach (var member in members)
        {
            for (var index = 0; index < (member == first ? 321 : 1); index++)
            {
                var entry = Entry(member, ParticipationActivityKind.Attended, "Event", "shared", "Shared event", evidence: new string('x', 600));

                db.ParticipationLedgerEntries.Add(entry);
            }
        }
        await db.SaveChangesAsync();
        var graph = await new CommunityGraphService(db).ReadAsync(first.UserId);
        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(graph);
        await Assert.That(payload.Length < 2_000_000).IsTrue();
        await Assert.That(graph.Evidence.Count).IsEqualTo(400);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<CommunityGraphSnapshot>(payload)!;
        await Assert.That(roundTrip.Relationships.Count(r => r.Kind == "Shared attendance")).IsEqualTo(200);
    }

    [Test]
    public async Task Graph_UsesOnlyVerifiedLedgerAndRespectsDiscoveryPrivacy()
    {
        await using var db = CreateDatabase();
        var viewer = Member("viewer");
        var visible = Member("visible");
        var hidden = Member("hidden");
        hidden.IsDiscoverableToCommunity = false;
        var privateMember = Member("private");
        privateMember.Visibility = ProfileVisibilityScope.Private;
        var outsider = Member("outsider");
        outsider.CommunitySlug = "another-community";
        db.CommunityMembers.AddRange(viewer, visible, hidden, outsider, privateMember);
        db.ParticipationLedgerEntries.AddRange(
            Entry(visible, ParticipationActivityKind.JoinedCommunity),
            Entry(hidden, ParticipationActivityKind.JoinedCommunity),
            Entry(privateMember, ParticipationActivityKind.JoinedCommunity),
            Entry(outsider, ParticipationActivityKind.JoinedCommunity),
            Entry(viewer, ParticipationActivityKind.JoinedCommunity, verified: false));
        await db.SaveChangesAsync();

        var graph = await new CommunityGraphService(db).ReadAsync("viewer");

        await Assert.That(graph.Nodes.Any(n => n.Label == "visible")).IsTrue();
        await Assert.That(graph.Nodes.Any(n => n.Label == "hidden" || n.Label == "outsider" || n.Label == "private")).IsFalse();
        await Assert.That(graph.Relationships.Count).IsEqualTo(1);
        await Assert.That(graph.Relationships[0].EvidenceIds.Count).IsEqualTo(1);
        await Assert.That(graph.Evidence.Single(p => p.EntryId == graph.Relationships[0].EvidenceIds[0]).Summary).IsEqualTo("Confirmed participation");
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
        await Assert.That(graph.Opportunities.Single().EvidenceIds.Count).IsEqualTo(2);
        await Assert.That(graph.Relationships.All(r => r.EvidenceIds.Count > 0)).IsTrue();
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
        await Assert.That(graph.Relationships.Single(r => r.Kind == "Shared attendance").EvidenceIds.Count).IsEqualTo(2);
        await Assert.That(graph.Evidence.Any(e => e.Summary == "Future")).IsFalse();
    }

    private static BethuyaDbContext CreateDatabase() => new(new DbContextOptionsBuilder<BethuyaDbContext>()
        .UseInMemoryDatabase($"graph-{Guid.NewGuid()}").Options);

    private static CommunityMember Member(string name) => new()
    {
        UserId = name, DisplayName = name, Email = $"{name}@example.com"
    };

    private static ParticipationLedgerEntry Entry(CommunityMember member, ParticipationActivityKind activity,
        string? kind = null, string? key = null, string? label = null, bool verified = true, string evidence = "Confirmed participation") => new()
    {
        CommunityMemberId = member.Id, Connector = ParticipationConnectorKind.Meetup,
        ExternalMemberKey = member.UserId, Activity = activity, Evidence = evidence,
        ProvenanceKey = Guid.NewGuid().ToString(), OccurredAt = DateTimeOffset.UtcNow.AddDays(-1),
        IsVerified = verified, TargetKind = kind, TargetKey = key, TargetLabel = label
    };
}

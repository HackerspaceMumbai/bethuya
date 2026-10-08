using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.UI;

public sealed class CommunityGraphRenderTests
{
    [Test]
    public async Task Graph_FocusIsSeparateFromSelectionAndBackRestoresOverview()
    {
        using var context = CreateContext();
        var fixture = Fixture();
        var project = new CommunityGraphNode(GraphNodeId.From("project:test"), "Project", "Portal", "");
        fixture = fixture with { Nodes = [.. fixture.Nodes, project], Relationships = [.. fixture.Relationships,
            new(fixture.Nodes[0].Id, project.Id, "Contributes to", fixture.Relationships[0].Evidence)] };
        var api = Substitute.For<ICommunityPassportApi>();
        api.GetGraphAsync(Arg.Any<CancellationToken>()).Returns(fixture);
        context.Services.AddSingleton(api);
        var cut = context.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.CommunityGraph>();
        cut.Find("[data-test='graph-node'][aria-label^='Portal']").Click();
        await Assert.That(cut.FindAll("[data-test='graph-node']").Count).IsEqualTo(3);
        cut.Find("[data-test='graph-explore'] button").Click();
        await Assert.That(cut.FindAll("[data-test='graph-node']").Count).IsEqualTo(2);
        await Assert.That(cut.Find("[data-test='graph-node'][aria-label^='Portal']").GetAttribute("style")).IsEqualTo("left:50%;top:50%");
        cut.Find("[data-test='graph-node'][aria-label^='Priya']").DoubleClick();
        await Assert.That(cut.FindAll("[data-test='graph-node']").Count).IsEqualTo(3);
        cut.Find("[data-test='graph-back'] button").Click();
        await Assert.That(cut.Find("[data-test='graph-focus']").TextContent).Contains("Portal");
        cut.Find("[data-test='graph-back'] button").Click();
        await Assert.That(cut.FindAll("[data-test='graph-node']").Count).IsEqualTo(3);
    }

    [Test]
    public async Task Graph_SelectingRelationshipShowsLedgerProofAndFilteringClearsHiddenSelection()
    {
        using var context = CreateContext();
        var api = Substitute.For<ICommunityPassportApi>();
        api.GetGraphAsync(Arg.Any<CancellationToken>()).Returns(Fixture());
        context.Services.AddSingleton(api);
        var cut = context.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.CommunityGraph>();
        cut.Find("[data-test='graph-edge']").Click();
        await Assert.That(cut.Find("[data-test='passport-preview']").TextContent).Contains("Checked in at Cloud Native Day");
        // Blueprint's immediate input is JS-driven; exercise its public callback here and real typing in Playwright.
        await cut.InvokeAsync(() => cut.FindComponent<BbInput>().Instance.ValueChanged.InvokeAsync("no matching entity"));
        await Assert.That(cut.FindAll("[data-test='graph-node']").Count).IsEqualTo(0);
        await Assert.That(cut.Find("[data-test='passport-preview']").TextContent).Contains("Select a node or relationship");
    }

    [Test]
    public async Task Graph_RendersEmptyAndRecoverableErrorStates()
    {
        using var context = CreateContext();
        var api = Substitute.For<ICommunityPassportApi>();
        api.GetGraphAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<CommunityGraphSnapshot>(new HttpRequestException()));
        context.Services.AddSingleton(api);
        var cut = context.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.CommunityGraph>();
        cut.Find("[data-test='graph-error']");
        api.GetGraphAsync(Arg.Any<CancellationToken>()).Returns(new CommunityGraphSnapshot([], [], [], [], DateTimeOffset.UtcNow, false));
        cut.Find("[data-test='graph-retry'] button").Click();
        await Assert.That(cut.Find("[data-test='graph-empty']").TextContent).Contains("No verified participation");
    }

    [Test]
    public async Task Graph_NodeAndKeyboardSelectionUpdatePassport()
    {
        using var context = CreateContext();
        var api = Substitute.For<ICommunityPassportApi>();
        api.GetGraphAsync(Arg.Any<CancellationToken>()).Returns(Fixture());
        context.Services.AddSingleton(api);
        var cut = context.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.CommunityGraph>();
        cut.FindAll("[data-test='graph-node']").Single(n => n.TextContent.Contains("Cloud Native Day", StringComparison.Ordinal)).Click();
        await Assert.That(cut.Find("[data-test='passport-preview']").TextContent).Contains("Cloud Native Day");
        cut.Find("[data-test='graph-edge']").KeyDown("Enter");
        await Assert.That(cut.Find("[data-test='passport-preview']").TextContent).Contains("Why this relationship exists");
    }

    private static Bunit.TestContext CreateContext()
    {
        var context = new Bunit.TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddBlazorBlueprintComponents();
        context.AddTestAuthorization().SetAuthorized("viewer");
        return context;
    }

    [Test]
    public async Task Graph_MemberPassportDoesNotAttributePeerEvidenceToSelectedMember()
    {
        using var context = CreateContext();
        var fixture = Fixture();
        var owner = fixture.Nodes[0];
        var peer = new CommunityGraphNode(GraphNodeId.From("member:peer"), "Member", "Peer Member", "");
        var peerProof = new CommunityGraphEvidence(ParticipationLedgerEntryId.From(Guid.NewGuid()), "Peer-only attendance evidence",
            "Attended", "Meetup", DateTimeOffset.UtcNow, peer.Id);
        fixture = fixture with
        {
            Nodes = [.. fixture.Nodes, peer],
            Relationships = [.. fixture.Relationships,
                new(owner.Id, peer.Id, "Shared attendance", [fixture.Relationships[0].Evidence[0], peerProof])]
        };
        var api = Substitute.For<ICommunityPassportApi>();
        api.GetGraphAsync(Arg.Any<CancellationToken>()).Returns(fixture);
        context.Services.AddSingleton(api);
        var cut = context.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.CommunityGraph>();
        cut.Find("[data-test='passport-tab-Participation-Ledger'] button").Click();
        await Assert.That(cut.Find("[data-test='passport-preview']").TextContent.Contains("Peer-only", StringComparison.Ordinal)).IsFalse();
        cut.Find("[data-test='graph-edge'][aria-label*='Shared attendance']").Click();
        await Assert.That(cut.Find("[data-test='relationship-proof']").TextContent).Contains("Peer-only attendance evidence");
    }

    private static CommunityGraphSnapshot Fixture()
    {
        var member = GraphNodeId.From("member:priya");
        var eventId = GraphNodeId.From("event:cloud");
        return new([
            new(member, "Member", "Priya Menon", "Platform Engineer"),
            new(eventId, "Event", "Cloud Native Day", "Community workshop")
        ], [new(member, eventId, "Attended", [new(ParticipationLedgerEntryId.From(Guid.NewGuid()),
            "Checked in at Cloud Native Day", "Attended", "Meetup", DateTimeOffset.UtcNow, member)])], [], [], DateTimeOffset.UtcNow, false);
    }
}

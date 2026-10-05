using Bethuya.Hybrid.Shared.Models.OpportunityEngine;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

public sealed class OpportunityEngineTests
{
    [Test]
    public async Task DeterministicProvider_ReturnsOperationalWorkspaceSeed()
    {
        var service = new DeterministicOpportunityEngineService();

        var workspace = await service.GetWorkspaceAsync();

        await Assert.That(workspace.Kpis).Count().IsEqualTo(5);
        await Assert.That(workspace.Kpis.Any(k => k.Label == "Awaiting Review" && k.IsPriority)).IsTrue();
        await Assert.That(workspace.Events[0].Title).IsEqualTo("Cloud Native Day 2026");
        await Assert.That(workspace.Events[0].Needs).Count().IsEqualTo(4);
        await Assert.That(workspace.Opportunities.Any(o => o.MemberName == "Maya Fernandes")).IsTrue();
        await Assert.That(workspace.Risks.Any(r => r.Title == "Organizer Burnout Risk")).IsTrue();
        await Assert.That(workspace.Champions.CandidateCount).IsEqualTo(13);
        await Assert.That(workspace.Pathways).Count().IsEqualTo(3);
        await Assert.That(workspace.Opportunities[0].EvidenceSources).Contains("Participation Ledger");
        await Assert.That(workspace.Opportunities[0].EvidenceSources).Contains("Community Passport");
        await Assert.That(workspace.Opportunities[0].EvidenceSources).Contains("Community Graph");
    }

    [Test]
    public async Task OpportunityEnginePage_RendersGovernanceAndCommunityNeedsByDefault()
    {
        using var ctx = CreateContext();
        var cut = ctx.RenderComponent<OpportunityEngine>();

        await Assert.That(cut.Find("[data-test='opportunity-engine']")).IsNotNull();
        await Assert.That(cut.Markup).Contains("Opportunity Engine");
        await Assert.That(cut.Markup).Contains("Turn verified participation into meaningful opportunities.");
        await Assert.That(cut.Find("[data-test='badge-human-in-the-loop']").TextContent).Contains("Human-in-the-Loop");
        await Assert.That(cut.Find("[data-test='badge-zero-synthetic-weights']").TextContent)
            .Contains("Zero Synthetic Weights");
        await Assert.That(cut.Find("[data-test='workspace-tab-community-needs']").GetAttribute("aria-current"))
            .IsEqualTo("page");
        await Assert.That(cut.Find("[data-test='community-needs-section']")).IsNotNull();
        await Assert.That(cut.FindAll("[data-test='staffing-need-card']").Count).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task OpportunityEnginePage_CardsDoNotContainApproveActions()
    {
        using var ctx = CreateContext();
        var cut = ctx.RenderComponent<OpportunityEngine>();

        var cards = cut.FindAll("[data-test='member-opportunity-card']");
        await Assert.That(cards.Count).IsGreaterThan(0);
        foreach (var card in cards)
        {
            await Assert.That(card.TextContent).DoesNotContain("Approve & Invite");
        }

        await Assert.That(cut.Find("[data-test='action-approve']")).IsNotNull();
        await Assert.That(cut.Find("[data-test='selected-opportunity-panel']").TextContent)
            .Contains("Approve & Invite");
    }

    [Test]
    public async Task OpportunityEnginePage_ApproveInviteUpdatesSelectedWorkflow()
    {
        using var ctx = CreateContext();
        var cut = ctx.RenderComponent<OpportunityEngine>();

        await Assert.That(cut.Find("[data-test='selected-opportunity-status']").TextContent)
            .Contains("Needs Review");

        cut.Find("[data-test='action-approve'] button").Click();

        await Assert.That(cut.Markup).Contains("Approved and invite prepared");
        await Assert.That(cut.Find("[data-test='selected-opportunity-status']").TextContent)
            .Contains("Offered");
    }

    [Test]
    public async Task OpportunityEnginePage_ViewMatchesSelectsLinkedOpportunity()
    {
        using var ctx = CreateContext();
        var cut = ctx.RenderComponent<OpportunityEngine>();

        cut.Find("[data-test='view-matches-action'] button").Click();

        await Assert.That(cut.Find("[data-test='selected-opportunity-title']").TextContent)
            .Contains("Workshop Speaker");
        await Assert.That(cut.Find("[data-test='community-need-context']").TextContent)
            .Contains("Cloud Native Day 2026");
        await Assert.That(cut.Find("[data-test='pathway-journey']").TextContent)
            .Contains("Recommended Speaker");
    }

    private static BunitCtx CreateContext()
    {
        var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton<IOpportunityEngineService, DeterministicOpportunityEngineService>();
        return ctx;
    }
}

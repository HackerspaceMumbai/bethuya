using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Models.OpportunityEngine;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
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
    public async Task DeterministicProvider_SummitNeeds_AreEventSpecificAndComplete()
    {
        var service = new DeterministicOpportunityEngineService();

        var workspace = await service.GetWorkspaceAsync();
        var summit = workspace.Events.Single(e => e.EventId == "evt-aioss-2026");

        await Assert.That(summit.OpenRoleCount).IsEqualTo(3);
        await Assert.That(summit.Needs).Count().IsEqualTo(3);
        await Assert.That(summit.NeedsSummary).Count().IsEqualTo(3);

        foreach (var need in summit.Needs)
        {
            var linked = workspace.Opportunities.Single(o => o.OpportunityId == need.LinkedOpportunityId);
            await Assert.That(linked.NeedContext.EventTitle).IsEqualTo("AI Open Source Summit");
            await Assert.That(linked.NeedContext.Role).IsEqualTo(need.Role);
            await Assert.That(linked.OpportunityTitle).IsEqualTo(need.Role);
        }
    }

    [Test]
    public async Task OpportunityEnginePage_RendersGovernanceAndCommunityNeedsByDefault()
    {
        using var ctx = CreateContext();
        var cut = ctx.RenderComponent<OpportunityEngine>();

        await Assert.That(cut.Find("[data-test='opportunity-engine']")).IsNotNull();
        await Assert.That(cut.Find("[data-test='opportunity-engine-back-link']").GetAttribute("href")).IsEqualTo("/");
        await Assert.That(cut.Find("[data-test='view-graph-link']")).IsNotNull();
        await Assert.That(cut.Find("[data-test='view-connections-link']")).IsNotNull();
        await Assert.That(cut.Markup).Contains("Opportunity Engine");
        await Assert.That(cut.Markup).Contains("Turn verified participation into meaningful opportunities.");
        await Assert.That(cut.Find("[data-test='badge-human-in-the-loop']").TextContent).Contains("Human-in-the-Loop");
        await Assert.That(cut.Find("[data-test='badge-zero-synthetic-weights']").TextContent)
            .Contains("Zero Synthetic Weights");
        await Assert.That(cut.Find("[data-test='badge-demo-data']").TextContent).Contains("Demo Data");
        await Assert.That(cut.Find("[data-test='demo-data-banner']").TextContent)
            .Contains("Demonstration data only");
        await Assert.That(cut.Find("[data-test='workspace-tab-community-needs']").GetAttribute("aria-pressed"))
            .IsEqualTo("true");
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

        await Assert.That(cut.Markup).Contains("Approved locally for this demo session");
        await Assert.That(cut.Find("[data-test='selected-opportunity-status']").TextContent)
            .Contains("Approved");
        await Assert.That(cut.Find("[data-test='selected-opportunity-status']").TextContent)
            .DoesNotContain("Offered");
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

        var scrollCalls = ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "bethuyaOpportunityEngine.scrollToSection")
            .ToList();
        await Assert.That(scrollCalls.Count).IsGreaterThan(0);
        await Assert.That(scrollCalls[^1].Arguments[0] as string).IsEqualTo("section-selected-opportunity");
    }

    [Test]
    public async Task OpportunityEnginePage_ChampionInspect_SelectsMatchingMemberOpportunity()
    {
        using var ctx = CreateContext();
        var cut = ctx.RenderComponent<OpportunityEngine>();

        cut.Find("[data-test='champion-inspect'] button").Click();

        await Assert.That(cut.Find("[data-test='selected-opportunity-panel']").TextContent)
            .Contains("Priya Menon");
        await Assert.That(cut.Find("[data-test='opportunity-action-message']").TextContent)
            .Contains("Champion review");
        await Assert.That(cut.Find("[data-test='selected-opportunity-panel']").TextContent)
            .DoesNotContain("Maya Fernandes");
    }

    [Test]
    public async Task OpportunityEnginePage_WorkspaceTab_InvokesScrollHelper()
    {
        using var ctx = CreateContext();
        var cut = ctx.RenderComponent<OpportunityEngine>();

        cut.Find("[data-test='workspace-tab-risks-interventions']").Click();

        var scrollCalls = ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "bethuyaOpportunityEngine.scrollToSection")
            .ToList();
        await Assert.That(scrollCalls.Count).IsGreaterThan(0);
        await Assert.That(scrollCalls[^1].Arguments[0] as string).IsEqualTo("section-risks-interventions");
        await Assert.That(cut.Find("[data-test='workspace-tab-risks-interventions']").GetAttribute("aria-pressed"))
            .IsEqualTo("true");
    }

    private static BunitCtx CreateContext()
    {
        var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton<IOpportunityEngineService, DeterministicOpportunityEngineService>();
        ctx.AddTestAuthorization().SetAuthorized("Organizer").SetRoles(BethuyaRoles.Organizer, BethuyaRoles.Admin);
        return ctx;
    }
}

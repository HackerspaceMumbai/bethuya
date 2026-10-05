using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class OpportunityEngineFlowTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task OpportunityEngine_ShouldRenderOperationalWorkspaceAndApproveInDetailPanel()
    {
        await GotoWithBudgetAsync("/opportunities");
        await Page.AddStyleTagAsync(new()
        {
            Content = "[data-test='dev-persona-toolbar']{pointer-events:none!important;opacity:0!important;}"
        });

        await Assertions.Expect(Page.Locator("[data-test='opportunity-engine']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='governance-banner']"))
            .ToContainTextAsync("Participation Ledger");
        await Assertions.Expect(Page.Locator("[data-test='badge-human-in-the-loop']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='badge-zero-synthetic-weights']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='community-needs-section']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='member-opportunities-section']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='risks-interventions-section']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='champion-pipeline-section']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='progression-pathways-section']")).ToBeVisibleAsync();

        var opportunityCard = Page.Locator("[data-test='member-opportunity-card']").First;
        await Assertions.Expect(opportunityCard).Not.ToContainTextAsync("Approve & Invite");
        await Assertions.Expect(Page.Locator("[data-test='selected-opportunity-panel']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='action-approve']")).ToContainTextAsync("Approve & Invite");
        await Assertions.Expect(Page.Locator("[data-test='selected-opportunity-status']"))
            .ToContainTextAsync("Needs Review");

        await Page.Locator("[data-test='action-approve'] button")
            .ClickAsync(new LocatorClickOptions { Force = true });
        await Assertions.Expect(Page.Locator("[data-test='opportunity-action-message']"))
            .ToContainTextAsync("Approved and invite prepared");
        await Assertions.Expect(Page.Locator("[data-test='selected-opportunity-status']"))
            .ToContainTextAsync("Offered");

        Directory.CreateDirectory("artifacts");
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Join("artifacts", "opportunity-engine-workspace.png"),
            FullPage = true
        });
    }
}

using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class CommandCenterFlowTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task Home_ShouldAdaptRoleModeAndResolveWorkspaceNavigation()
    {
        await GotoWithBudgetAsync("/");
        await CollapseDevPersonaToolbarAsync();

        var commandCenter = Page.Locator("[data-test='community-command-center']");
        await Assertions.Expect(commandCenter).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='mode-explanation']")).ToContainTextAsync("Hacktoberfest Mumbai");

        await Page.Locator("[data-test='role-volunteer-lead'] button").ClickAsync();
        await Page.Locator("[data-test='mode-strategic'] button").ClickAsync();
        await Assertions.Expect(Page.Locator("[data-test='mode-explanation']")).ToContainTextAsync("Strategic Mode");
        await Assertions.Expect(Page.Locator("[data-test='attention-queue'] [data-test='attention-item']").First)
            .ToContainTextAsync("Volunteer network");
        await Assertions.Expect(Page.Locator("[data-test='attention-queue']")).ToContainTextAsync("Hacktoberfest Mumbai");

        Directory.CreateDirectory("artifacts");
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Join("artifacts", "homepage-command-center-desktop.png"),
            FullPage = true
        });

        var communityGraphLink = Page.Locator("[data-test='quick-navigation'] a").First;
        await Assertions.Expect(communityGraphLink).ToHaveAttributeAsync("href", "/community-graph");
        await GotoWithBudgetAsync("/community-graph");
        await Assertions.Expect(Page.Locator("[data-test='workspace-preview']")).ToContainTextAsync("Community Graph");
    }

    [TestMethod]
    public async Task Home_ShouldRemainLegibleAtMobileWidth()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GotoWithBudgetAsync("/");
        await CollapseDevPersonaToolbarAsync();

        await Assertions.Expect(Page.Locator("[data-test='community-command-center']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='community-snapshot']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='human-review-queue']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='quick-navigation']")).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(700);

        Directory.CreateDirectory("artifacts");
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Join("artifacts", "homepage-command-center-mobile.png"),
            FullPage = true
        });
    }

    private async Task CollapseDevPersonaToolbarAsync()
    {
        var toggle = Page.Locator("[data-test='dev-persona-toggle'] button");
        if (await toggle.IsVisibleAsync() &&
            (await toggle.InnerTextAsync()).Contains("Hide dev personas", StringComparison.Ordinal))
        {
            await toggle.ClickAsync();
        }
    }
}

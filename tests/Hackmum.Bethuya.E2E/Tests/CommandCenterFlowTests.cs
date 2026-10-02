using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class CommandCenterFlowTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task Home_ShouldAdaptRoleModeAndResolveWorkspaceNavigation()
    {
        await GotoWithBudgetAsync("/");
        await SelectPersonaAsync("rohan", "Event organizer");
        await CollapseDevPersonaToolbarAsync();
        await SelectEventModeAsync();

        var commandCenter = Page.Locator("[data-test='community-command-center']");
        await Assertions.Expect(commandCenter).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='mode-explanation']")).ToContainTextAsync("Hacktoberfest Mumbai");
        await Assertions.Expect(Page.Locator("[data-test='mode-layout-event']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='community-snapshot']")).ToContainTextAsync("Event operations snapshot");
        await Assertions.Expect(Page.Locator("[data-test='event-primary-work']")).ToContainTextAsync("Event attention queue");
        await Assertions.Expect(Page.Locator("[data-test='pending-event-approvals']")).ToContainTextAsync("Waitlist decisions");
        await Assertions.Expect(Page.Locator("[data-test='pending-approvals']")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator("[data-test='event-readiness-card']").First).ToContainTextAsync("82% ready");
        await Assertions.Expect(Page.Locator("[data-test='event-readiness-card']").First).ToContainTextAsync("12 / 14");
        await Page.WaitForTimeoutAsync(700);

        Directory.CreateDirectory("artifacts");
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Join("artifacts", "homepage-command-center-event-mode.png"),
            FullPage = true
        });

        await GotoWithBudgetAsync("/");
        await SelectPersonaAsync("priya", "Volunteer lead");
        await CollapseDevPersonaToolbarAsync();
        await Page.Locator("[data-test='mode-strategic'] button").ClickAsync();
        await Assertions.Expect(Page.Locator("[data-test='mode-explanation']")).ToContainTextAsync("Strategic Mode");
        await Assertions.Expect(Page.Locator("[data-test='mode-layout-strategic']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='community-snapshot']")).ToContainTextAsync("Community snapshot");
        await Assertions.Expect(Page.Locator("[data-test='strategic-primary-work']")).ToContainTextAsync("Community insight of the week");
        await Assertions.Expect(Page.Locator("[data-test='pending-approvals']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='attention-queue'] [data-test='attention-item']").First)
            .ToContainTextAsync("Volunteer network");
        await Assertions.Expect(Page.Locator("[data-test='attention-queue']")).ToContainTextAsync("Hacktoberfest Mumbai");
        await Page.WaitForTimeoutAsync(700);

        await Page.ScreenshotAsync(new()
        {
            Path = Path.Join("artifacts", "homepage-command-center-strategic-mode.png"),
            FullPage = true
        });

        var communityGraphLink = Page.Locator("[data-test='quick-navigation'] a").First;
        await Assertions.Expect(communityGraphLink).ToHaveAttributeAsync("href", "/community-graph");
        await GotoWithBudgetAsync("/community-graph");
        await Assertions.Expect(Page.Locator("[data-test='workspace-preview']")).ToContainTextAsync("Community Graph");
    }

    [TestMethod]
    public async Task Home_ShouldResolveAllSixPersonaExperiencesFromAuthenticatedClaims()
    {
        (string Key, string Name, string Experience)[] personas =
        [
            ("anish", "Anish", "Member journey"),
            ("priya", "Priya", "Volunteer lead"),
            ("rohan", "Rohan", "Event organizer"),
            ("maya", "Maya", "Mentorship lead"),
            ("farah", "Farah", "Emerging contributor"),
            ("vikram", "Vikram", "Community administrator")
        ];

        await GotoWithBudgetAsync("/");
        foreach (var persona in personas)
        {
            await SelectPersonaAsync(persona.Key, persona.Experience);
            await Assertions.Expect(Page.Locator("[data-test='active-persona']")).ToContainTextAsync(persona.Name);
            await Assertions.Expect(Page.Locator("[data-test='command-center-audience']")).ToContainTextAsync(persona.Experience);
            await Assertions.Expect(Page.Locator("[data-test^='role-']")).ToHaveCountAsync(0);
        }

        await SelectPersonaAsync("farah", "Emerging contributor");
        await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load });
        await Assertions.Expect(Page.Locator("[data-test='command-center-audience']")).ToContainTextAsync("Emerging contributor");
    }

    [TestMethod]
    public async Task Home_ShouldRemainLegibleAtMobileWidth()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GotoWithBudgetAsync("/");
        await CollapseDevPersonaToolbarAsync();
        await SelectEventModeAsync();

        await Assertions.Expect(Page.Locator("[data-test='community-command-center']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='community-snapshot']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='pending-event-approvals']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='quick-navigation']")).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(700);

        Directory.CreateDirectory("artifacts");
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Join("artifacts", "homepage-command-center-mobile.png"),
            FullPage = true
        });
    }

    private async Task SelectPersonaAsync(string key, string expectedExperience)
    {
        var personaButton = Page.Locator($"[data-test='persona-{key}']");
        if (!await personaButton.IsVisibleAsync())
        {
            Assert.Inconclusive("Dev persona toolbar is unavailable; run with Development and Authentication:Provider=None.");
        }

        await ClickAndNavigateWithBudgetAsync(personaButton);
        await Assertions.Expect(Page.Locator("[data-test='command-center-audience']"))
            .ToContainTextAsync(expectedExperience);
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

    private async Task SelectEventModeAsync()
    {
        await Page.Locator("[data-test='mode-event'] button").ClickAsync();
        await Assertions.Expect(Page.Locator("[data-test='mode-layout-event']")).ToBeVisibleAsync();
    }
}

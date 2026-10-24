using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class CommandCenterFlowTests : BethuyaE2ETest
{
    private static readonly string[] OperationsModules =
    [
        "strategic-insight",
        "immediate-decisions",
        "programs-guilds",
        "active-event-workspaces",
        "leadership-signals",
        "member-growth-journeys",
        "chapter-recommendations",
        "chapter-health"
    ];

    [TestMethod]
    public async Task Home_ShouldRenderV9SectionsAndGovernance()
    {
        await GotoWithBudgetAsync("/");
        await SelectPersonaAsync("rohan", "Event organizer");
        await CollapseDevPersonaToolbarAsync();

        await Assertions.Expect(Page.Locator("[data-test='community-command-center']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='strategic-insight']"))
            .ToContainTextAsync("waitlist", new() { IgnoreCase = true });
        await Assertions.Expect(Page.Locator("[data-test='governance-no-batch']"))
            .ToContainTextAsync("human-in-the-loop");
        await Assertions.Expect(Page.Locator("[data-test='immediate-decisions']"))
            .ToContainTextAsync("Immediate Decisions");
        await Assertions.Expect(Page.Locator("[data-test='decision-card']").First).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='active-event-workspaces']"))
            .ToContainTextAsync("Hacktoberfest Mumbai");
        await Assertions.Expect(Page.Locator("[data-test='leadership-signals']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='signal-evidence']").First).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Approve All")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator("[data-test='community-snapshot']")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator("[data-test='mode-explanation']")).ToHaveCountAsync(0);
        await Page.WaitForTimeoutAsync(700);

        Directory.CreateDirectory("artifacts");
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Join("artifacts", "homepage-command-center-v9.png"),
            FullPage = true
        });

        await GotoWithBudgetAsync("/");
        await SelectPersonaAsync("priya", "Volunteer lead");
        await CollapseDevPersonaToolbarAsync();
        await Assertions.Expect(Page.Locator("[data-test='immediate-decisions']"))
            .ToContainTextAsync("Volunteer");
        await Assertions.Expect(Page.Locator("[data-test='member-growth-journeys']")).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(700);

        await Page.ScreenshotAsync(new()
        {
            Path = Path.Join("artifacts", "homepage-command-center-v9-volunteer.png"),
            FullPage = true
        });

        var graphLink = Page.Locator("[data-test='nav-community-graph-link']");
        await Assertions.Expect(graphLink).ToBeVisibleAsync();
        await GotoWithBudgetAsync("/community-graph");
        await Assertions.Expect(Page.Locator("[data-test='community-graph-page']")).ToContainTextAsync("Community Graph");
    }

    [TestMethod]
    public async Task Home_ShouldGiveEveryOperatingPersonaTheSameCommandCenter()
    {
        (string Key, string Name, string Experience)[] operators =
        [
            ("priya", "Priya", "Volunteer lead"),
            ("rohan", "Rohan", "Event organizer"),
            ("maya", "Maya", "Mentorship lead"),
            ("vikram", "Vikram", "Community administrator")
        ];

        await GotoWithBudgetAsync("/");
        foreach (var persona in operators)
        {
            await SelectPersonaAsync(persona.Key, persona.Experience);
            await Assertions.Expect(Page.Locator("[data-test='active-persona']")).ToContainTextAsync(persona.Name);
            await Assertions.Expect(Page.Locator("[data-test='community-command-center']")).ToBeVisibleAsync();
            await Assertions.Expect(Page.Locator("[data-test='community-participation']")).ToHaveCountAsync(0);
            await Assertions.Expect(Page.Locator("[data-test^='role-']")).ToHaveCountAsync(0);

            foreach (var module in OperationsModules)
            {
                await Assertions.Expect(Page.Locator($"[data-test='{module}']")).ToHaveCountAsync(1);
            }

            await Assertions.Expect(Page.Locator("[data-test='governance-no-batch']")).ToHaveCountAsync(1);
        }
    }

    [TestMethod]
    public async Task Home_ShouldGiveParticipationPersonasAJourneySurfaceWithoutOperations()
    {
        (string Key, string Name, string Experience, string Mode)[] participants =
        [
            ("anish", "Anish", "Event participant", "onboarding"),
            ("farah", "Farah", "Emerging contributor", "journey")
        ];

        await GotoWithBudgetAsync("/");
        foreach (var persona in participants)
        {
            await SelectPersonaAsync(persona.Key, persona.Experience);
            await Assertions.Expect(Page.Locator("[data-test='active-persona']")).ToContainTextAsync(persona.Name);
            await Assertions.Expect(Page.Locator("[data-test='community-participation']"))
                .ToHaveAttributeAsync("data-participation-mode", persona.Mode);
            await Assertions.Expect(Page.Locator("[data-test='community-passport']")).ToBeVisibleAsync();
            await Assertions.Expect(Page.Locator("[data-test='recommended-opportunities']")).ToBeVisibleAsync();
            await Assertions.Expect(Page.Locator("[data-test='community-command-center']")).ToHaveCountAsync(0);

            foreach (var module in OperationsModules)
            {
                await Assertions.Expect(Page.Locator($"[data-test='{module}']")).ToHaveCountAsync(0);
            }
        }

        await Assertions.Expect(Page.Locator("[data-test='earned-volunteer']")).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(500);

        Directory.CreateDirectory("artifacts");
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Join("artifacts", "homepage-participation-surface.png"),
            FullPage = true
        });

        await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load });
        await Assertions.Expect(Page.Locator("[data-test='participation-audience']")).ToContainTextAsync("Emerging contributor");
    }

    [TestMethod]
    public async Task Home_ShouldRemainLegibleAtMobileWidth()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GotoWithBudgetAsync("/");
        await CollapseDevPersonaToolbarAsync();

        await Assertions.Expect(Page.Locator("[data-test='community-command-center']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='strategic-insight']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='immediate-decisions']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='member-growth-journeys']")).ToBeVisibleAsync();
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

        var audience = Page.Locator("[data-test='command-center-audience'], [data-test='participation-audience']").First;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await ClickAndNavigateWithBudgetAsync(personaButton);

            try
            {
                await Assertions
                    .Expect(audience)
                    .ToContainTextAsync(expectedExperience, new() { Timeout = 5000 });
                return;
            }
            catch (PlaywrightException) when (attempt < 2)
            {
                await Page.ReloadAsync();
                await Assertions.Expect(audience).ToBeVisibleAsync();
            }
        }

        await Assertions.Expect(audience).ToContainTextAsync(expectedExperience);
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

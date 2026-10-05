using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class CommandCenterFlowTests : BethuyaE2ETest
{
    private static readonly string[] OperationsModules =
    [
        "community-snapshot",
        "weekly-insight",
        "people-to-watch",
        "attention-queue",
        "upcoming-touchpoints"
    ];

    private const string ReviewQueueSelector =
        "[data-test='human-review-queue'], [data-test='pending-event-approvals']";

    [TestMethod]
    public async Task Home_ShouldAdaptModeAndResolveWorkspaceNavigation()
    {
        await GotoWithBudgetAsync("/");
        await SelectPersonaAsync("rohan", "Event organizer");
        await CollapseDevPersonaToolbarAsync();
        await SelectEventModeAsync();

        await Assertions.Expect(Page.Locator("[data-test='community-command-center']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='mode-question']")).ToContainTextAsync("Can this event succeed?");
        await Assertions.Expect(Page.Locator("[data-test='mode-explanation']")).ToContainTextAsync("Hacktoberfest Mumbai");
        await Assertions.Expect(Page.Locator("[data-test='mode-layout-event']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='community-snapshot']")).ToContainTextAsync("Event operations snapshot");
        await Assertions.Expect(Page.Locator("[data-test='event-primary-work']")).ToContainTextAsync("Event attention queue");
        await Assertions.Expect(Page.Locator("[data-test='pending-event-approvals']")).ToContainTextAsync("Waitlist decisions");
        await Assertions.Expect(Page.Locator("[data-test='pending-approvals']")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator("[data-test='upcoming-touchpoints']")).ToHaveAttributeAsync("data-expanded", "true");
        await Assertions.Expect(Page.Locator("[data-test='touchpoint-readiness-card']").First).ToContainTextAsync("82% ready");
        await Assertions.Expect(Page.Locator("[data-test='touchpoint-readiness-card']").First).ToContainTextAsync("12 / 14");
        var pillsStayWithinCards = await Page
            .Locator("[data-test='touchpoint-readiness-state']")
            .EvaluateAllAsync<bool>(
                """
                pills => pills.every(pill => {
                    const card = pill.closest("[data-test='touchpoint-readiness-card']");
                    if (!card) return false;
                    const pillBounds = pill.getBoundingClientRect();
                    const cardBounds = card.getBoundingClientRect();
                    return pillBounds.left >= cardBounds.left &&
                        pillBounds.right <= cardBounds.right &&
                        pillBounds.top >= cardBounds.top &&
                        pillBounds.bottom <= cardBounds.bottom;
                })
                """);
        Assert.IsTrue(pillsStayWithinCards, "Every readiness status pill must remain inside its card.");
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
        await Assertions.Expect(Page.Locator("[data-test='mode-layout-strategic']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='mode-question']")).ToContainTextAsync("How is the community evolving?");
        await Assertions.Expect(Page.Locator("[data-test='community-snapshot']")).ToContainTextAsync("Community snapshot");
        await Assertions.Expect(Page.Locator("[data-test='strategic-primary-work']")).ToContainTextAsync("Community insight of the week");
        await Assertions.Expect(Page.Locator("[data-test='people-to-watch']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='pending-approvals']")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='quick-actions'] [data-test='quick-action']").First).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("[data-test='attention-queue'] [data-test='attention-item']").First)
            .ToContainTextAsync("Hacktoberfest Mumbai");
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

            await Assertions.Expect(Page.Locator(ReviewQueueSelector)).ToHaveCountAsync(1);
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

            await Assertions.Expect(Page.Locator(ReviewQueueSelector)).ToHaveCountAsync(0);
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

        var audience = Page.Locator("[data-test='command-center-audience'], [data-test='participation-audience']").First;

        // The toolbar renders before the Blazor circuit attaches its click handler, so the first
        // click after a cold navigation can be dropped. Re-issue it until the audience updates.
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

    private async Task SelectEventModeAsync()
    {
        await Page.Locator("[data-test='mode-event'] button").ClickAsync();
        await Assertions.Expect(Page.Locator("[data-test='mode-layout-event']")).ToBeVisibleAsync();
    }
}

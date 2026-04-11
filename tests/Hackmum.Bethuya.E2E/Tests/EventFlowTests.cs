using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class EventFlowTests : BethuyaE2ETest
{
    /// <summary>
    /// Verifies the "Plan New Event" button on the Home dashboard navigates
    /// to the dedicated /events/plan page within the navigation budget.
    /// </summary>
    [TestMethod]
    public async Task HomePlanButton_ShouldNavigateToPlanPage()
    {
        await GotoWithBudgetAsync("/");

        // Wait for Blazor interactive rendering within budget
        var createBtn = Page.Locator("[data-test='plan-event-cta'] button").First;
        await WithBudgetAsync("Blazor interactive ready", PerformanceBudgets.InteractiveReadyMs, async () =>
        {
            await Assertions.Expect(createBtn).ToBeVisibleAsync();
            await Assertions.Expect(createBtn).ToBeEnabledAsync();
        });

        // Click and assert navigation to /events/plan
        await createBtn.ClickAsync();
        await WaitForClientSideNavigationAsync(
            "/events/plan$",
            Page.Locator("[data-test='plan-event-page']"),
            PerformanceBudgets.NavigationMs);
    }

    [TestMethod]
    public async Task PlanEvent_OnEventsPage_ShouldShowInList()
    {
        await GotoWithBudgetAsync("/events");

        // Click the plan event button — navigates to /events/plan
        var planEventBtn = Page.Locator("[data-test='plan-event-btn'] button");
        await WithBudgetAsync("Blazor interactive ready", PerformanceBudgets.InteractiveReadyMs, async () =>
        {
            await Assertions.Expect(planEventBtn).ToBeVisibleAsync();
            await Assertions.Expect(planEventBtn).ToBeEnabledAsync();
        });
        await planEventBtn.ClickAsync();
        await WaitForClientSideNavigationAsync(
            "/events/plan$",
            Page.Locator("[data-test='plan-event-page']"),
            PerformanceBudgets.NavigationMs);

        // Wait for the plan form to be interactive (Blazor Server circuit)
        var submitBtn = Page.Locator("[data-test='publish-event-btn'] button");
        await Assertions.Expect(submitBtn).ToBeEnabledAsync(new() { Timeout = PerformanceBudgets.InteractiveReadyMs });

        // Use unique title to avoid strict mode violations from previous test runs
        var uniqueTitle = $"E2E Meetup {Guid.NewGuid().ToString("N")[..8]}";
        await Page.GetByPlaceholder("Event title").FillAsync(uniqueTitle);
        await Page.GetByPlaceholder("Event description").FillAsync("A test event for E2E testing");

        // Publish and assert client-side redirect within budget
        await submitBtn.ClickAsync();
        await WaitForClientSideNavigationAsync(
            "/events$",
            Page.Locator("[data-test='events-page']"),
            PerformanceBudgets.FormSubmitMs);

        // Verify events page loaded with event (use .First to avoid strict mode if multiple matches)
        var eventRow = Page.Locator("[data-test='event-row']").Filter(new() { HasText = uniqueTitle }).First;
        await Assertions.Expect(eventRow).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task EventDetail_ShouldShowScheduleEditor()
    {
        // Create a deterministic published event first so the list renders a view button
        await GotoWithBudgetAsync("/events/plan");
        var submitBtn = Page.Locator("[data-test='publish-event-btn'] button");
        await Assertions.Expect(submitBtn).ToBeEnabledAsync(new() { Timeout = PerformanceBudgets.InteractiveReadyMs });

        var uniqueTitle = $"E2E Detail {Guid.NewGuid().ToString("N")[..8]}";
        await Page.GetByPlaceholder("Event title").FillAsync(uniqueTitle);
        await Page.GetByPlaceholder("Event description").FillAsync("Seeded for detail page test");
        await submitBtn.ClickAsync();
        await WaitForClientSideNavigationAsync(
            "/events$",
            Page.Locator("[data-test='events-page']"),
            PerformanceBudgets.FormSubmitMs);

        // Now navigate to the created event's detail page
        var viewBtn = Page.Locator("[data-test='event-row']")
            .Filter(new() { HasText = uniqueTitle })
            .Locator("[data-test='view-event-btn'] button");
        await Assertions.Expect(viewBtn).ToBeVisibleAsync(new() { Timeout = PerformanceBudgets.InteractiveReadyMs });
        await viewBtn.ClickAsync();
        await WaitForClientSideNavigationAsync(
            "/events/[0-9a-fA-F-]{36}$",
            Page.Locator("[data-test='schedule-editor']"),
            PerformanceBudgets.NavigationMs);

        // Verify event detail page has the schedule editor
        await Assertions.Expect(Page.Locator("[data-test='schedule-editor']"))
            .ToBeVisibleAsync(new() { Timeout = PerformanceBudgets.InteractiveReadyMs });
    }
}

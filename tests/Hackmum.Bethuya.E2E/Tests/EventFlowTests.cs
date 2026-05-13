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

        // Click the plan event button - navigates to /events/plan
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

    [TestMethod]
    public async Task EventDetail_HybridViewer_ShouldToggleBetweenMarkdownAndJsonTabs()
    {
        // Create event and generate planner draft
        await GotoWithBudgetAsync("/events/plan");
        var submitBtn = Page.Locator("[data-test='publish-event-btn'] button");
        await Assertions.Expect(submitBtn).ToBeEnabledAsync(new() { Timeout = PerformanceBudgets.InteractiveReadyMs });

        var uniqueTitle = $"E2E Hybrid {Guid.NewGuid().ToString("N")[..8]}";
        await Page.GetByPlaceholder("Event title").FillAsync(uniqueTitle);
        await Page.GetByPlaceholder("Event description").FillAsync("Hybrid viewer E2E coverage");
        await submitBtn.ClickAsync();
        await WaitForClientSideNavigationAsync(
            "/events$",
            Page.Locator("[data-test='events-page']"),
            PerformanceBudgets.FormSubmitMs);

        var viewBtn = Page.Locator("[data-test='event-row']")
            .Filter(new() { HasText = uniqueTitle })
            .Locator("[data-test='view-event-btn'] button");
        await Assertions.Expect(viewBtn).ToBeVisibleAsync(new() { Timeout = PerformanceBudgets.InteractiveReadyMs });
        await viewBtn.ClickAsync();
        await WaitForClientSideNavigationAsync(
            "/events/[0-9a-fA-F-]{36}$",
            Page.Locator("[data-test='schedule-editor']"),
            PerformanceBudgets.NavigationMs);

        // Draft with AI
        var draftBtn = Page.Locator("[data-test='ai-draft-btn'] button");
        await Assertions.Expect(draftBtn).ToBeEnabledAsync();
        await draftBtn.ClickAsync();

        // Wait for markdown editor to appear (default tab)
        var markdownEditor = Page.Locator("[data-test='planner-markdown-editor'] textarea");
        await Assertions.Expect(markdownEditor).ToBeVisibleAsync(new() { Timeout = PerformanceBudgets.FormSubmitMs });

        // Verify JSON viewer is initially hidden
        var jsonViewer = Page.Locator("[data-test='planner-json-viewer']");
        await Assertions.Expect(jsonViewer).ToBeHiddenAsync();

        // Click JSON tab (uses button with text containing "Schema")
        var jsonTab = Page.Locator("button:has-text('Schema')").First;
        await jsonTab.ClickAsync();

        // Verify JSON viewer is now visible
        await Assertions.Expect(jsonViewer).ToBeVisibleAsync();
        
        // Verify it contains valid JSON structure (agendaVersion, event, agenda, etc.)
        var jsonContent = await jsonViewer.TextContentAsync();
        
        if (string.IsNullOrEmpty(jsonContent) || !jsonContent.Contains("agendaVersion"))
            throw new InvalidOperationException("JSON content missing agendaVersion");
        if (!jsonContent.Contains("event"))
            throw new InvalidOperationException("JSON content missing event");
        if (!jsonContent.Contains("agenda"))
            throw new InvalidOperationException("JSON content missing agenda");

        // Click back to Markdown tab
        var markdownTab = Page.Locator("button:has-text('Markdown')").First;
        await markdownTab.ClickAsync();

        // Verify markdown editor is visible again
        await Assertions.Expect(markdownEditor).ToBeVisibleAsync();
        await Assertions.Expect(jsonViewer).ToBeHiddenAsync();
    }
}


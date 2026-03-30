using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class EventFlowTests : BethuyaE2ETest
{
    /// <summary>
    /// Verifies the "Create New Event" button on the Home dashboard navigates
    /// to the dedicated /events/create page.
    /// </summary>
    [TestMethod]
    public async Task HomeCreateButton_ShouldNavigateToCreatePage()
    {
        await Page.GotoAsync("/");

        // Wait for Blazor interactive rendering — button must be present and enabled
        var createBtn = Page.Locator("[data-test='create-event-btn']").First;
        await Expect(createBtn).ToBeVisibleAsync();
        await Expect(createBtn).ToBeEnabledAsync();

        // Click and assert navigation to /events/create
        await createBtn.ClickAsync();
        await Expect(Page.Locator("[data-test='create-event-page']"))
            .ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(Page).ToHaveURLAsync(new Regex("/events/create$"));
    }

    [TestMethod]
    public async Task CreateEvent_OnEventsPage_ShouldShowInList()
    {
        await Page.GotoAsync("/events");

        // Click the new event button — navigates to /events/create
        var newEventBtn = Page.Locator("[data-test='new-event-btn']");
        await Expect(newEventBtn).ToBeVisibleAsync();
        await Expect(newEventBtn).ToBeEnabledAsync();
        await newEventBtn.ClickAsync();

        // Assert we're on the create page
        await Expect(Page.Locator("[data-test='create-event-page']"))
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        // Fill in form fields
        await Page.GetByPlaceholder("Event title").FillAsync("Test Community Meetup");
        await Page.GetByPlaceholder("Event description").FillAsync("A test event for E2E testing");

        // Submit the form
        await Page.Locator("[data-test='create-event-submit']").ClickAsync();

        // Should redirect back to /events after successful creation
        await Expect(Page.Locator("[data-test='events-page']"))
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        // Verify event appears in the list
        var eventRow = Page.Locator("[data-test='event-row']").Filter(new() { HasText = "Test Community Meetup" });
        await Expect(eventRow).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task EventDetail_ShouldShowAgentPanels()
    {
        // Navigate to create page directly
        await Page.GotoAsync("/events/create");
        await Expect(Page.Locator("[data-test='create-event-page']"))
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        // Create an event
        await Page.GetByPlaceholder("Event title").FillAsync("Agent Test Event");
        await Page.Locator("[data-test='create-event-submit']").ClickAsync();

        // Should redirect to events list
        await Expect(Page.Locator("[data-test='events-page']"))
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        // Navigate to event detail using data-test selector
        await Page.Locator("[data-test='view-event-btn']").First.ClickAsync();

        await Expect(Page.GetByText("Agenda")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Registrations")).ToBeVisibleAsync();
    }
}

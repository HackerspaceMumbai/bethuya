using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class EventFlowTests : BethuyaE2ETest
{
    /// <summary>
    /// Verifies the "Create New Event" button on the Home dashboard opens the dialog.
    /// This test specifically targets the Home page button (FeaturedEventCard),
    /// which has historically been broken by CSS overlays and Blazor activation issues.
    /// </summary>
    [TestMethod]
    public async Task HomeCreateButton_ShouldOpenDialog()
    {
        await Page.GotoAsync("/");

        // Wait for Blazor interactive rendering — button must be present and enabled
        var createBtn = Page.Locator("[data-test='create-event-btn']").First;
        await Expect(createBtn).ToBeVisibleAsync();
        await Expect(createBtn).ToBeEnabledAsync();

        // Click and immediately assert the dialog is visible
        // If nothing happens (CSS overlay, non-interactive Blazor), this assertion fails
        await createBtn.ClickAsync();
        await Expect(Page.Locator("[data-test='create-dialog']"))
            .ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [TestMethod]
    public async Task CreateEvent_OnEventsPage_ShouldShowInList()
    {
        await Page.GotoAsync("/events");

        // Click the new event button
        var newEventBtn = Page.Locator("[data-test='new-event-btn']");
        await Expect(newEventBtn).ToBeVisibleAsync();
        await Expect(newEventBtn).ToBeEnabledAsync();
        await newEventBtn.ClickAsync();

        // Assert dialog opened — catches static SSR (no interactivity) failures immediately
        await Expect(Page.Locator("[data-test='create-dialog']"))
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        // Fill in form fields
        await Page.GetByPlaceholder("Event title").FillAsync("Test Community Meetup");
        await Page.GetByPlaceholder("Event description").FillAsync("A test event for E2E testing");

        // Submit the form
        await Page.Locator("[data-test='create-event-submit']").ClickAsync();

        // Verify success notification appears
        await Expect(Page.Locator("[data-test='notification']")).ToBeVisibleAsync();

        // Verify event appears in the list (data-test='event-row' per Events.razor markup)
        var eventRow = Page.Locator("[data-test='event-row']").Filter(new() { HasText = "Test Community Meetup" });
        await Expect(eventRow).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task EventDetail_ShouldShowAgentPanels()
    {
        await Page.GotoAsync("/events");

        // Create an event first
        var newEventBtn = Page.Locator("[data-test='new-event-btn']");
        await Expect(newEventBtn).ToBeEnabledAsync();
        await newEventBtn.ClickAsync();

        await Expect(Page.Locator("[data-test='create-dialog']"))
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        await Page.GetByPlaceholder("Event title").FillAsync("Agent Test Event");
        await Page.Locator("[data-test='create-event-submit']").ClickAsync();

        // Wait for creation to complete
        await Expect(Page.Locator("[data-test='notification']")).ToBeVisibleAsync();

        // Navigate to event detail using data-test selector
        await Page.Locator("[data-test='view-event-btn']").First.ClickAsync();

        await Expect(Page.GetByText("Agenda")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Registrations")).ToBeVisibleAsync();
    }
}

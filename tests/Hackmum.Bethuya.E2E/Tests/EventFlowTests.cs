using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class EventFlowTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task CreateEvent_ShouldShowInList()
    {
        await Page.GotoAsync("/events");

        // Click the new event button
        await Page.Locator("[data-test='new-event-btn']").ClickAsync();

        // Assert dialog opened — catches static SSR (no interactivity) failures immediately
        await Expect(Page.Locator("[data-test='create-dialog']")).ToBeVisibleAsync();

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
        await Page.Locator("[data-test='new-event-btn']").ClickAsync();
        await Expect(Page.Locator("[data-test='create-dialog']")).ToBeVisibleAsync();

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

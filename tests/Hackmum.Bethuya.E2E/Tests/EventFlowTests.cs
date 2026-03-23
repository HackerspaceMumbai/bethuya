using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class EventFlowTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task CreateEvent_ShouldShowInList()
    {
        await Page.GotoAsync("/events");

        // Click the new event button using data-test selector
        await Page.Locator("[data-test='new-event-btn']").ClickAsync();

        // Fill in form fields
        await Page.GetByPlaceholder("Event title").FillAsync("Test Community Meetup");
        await Page.GetByPlaceholder("Event description").FillAsync("A test event for E2E testing");

        // Submit the form using data-test selector
        await Page.Locator("[data-test='create-event-submit']").ClickAsync();

        // Verify success notification appears
        await Expect(Page.Locator("[data-test='notification']")).ToBeVisibleAsync();

        // Verify event appears in the list
        var eventCard = Page.Locator("[data-test='event-card']").Filter(new() { HasText = "Test Community Meetup" });
        await Expect(eventCard).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task EventDetail_ShouldShowAgentPanels()
    {
        await Page.GotoAsync("/events");

        // Click the new event button using data-test selector
        await Page.Locator("[data-test='new-event-btn']").ClickAsync();
        await Page.GetByPlaceholder("Event title").FillAsync("Agent Test Event");
        await Page.Locator("[data-test='create-event-submit']").ClickAsync();

        // Wait for navigation after creation
        await Expect(Page.Locator("[data-test='notification']")).ToBeVisibleAsync();

        // Navigate to event detail - using role-based selector as view button may not have data-test yet
        await Page.GetByRole(AriaRole.Button, new() { Name = "View →" }).First.ClickAsync();

        await Expect(Page.GetByText("Agenda")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Registrations")).ToBeVisibleAsync();
    }
}

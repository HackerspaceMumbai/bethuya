using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class EventFlowTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task CreateEvent_ShouldShowInList()
    {
        await Page.GotoAsync("/events");

        await Page.GetByRole(AriaRole.Button, new() { Name = "New Event" }).ClickAsync();

        await Page.GetByPlaceholder("Event title").FillAsync("Test Community Meetup");
        await Page.GetByPlaceholder("Event description").FillAsync("A test event for E2E testing");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        var eventCard = Page.Locator("[data-test='event-card']").First;
        await Expect(eventCard).ToContainTextAsync("Test Community Meetup");
    }

    [TestMethod]
    public async Task EventDetail_ShouldShowAgentPanels()
    {
        await Page.GotoAsync("/events");

        await Page.GetByRole(AriaRole.Button, new() { Name = "New Event" }).ClickAsync();
        await Page.GetByPlaceholder("Event title").FillAsync("Agent Test Event");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "View →" }).First.ClickAsync();

        await Expect(Page.GetByText("Agenda")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Registrations")).ToBeVisibleAsync();
    }
}

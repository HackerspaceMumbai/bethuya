using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class NavigationTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task HomePage_ShouldLoad()
    {
        await GotoWithBudgetAsync("/");
        await Assertions.Expect(Page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Bethuya|Home"));
    }

    [TestMethod]
    public async Task EventsPage_ShouldLoad()
    {
        await GotoWithBudgetAsync("/events");
        await Assertions.Expect(Page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Events"));
    }

    [TestMethod]
    public async Task Navigation_ShouldHaveEventsLink()
    {
        await GotoWithBudgetAsync("/");
        var eventsLink = Page.GetByRole(AriaRole.Link, new() { Name = "Events" });
        await Assertions.Expect(eventsLink).ToBeVisibleAsync();
    }
}

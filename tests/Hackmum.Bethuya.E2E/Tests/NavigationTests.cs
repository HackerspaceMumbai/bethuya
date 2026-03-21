using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class NavigationTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task HomePage_ShouldLoad()
    {
        await Page.GotoAsync("/");
        await Expect(Page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Bethuya|Home"));
    }

    [TestMethod]
    public async Task EventsPage_ShouldLoad()
    {
        await Page.GotoAsync("/events");
        await Expect(Page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Events"));
    }

    [TestMethod]
    public async Task Navigation_ShouldHaveEventsLink()
    {
        await Page.GotoAsync("/");
        var eventsLink = Page.GetByRole(AriaRole.Link, new() { Name = "Events" });
        await Expect(eventsLink).ToBeVisibleAsync();
    }
}

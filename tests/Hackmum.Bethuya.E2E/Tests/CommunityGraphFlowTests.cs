using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

/// <summary>Visual and interaction proof against the real ledger projection and server-rendered UI.</summary>
[TestClass]
public class CommunityGraphFlowTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task Graph_SelectFilterInspectAndResize_UsesVerifiedLedger()
    {
        var backendUrl = Environment.GetEnvironmentVariable("BETHUYA_BACKEND_URL")
            ?? throw new InvalidOperationException("Set BETHUYA_BACKEND_URL to the active development Backend URL.");
        var headers = new Dictionary<string, string> { ["X-Bethuya-Dev-Persona"] = "Vikram" };
        var provision = await Page.APIRequest.GetAsync($"{backendUrl}/api/community/passport", new() { Headers = headers });
        Assert.IsTrue(provision.Ok, "Provision the viewing development persona.");
        var seed = await Page.APIRequest.PostAsync($"{backendUrl}/api/dev/community-graph/seed", new() { Headers = headers });
        Assert.IsTrue(seed.Ok, "Graph fixture endpoint must seed actual development ledger records.");
        await Page.SetViewportSizeAsync(1600, 1100);
        await Page.GotoAsync($"{BaseUrl}/dev/persona/Vikram?returnUrl=/community-graph");
        var nodes = Page.Locator("[data-test='graph-node']");
        await Assertions.Expect(nodes.First).ToBeVisibleAsync(new() { Timeout = 30000 });
        var personaToggle = Page.Locator("[data-test='dev-persona-toggle'] button");
        if ((await personaToggle.InnerTextAsync()).Contains("Hide", StringComparison.OrdinalIgnoreCase))
            await personaToggle.ClickAsync();
        await nodes.Filter(new() { HasText = "Priya Menon" }).ClickAsync();
        var preview = Page.Locator("[data-test='passport-preview']");
        await Assertions.Expect(preview).ToContainTextAsync("Priya Menon");
        await Page.Locator("[data-test='passport-relationship'] button").Filter(new() { HasText = "Mentors · David" }).ClickAsync();
        await Assertions.Expect(Page.Locator("[data-test='relationship-proof']")).ToContainTextAsync("Confirmed mentorship with David");
        var output = Path.Combine(Environment.CurrentDirectory, "artifacts", "community-graph");
        Directory.CreateDirectory(output);
        await Page.EvaluateAsync("window.scrollTo(0, 0)");
        await Page.ScreenshotAsync(new() { Path = Path.Combine(output, "desktop.png"), FullPage = true });

        await nodes.Filter(new() { HasText = "Community Portal" }).DblClickAsync();
        await Assertions.Expect(Page.Locator("[data-test='graph-focus']")).ToContainTextAsync("Exploring Community Portal");
        await Assertions.Expect(nodes.Filter(new() { HasText = "Community Portal" })).ToHaveAttributeAsync("style", "left:50%;top:50%");
        await Page.ScreenshotAsync(new() { Path = Path.Combine(output, "project-focus.png"), FullPage = true });
        await nodes.Filter(new() { HasText = "Maya F." }).ClickAsync();
        await Assertions.Expect(Page.Locator("[data-test='graph-explore'] button")).ToBeEnabledAsync();
        await Page.Locator("[data-test='graph-explore'] button").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Page.Locator("[data-test='graph-focus']")).ToContainTextAsync("Exploring Maya F.");
        await Assertions.Expect(nodes.Filter(new() { HasText = "Maya F." })).ToHaveAttributeAsync("style", "left:50%;top:50%");
        await Page.Locator("[data-test='graph-back'] button").ClickAsync();
        await Assertions.Expect(Page.Locator("[data-test='graph-focus']")).ToContainTextAsync("Exploring Community Portal");
        await Page.Locator("[data-test='graph-back'] button").ClickAsync();
        await Assertions.Expect(Page.Locator("[data-test='graph-focus']")).ToContainTextAsync("Community overview");
        foreach (var kind in new[] { "Member", "Event", "Project", "Community", "Chapter", "Technology", "Opportunity" })
        {
            var candidate = Page.Locator($"[data-test='graph-node'][aria-label*=', {kind}.']").First;
            var label = await candidate.GetAttributeAsync("aria-label");
            Assert.IsNotNull(label);
            await candidate.ClickAsync();
            await Assertions.Expect(Page.Locator("[data-test='graph-focus']")).ToContainTextAsync("Community overview");
            await candidate.DblClickAsync();
            await Assertions.Expect(Page.Locator("[data-test='graph-focus']")).ToContainTextAsync($"Exploring {label.Split(", ", StringSplitOptions.None)[0]}");
            await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = label, Exact = true }))
                .ToHaveAttributeAsync("style", "left:50%;top:50%");
            await Assertions.Expect(Page.Locator("[data-test='graph-edge']").First).ToBeVisibleAsync();
            await Assertions.Expect(preview).ToContainTextAsync($"Selected entity · {kind}");
            await Page.ScreenshotAsync(new() { Path = Path.Combine(output, $"focus-{kind.ToLowerInvariant()}.png"), FullPage = true });
            await Page.Locator("[data-test='graph-back'] button").ClickAsync();
            await Assertions.Expect(Page.Locator("[data-test='graph-focus']")).ToContainTextAsync("Community overview");
        }
        await nodes.Filter(new() { HasText = "Priya Menon" }).ClickAsync();

        await Page.Locator("[data-test='passport-tab-Participation-Ledger'] button").ClickAsync();
        await Assertions.Expect(preview).ToContainTextAsync("Reviewed PR #142");
        await Page.Locator("[data-test='graph-search'] input").FillAsync("Community Portal");
        await Assertions.Expect(nodes).ToHaveCountAsync(1);
        await Assertions.Expect(preview).ToContainTextAsync("Select a node or relationship");
        await Page.Locator("[data-test='graph-search'] input").FillAsync("no-such-entity");
        await Assertions.Expect(Page.Locator("[data-test='graph-no-results']")).ToBeVisibleAsync();
        await Page.Locator("[data-test='graph-reset-filters'] button").ClickAsync();
        await Assertions.Expect(nodes.First).ToBeVisibleAsync();
        await Page.Locator("[data-test='discovery-Discover-mentors'] button").ClickAsync();
        await Assertions.Expect(nodes).ToHaveCountAsync(3);
        var edge = Page.Locator("[data-test='graph-edge']").First;
        await edge.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Page.Locator("[data-test='relationship-proof']")).ToBeVisibleAsync();
        await Page.Locator("[data-test='graph-health-toggle']").UncheckAsync();
        await Assertions.Expect(Page.Locator("[data-test='graph-health']")).ToHaveCountAsync(0);
        await Page.SetViewportSizeAsync(693, 648);
        await Page.EvaluateAsync("window.scrollTo(0, 0)");
        await Page.ScreenshotAsync(new() { Path = Path.Combine(output, "compact.png"), FullPage = true });
        await Page.SetViewportSizeAsync(390, 844);
        await Page.EvaluateAsync("window.scrollTo(0, 0)");
        await Page.ScreenshotAsync(new() { Path = Path.Combine(output, "mobile.png"), FullPage = true });
        var noHorizontalOverflow = await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1");
        Assert.IsTrue(noHorizontalOverflow, "The graph scrolls internally without overflowing the mobile page.");
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }
}

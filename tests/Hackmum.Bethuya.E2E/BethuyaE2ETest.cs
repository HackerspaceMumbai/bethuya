using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E;

/// <summary>
/// Base class for Bethuya E2E tests with Playwright browser lifecycle management.
/// Replaces Microsoft.Playwright.MSTest.PageTest (binary-incompatible with MSTest 4.x).
/// Includes performance timing helpers for budget enforcement.
/// </summary>
[TestClass]
public class BethuyaE2ETest
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;

    protected IPage Page { get; private set; } = null!;
    protected static string BaseUrl => Environment.GetEnvironmentVariable("BETHUYA_BASE_URL") ?? "https://localhost:7112";

    [TestInitialize]
    public async Task SetUpAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl
        });
        Page = await _context.NewPageAsync();
    }

    [TestCleanup]
    public async Task TearDownAsync()
    {
        if (Page != null) await Page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    /// <summary>
    /// Assert a locator is visible within a timeout using Playwright's built-in expect.
    /// </summary>
    protected static async Task ExpectVisibleAsync(ILocator locator, float? timeoutMs = null)
    {
        await Assertions.Expect(locator).ToBeVisibleAsync(new() { Timeout = timeoutMs });
    }

    /// <summary>
    /// Navigate to a URL and assert the navigation completes within the page load budget.
    /// Waits for NetworkIdle to ensure Blazor Server SignalR circuit is established.
    /// </summary>
    protected async Task<IResponse?> GotoWithBudgetAsync(string url, int? budgetMs = null)
    {
        var budget = budgetMs ?? PerformanceBudgets.PageLoadMs;
        var sw = Stopwatch.StartNew();
        var response = await Page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });
        sw.Stop();

        Assert.IsTrue(
            sw.ElapsedMilliseconds <= budget,
            $"Page load for '{url}' took {sw.ElapsedMilliseconds}ms, exceeding budget of {budget}ms");

        return response;
    }

    /// <summary>
    /// Click an element and wait for navigation, asserting it completes within the navigation budget.
    /// </summary>
    protected async Task ClickAndNavigateWithBudgetAsync(ILocator locator, int? budgetMs = null)
    {
        var budget = budgetMs ?? PerformanceBudgets.NavigationMs;
        var sw = Stopwatch.StartNew();
        await locator.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        sw.Stop();

        Assert.IsTrue(
            sw.ElapsedMilliseconds <= budget,
            $"Navigation took {sw.ElapsedMilliseconds}ms, exceeding budget of {budget}ms");
    }

    /// <summary>
    /// Wait for Blazor client-side navigation by polling the current URL and a ready locator
    /// instead of waiting for a full page load event.
    /// </summary>
    protected async Task WaitForClientSideNavigationAsync(string urlPattern, ILocator readyLocator, int budgetMs)
    {
        var sw = Stopwatch.StartNew();

        await Assertions.Expect(Page)
            .ToHaveURLAsync(new Regex(urlPattern), new() { Timeout = budgetMs });

        var remainingBudgetMs = Math.Max(1, budgetMs - (int)sw.ElapsedMilliseconds);

        await Assertions.Expect(readyLocator)
            .ToBeVisibleAsync(new() { Timeout = remainingBudgetMs });

        sw.Stop();

        Assert.IsTrue(
            sw.ElapsedMilliseconds <= budgetMs,
            $"'Client-side navigation' took {sw.ElapsedMilliseconds}ms, exceeding budget of {budgetMs}ms");
    }

    /// <summary>
    /// Execute an action and assert it completes within the given budget.
    /// Returns the elapsed time in milliseconds.
    /// </summary>
    protected static async Task<long> WithBudgetAsync(string operationName, int budgetMs, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();

        Assert.IsTrue(
            sw.ElapsedMilliseconds <= budgetMs,
            $"'{operationName}' took {sw.ElapsedMilliseconds}ms, exceeding budget of {budgetMs}ms");

        return sw.ElapsedMilliseconds;
    }
}

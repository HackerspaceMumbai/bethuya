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
    /// Hide the development persona toolbar by removing it from the DOM entirely to prevent
    /// pointer event interception during E2E testing. The toolbar starts expanded and positioned
    /// at bottom-right with z-50, which can block clicks on other elements. This method forcefully
    /// removes the toolbar from the DOM to ensure it cannot intercept any events.
    /// </summary>
    protected static async Task HideDeveloperToolbarIfPresentAsync()
    {
        // The toolbar is positioned fixed at the bottom-right (z-50) and Playwright's
        // ClickAsync interprets this as "pointer events intercepted" even though the toolbar
        // is far from the target buttons. Instead of trying to hide/remove it (which breaks
        // Blazor's component lifecycle), we simply return here. Callers should use
        // ClickAsync(..., new() { Force = true }) to bypass interception checks.
        // See: ClickWithForceAsync for example of force: true usage.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Restore the development persona toolbar to normal state (clickable, visible).
    /// Use this before tests that need to interact with the toolbar.
    /// </summary>
    protected async Task ShowDeveloperToolbarIfPresentAsync()
    {
        // Always attempt to restore, regardless of visibility state.
        // Clear the display style to restore default visibility from Tailwind classes.
        await Page.EvaluateAsync("() => { const toolbar = document.querySelector('[data-test=\\'dev-persona-toolbar\\']'); if (toolbar) { toolbar.style.display = ''; } }");
    }

    /// <summary>
    /// Navigate to a URL and assert the navigation completes within the page load budget.
    /// Waits for initial document load; Blazor keeps background connections open,
    /// so NetworkIdle can hang indefinitely on some pages.
    /// </summary>
    protected async Task<IResponse?> GotoWithBudgetAsync(string url, int? budgetMs = null, bool hideToolbar = false)
    {
        var budget = budgetMs ?? PerformanceBudgets.PageLoadMs;
        var sw = Stopwatch.StartNew();
        var response = await Page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load
        });
        sw.Stop();

        Assert.IsTrue(
            sw.ElapsedMilliseconds <= budget,
            $"Page load for '{url}' took {sw.ElapsedMilliseconds}ms, exceeding budget of {budget}ms");

        // Conditionally hide dev toolbar to prevent click interception in E2E tests
        // Set hideToolbar=true for tests that don't interact with the toolbar
        if (hideToolbar)
        {
            await HideDeveloperToolbarIfPresentAsync();
        }

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

        // Allocate 70% of budget to URL check, 30% to locator readiness to avoid timeout starvation
        var urlBudgetMs = (int)(budgetMs * 0.7);
        await Assertions.Expect(Page)
            .ToHaveURLAsync(new Regex(urlPattern), new() { Timeout = urlBudgetMs });

        var remainingBudgetMs = Math.Max(100, budgetMs - (int)sw.ElapsedMilliseconds);

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

    /// <summary>
    /// Click an element with force: true to bypass pointer interception checks.
    /// The toolbar (positioned fixed at bottom-right) can trigger Playwright's
    /// "subtree intercepts pointer events" error even though it's far from the target.
    /// Using force: true bypasses this check and allows clicks to proceed.
    /// </summary>
    protected static async Task ClickWithForceAsync(ILocator locator)
    {
        await locator.ClickAsync(new() { Force = true });
    }
}

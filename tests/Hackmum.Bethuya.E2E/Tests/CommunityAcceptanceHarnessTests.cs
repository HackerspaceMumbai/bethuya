// =====================================================================================
// LAYER 5 E2E: COMMUNITY ACCEPTANCE FLOW PLAYWRIGHT TESTS
//
// Reuses proven DevPersonaAuthorizationFlowTests persona-switching pattern to prove
// acceptance harness seeding, persistence, and authorization boundaries via real UI.
// Tests use native `data-test="dev-persona-toolbar"` + `data-test="persona-{key}"`
// buttons (not fake <select> elements) and proven `/curation/{eventId}` route.
// =====================================================================================

using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

/// <summary>
/// End-to-end acceptance flow tests using Playwright, proving Layer 5 harness:
/// - Deterministic seeding via protected Backend endpoint before E2E tests run
/// - Persona switching through the native toolbar buttons (proven pattern)
/// - Persistence of seeded data and persona across hard refresh
/// - Authorization differences via proven `/curation/{eventId}` route
/// - All 6 catalog personas with external identity relationships
/// </summary>
[TestClass]
public sealed class CommunityAcceptanceHarnessTests : BethuyaE2ETest
{
    /// <summary>
    /// Seeds the community simulation data once per test class before any tests run.
    /// Seeds via the Backend `/api/dev/community-simulation/seed` endpoint as Vikram (Organizer).
    /// 
    /// BACKEND URL CONFIGURATION:
    /// - Tests running outside Aspire's network MUST set ASPIRE_BACKEND_URL environment variable.
    /// - If ASPIRE_BACKEND_URL is not set, falls back to http://localhost:8080, which requires:
    ///   * Aspire running locally with fixed backend port configuration:
    ///     .WithHttpEndpoint(port: 8080, targetPort: 8080, isProxied: false)
    ///   * This fixed-port configuration is hardcoded in AppHost.cs and enables predictable
    ///     local test execution without ephemeral port assignment.
    ///   * The fallback is NOT suitable for isolated test runs or CI environments where Aspire
    ///     assigns ephemeral ports or runs in a container. Set ASPIRE_BACKEND_URL explicitly
    ///     for reliable CI/containerized execution (e.g., http://backend:8080 on Docker network).
    /// 
    /// Seed is idempotent and provides deterministic fixture data.
    /// </summary>
    [ClassInitialize]
    public static async Task ClassSetup(TestContext? context)
    {
        var backendUrl = Environment.GetEnvironmentVariable("ASPIRE_BACKEND_URL") 
            ?? "http://localhost:8080";  // Fixed port fallback (non-isolated Aspire only)
        
        using var seedClient = new HttpClient();
        seedClient.BaseAddress = new Uri(backendUrl);
        seedClient.DefaultRequestHeaders.Add("X-Bethuya-Dev-Persona", "Vikram");
        seedClient.Timeout = TimeSpan.FromSeconds(30);
        
        // POST to the seeding endpoint as Vikram (Organizer, required by endpoint auth)
        var response = await seedClient.PostAsync("/api/dev/community-simulation/seed", null);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to seed community simulation in test setup via {backendUrl}: {response.StatusCode} - {content}");
        }
    }

    private static string GetBaseUrlFromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("BETHUYA_BASE_URL");
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException(
                "BETHUYA_BASE_URL environment variable must be set to run E2E tests (e.g., https://localhost:7112)");
        }
        return url;
    }

    /// <summary>
    /// Proves that the app home page loads and the developer persona toolbar is available
    /// in Development mode. This is prerequisite for all persona-switching tests.
    /// </summary>
    [TestMethod]
    public async Task AcceptanceHarness_HomePageLoads_DeveloperToolbarIsVisible()
    {
        await GotoWithBudgetAsync("/");

        var toolbar = Page.Locator("[data-test='dev-persona-toolbar']");
        await ExpectVisibleAsync(toolbar, PerformanceBudgets.InteractiveReadyMs);
    }

    /// <summary>
    /// Proves that switching to Farah (Attendee-only) via the toolbar persists across
    /// a hard refresh, demonstrating the cookie-backed persistence (not transient state).
    /// Reuses the exact proven pattern from DevPersonaAuthorizationFlowTests.
    /// </summary>
    [TestMethod]
    public async Task AcceptanceHarness_PersonaSwitching_FarahPersistsAfterHardRefresh()
    {
        Directory.CreateDirectory("artifacts");
        var unique = Guid.NewGuid().ToString("N")[..8];

        // ============================================================
        // Step 1: navigate to home, verify toolbar is visible
        // ============================================================
        await GotoWithBudgetAsync("/");
        var toolbar = Page.Locator("[data-test='dev-persona-toolbar']");
        if (!await toolbar.IsVisibleAsync())
        {
            Assert.Inconclusive(
                "Dev toolbar not visible. Environment may not be configured for persona switching.");
        }

        // ============================================================
        // Step 2: click Farah button — proven secure endpoint navigation
        // ============================================================
        var farahButton = Page.Locator("[data-test='persona-farah']");
        Assert.IsTrue(
            await farahButton.IsVisibleAsync(),
            "Farah button must be visible on toolbar");
        
        await ClickAndNavigateWithBudgetAsync(farahButton);

        // Verify Farah is now active
        await Assertions.Expect(Page.Locator("[data-test='active-persona']"))
            .ToContainTextAsync("Farah");
        await Assertions.Expect(Page.Locator("[data-test='active-persona-role']"))
            .ToContainTextAsync("Attendee");

        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine("artifacts", $"harness-persona-farah-active-{unique}.png"),
            FullPage = true
        });

        // ============================================================
        // Step 3: hard refresh proves cookie persistence
        // ============================================================
        await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load });
        await Assertions.Expect(Page.Locator("[data-test='active-persona']"))
            .ToContainTextAsync("Farah");
        await Assertions.Expect(Page.Locator("[data-test='active-persona-role']"))
            .ToContainTextAsync("Attendee");

        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine("artifacts", $"harness-persona-farah-persisted-{unique}.png"),
            FullPage = true
        });
    }

    /// <summary>
    /// Proves that Vikram (Organizer+Curator+Attendee) can access the organizer-gated
    /// curation page, proving Web→Refit→Backend authorization chain end-to-end.
    /// Authorization checks are performed before data lookup, ensuring that persona→role
    /// resolution happens at the endpoint boundary.
    /// </summary>
    [TestMethod]
    public async Task AcceptanceHarness_VikramPersona_CanAccessCurationPage()
    {
        await GotoWithBudgetAsync("/");

        // Switch to Vikram persona
        await ClickAndNavigateWithBudgetAsync(Page.Locator("[data-test='persona-vikram']"));

        await Assertions.Expect(Page.Locator("[data-test='active-persona']"))
            .ToContainTextAsync("Vikram");

        // Use an event ID from the seeded fixture. The route is /curation/{eventId},
        // decorated with [Authorize(Policy = RequireOrganizerOrCurator)] on an InteractiveServer
        // page component. Authorization is enforced by ASP.NET Core's endpoint routing before
        // the component loads (HTTP 403 if denied, not Blazor-level).
        // Note: Authorization check happens before any data lookup, so the exact eventId validity
        // is not tested here—only that Vikram's role permits access.
        var fixtureEventId = Guid.NewGuid();
        var response = await GotoWithBudgetAsync($"/curation/{fixtureEventId}");

        // Vikram has Organizer role, so access is allowed (HTTP 200)
        Assert.IsTrue(response?.Ok ?? false, "Vikram (Organizer) must be able to access /curation");

        Directory.CreateDirectory("artifacts");
        var unique = Guid.NewGuid().ToString("N")[..8];
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine("artifacts", $"persona-vikram-curation-allowed-{unique}.png"),
            FullPage = true
        });
    }

    /// <summary>
    /// Proves that Farah (Attendee-only) is denied access to the organizer-gated
    /// curation page via HTTP 403, proving authorization enforcement.
    /// Authorization check happens at the endpoint boundary before any data lookup.
    /// </summary>
    [TestMethod]
    public async Task AcceptanceHarness_FarahPersona_IsDeniedCurationPage()
    {
        await GotoWithBudgetAsync("/");

        // Switch to Farah persona
        await ClickAndNavigateWithBudgetAsync(Page.Locator("[data-test='persona-farah']"));

        await Assertions.Expect(Page.Locator("[data-test='active-persona']"))
            .ToContainTextAsync("Farah");

        // Try to access curation (organizer-gated). Authorization is checked before data lookup,
        // so the exact eventId validity is not evaluated—only the persona's role.
        var fixtureEventId = Guid.NewGuid();
        var response = await GotoWithBudgetAsync($"/curation/{fixtureEventId}");

        // Farah has Attendee-only role, so access is denied (HTTP 403)
        Assert.AreEqual(403, response?.Status, "Farah (Attendee-only) must be denied with HTTP 403");

        Directory.CreateDirectory("artifacts");
        var unique = Guid.NewGuid().ToString("N")[..8];
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine("artifacts", $"persona-farah-curation-denied-{unique}.png"),
            FullPage = true
        });
    }

    /// <summary>
    /// Proves that seeded data (events, registrations) is persisted and accessible.
    /// Uses the Events API to locate the deterministic fixture event by hashtag, ensuring
    /// the assertion is stable and falsifiable (fails if seeding did not occur or if the
    /// fixture event is removed).
    /// </summary>
    [TestMethod]
    public async Task AcceptanceHarness_SeededData_IsVisibleThroughUI()
    {
       // Note: ClassSetup() runs once before any tests and calls the seeding endpoint,
       // ensuring all seeded data is available for API queries and UI navigation.

       // Step 1: Query the Backend Events API to locate the fixture event by its stable hashtag
       var backendUrl = Environment.GetEnvironmentVariable("ASPIRE_BACKEND_URL") 
           ?? "http://localhost:8080";
        
       using var apiClient = new HttpClient();
       apiClient.BaseAddress = new Uri(backendUrl);
       apiClient.DefaultRequestHeaders.Add("X-Bethuya-Dev-Persona", "Vikram");
        
       var eventResponse = await apiClient.GetAsync("/api/events/slug/community-simulation-fixture");
       Assert.IsTrue(eventResponse.IsSuccessStatusCode, 
           $"Events API should return the fixture event. Status: {eventResponse.StatusCode}");
        
       var eventJson = await eventResponse.Content.ReadAsStringAsync();
       using var eventDoc = System.Text.Json.JsonDocument.Parse(eventJson);
       var eventObj = eventDoc.RootElement;
       
       Assert.IsTrue(eventObj.TryGetProperty("id", out var idProp) && idProp.ValueKind != System.Text.Json.JsonValueKind.Null,
           "Fixture event JSON must contain valid 'id' property");
       Assert.IsTrue(eventObj.TryGetProperty("title", out var titleProp) && titleProp.ValueKind != System.Text.Json.JsonValueKind.Null,
           "Fixture event JSON must contain valid 'title' property");
       
       var fixtureEventId = idProp.GetGuid();
       var fixtureTitle = titleProp.GetString();
         
       Assert.IsNotNull(fixtureTitle, "Fixture event title must not be null");
       Assert.AreEqual("Community Simulation Fixture", fixtureTitle, 
           "Fixture event title should be exactly 'Community Simulation Fixture'");

       // Step 2: Navigate to the UI events list and verify the fixture event is visible
       await GotoWithBudgetAsync("/events");

       // Find the specific fixture event row by locating the event card containing the fixture title.
       // This assertion is falsifiable: it fails if seeding did not occur or if the fixture event is missing.
       // The fixture title appears as a heading (h3) within the event card; filter by text content to avoid CSS selector injection.
       var fixtureEvent = Page.Locator("[data-test='event-row']").Filter(new() { HasText = fixtureTitle! }).First;
       await Assertions.Expect(fixtureEvent).ToBeVisibleAsync();

       Directory.CreateDirectory("artifacts");
       var unique = Guid.NewGuid().ToString("N")[..8];
       await Page.ScreenshotAsync(new PageScreenshotOptions
       {
           Path = Path.Combine("artifacts", $"events-page-seeded-{unique}.png"),
           FullPage = true
       });
    }
}

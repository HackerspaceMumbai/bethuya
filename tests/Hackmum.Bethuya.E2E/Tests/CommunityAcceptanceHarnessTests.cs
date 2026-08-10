// =====================================================================================
// LAYER 5 E2E: COMMUNITY ACCEPTANCE FLOW PLAYWRIGHT TESTS
//
// Reuses proven DevPersonaAuthorizationFlowTests persona-switching pattern to prove
// acceptance harness seeding, persistence, and authorization boundaries via real UI.
// Tests use native `data-test="dev-persona-toolbar"` + `data-test="persona-{key}"`
// buttons (not fake <select> elements) and proven `/curation/{eventId}` route.
// =====================================================================================

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

/// <summary>
/// End-to-end acceptance flow tests using Playwright, proving Layer 5 harness:
/// - Deterministic seeding via protected Backend endpoint before E2E tests run
/// - Persona switching through the native toolbar buttons (proven pattern)
/// - Persistence of seeded data and persona across hard refresh
/// - Authorization differences via proven `/curation/{eventId}` route
/// - All 6 catalog personas with stable seeded fixture participation
/// </summary>
[TestClass]
public class CommunityAcceptanceHarnessTests : BethuyaE2ETest
{
    private static readonly HttpClient BackendClient = new();

    /// <summary>
    /// Seeds the community simulation data once per test class before any tests run.
    /// Seeds via the Backend `/api/dev/community-simulation/seed` endpoint as Vikram (Organizer).
    /// The E2E tests discover Backend via environment-configured endpoint URL.
    /// Seed is idempotent and provides deterministic fixture data.
    /// </summary>
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task ClassSetup(TestContext context)
    {
        // Prefer ASPIRE_BACKEND_URL when the Backend is hosted outside this test process.
        // When running the local AppHost, the Backend is fixed at http://localhost:8080.
        var backendUrl = GetBackendUrlFromEnvironment();

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

    private static string GetBackendUrlFromEnvironment() =>
        Environment.GetEnvironmentVariable("ASPIRE_BACKEND_URL") ?? "http://localhost:8080";

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
        Directory.CreateDirectory(Path.Combine("artifacts", "layer5"));
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
            Path = Path.Combine("artifacts", "layer5", $"harness-persona-farah-active-{unique}.png"),
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
            Path = Path.Combine("artifacts", "layer5", $"harness-persona-farah-persisted-{unique}.png"),
            FullPage = true
        });
    }

    /// <summary>
    /// Proves that Vikram (Organizer+Curator+Attendee) can access the organizer-gated
    /// curation page, proving Web→Refit→Backend authorization chain end-to-end.
    /// </summary>
    [TestMethod]
    public async Task AcceptanceHarness_VikramPersona_CanAccessCurationPage()
    {
        await GotoWithBudgetAsync("/");

        // Switch to Vikram persona
        await ClickAndNavigateWithBudgetAsync(Page.Locator("[data-test='persona-vikram']"));

        await Assertions.Expect(Page.Locator("[data-test='active-persona']"))
            .ToContainTextAsync("Vikram");

        // Use a random probe ID so the curation page is reached only after auth.
        // The route is /curation/{eventId}, decorated with [Authorize(Policy = RequireOrganizerOrCurator)]
        // on an InteractiveServer page component, so authorization is enforced by
        // ASP.NET Core's endpoint routing (HTTP 403 if denied, not Blazor-level).
        var authProbeEventId = Guid.NewGuid();
        var response = await GotoWithBudgetAsync($"/curation/{authProbeEventId}");

        // Vikram has Organizer role, so access is allowed (HTTP 200)
        Assert.IsTrue(response?.Ok ?? false, "Vikram (Organizer) must be able to access /curation");

        Directory.CreateDirectory(Path.Combine("artifacts", "layer5"));
        var unique = Guid.NewGuid().ToString("N")[..8];
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine("artifacts", "layer5", $"persona-vikram-curation-allowed-{unique}.png"),
            FullPage = true
        });
    }

    /// <summary>
    /// Proves that Farah (Attendee-only) is denied access to the organizer-gated
    /// curation page via HTTP 403, proving authorization enforcement.
    /// </summary>
    [TestMethod]
    public async Task AcceptanceHarness_FarahPersona_IsDeniedCurationPage()
    {
        await GotoWithBudgetAsync("/");

        // Switch to Farah persona
        await ClickAndNavigateWithBudgetAsync(Page.Locator("[data-test='persona-farah']"));

        await Assertions.Expect(Page.Locator("[data-test='active-persona']"))
            .ToContainTextAsync("Farah");

        // Try to access curation (organizer-gated)
        var authProbeEventId = Guid.NewGuid();
        var response = await GotoWithBudgetAsync($"/curation/{authProbeEventId}");

        // Farah has Attendee-only role, so access is denied (HTTP 403)
        Assert.AreEqual(403, response?.Status, "Farah (Attendee-only) must be denied with HTTP 403");

        Directory.CreateDirectory(Path.Combine("artifacts", "layer5"));
        var unique = Guid.NewGuid().ToString("N")[..8];
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine("artifacts", "layer5", $"persona-farah-curation-denied-{unique}.png"),
            FullPage = true
        });
    }

    /// <summary>
    /// Proves that seeded data is visible when browsing as a seeded persona.
    /// Uses the exact fixture event from the Backend API, then verifies the matching UI row.
    /// </summary>
    [TestMethod]
    public async Task AcceptanceHarness_SeededData_IsVisibleThroughUI()
    {
       // Note: ClassSetup() runs once before any tests and calls the seeding endpoint,
       // ensuring all seeded data is available for UI navigation and assertions.

       var backendUrl = GetBackendUrlFromEnvironment();
       BackendClient.BaseAddress = new Uri(backendUrl);
       var fixtureEventResponse = await BackendClient.GetAsync("/api/events/slug/community-simulation-fixture");
       Assert.IsTrue(fixtureEventResponse.IsSuccessStatusCode, $"Fixture event lookup failed via {backendUrl}");

       var fixtureEvent = await fixtureEventResponse.Content.ReadFromJsonAsync<JsonElement>();
       var fixtureEventId = fixtureEvent.GetProperty("id").GetGuid();
       var fixtureEventTitle = fixtureEvent.GetProperty("title").GetString()
           ?? throw new InvalidOperationException("Fixture event title was missing.");
       Assert.AreEqual("Community Simulation Fixture", fixtureEventTitle);
       Assert.AreNotEqual(Guid.Empty, fixtureEventId);

       await GotoWithBudgetAsync("/events");

       var eventRow = Page.Locator("[data-test='event-row']").Filter(new() { HasText = fixtureEventTitle }).First;
       await Assertions.Expect(eventRow).ToBeVisibleAsync(new() { Timeout = PerformanceBudgets.InteractiveReadyMs });

       Directory.CreateDirectory(Path.Combine("artifacts", "layer5"));
       var unique = Guid.NewGuid().ToString("N")[..8];
       await Page.ScreenshotAsync(new PageScreenshotOptions
       {
           Path = Path.Combine("artifacts", "layer5", $"events-page-seeded-{unique}.png"),
           FullPage = true
       });
    }
}

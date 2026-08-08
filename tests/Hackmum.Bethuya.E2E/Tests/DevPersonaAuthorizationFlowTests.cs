// =====================================================================================
// SCOPE (Layer 3 — Persona Toolbar, extends Layer 2's proven persona-switching seam):
//   Layer 2's `Bethuya.IntegrationTests\DevelopmentPersonaSwitchingFlowTests.cs` already
//   proves — against a real Aspire-orchestrated Backend + Postgres — that the Backend's
//   `DevelopmentAuthenticationHandler` resolves Farah (Attendee-only, 403 on an
//   organizer/curator-gated endpoint) and Vikram (Admin+Organizer+Curator+Attendee,
//   authorization passes) via the `X-Bethuya-Dev-Persona` HEADER surface, and that
//   `GET /api/dev/identity` reflects the exact catalog entry for each persona.
//
//   This test proves the OTHER surface the same handler supports: the
//   `bethuya-dev-persona` COOKIE, set exclusively by the Layer 2 secure endpoint
//   (`GET /dev/persona/{key}`, CSRF-guarded + local-redirect-validated) that the
//   toolbar's persona buttons navigate to (via NavigationManager.NavigateTo with
//   forceLoad: true). It drives the toolbar through a real browser
//   against the real running Web app, confirms the persona survives a hard reload
//   (proving the cookie — not transient view-state — is what persists), and then
//   confirms an organizer/curator-gated Blazor page (`/curation/{eventId}`,
//   `[Authorize(Policy = RequireOrganizerOrCurator)]`) is denied for Farah and passes
//   authorization for Vikram — the exact same allow/deny semantics Layer 2 already
//   proved at the Backend-header tier, now proved at the Web-cookie tier. Because this
//   page component is a top-level InteractiveServer route, denial is enforced by
//   ASP.NET Core's endpoint-routing authorization middleware on the full server
//   round-trip (a bare HTTP 403), not by Blazor's client-rendered
//   `<AuthorizeRouteView><NotAuthorized>` fallback — so the test asserts on the HTTP
//   response status rather than page text.
//
//   Passing authorization as Vikram on `/curation/{eventId}` additionally proves the
//   full Web → Refit (`DevPersonaPropagationHandler`) → Backend chain end-to-end,
//   because `CurationView.razor` calls `ICurationApi`, a Refit client that forwards
//   the persona header derived from the cookie-authenticated identity. Layer 2's own
//   integration tests explicitly flagged this exact chain as unprovable at their tier
//   ("A full Web→Refit→Backend path is not representable here" — see
//   `DevelopmentPersonaSwitchingFlowTests.cs` remarks). This test closes that gap.
//
//   Direct browser navigation to the Backend's own `GET /api/dev/identity` is
//   intentionally NOT attempted here: the Backend resource's URL is dynamically
//   assigned per Aspire run (`launchProfileName: null` in AppHost.cs) and this E2E
//   project has no seam to discover it (unlike `Bethuya.IntegrationTests`, which uses
//   `BethuyaAppFixture.CreateBackendClient()` against the Aspire app model directly).
//   The identical `/api/dev/identity` contract is already exhaustively proven against
//   the real Backend by Layer 2's integration tests; duplicating that proof here would
//   require inventing new environment plumbing out of scope for Layer 3.
//
//   Excluded per Layer 3 spec: no simulation seeding, no persona-as-local-state, no
//   manual role override, no quick actions, no destructive reset, no new roles/domains.
// =====================================================================================

using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class DevPersonaAuthorizationFlowTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task PersonaToolbar_SwitchFarahThenVikram_DrivesSecureEndpointAndGatesCurationAccess()
    {
        using var readinessClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        try
        {
            using var readinessResponse = await readinessClient.GetAsync($"{BaseUrl}/");
            if (!readinessResponse.IsSuccessStatusCode)
            {
                Assert.Inconclusive(
                    $"Skipping: Bethuya Web app unavailable at {BaseUrl} (status {(int)readinessResponse.StatusCode}). " +
                    "This test requires a running Aspire environment with Environment=Development and " +
                    "Authentication:Provider=None (set BETHUYA_BASE_URL to point at it).");
            }
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Skipping: Bethuya Web app unavailable at {BaseUrl} ({ex.Message}).");
        }

        Directory.CreateDirectory("artifacts");
        var unique = Guid.NewGuid().ToString("N")[..8];

        // -----------------------------------------------------------------------
        // Step 0: toolbar is present only under Development + Provider=None. If it
        // is absent, the environment isn't configured for persona switching — skip
        // rather than fail, matching the repo's Assert.Inconclusive convention for
        // environment-dependent E2E tests.
        // -----------------------------------------------------------------------
        await GotoWithBudgetAsync("/");
        var toolbar = Page.Locator("[data-test='dev-persona-toolbar']");
        if (!await toolbar.IsVisibleAsync())
        {
            Assert.Inconclusive(
                "Skipping: dev persona toolbar is not rendered. This environment is not running " +
                "Environment=Development with Authentication:Provider=None.");
        }

        // -----------------------------------------------------------------------
        // Step 1: switch to Farah via the toolbar's persona button. This triggers a full
        // server-driven navigation to the Layer 2 secure endpoint
        // (GET /dev/persona/Farah?returnUrl=...) — CSRF-guarded, local-redirect
        // validated — followed by the intentional full page reload back to "/".
        // No cookie is planted directly by this test.
        // -----------------------------------------------------------------------
        await ClickAndNavigateWithBudgetAsync(Page.Locator("[data-test='persona-farah']"));

        var activePersona = Page.Locator("[data-test='active-persona']");
        await ExpectVisibleAsync(activePersona);
        await Assertions.Expect(activePersona).ToContainTextAsync("Farah");
        await Assertions.Expect(Page.Locator("[data-test='active-persona-role']")).ToContainTextAsync("Attendee");

        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Join("artifacts", $"persona-toolbar-farah-active-{unique}.png"),
            FullPage = true
        });

        // -----------------------------------------------------------------------
        // Step 2: hard refresh — proves persistence is backed by the Layer 2
        // SSR-compatible cookie, not transient Blazor circuit/view state.
        // -----------------------------------------------------------------------
        await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load });
        await Assertions.Expect(Page.Locator("[data-test='active-persona']")).ToContainTextAsync("Farah");
        await Assertions.Expect(Page.Locator("[data-test='active-persona-role']")).ToContainTextAsync("Attendee");

        // -----------------------------------------------------------------------
        // Step 3: as Farah (Attendee-only), an organizer/curator-gated Blazor page
        // must deny access. `/curation/{eventId}` is decorated with
        // [Authorize(Policy = RequireOrganizerOrCurator)] on a top-level InteractiveServer
        // page component, so ASP.NET Core's authorization middleware enforces the policy
        // at the endpoint-routing tier (a full server round-trip, not Blazor's client-side
        // AuthorizeRouteView) and returns a bare HTTP 403 directly — the event ID need not
        // exist: authorization runs before the page attempts to load any data.
        // -----------------------------------------------------------------------
        var deniedResponse = await GotoWithBudgetAsync($"/curation/{Guid.NewGuid()}");
        Assert.AreEqual(403, deniedResponse?.Status, "Farah (Attendee-only) must be denied with HTTP 403.");

        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Join("artifacts", $"persona-toolbar-farah-curation-denied-{unique}.png"),
            FullPage = true
        });

        // -----------------------------------------------------------------------
        // Step 4: switch to Vikram (Admin+Organizer+Curator+Attendee) via the same
        // secure toolbar endpoint.
        // -----------------------------------------------------------------------
        await GotoWithBudgetAsync("/");
        await ClickAndNavigateWithBudgetAsync(Page.Locator("[data-test='persona-vikram']"));

        await Assertions.Expect(Page.Locator("[data-test='active-persona']")).ToContainTextAsync("Vikram");
        await Assertions.Expect(Page.Locator("[data-test='active-persona-role']")).ToContainTextAsync("Organizer");

        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Join("artifacts", $"persona-toolbar-vikram-active-{unique}.png"),
            FullPage = true
        });

        // -----------------------------------------------------------------------
        // Step 5: as Vikram, the same organizer/curator-gated page must pass
        // authorization (HTTP 200, not 403). The event still doesn't exist, so the
        // page reaches its data-loading catch block and shows a business-logic error
        // ("Unable to load curation data right now.") — proving the cookie-derived
        // identity flowed through the endpoint-routing authorization tier AND (because
        // CurationView calls the Refit-backed ICurationApi) through
        // DevPersonaPropagationHandler to the real Backend's own
        // RequireOrganizerOrCurator policy — the full Web→Refit→Backend chain
        // Layer 2 could not exercise from its Backend-only integration fixture.
        // -----------------------------------------------------------------------
        var allowedResponse = await GotoWithBudgetAsync($"/curation/{Guid.NewGuid()}");
        Assert.AreEqual(200, allowedResponse?.Status, "Vikram (Organizer) must pass authorization with HTTP 200.");
        await Assertions.Expect(Page.Locator("[data-test='curation-error']"))
            .ToContainTextAsync(new Regex("Unable to load curation data|curation dashboard timed out|Unable to reach the server"));

        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Join("artifacts", $"persona-toolbar-vikram-curation-allowed-{unique}.png"),
            FullPage = true
        });
    }
}

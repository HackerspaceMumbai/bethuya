// =====================================================================================
// EXECUTION STATUS (independently re-verified by Squad Coordinator)
// Build: SUCCEEDED (dotnet build, 0 errors, 0 warnings) — see commit on this branch.
// Run:   EXECUTED FOR REAL against Aspire-orchestrated Backend + Postgres (Docker 29.6.2).
//        `dotnet test tests\Bethuya.IntegrationTests` → 9/9 passed (all tests in this
//        project, including this file's 5 persona-switching tests), 0 failed, ~61s.
//        This is genuine end-to-end proof, not a build-only confirmation: Farah's 403,
//        Vikram's 404-after-auth-pass, and the persisted Decision.DecidedBy ==
//        "vikram@bethuya.dev" assertion all passed against a live containerized Backend.
//
// STRUCTURED LOG VERIFICATION (sub-deliverable 5):
//   No seam exists in BethuyaAppFixture to capture Backend console/structured logs at the
//   Aspire/Postgres integration tier. Log verification for the Layer 2 persona resolution
//   (LoggerMessage.Define EventId 3100 "s_personaResolved" and EventId 3101 "s_personaUnknown")
//   relies exclusively on Tank's unit-test-level proof in
//   `tests/Hackmum.Bethuya.Tests/Auth/DevelopmentPersonaSwitchingTests.cs`, which validates
//   the structured log fields via a fake ILogger capture. No new logging infrastructure was
//   invented for this integration tier — doing so would be out of scope (Layer 2 spec §5).
//
// SCOPE LIMITATION (same as Layer 1 — DevelopmentAuthenticationFlowTests.cs):
//   This project references only AppHost. BethuyaAppFixture exposes an HttpClient only for the
//   `backend` resource. There is no seam to drive the Blazor `web` resource's rendered UI or
//   its Refit-issued HTTP calls (including DevPersonaPropagationHandler) from this test project.
//   These tests exercise the Backend directly, which is exactly the right layer to prove:
//   (a) the Backend's DevelopmentAuthenticationHandler interprets the persona header correctly,
//   (b) the RequireOrganizerOrCurator policy produces different outcomes for Farah vs Vikram,
//   (c) Decision.DecidedBy records the persona email when a decision is applied as Vikram.
//   A full Web→Refit→Backend path is not representable here — that seam is proven at the
//   unit-test level by DevPersonaPropagationHandler's DelegatingHandler tests.
// =====================================================================================

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bethuya.IntegrationTests;

/// <summary>
/// Integration tests proving Layer 2 persona switching (Developer Identity Switching) against
/// the real Aspire-orchestrated Backend with Postgres. Companion to
/// <c>DevelopmentAuthenticationFlowTests</c> (Layer 1) — uses the identical fixture pattern.
/// </summary>
/// <remarks>
/// <para>
/// Header/cookie constant strings are deliberately duplicated here rather than imported from
/// <c>ServiceDefaults.Auth.DevelopmentPersonaCatalog</c> per BP6 (anti-regression contract):
/// a rename in ServiceDefaults that breaks this test project signals a breaking API contract.
/// </para>
/// <para>
/// Persona catalog values (subjects, emails, roles) are also hardcoded for the same reason:
/// a change to the catalog must visibly break this test file, not silently pass.
/// </para>
/// </remarks>
[ClassDataSource<BethuyaAppFixture>(Shared = SharedType.PerTestSession)]
public sealed class DevelopmentPersonaSwitchingFlowTests(BethuyaAppFixture fixture)
{
    // BP6: Mirror DevelopmentPersonaCatalog constants without importing ServiceDefaults
    private const string PersonaHeaderName = "X-Bethuya-Dev-Persona";

    // -----------------------------------------------------------------------
    // Sub-deliverable 1: Farah (Attendee-only) → 403 on curation endpoint
    // -----------------------------------------------------------------------

    [Test]
    public async Task CurationGet_FarahPersona_Returns403Forbidden()
    {
        // Farah holds only the Attendee role. /api/curation/{eventId} requires
        // RequireOrganizerOrCurator. The Backend should deny her with 403 — even
        // before reaching the business logic that would otherwise return 404 for
        // a never-seeded eventId.
        using var client = fixture.CreateBackendClient();
        client.DefaultRequestHeaders.Add(PersonaHeaderName, "Farah");

        var response = await client.GetAsync($"/api/curation/{Guid.NewGuid()}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // Sub-deliverable 2 (authorization gate only): Vikram passes auth → 404
    // The full Decision.DecidedBy proof is in the seeded test below.
    // -----------------------------------------------------------------------

    [Test]
    public async Task CurationGet_VikramPersona_PassesAuthorizationGate()
    {
        // Vikram holds Admin + Organizer + Curator + Attendee. The same endpoint
        // should pass authorization and then 404 on the missing random event —
        // the opposite of Farah's 403 (DevelopmentAuthenticationFlowTests.cs already
        // characterizes the fixed-dev-admin variant of this pattern; this extends it
        // to the Vikram persona, proving policy parity end-to-end on the real Backend).
        using var client = fixture.CreateBackendClient();
        client.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");

        var response = await client.GetAsync($"/api/curation/{Guid.NewGuid()}");

        // 404 = auth passed; business logic reports the unseen event as not found
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------------
    // Sub-deliverable 3: Decision.DecidedBy reflects selected persona
    // This also exercises the "Vikram → success" seeded path.
    // -----------------------------------------------------------------------

    [Test]
    public async Task CurationDecision_VikramPersona_PersistsDecidedByAsVikramEmail()
    {
        // Step 1 — Seed a minimal Event + Registrations via the dev seeder.
        //   reviewableCount=26 is the minimum accepted by CurationSampleSeeder.SeedAsync
        //   (Math.Clamp clamps anything below SandboxCapacity+1 = 26 up to 26).
        using var seedClient = fixture.CreateBackendClient();
        var seedResponse = await seedClient.PostAsync(
            "/api/dev/curation/seed?reviewableCount=26", null);
        await Assert.That(seedResponse.IsSuccessStatusCode).IsTrue();

        var seed = await seedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = seed.GetProperty("eventId").GetGuid();

        // Step 2 — Retrieve the curation dashboard as Vikram to discover a reviewable
        //   registrant's ID. The seeder always produces at least one Pending registrant.
        using var vikramClient = fixture.CreateBackendClient();
        vikramClient.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");

        var dashboard = await vikramClient.GetFromJsonAsync<JsonElement>(
            $"/api/curation/{eventId}");

        // RegistrationId is a Vogen value object wrapping Guid; its System.Text.Json
        // converter serialises it as a plain Guid string in the JSON response.
        var registrationId = dashboard
            .GetProperty("registrants")[0]
            .GetProperty("registrationId")
            .GetGuid();

        // Step 3 — Apply a curation decision as Vikram.
        var decisionResponse = await vikramClient.PostAsJsonAsync(
            $"/api/curation/{eventId}/registrants/{registrationId}/decision",
            new { action = "approve", reason = "integration-test-persona-verify" });
        await Assert.That(decisionResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Step 4 — Read back the persisted Decision record via GET /api/approvals.
        //   Note: /api/approvals has no RequireAuthorization — accessible to any identity.
        //   Entity type is "registration" (hard-coded in CurationEndpoints.cs line ~142).
        using var readClient = fixture.CreateBackendClient();
        var decisions = await readClient.GetFromJsonAsync<JsonElement>(
            $"/api/approvals/registration/{registrationId}");

        var decidedBy = decisions[0].GetProperty("decidedBy").GetString();

        // THE CRITICAL ASSERTION: DecidedBy must be Vikram's catalog email,
        // not the fixed dev-admin email ("dev@bethuya.local").
        // This proves the "at least one persisted Decision.DecidedBy path records the
        // selected persona identity" exit-gate requirement from the Layer 2 spec.
        await Assert.That(decidedBy).IsEqualTo("vikram@bethuya.dev");
    }

    // -----------------------------------------------------------------------
    // Sub-deliverable 4a: GET /api/dev/identity → Farah catalog entry
    // -----------------------------------------------------------------------

    [Test]
    public async Task IdentityDiagnostic_FarahPersona_ReturnsExactFarahCatalogEntry()
    {
        // Proves gate (a) "persona changes observable in Backend authentication" for Farah
        // against the real Aspire-run Backend, not just an in-process TestServer.
        using var client = fixture.CreateBackendClient();
        client.DefaultRequestHeaders.Add(PersonaHeaderName, "Farah");

        var identity = await client.GetFromJsonAsync<JsonElement>("/api/dev/identity");

        await Assert.That(identity.GetProperty("sub").GetString())
            .IsEqualTo("dev-persona-farah");
        await Assert.That(identity.GetProperty("email").GetString())
            .IsEqualTo("farah@bethuya.dev");

        var roles = identity.GetProperty("roles")
            .EnumerateArray()
            .Select(r => r.GetString()!)
            .ToArray();

        // Farah has exactly one role: Attendee
        await Assert.That(roles.Length).IsEqualTo(1);
        await Assert.That(roles[0]).IsEqualTo("Attendee");
    }

    // -----------------------------------------------------------------------
    // Sub-deliverable 4b: GET /api/dev/identity → Vikram catalog entry (all 4 roles)
    // -----------------------------------------------------------------------

    [Test]
    public async Task IdentityDiagnostic_VikramPersona_ReturnsAllFourRoles()
    {
        // Proves gate (a) "persona changes observable in Backend authentication" for Vikram.
        // Vikram's full role set (Admin, Organizer, Curator, Attendee) must be present.
        using var client = fixture.CreateBackendClient();
        client.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");

        var identity = await client.GetFromJsonAsync<JsonElement>("/api/dev/identity");

        await Assert.That(identity.GetProperty("sub").GetString())
            .IsEqualTo("dev-persona-vikram");
        await Assert.That(identity.GetProperty("email").GetString())
            .IsEqualTo("vikram@bethuya.dev");

        var roles = identity.GetProperty("roles")
            .EnumerateArray()
            .Select(r => r.GetString()!)
            .OrderBy(r => r)
            .ToArray();

        // All four roles — order-insensitive via Sort
        await Assert.That(roles.Length).IsEqualTo(4);
        await Assert.That(roles[0]).IsEqualTo("Admin");
        await Assert.That(roles[1]).IsEqualTo("Attendee");
        await Assert.That(roles[2]).IsEqualTo("Curator");
        await Assert.That(roles[3]).IsEqualTo("Organizer");
    }
}

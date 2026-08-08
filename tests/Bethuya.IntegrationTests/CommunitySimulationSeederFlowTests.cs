// =====================================================================================
// SCOPE LIMITATION (same as DevelopmentPersonaSwitchingFlowTests.cs):
//   This project references only AppHost. BethuyaAppFixture exposes an HttpClient only for the
//   `backend` resource. There is no seam to drive the Blazor `web` resource from this test project.
//   These tests exercise the Backend directly — which is the right layer to prove:
//   (a) the /api/dev/community-simulation/seed endpoint enforces RequireOrganizer authorization,
//   (b) seeding produces stable, bounded counts against real Postgres,
//   (c) the seeded data is visible through existing Community Passport and dashboard read-model APIs.
//   A "non-Development" execution test is not feasible here because BethuyaAppFixture always starts
//   the AppHost in Development mode. The method-level IsDevelopment() guard in MapDevelopmentEndpoints()
//   already enforces this invariant at startup — no fake test is introduced.
//
// NOTE ON DB STATE:
//   These tests do NOT reset the database between runs (no Respawn). The seeder is idempotent so
//   tests work whether the DB is fresh or already has community simulation data. The idempotency
//   proof test (CalledTwice) seeds twice within a single test method to prove same-call idempotency.
// =====================================================================================

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bethuya.IntegrationTests;

/// <summary>
/// Integration tests proving the Layer 4 Community Simulation Seeder against the real
/// Aspire-orchestrated Backend with Postgres.
/// </summary>
/// <remarks>
/// BP6: persona header name and catalog values are hardcoded here rather than imported from
/// ServiceDefaults.Auth so a breaking rename fails loudly in this test file.
/// </remarks>
[ClassDataSource<BethuyaAppFixture>(Shared = SharedType.PerTestSession)]
public sealed class CommunitySimulationSeederFlowTests(BethuyaAppFixture fixture) : IAsyncDisposable
{
    // BP6: Mirror DevelopmentPersonaCatalog constants without importing ServiceDefaults
    private const string PersonaHeaderName = "X-Bethuya-Dev-Persona";

    private static readonly string[] ExpectedPersonaKeys =
        ["Anish", "Farah", "Maya", "Priya", "Rohan", "Vikram"];

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Authorization: Farah (Attendee-only) → 403 Forbidden
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CommunitySimulationSeed_FarahPersona_Returns403Forbidden()
    {
        using var client = fixture.CreateBackendClient();
        client.DefaultRequestHeaders.Add(PersonaHeaderName, "Farah");

        var response = await client.PostAsync("/api/dev/community-simulation/seed", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Success: Vikram (Organizer) → 200 with all 6 persona keys
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CommunitySimulationSeed_VikramPersona_Returns200WithAllSixPersonaKeys()
    {
        using var client = fixture.CreateBackendClient();
        client.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");

        var response = await client.PostAsync("/api/dev/community-simulation/seed", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(result.GetProperty("personasProvisioned").GetInt32()).IsEqualTo(6);

        var personaKeys = result.GetProperty("personaKeys")
            .EnumerateArray()
            .Select(k => k.GetString()!)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();
        await Assert.That(personaKeys.Length).IsEqualTo(6);
        for (var i = 0; i < ExpectedPersonaKeys.Length; i++)
        {
            await Assert.That(personaKeys[i]).IsEqualTo(ExpectedPersonaKeys[i]);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Idempotency: second consecutive call produces zero new rows
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CommunitySimulationSeed_CalledTwice_SecondCallProducesZeroNewRows()
    {
        using var client = fixture.CreateBackendClient();
        client.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");

        // First call — seeds or finds existing data
        var response1 = await client.PostAsync("/api/dev/community-simulation/seed", null);
        await Assert.That(response1.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        var firstCallTotalMembers = result1.GetProperty("membersCreated").GetInt32()
                                   + result1.GetProperty("membersAlreadyExisted").GetInt32();
        await Assert.That(firstCallTotalMembers).IsEqualTo(6);

        // Second call — must find all rows already present
        var response2 = await client.PostAsync("/api/dev/community-simulation/seed", null);
        await Assert.That(response2.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result2 = await response2.Content.ReadFromJsonAsync<JsonElement>();

        await Assert.That(result2.GetProperty("membersCreated").GetInt32()).IsEqualTo(0);
        await Assert.That(result2.GetProperty("membersAlreadyExisted").GetInt32()).IsEqualTo(6);
        await Assert.That(result2.GetProperty("ledgerEntriesCreated").GetInt32()).IsEqualTo(0);
        await Assert.That(result2.GetProperty("registrationsCreated").GetInt32()).IsEqualTo(0);
        await Assert.That(result2.GetProperty("registrationsAlreadyExisted").GetInt32()).IsEqualTo(6);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4a. Seeded data visible through Community Passport journey API — Anish
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CommunitySimulationSeed_AnishPersona_JourneyApiShowsParticipationSignals()
    {
        using var seedClient = fixture.CreateBackendClient();
        seedClient.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");
        var seedResponse = await seedClient.PostAsync("/api/dev/community-simulation/seed", null);
        await Assert.That(seedResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var anishClient = fixture.CreateBackendClient();
        anishClient.DefaultRequestHeaders.Add(PersonaHeaderName, "Anish");
        var journeyResponse = await anishClient.GetAsync("/api/community/passport/journey?timelineLimit=20");
        await Assert.That(journeyResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var journey = await journeyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var timelineCount = journey.GetProperty("timeline").GetArrayLength();
        await Assert.That(timelineCount).IsGreaterThan(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4b. Seeded data visible through journey API — Priya (Curator)
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CommunitySimulationSeed_PriyaPersona_JourneyApiShowsParticipationSignals()
    {
        using var seedClient = fixture.CreateBackendClient();
        seedClient.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");
        await seedClient.PostAsync("/api/dev/community-simulation/seed", null);

        using var priyaClient = fixture.CreateBackendClient();
        priyaClient.DefaultRequestHeaders.Add(PersonaHeaderName, "Priya");
        var journeyResponse = await priyaClient.GetAsync("/api/community/passport/journey?timelineLimit=20");
        await Assert.That(journeyResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var journey = await journeyResponse.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(journey.GetProperty("timeline").GetArrayLength()).IsGreaterThan(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4c. Seeded data visible through journey API — Vikram (Organizer)
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CommunitySimulationSeed_VikramPersona_JourneyApiShowsParticipationSignals()
    {
        using var vikramClient = fixture.CreateBackendClient();
        vikramClient.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");
        await vikramClient.PostAsync("/api/dev/community-simulation/seed", null);

        var journeyResponse = await vikramClient.GetAsync("/api/community/passport/journey?timelineLimit=20");
        await Assert.That(journeyResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var journey = await journeyResponse.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(journey.GetProperty("timeline").GetArrayLength()).IsGreaterThan(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Dashboard read-model (Vikram only) shows seeded data
    //    Response shape: { retention: { currentlyActiveMembers }, attendance: {}, ... }
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CommunitySimulationSeed_DashboardReadModel_ReflectsSeededData()
    {
        using var vikramClient = fixture.CreateBackendClient();
        vikramClient.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");
        await vikramClient.PostAsync("/api/dev/community-simulation/seed", null);

        var dashboardResponse = await vikramClient.GetAsync(
            "/api/community/passport/dashboard/read-model?lookbackDays=90");
        await Assert.That(dashboardResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<JsonElement>();
        var currentlyActiveMembers = dashboard
            .GetProperty("retention")
            .GetProperty("currentlyActiveMembers")
            .GetInt32();
        await Assert.That(currentlyActiveMembers).IsGreaterThanOrEqualTo(6);
    }

}

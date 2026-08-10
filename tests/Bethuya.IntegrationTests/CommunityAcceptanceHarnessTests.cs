// =====================================================================================
// LAYER 5: COMMUNITY ACCEPTANCE TEST HARNESS
// 
// This harness proves that the deterministic community simulation seeding (Layer 4)
// enables repeatable acceptance tests proving:
//   1. Stable persona identifiers and deterministic counts
//   2. Idempotent reseeding (no duplicates)
//   3. Persona persistence through existing APIs (Passport/Dashboard)
//   4. Authorization differences (Farah→403, Vikram→200)
//   5. Stable fixture participation in all six persona journey reads
//
// SCOPE BOUNDARY: Tests only current models/APIs. No Graph/Chapters/Projects/Mentorship.
// No expanded datasets (1000+), Bogus, or Layer 6 work.
// =====================================================================================

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TUnit.Core.Interfaces;

namespace Bethuya.IntegrationTests;

/// <summary>
/// Acceptance tests proving the Community Acceptance Test Harness: deterministic seeding,
/// persona persistence, and authorization.
/// </summary>
[ClassDataSource<BethuyaAppFixture>(Shared = SharedType.PerTestSession)]
public sealed class CommunityAcceptanceHarnessTests(BethuyaAppFixture fixture) : IAsyncDisposable
{
    // BP6: Mirror DevelopmentPersonaCatalog constants without importing ServiceDefaults
    private const string PersonaHeaderName = "X-Bethuya-Dev-Persona";
    
    // Reuse the shared persona key array from the fixture to avoid duplication
    private static readonly string[] AllPersonaKeys = CommunityAcceptanceHarnessFixture.AllPersonaKeys;

    private CommunityAcceptanceHarnessFixture? _harness;

    private CommunityAcceptanceHarnessFixture Harness =>
        _harness ??= new CommunityAcceptanceHarnessFixture(fixture);

    public ValueTask DisposeAsync()
    {
        _harness?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST GROUP 1: Deterministic Seeding & Idempotency
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proves that the seeded dataset contains all six expected personas with
    /// deterministic identifiers.
    /// </summary>
    [Test]
    public async Task Harness_Seed_ProvisionsAllSixPersonasWithStableIdentifiers()
    {
        await Harness.SeedAsync();

        // Do NOT dispose the client - the fixture manages client lifetime
        var client = Harness.GetPersonaClient("Vikram");
        var response = await client.PostAsync("/api/dev/community-simulation/seed", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        var personaKeys = result.GetProperty("personaKeys")
            .EnumerateArray()
            .Select(k => k.GetString()!)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var expectedKeys = AllPersonaKeys
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        await Assert.That(personaKeys.Count).IsEqualTo(expectedKeys.Count);
        for (var i = 0; i < expectedKeys.Count; i++)
        {
            await Assert.That(personaKeys[i]).IsEqualTo(expectedKeys[i]);
        }
    }

    /// <summary>
    /// Proves that reseeding produces identical logical counts (no duplicates).
    /// </summary>
    [Test]
    public async Task Harness_ReseedTwice_SecondSeedProducesZeroNewRows()
    {
        // First seed via harness
        await Harness.SeedAsync();

        // Do NOT dispose the client - the fixture manages client lifetime
        var client = Harness.GetPersonaClient("Vikram");

        // Second explicit seed to prove idempotency
        var response2 = await client.PostAsync("/api/dev/community-simulation/seed", null);
        await Assert.That(response2.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var result2 = await response2.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(result2.GetProperty("membersCreated").GetInt32()).IsEqualTo(0);
        await Assert.That(result2.GetProperty("membersAlreadyExisted").GetInt32()).IsEqualTo(6);
        await Assert.That(result2.GetProperty("registrationsCreated").GetInt32()).IsEqualTo(0);
        await Assert.That(result2.GetProperty("registrationsAlreadyExisted").GetInt32()).IsEqualTo(6);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST GROUP 2: Persona Persistence Through Existing APIs
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proves that seeded personas are persisted and visible through the
    /// Community Passport journey API (participation timeline). Tests all 6 personas.
    /// </summary>
    [Test]
    public async Task Harness_PassportJourney_ReturnsFixtureParticipationForAllPersonas()
    {
        await Harness.SeedAsync();
        var fixtureEvent = await Harness.GetEventByHashtagAsync("community-simulation-fixture");
        var fixtureEventId = fixtureEvent.GetProperty("id").GetGuid();
        var fixtureEventTitle = fixtureEvent.GetProperty("title").GetString()
            ?? throw new InvalidOperationException("Fixture event title was missing.");

        await Assert.That(fixtureEventTitle).IsEqualTo("Community Simulation Fixture");
        await Assert.That(fixtureEventId).IsNotEqualTo(Guid.Empty);

        // Test all six personas can see the seeded fixture participation in their own journey
        var journeys = await Task.WhenAll(AllPersonaKeys.Select(async personaKey =>
            (personaKey, journey: await Harness.GetPassportJourneyAsync(personaKey, timelineLimit: 100))));

        foreach (var (personaKey, journey) in journeys)
        {
            var timeline = journey.GetProperty("timeline");
            await Assert.That(timeline.GetArrayLength()).IsGreaterThan(0);

            var matchingEntries = timeline.EnumerateArray()
                .Where(entry =>
                    entry.GetProperty("eventTitle").GetString() == fixtureEventTitle &&
                    entry.GetProperty("eventId").GetGuid() == fixtureEventId)
                .ToList();

            await Assert.That(matchingEntries.Count).IsGreaterThan(0);
        }
    }

    /// <summary>
    /// Proves that the Community Passport dashboard read-model reflects seeded data
    /// and is accessible to Organizers.
    /// </summary>
    [Test]
    public async Task Harness_DashboardReadModel_ReflectsSeededMembersAndAttendance()
    {
        await Harness.SeedAsync();

        var dashboard = await Harness.GetDashboardReadModelAsync(
            "Vikram",
            lookbackDays: 90);

        var retention = dashboard.GetProperty("retention");
        var currentlyActiveMembers = retention.GetProperty("currentlyActiveMembers").GetInt32();

        // The seeded fixture currently yields at least two active members in the dashboard window.
        await Assert.That(currentlyActiveMembers).IsGreaterThanOrEqualTo(2);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST GROUP 3: Authorization Differences
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proves that Farah (Attendee role) cannot access the seeding endpoint
    /// and receives 403 Forbidden.
    /// </summary>
    [Test]
    public async Task Harness_FarahPersona_CannotSeedAndReceives403()
    {
        // Do NOT dispose the client - the fixture manages client lifetime
        var client = Harness.GetPersonaClient("Farah");
        var response = await client.PostAsync("/api/dev/community-simulation/seed", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Proves that Vikram (Organizer role) can successfully seed.
    /// </summary>
    [Test]
    public async Task Harness_VikramPersona_CanSeedAndReceives200()
    {
        // Do NOT dispose the client - the fixture manages client lifetime
        var client = Harness.GetPersonaClient("Vikram");
        var response = await client.PostAsync("/api/dev/community-simulation/seed", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    /// <summary>
    /// Proves that Farah (Attendee role) cannot access the dashboard read-model
    /// (which requires Organizer).
    /// </summary>
    [Test]
    public async Task Harness_FarahPersona_CannotAccessDashboard()
    {
        await Harness.SeedAsync();

        // Do NOT dispose the client - the fixture manages client lifetime
        var client = Harness.GetPersonaClient("Farah");
        var response = await client.GetAsync("/api/community/passport/dashboard/read-model?lookbackDays=90");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

}

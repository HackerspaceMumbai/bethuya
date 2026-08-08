namespace Bethuya.IntegrationTests;

using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// Acceptance test helper for Layer 5 that orchestrates deterministic seeding and
/// provides typed accessors to seeded persona clients and common API calls.
/// Reuses <see cref="BethuyaAppFixture"/> for HTTP client creation.
/// 
/// BP6: Persona header name and catalog values are hardcoded here rather than imported
/// from ServiceDefaults.Auth so a breaking rename fails loudly in this test fixture.
/// </summary>
public sealed class CommunityAcceptanceHarnessFixture
{
    private const string PersonaHeaderName = "X-Bethuya-Dev-Persona";
    
    // BP6: Mirror DevelopmentPersonaCatalog constants without importing ServiceDefaults
    // Alphabetical order for test clarity; these must match the deployed catalog
    public static readonly string[] AllPersonaKeys = ["Anish", "Farah", "Maya", "Priya", "Rohan", "Vikram"];

    private readonly BethuyaAppFixture _appFixture;
    private readonly Dictionary<string, HttpClient> _personaClients = [];
    private bool _seeded;

    public CommunityAcceptanceHarnessFixture(BethuyaAppFixture appFixture)
    {
        _appFixture = appFixture ?? throw new ArgumentNullException(nameof(appFixture));
    }

    /// <summary>
    /// Deterministically seeds community simulation data via POST /api/dev/community-simulation/seed
    /// as Vikram (Organizer). Idempotent — subsequent calls are safe.
    /// </summary>
    public async Task SeedAsync()
    {
        if (_seeded)
            return;

        using var seedClient = _appFixture.CreateBackendClient();
        seedClient.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");

        var response = await seedClient.PostAsync("/api/dev/community-simulation/seed", null);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Seeding failed with {response.StatusCode}: {content}");
        }

        _seeded = true;
    }

    /// <summary>Gets or creates an HttpClient pre-configured with a persona header.</summary>
    public HttpClient GetPersonaClient(string personaKey)
    {
        if (!_personaClients.TryGetValue(personaKey, out var client))
        {
            client = _appFixture.CreateBackendClient();
            client.DefaultRequestHeaders.Add(PersonaHeaderName, personaKey);
            _personaClients[personaKey] = client;
        }
        return client;
    }

    /// <summary>Gets the Community Passport journey (participation timeline) for a persona.</summary>
    public async Task<JsonElement> GetPassportJourneyAsync(string personaKey, int? timelineLimit = 20)
    {
        var client = GetPersonaClient(personaKey);
        var response = await client.GetAsync($"/api/community/passport/journey?timelineLimit={timelineLimit}");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Journey API failed: {response.StatusCode}");

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Gets the Community Passport dashboard read-model (retention/attendance stats).
    /// Requires Organizer role.
    /// </summary>
    public async Task<JsonElement> GetDashboardReadModelAsync(string personaKey, int lookbackDays = 90)
    {
        var client = GetPersonaClient(personaKey);
        var response = await client.GetAsync(
            $"/api/community/passport/dashboard/read-model?lookbackDays={lookbackDays}");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Dashboard API failed: {response.StatusCode}");

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Cleans up all created persona clients.</summary>
    public void Dispose()
    {
        foreach (var client in _personaClients.Values)
            client?.Dispose();
        _personaClients.Clear();
    }
}

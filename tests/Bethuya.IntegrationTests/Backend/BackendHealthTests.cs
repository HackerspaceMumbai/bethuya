namespace Bethuya.IntegrationTests.Backend;

/// <summary>
/// Integration tests for the backend health and liveness endpoints.
/// Demonstrates the TUnit + Aspire fixture pattern for all integration tests.
/// </summary>
[ClassDataSource<BethuyaAppFixture>(Shared = SharedType.PerTestSession)]
public sealed class BackendHealthTests(BethuyaAppFixture fixture)
{
    [Test]
    public async Task Backend_HealthEndpoint_Returns200()
    {
        using var client = fixture.CreateBackendClient();

        var response = await client.GetAsync("/health");

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
    }

    [Test]
    public async Task Backend_LivenessEndpoint_Returns200()
    {
        using var client = fixture.CreateBackendClient();

        var response = await client.GetAsync("/alive");

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
    }
}

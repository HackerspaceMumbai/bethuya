namespace Bethuya.IntegrationTests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Projects;
using TUnit.Core.Interfaces;

/// <summary>
/// Shared TUnit fixture that starts a real Aspire DistributedApplication for integration tests.
/// Use <c>[ClassDataSource&lt;BethuyaAppFixture&gt;(Shared = SharedType.PerTestSession)]</c> to share
/// one running instance across all tests in the session (avoids per-test startup cost).
/// </summary>
public sealed class BethuyaAppFixture : IAsyncInitializer, IAsyncDisposable
{
    private DistributedApplication? _app;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<AppHost>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();
        // BP3: Wait for the backend to be healthy before any test runs
        await _app.Services
            .GetRequiredService<ResourceNotificationService>()
            .WaitForResourceHealthyAsync("backend");
    }

    /// <summary>Creates an <see cref="HttpClient"/> pre-configured for the backend service.</summary>
    public HttpClient CreateBackendClient() =>
        _app?.CreateHttpClient("backend")
        ?? throw new InvalidOperationException("Fixture not initialized — InitializeAsync must complete before use.");

    /// <summary>Gets the SQL Server connection string for the BethuyaDb database.</summary>
    public async Task<string> GetSqlConnectionStringAsync() =>
        await _app!.GetConnectionStringAsync("BethuyaDb")
        ?? throw new InvalidOperationException("BethuyaDb connection string not found in Aspire resources.");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}

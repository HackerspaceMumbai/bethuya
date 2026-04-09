namespace Bethuya.IntegrationTests;

using Microsoft.Data.SqlClient;
using Respawn;
using TUnit.Core.Interfaces;

/// <summary>
/// Wraps <see cref="BethuyaAppFixture"/> to provide fast database reset between tests.
/// Prevents cross-test state pollution (BP5). Uses Respawn with SqlServer adapter.
/// </summary>
public sealed class DatabaseFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly BethuyaAppFixture _appFixture;
    private Respawner? _respawner;
    private string? _connectionString;

    public DatabaseFixture(BethuyaAppFixture appFixture)
    {
        _appFixture = appFixture;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connectionString = await _appFixture.GetSqlConnectionStringAsync();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["dbo"],
        });
    }

    /// <summary>Resets the database to a clean state. Call in a <c>[Before(Test)]</c> hook.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

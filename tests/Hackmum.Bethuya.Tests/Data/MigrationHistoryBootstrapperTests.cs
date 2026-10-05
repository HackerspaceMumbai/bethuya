using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Tests.Data;

public sealed class MigrationHistoryBootstrapperTests
{
    [Test]
    public async Task EnsureMigrationHistoryTableAsync_CreatesTableIdempotently()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new BethuyaDbContext(options);

        await dbContext.EnsureMigrationHistoryTableAsync();
        await dbContext.EnsureMigrationHistoryTableAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = '__EFMigrationsHistory';
            """;

        var tableCount = (long)(await command.ExecuteScalarAsync() ?? 0L);
        await Assert.That(tableCount).IsEqualTo(1L);
    }
}

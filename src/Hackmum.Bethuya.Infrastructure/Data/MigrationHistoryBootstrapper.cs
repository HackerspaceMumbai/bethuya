using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hackmum.Bethuya.Infrastructure.Data;

/// <summary>Creates the provider-specific EF migration history table without an error-producing existence probe.</summary>
public static class MigrationHistoryBootstrapper
{
    /// <summary>Ensures the EF migration history table exists before migrations inspect it.</summary>
    public static Task EnsureMigrationHistoryTableAsync(
        this BethuyaDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var historyRepository = dbContext.GetService<IHistoryRepository>();
        var createScript = historyRepository.GetCreateIfNotExistsScript();
        return dbContext.Database.ExecuteSqlRawAsync(createScript, cancellationToken);
    }
}

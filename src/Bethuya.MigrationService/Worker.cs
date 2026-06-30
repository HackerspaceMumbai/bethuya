using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bethuya.MigrationService;

/// <summary>
/// Applies all pending EF Core migrations, then signals the host to stop.
/// Safe on existing databases — already-applied migrations are skipped.
/// Aspire's WaitForCompletion ensures the backend does not start until this finishes.
/// </summary>
public sealed partial class MigrationWorker(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
            await EnsureSchemaAsync(dbContext, stoppingToken);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private async Task EnsureSchemaAsync(BethuyaDbContext dbContext, CancellationToken cancellationToken)
    {
        LogApplyingMigrations();
        await dbContext.Database.MigrateAsync(cancellationToken);
        LogSchemaReady();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying pending EF Core migrations.")]
    private partial void LogApplyingMigrations();

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is ready.")]
    private partial void LogSchemaReady();
}

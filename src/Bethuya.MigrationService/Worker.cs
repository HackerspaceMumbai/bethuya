using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bethuya.MigrationService;

/// <summary>
/// Applies pending EF Core database migrations then signals the host to stop.
/// Aspire's WaitForCompletion ensures the backend does not start until migrations finish.
/// </summary>
/// <remarks>
/// Before the first Azure deployment, generate migration files:
///   dotnet ef migrations add InitialCreate --project src/Hackmum.Bethuya.Infrastructure
///     --startup-project src/Hackmum.Bethuya.Backend
/// </remarks>
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
            await ApplyMigrationsAsync(dbContext, stoppingToken);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private async Task ApplyMigrationsAsync(BethuyaDbContext dbContext, CancellationToken cancellationToken)
    {
        LogCheckingMigrations();

        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
        {
            LogNoMigrations();
            return;
        }

        LogApplyingMigrations(pending.Count);
        await dbContext.Database.MigrateAsync(cancellationToken);
        LogMigrationsApplied();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Checking for pending database migrations...")]
    private partial void LogCheckingMigrations();

    [LoggerMessage(Level = LogLevel.Information, Message = "No pending migrations — database schema is up to date.")]
    private partial void LogNoMigrations();

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} pending migration(s).")]
    private partial void LogApplyingMigrations(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied successfully.")]
    private partial void LogMigrationsApplied();
}

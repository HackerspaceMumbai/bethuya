using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bethuya.MigrationService;

/// <summary>
/// Ensures the database and schema exist, then signals the host to stop.
/// Uses <see cref="DatabaseFacade.EnsureCreatedAsync"/> (no-migration mode) until the formal
/// release, when EF Core migrations will be introduced.
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
        LogEnsureSchema();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        LogSchemaReady();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ensuring database schema exists (EnsureCreated — migrations deferred to formal release).")]
    private partial void LogEnsureSchema();

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is ready.")]
    private partial void LogSchemaReady();
}

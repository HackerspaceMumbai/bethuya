using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hackmum.Bethuya.Infrastructure.Data;

/// <summary>
/// Design-time factory used by EF Core tools (dotnet ef migrations add / update-database).
/// A local Postgres instance or DATABASE_URL env var is used; connection string does not need
/// to match production — it is only required for the tooling to introspect the model.
/// </summary>
public sealed class BethuyaDbContextFactory : IDesignTimeDbContextFactory<BethuyaDbContext>
{
    public BethuyaDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=BethuyaDb";

        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(BethuyaDbContext).Assembly.GetName().Name))
            .Options;

        return new BethuyaDbContext(options);
    }
}

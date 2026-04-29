using Hackmum.Bethuya.Core.Data;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Data.PII;
using Hackmum.Bethuya.Infrastructure.Repositories;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hackmum.Bethuya.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds Bethuya Infrastructure services: EF Core DbContext (Azure SQL via Aspire),
    /// PII store (local SQLite), and repository implementations.
    /// </summary>
    public static IHostApplicationBuilder AddBethuyaInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddSqlServerDbContext<BethuyaDbContext>("BethuyaDb");

        // Register PII store (local SQLite — never cloud)
        builder.Services.AddDbContextFactory<PiiDbContext>(options =>
        {
            var piiDbPath = Path.Combine(Path.GetTempPath(), "pii_registrations.db");
            options.UseSqlite($"Data Source={piiDbPath};");
        });

        builder.Services.AddScoped<IEventRepository, EventRepository>();
        builder.Services.AddScoped<IRegistrationRepository, RegistrationRepository>();
        builder.Services.AddScoped<IDecisionRepository, DecisionRepository>();
        builder.Services.AddScoped<IPiiDataStore, PiiDataStore>();

        builder.Services.Configure<CloudinaryOptions>(
            builder.Configuration.GetSection(CloudinaryOptions.SectionName));
        builder.Services.AddSingleton<IImageUploadService, CloudinaryImageUploadService>();

        return builder;
    }
}

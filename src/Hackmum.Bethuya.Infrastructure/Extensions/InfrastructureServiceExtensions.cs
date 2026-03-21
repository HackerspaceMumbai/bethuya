using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hackmum.Bethuya.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds Bethuya Infrastructure services: EF Core DbContext (Azure SQL via Aspire) and repository implementations.
    /// </summary>
    public static IHostApplicationBuilder AddBethuyaInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddSqlServerDbContext<BethuyaDbContext>("BethuyaDb");

        builder.Services.AddScoped<IEventRepository, EventRepository>();
        builder.Services.AddScoped<IRegistrationRepository, RegistrationRepository>();
        builder.Services.AddScoped<IDecisionRepository, DecisionRepository>();

        return builder;
    }
}

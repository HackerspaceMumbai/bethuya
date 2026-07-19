using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Repositories;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hackmum.Bethuya.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds Bethuya Infrastructure services: EF Core DbContext (Postgres via Aspire) and repository implementations.
    /// </summary>
    public static IHostApplicationBuilder AddBethuyaInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<BethuyaDbContext>("BethuyaDb");

        builder.Services.AddScoped<IEventRepository, EventRepository>();
        builder.Services.AddScoped<IRegistrationRepository, RegistrationRepository>();
        builder.Services.AddScoped<IDecisionRepository, DecisionRepository>();
        builder.Services.AddScoped<IAttendeeProfileRepository, AttendeeProfileRepository>();

        builder.Services.Configure<SessionizeOptions>(
            builder.Configuration.GetSection(SessionizeOptions.SectionName));
        builder.Services
            .AddHttpClient<ISessionizeService, SessionizeService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SessionizeOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            });

        builder.Services.Configure<GitHubEventRepositoryOptions>(
            builder.Configuration.GetSection(GitHubEventRepositoryOptions.SectionName));
        builder.Services
            .AddHttpClient<IGitHubEventRepository, GitHubEventRepository>(client =>
            {
                client.BaseAddress = new Uri("https://api.github.com");
            });
        builder.Services.AddScoped<ITeamsNotificationService, NoOpTeamsNotificationService>();
        builder.Services.AddScoped<ILumaRegistrationService, NoOpLumaRegistrationService>();
        builder.Services.AddScoped<IWebCacheInvalidationService, NoOpWebCacheInvalidationService>();

        builder.Services.Configure<CloudinaryOptions>(
            builder.Configuration.GetSection(CloudinaryOptions.SectionName));
        builder.Services.AddScoped<IImageUploadService, CloudinaryImageUploadService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHostedService<PendingImageUploadCleanupService>();

        return builder;
    }
}

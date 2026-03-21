using Hackmum.Bethuya.AI.Configuration;
using Hackmum.Bethuya.AI.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hackmum.Bethuya.AI.Extensions;

public static class AIServiceExtensions
{
    /// <summary>
    /// Adds Bethuya AI provider routing to the service collection.
    /// Reads configuration from "AI" section of appsettings.
    /// </summary>
    public static IHostApplicationBuilder AddBethuyaAI(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<AIRoutingOptions>(
            builder.Configuration.GetSection(AIRoutingOptions.SectionName));

        builder.Services.AddSingleton<IAIRouter, AIRouter>();

        return builder;
    }
}

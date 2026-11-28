using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AppHost.Infrastructure;

public static class EnvironmentalPolicies
{
    public static bool IsLocalDevelopment(this IDistributedApplicationBuilder builder)
    {
        return !builder.ExecutionContext.IsPublishMode;
    }

    public static bool IsCloudDeployment(this IDistributedApplicationBuilder builder)
    {
        return builder.ExecutionContext.IsPublishMode;
    }

    public static bool ShouldEnableOnboardingBypass(
        this IDistributedApplicationBuilder builder,
        bool enableOnboardingFlowInDevelopment)
    {
        return IsLocalDevelopment(builder) && !enableOnboardingFlowInDevelopment;
    }

    public static string ResolveBuildConfiguration(Assembly? assembly = null)
    {
        var configuration = (assembly ?? typeof(EnvironmentalPolicies).Assembly)
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?
            .Configuration;

        return string.IsNullOrWhiteSpace(configuration) ? "Debug" : configuration;
    }

    public static string ResolveWebStaticAssetsManifestPath(
        string appHostDirectory,
        string buildConfiguration)
    {
        return Path.GetFullPath(Path.Combine(
            appHostDirectory,
            "..",
            "..",
            "src",
            "Bethuya.Hybrid",
            "Bethuya.Hybrid.Web",
            "obj",
            buildConfiguration,
            "net10.0",
            "staticwebassets.development.json"));
    }
}

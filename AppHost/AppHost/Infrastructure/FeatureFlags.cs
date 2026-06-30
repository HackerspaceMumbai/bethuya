namespace AppHost.Infrastructure;

public static class FeatureFlags
{
    public static bool EnableAgents(
        this IDistributedApplicationBuilder builder)
    {
        // Agents enabled ONLY in local/dev for now
        return builder.ExecutionContext.IsRunMode;
    }

    public static bool EnableCloudInfrastructure(
        this IDistributedApplicationBuilder builder)
    {
        return builder.ExecutionContext.IsPublishMode;
    }
}
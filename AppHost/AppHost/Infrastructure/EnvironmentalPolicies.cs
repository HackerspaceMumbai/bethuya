using System;
using System.Collections.Generic;
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

    public static bool ShouldEnableOnboardingBypass(this IDistributedApplicationBuilder builder, bool EnableInDevelopment)
    {
      return EnableInDevelopment && IsLocalDevelopment(builder);
    }
}

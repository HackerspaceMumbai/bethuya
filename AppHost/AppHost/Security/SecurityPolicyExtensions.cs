using System;
using System.Collections.Generic;
using System.Text;

namespace AppHost.Security;

public static class SecurityPolicyExtensions
{
    public static void EnforceProductionSecurityPolicies(this IDistributedApplicationBuilder builder, bool onboardingBypassEnabled)
    {
        if (builder.ExecutionContext.IsPublishMode && onboardingBypassEnabled)
        {
            // Enforce HTTPS in production
            throw new InvalidOperationException(
                "Onboarding bypass cannot be enabled in production." );
        }
    }
}

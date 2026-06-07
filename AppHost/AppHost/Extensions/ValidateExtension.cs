using System;
using Aspire.Hosting.Azure;

namespace AppHost.Extensions;
public static class ValidationExtensions
{
	public static void ValidateRequired(this IDistributedApplicationBuilder builder, string? value, string configurationName)
    {
        if(string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required configuration: '{configurationName}'");

        
    }
}

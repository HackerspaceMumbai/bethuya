using System;
using System.Collections.Generic;
using System.Text;

namespace AppHost.Security;

public static class ConfigurationResolverExtensions
{
    public static string ResolveRequired(
    this IDistributedApplicationBuilder builder,
    params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = builder.Configuration[key];

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"Missing required configuration. Keys searched: {string.Join(", ", keys)}");
    }

    public static string ResolveOptional(
    this IDistributedApplicationBuilder builder,
    string defaultValue,
    params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = builder.Configuration[key];

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return defaultValue;
    }
}

using Microsoft.AspNetCore.Http;
using ServiceDefaults.Auth;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registration helpers for the Bethuya server-side user context.</summary>
public static class BethuyaUserContextExtensions
{
    /// <summary>
    /// Registers <see cref="IHttpContextAccessor"/> and the scoped
    /// <see cref="IUserContext"/> implementation backed by the current HTTP request principal.
    /// </summary>
    public static IServiceCollection AddBethuyaUserContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpContextUserContext>();
        return services;
    }
}

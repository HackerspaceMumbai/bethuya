using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDefaults.Auth;

namespace Microsoft.Extensions.Hosting;

/// <summary>Extension methods for configuring Bethuya authorization policies.</summary>
public static class BethuyaAuthorizationExtensions
{
    /// <summary>
    /// Configuration key that, when set to <c>true</c>, enables a default-deny fallback policy
    /// requiring an authenticated user on every endpoint that does not declare its own policy.
    /// Disabled by default so it can be rolled out independently once public endpoints are
    /// explicitly marked anonymous.
    /// </summary>
    public const string EnforceAuthenticatedFallbackKey = "Authorization:EnforceAuthenticatedFallback";

    /// <summary>
    /// Adds Bethuya authorization policies based on platform roles.
    /// Call from both Web and Backend projects to ensure consistent policy enforcement.
    /// </summary>
    /// <remarks>
    /// Policy and role names come from <see cref="BethuyaPolicyNames"/> and <see cref="BethuyaRoleNames"/>,
    /// the single source of truth mirrored by <c>Bethuya.Hybrid.Shared.Auth</c>.
    /// </remarks>
    public static TBuilder AddBethuyaAuthorization<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var authorizationBuilder = builder.Services.AddAuthorizationBuilder()
            .AddPolicy(BethuyaPolicyNames.RequireAdmin, policy =>
                policy.RequireRole(BethuyaRoleNames.Admin))
            .AddPolicy(BethuyaPolicyNames.RequireOrganizer, policy =>
                policy.RequireRole(BethuyaRoleNames.Admin, BethuyaRoleNames.Organizer))
            .AddPolicy(BethuyaPolicyNames.RequireCurator, policy =>
                policy.RequireRole(BethuyaRoleNames.Admin, BethuyaRoleNames.Curator))
            .AddPolicy(BethuyaPolicyNames.RequireAttendee, policy =>
                policy.RequireRole(
                    BethuyaRoleNames.Admin,
                    BethuyaRoleNames.Organizer,
                    BethuyaRoleNames.Curator,
                    BethuyaRoleNames.Attendee));

        if (builder.Configuration.GetValue<bool>(EnforceAuthenticatedFallbackKey))
        {
            authorizationBuilder.SetFallbackPolicy(
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());
        }

        return builder;
    }
}

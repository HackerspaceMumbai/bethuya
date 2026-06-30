using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ServiceDefaults.Auth;

namespace Microsoft.Extensions.Hosting;

/// <summary>Extension methods for configuring Bethuya authorization policies.</summary>
public static class BethuyaAuthorizationExtensions
{
    /// <summary>
    /// Configuration key that controls the default-deny fallback policy requiring an authenticated
    /// user on every endpoint that does not declare its own policy or <c>AllowAnonymous</c>.
    /// <para>
    /// Default-deny is <b>enabled by default</b> (PR3). The key is retained purely as an emergency
    /// escape hatch: set it to <c>false</c> to disable the fallback during an incident. Public and
    /// infrastructure endpoints (health, OpenAPI/Scalar, <c>/api/public/*</c>, auth login/logout)
    /// are explicitly marked anonymous so they remain reachable under default-deny.
    /// </para>
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
                    BethuyaRoleNames.Attendee))
            .AddPolicy(BethuyaPolicyNames.ResourceOwner, policy =>
                policy.AddRequirements(new SameOwnerRequirement()));

        // Resource-based ownership handler backing the ResourceOwner policy. Registered idempotently so
        // repeated AddBethuyaAuthorization calls (e.g. across test hosts) don't add duplicate handlers.
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAuthorizationHandler, ResourceOwnerHandler>());

        if (builder.Configuration.GetValue(EnforceAuthenticatedFallbackKey, defaultValue: true))
        {
            authorizationBuilder.SetFallbackPolicy(
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());
        }

        return builder;
    }
}

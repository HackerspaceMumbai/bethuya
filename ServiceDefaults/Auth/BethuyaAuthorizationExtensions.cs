using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ServiceDefaults.Auth;
using ServiceDefaults.Auth.Observability;

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

        // PR5: authorization audit + metrics sink. Registered idempotently here so every host that wires
        // the Bethuya policies (including minimal test hosts) can resolve IAuthorizationAuditor for the
        // instrumented endpoints. The Meter is a no-op until an OpenTelemetry/MeterListener subscribes,
        // so this is harmless in the Web (Blazor) host. The framework-outcome middleware handler is NOT
        // registered here — it is backend-scoped via AddBethuyaAuthorizationObservability.
        builder.Services.AddMetrics();
        builder.Services.TryAddSingleton<AuthorizationMetrics>();
        builder.Services.TryAddSingleton<IAuthorizationAuditor, AuthorizationAuditor>();

        if (builder.Configuration.GetValue(EnforceAuthenticatedFallbackKey, defaultValue: true))
        {
            authorizationBuilder.SetFallbackPolicy(
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());
        }

        return builder;
    }

    /// <summary>
    /// Registers backend-only authorization observability: the
    /// <see cref="BethuyaAuthorizationAuditMiddlewareResultHandler"/> that records framework-level
    /// challenge/forbid outcomes (401/403 by route group) and grants, then delegates to the default
    /// handler so HTTP behavior is unchanged. Call this from the API host only — not the Blazor Web host,
    /// whose authorization pipeline must keep the framework default handler.
    /// </summary>
    public static TBuilder AddBethuyaAuthorizationObservability<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, BethuyaAuthorizationAuditMiddlewareResultHandler>();
        return builder;
    }
}

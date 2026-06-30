using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// Custom <see cref="IAuthorizationMiddlewareResultHandler"/> that observes framework-level
/// authorization outcomes (the default-deny fallback and per-route role policies) and then delegates to
/// the built-in handler so the HTTP behavior is byte-for-byte unchanged.
/// <para>
/// It records a metric and audit record for every challenge (401) and forbid (403), tagged with the
/// route group (first path segment under <c>/api</c>, e.g. <c>attendee</c>/<c>curator</c>) so the Aspire
/// dashboard can break denials down by area. Registered <b>backend-only</b> to avoid interfering with the
/// Blazor Web authorization pipeline.
/// </para>
/// </summary>
public sealed class BethuyaAuthorizationAuditMiddlewareResultHandler(
    AuthorizationMetrics metrics,
    IAuthorizationAuditor auditor) : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler Default = new();

    /// <inheritdoc />
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (!authorizeResult.Succeeded)
        {
            // Challenged → the caller is unauthenticated (401); Forbidden → authenticated but lacks the
            // required role/policy (403). Mirrors the status the default handler is about to emit.
            var statusCode = authorizeResult.Challenged
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status403Forbidden;

            var routeGroup = ResolveRouteGroup(context.Request.Path);
            metrics.RecordOutcome(routeGroup, statusCode);

            auditor.RecordDecision(
                AuthorizationDecision.Deny,
                policyName: ResolvePolicyName(routeGroup),
                resourceType: routeGroup,
                subject: OwnershipBypass.ResolveSubject(context.User ?? new ClaimsPrincipal(new ClaimsIdentity())),
                resourceId: null,
                route: context.Request.Path.Value,
                outcomeStatusCode: statusCode);
        }

        await Default.HandleAsync(next, context, policy, authorizeResult);
    }

    /// <summary>
    /// Maps a coarse route group to the role policy that guards it, so audit records and metrics carry the
    /// policy that actually denied the request (not a hard-coded one). Falls back to
    /// <see cref="BethuyaPolicyNames.RequireAttendee"/> — the any-authenticated baseline applied by the
    /// default-deny fallback — for routes outside the named role groups.
    /// </summary>
    private static string ResolvePolicyName(string routeGroup) => routeGroup switch
    {
        "admin" => BethuyaPolicyNames.RequireAdmin,
        "organizer" => BethuyaPolicyNames.RequireOrganizer,
        "curator" => BethuyaPolicyNames.RequireCurator,
        "attendee" => BethuyaPolicyNames.RequireAttendee,
        _ => BethuyaPolicyNames.RequireAttendee
    };

    /// <summary>
    /// Extracts a coarse route-group label from the request path. For <c>/api/{group}/...</c> this is
    /// <c>{group}</c>; otherwise the first path segment, or <c>"root"</c> for <c>/</c>.
    /// </summary>
    private static string ResolveRouteGroup(PathString path)
    {
        if (!path.HasValue)
        {
            return "root";
        }

        var segments = path.Value!.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return "root";
        }

        if (string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) && segments.Length > 1)
        {
            return segments[1].ToLowerInvariant();
        }

        return segments[0].ToLowerInvariant();
    }
}

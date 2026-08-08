using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ServiceDefaults.Auth;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Maps the development-only persona selection endpoints onto the Web SSR host.
/// These endpoints are ONLY registered when <c>Authentication:Provider=None</c> AND
/// <c>Environment=Development</c> (enforced at the call-site in <c>Program.cs</c>).
/// </summary>
public static class DevPersonaEndpointExtensions
{
    /// <summary>
    /// Maps the following endpoints under <c>/dev/persona</c>:
    /// <list type="bullet">
    ///   <item><description><c>GET /dev/persona/{key}</c> — select a persona (sets cookie, redirects)</description></item>
    ///   <item><description><c>GET /dev/persona/clear</c> — clear persona (deletes cookie, redirects to "/")</description></item>
    /// </list>
    /// </summary>
    public static IEndpointRouteBuilder MapDevPersonaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dev/persona").AllowAnonymous();

        // Select a persona: validate key, write cookie, redirect.
        group.MapGet("/{key}", (string key, string? returnUrl, HttpContext context) =>
        {
            // Reject cross-origin requests (drive-by cookie planting via <img>, <script>, or
            // a cross-origin link). Sec-Fetch-Site is sent by all modern browsers and cannot
            // be forged by cross-origin scripts. Absent means direct navigation (typed URL or
            // bookmark) — the intended dev UX — and must be allowed.
            var fetchSite = context.Request.Headers["Sec-Fetch-Site"].FirstOrDefault();
            if (string.Equals(fetchSite, "cross-site", StringComparison.Ordinal))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!DevelopmentPersonaCatalog.TryGet(key, out _))
            {
                var validKeys = string.Join(", ", DevelopmentPersonaCatalog.Keys);
                return Results.BadRequest($"Unknown persona key '{key}'. Valid keys: {validKeys}");
            }

            context.Response.Cookies.Append(
                DevelopmentPersonaCatalog.PersonaCookieName,
                key,
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    // Secure only when the request is over HTTPS; HTTP is acceptable in local dev.
                    Secure = context.Request.IsHttps,
                    IsEssential = true,
                });

            // Restrict to local relative paths only (starts with '/' but not '//').
            // Falls back to "/" for absolute URLs, protocol-relative URLs, and empty values,
            // preventing open-redirect to attacker-controlled destinations.
            var safeReturnUrl = IsLocalUrl(returnUrl) ? returnUrl! : "/";
            return Results.LocalRedirect(safeReturnUrl);
        });

        // Clear persona: delete cookie, redirect to home (restores legacy fixed-admin default).
        group.MapGet("/clear", (HttpContext context) =>
        {
            // Same cross-origin guard as the select endpoint — clearing is also a state change.
            var fetchSite = context.Request.Headers["Sec-Fetch-Site"].FirstOrDefault();
            if (string.Equals(fetchSite, "cross-site", StringComparison.Ordinal))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            context.Response.Cookies.Delete(DevelopmentPersonaCatalog.PersonaCookieName);
            return Results.LocalRedirect("/");
        });

        return endpoints;
    }

    /// <summary>
    /// Returns <see langword="true"/> only for local relative paths: starts with <c>/</c>
    /// but not <c>//</c> (which is a protocol-relative URL pointing to an external host).
    /// </summary>
    private static bool IsLocalUrl([NotNullWhen(true)] string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.StartsWith('/')
        && !url.StartsWith("//", StringComparison.Ordinal);
}

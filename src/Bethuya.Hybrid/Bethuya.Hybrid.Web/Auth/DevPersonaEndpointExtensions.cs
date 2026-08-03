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

            return Results.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
        });

        // Clear persona: delete cookie, redirect to home (restores legacy fixed-admin default).
        group.MapGet("/clear", (HttpContext context) =>
        {
            context.Response.Cookies.Delete(DevelopmentPersonaCatalog.PersonaCookieName);
            return Results.Redirect("/");
        });

        return endpoints;
    }
}

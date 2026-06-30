using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Endpoints;

/// <summary>
/// Test-host helpers that wire up the Bethuya authorization pipeline with a permissive
/// authentication scheme. PR2 attaches per-route-group authorization metadata, so functional
/// endpoint tests must provide an authorization middleware and an authenticated principal.
/// The stub principal carries the <see cref="BethuyaRoleNames.Admin"/> role, which satisfies
/// every Bethuya policy, keeping these tests focused on handler behavior rather than auth.
/// </summary>
internal static class TestAuthHost
{
    public const string SchemeName = "TestAuth";

    /// <summary>Registers Bethuya policies and a permissive Admin-authenticated test scheme.</summary>
    public static IServiceCollection AddTestAuthorization(this IServiceCollection services)
    {
        services.AddAuthentication(SchemeName)
            .AddScheme<AuthenticationSchemeOptions, AdminTestAuthenticationHandler>(SchemeName, _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy(BethuyaPolicyNames.RequireAdmin, policy => policy.RequireRole(BethuyaRoleNames.Admin))
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

        return services;
    }

    /// <summary>Adds authentication and authorization middleware to the test pipeline.</summary>
    public static WebApplication UseTestAuthorization(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    private sealed class AdminTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            Claim[] claims =
            [
                new(ClaimTypes.NameIdentifier, "test-admin"),
                new("sub", "test-admin"),
                new(ClaimTypes.Name, "test-admin"),
                new(ClaimTypes.Role, BethuyaRoleNames.Admin)
            ];

            var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

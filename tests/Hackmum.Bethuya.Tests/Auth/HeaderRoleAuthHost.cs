using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// Test authentication scheme whose principal is driven entirely by request headers, so a single
/// host can serve anonymous, wrong-role and correct-role requests. A request is treated as
/// anonymous (no authenticated user) unless it carries <see cref="SubHeader"/> or
/// <see cref="RolesHeader"/>. PR3 functional authorization tests use this to assert default-deny
/// (anonymous → 401) and per-policy role enforcement (wrong role → 403, correct role → 200).
/// </summary>
internal sealed class HeaderRoleAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "HeaderRoleAuth";
    public const string RolesHeader = "X-Test-Roles";
    public const string SubHeader = "X-Test-Sub";
    public const string EmailHeader = "X-Test-Email";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var hasRoles = Request.Headers.TryGetValue(RolesHeader, out var rolesValue)
            && !string.IsNullOrWhiteSpace(rolesValue);
        var hasSub = Request.Headers.TryGetValue(SubHeader, out var subValue)
            && !string.IsNullOrWhiteSpace(subValue);

        if (!hasRoles && !hasSub)
        {
            // No identity headers → leave the request anonymous so default-deny / explicit
            // policies can challenge it.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var sub = hasSub ? subValue.ToString() : "test-user";
        var email = Request.Headers.TryGetValue(EmailHeader, out var emailValue)
            && !string.IsNullOrWhiteSpace(emailValue)
            ? emailValue.ToString()
            : $"{sub}@bethuya.test";

        var claims = new List<Claim>
        {
            new("sub", sub),
            new(ClaimTypes.NameIdentifier, sub),
            new("name", sub),
            new("email", email),
        };

        if (hasRoles)
        {
            foreach (var role in rolesValue.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim("role", role));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName, nameType: "name", roleType: "role");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Builds minimal TestServer hosts wired with the Bethuya authorization pipeline.</summary>
internal static class HeaderRoleAuthHost
{
    /// <summary>
    /// Builds and starts a TestServer host that registers the canonical Bethuya policies
    /// (via <c>AddBethuyaAuthorization</c>) plus the header-driven test scheme, then maps routes
    /// via <paramref name="map"/>.
    /// </summary>
    /// <param name="map">Maps endpoints onto the application.</param>
    /// <param name="enforceFallback">
    /// Optional explicit override for <c>Authorization:EnforceAuthenticatedFallback</c>. When
    /// <see langword="null"/> the production default applies (default-deny on after PR3).
    /// </param>
    /// <param name="configureServices">Optional extra service registration (stubs).</param>
    public static async Task<WebApplication> StartAsync(
        Action<WebApplication> map,
        bool? enforceFallback = null,
        Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string?>? configuration = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Clear ambient JSON/env configuration so default-deny semantics are deterministic.
        // (Referenced app projects flow an appsettings.json into the test output that could
        // otherwise set Authorization:EnforceAuthenticatedFallback and mask the production default.)
        builder.Configuration.Sources.Clear();

        var settings = new Dictionary<string, string?>();
        if (enforceFallback is not null)
        {
            settings["Authorization:EnforceAuthenticatedFallback"] = enforceFallback.Value ? "true" : "false";
        }

        if (configuration is not null)
        {
            foreach (var pair in configuration)
            {
                settings[pair.Key] = pair.Value;
            }
        }

        if (settings.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(settings);
        }

        builder.Services.AddAuthentication(HeaderRoleAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderRoleAuthHandler>(HeaderRoleAuthHandler.SchemeName, _ => { });
        builder.AddBethuyaAuthorization();

        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        map(app);

        await app.StartAsync();
        return app;
    }
}

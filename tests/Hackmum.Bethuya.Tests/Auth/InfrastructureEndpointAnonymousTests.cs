using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR3: enabling default-deny must not break infrastructure or sign-in. Health/liveness probes
/// and the OIDC login/logout group must stay anonymous so Aspire/Container Apps health routing and
/// the sign-in flow keep working when <c>Authorization:EnforceAuthenticatedFallback</c> is on.
/// </summary>
public sealed class InfrastructureEndpointAnonymousTests
{
    [Test]
    public async Task HealthEndpoint_AllowsAnonymous()
    {
        await Assert.That(IsAnonymous(BuildDefaultEndpointsApp(), "/health")).IsTrue();
    }

    [Test]
    public async Task AliveEndpoint_AllowsAnonymous()
    {
        await Assert.That(IsAnonymous(BuildDefaultEndpointsApp(), "/alive")).IsTrue();
    }

    [Test]
    public async Task AuthLogin_AllowsAnonymous()
    {
        await Assert.That(IsAnonymous(BuildAuthEndpointsApp(), "/authentication/login")).IsTrue();
    }

    [Test]
    public async Task AuthLogout_AllowsAnonymous()
    {
        await Assert.That(IsAnonymous(BuildAuthEndpointsApp(), "/authentication/logout")).IsTrue();
    }

    private static WebApplication BuildDefaultEndpointsApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddDefaultHealthChecks();
        var app = builder.Build();
        app.MapDefaultEndpoints();
        return app;
    }

    private static WebApplication BuildAuthEndpointsApp()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        app.MapBethuyaAuthEndpoints();
        return app;
    }

    private static bool IsAnonymous(WebApplication app, string pattern)
    {
        using (app)
        {
            var normalizedTarget = "/" + pattern.Trim('/');
            var endpoint = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(ds => ds.Endpoints)
                .OfType<RouteEndpoint>()
                .FirstOrDefault(e =>
                    string.Equals("/" + (e.RoutePattern.RawText ?? string.Empty).Trim('/'), normalizedTarget, StringComparison.OrdinalIgnoreCase));

            return endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        }
    }
}

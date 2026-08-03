using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// Characterizes the <b>current</b> (pre-persona-switching) development authentication
/// boundary described in <c>docs/development-authentication.md</c>. These tests intentionally
/// prove the existing fixed-admin behavior — they are not testing a desired future state and
/// must not be "fixed" to make a different persona possible. See
/// <c>.squad/decisions/inbox/copilot-dev-persona-auth.md</c> for the proposed future boundary
/// that a later layer will implement.
/// </summary>
public class DevelopmentAuthenticationBoundaryTests : IAsyncDisposable
{
    private readonly List<WebApplication> _apps = [];
    private readonly List<HttpClient> _clients = [];

    [Test]
    public async Task DevelopmentPrincipal_AlwaysHasAllFourRolesSimultaneously()
    {
        var principal = DevelopmentAuthenticationDefaults.CreatePrincipal();

        await Assert.That(principal.IsInRole(BethuyaRoleNames.Admin)).IsTrue();
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Organizer)).IsTrue();
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Curator)).IsTrue();
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Attendee)).IsTrue();
        await Assert.That(principal.FindFirst("sub")?.Value).IsEqualTo("dev-user-001");
        await Assert.That(principal.FindFirst("email")?.Value).IsEqualTo("dev@bethuya.local");
    }

    // Note: `Bethuya.Hybrid.Web.Auth.DevelopmentAuthenticationStateProvider` (the Web-tier
    // AuthenticationStateProvider used when Provider=None) is `internal sealed` to its project
    // and registered ad hoc in Program.cs rather than through a reusable, test-seamed extension
    // method, so it cannot be instantiated or resolved directly from this test project without
    // widening its visibility (out of scope for a characterization-only layer). Its behavior —
    // returning a single static `AuthenticationState` built once from
    // `DevelopmentAuthenticationDefaults.CreatePrincipal()` (see source) — is provably identical
    // to what `DevelopmentPrincipal_AlwaysHasAllFourRolesSimultaneously` above already
    // characterizes, since both read from the exact same shared factory. This is documented in
    // `docs/development-authentication.md` instead of duplicated here via reflection.

    [Test]
    public async Task ApiAuthentication_NoneProvider_IgnoresClientSuppliedAuthorizationHeader()
    {
        // Characterizes that the Backend's DevelopmentAuthenticationHandler never inspects
        // request headers at all — a caller cannot influence the resolved identity by sending
        // any Authorization header, valid or not, because Provider=None Web clients never
        // attach one in the first place (see docs/development-authentication.md).
        var builder = CreateBuilderWithProviderNone();
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapGet("/whoami", (HttpContext context) => Results.Ok(new
            {
                sub = context.User.FindFirst("sub")?.Value,
                roles = context.User.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToArray()
            })).RequireAuthorization();
        });

        var client = app.GetTestClient();
        _clients.Add(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "totally-different-user-claiming-no-roles");

        var response = await client.GetAsync("/whoami");
        var body = await response.Content.ReadFromJsonAsync<WhoAmIResponse>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Sub).IsEqualTo("dev-user-001");
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Admin);
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Organizer);
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Curator);
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Attendee);
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_SinglePrincipalSatisfiesEveryRolePolicySimultaneously()
    {
        // Characterizes that today there is no way to exercise Organizer-only, Curator-only,
        // or Attendee-only authorization paths in isolation: the one fixed dev principal
        // passes every policy at once. A future persona-switching layer must change this by
        // having the Backend construct a *different* principal per persona — not by having the
        // Web tier merely claim to be a different persona.
        var builder = CreateBuilderWithProviderNone();
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapGet("/admin-only", () => Results.Ok("admin")).RequireAuthorization(BethuyaPolicyNames.RequireAdmin);
            app.MapGet("/organizer-only", () => Results.Ok("organizer")).RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);
            app.MapGet("/curator-only", () => Results.Ok("curator")).RequireAuthorization(BethuyaPolicyNames.RequireCurator);
            app.MapGet("/attendee-only", () => Results.Ok("attendee")).RequireAuthorization(BethuyaPolicyNames.RequireAttendee);
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        var adminResponse = await client.GetAsync("/admin-only");
        var organizerResponse = await client.GetAsync("/organizer-only");
        var curatorResponse = await client.GetAsync("/curator-only");
        var attendeeResponse = await client.GetAsync("/attendee-only");

        await Assert.That(adminResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(organizerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(curatorResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(attendeeResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_UnauthenticatedRequestIsStillAuthenticatedAsFixedAdmin()
    {
        // Characterizes the core defect: there is no "logged out" or "anonymous" state
        // reachable via the Development scheme. Every request — including one with zero
        // auth-related headers — is authenticated as the fixed admin principal.
        var builder = CreateBuilderWithProviderNone();
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapGet("/admin-only", () => Results.Ok("admin")).RequireAuthorization(BethuyaPolicyNames.RequireAdmin);
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        var response = await client.GetAsync("/admin-only");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        foreach (var app in _apps)
        {
            await app.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    private static WebApplicationBuilder CreateBuilderWithProviderNone()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "None"
        });

        return builder;
    }

    private async Task<WebApplication> StartAppAsync(WebApplicationBuilder builder, Action<WebApplication> configure)
    {
        var app = builder.Build();
        configure(app);
        await app.StartAsync();
        _apps.Add(app);
        return app;
    }

    private sealed record WhoAmIResponse(string? Sub, string[] Roles);
}

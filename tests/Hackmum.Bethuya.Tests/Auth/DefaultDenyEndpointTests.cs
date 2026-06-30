using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR3 (C1/C4): the authenticated fallback policy is ON by default. Every endpoint without an
/// explicit <c>AllowAnonymous</c> requires an authenticated user, while endpoints explicitly marked
/// anonymous remain reachable. The config key stays as an emergency escape hatch.
/// </summary>
public sealed class DefaultDenyEndpointTests
{
    [Test]
    public async Task ProtectedEndpoint_Default_RejectsAnonymous()
    {
        await using var app = await HeaderRoleAuthHost.StartAsync(MapProbes);
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/protected", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ProtectedEndpoint_Default_AllowsAuthenticatedUser()
    {
        await using var app = await HeaderRoleAuthHost.StartAsync(MapProbes);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/protected", UriKind.Relative));
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, "user-1");

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task AnonymousEndpoint_Default_RemainsReachable()
    {
        await using var app = await HeaderRoleAuthHost.StartAsync(MapProbes);
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/open", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ProtectedEndpoint_EscapeHatchDisabled_AllowsAnonymous()
    {
        await using var app = await HeaderRoleAuthHost.StartAsync(MapProbes, enforceFallback: false);
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/protected", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private static void MapProbes(WebApplication app)
    {
        app.MapGet("/protected", () => Results.Ok("protected"));
        app.MapGet("/open", () => Results.Ok("open")).AllowAnonymous();
    }
}

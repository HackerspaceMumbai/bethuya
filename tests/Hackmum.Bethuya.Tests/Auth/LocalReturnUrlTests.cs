using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

public sealed class LocalReturnUrlTests
{
    [Test]
    [Arguments("/events", "/events")]
    [Arguments("/events/create?tab=2", "/events/create?tab=2")]
    [Arguments("/", "/")]
    public async Task Resolve_KeepsLocalPaths(string input, string expected)
    {
        await Assert.That(LocalReturnUrl.Resolve(input)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("https://evil.example/phish")]
    [Arguments("http://evil.example")]
    [Arguments("//evil.example/phish")]
    [Arguments("/\\evil.example")]
    [Arguments("\\\\evil.example")]
    [Arguments("/\t/evil.example")]
    [Arguments("/\n/evil.example")]
    [Arguments("javascript:alert(1)")]
    [Arguments("events")]
    public async Task Resolve_FallsBackToRootForExternalOrMalformedTargets(string? input)
    {
        await Assert.That(LocalReturnUrl.Resolve(input)).IsEqualTo(LocalReturnUrl.DefaultPath);
    }

    [Test]
    public async Task LoginEndpoint_DoesNotRedirectToExternalReturnUrl()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "None"
        });

        await using var app = builder.Build();
        app.MapBethuyaAuthEndpoints();
        await app.StartAsync();

        // Use the raw handler so the test observes the redirect instead of following it.
        using var client = new HttpClient(app.GetTestServer().CreateHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };

        var response = await client.GetAsync("/authentication/login?returnUrl=https%3A%2F%2Fevil.example%2Fphish");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location?.ToString()).IsEqualTo("/");
    }

    [Test]
    public async Task LoginEndpoint_KeepsLocalReturnUrl()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "None"
        });

        await using var app = builder.Build();
        app.MapBethuyaAuthEndpoints();
        await app.StartAsync();

        using var client = new HttpClient(app.GetTestServer().CreateHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };

        var response = await client.GetAsync("/authentication/login?returnUrl=%2Fevents%2Fcreate");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location?.ToString()).IsEqualTo("/events/create");
    }
}

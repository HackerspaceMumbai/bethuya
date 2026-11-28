using System.Net;
using Bethuya.Hybrid.Web.Auth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// Sign-in and social-connect endpoints must only redirect to paths on this site, so a crafted
/// <c>returnUrl</c> cannot bounce a user (and, for social connect, their verified handle) off-site.
/// </summary>
public class ReturnUrlRedirectTests : IAsyncDisposable
{
    private readonly List<WebApplication> _apps = [];
    private readonly List<HttpClient> _clients = [];

    [Test]
    [Arguments("https%3A%2F%2Fevil.example%2Fphish")]
    [Arguments("%2F%2Fevil.example")]
    [Arguments("%2F%5Cevil.example")]
    [Arguments("%2F%09%2Fevil.example")]
    public async Task Login_ProviderNone_OffSiteReturnUrl_RedirectsToRoot(string encodedReturnUrl)
    {
        var client = await CreateProviderNoneServerAsync();

        var response = await client.GetAsync($"/authentication/login?returnUrl={encodedReturnUrl}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location?.OriginalString).IsEqualTo("/");
    }

    [Test]
    public async Task Login_ProviderNone_LocalReturnUrl_IsKept()
    {
        var client = await CreateProviderNoneServerAsync();

        var response = await client.GetAsync("/authentication/login?returnUrl=%2Fevents%3Ftab%3Dmine");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location?.OriginalString).IsEqualTo("/events?tab=mine");
    }

    [Test]
    [Arguments("https%3A%2F%2Fevil.example%2Fphish", "/")]
    [Arguments("%2F%5Cevil.example", "/")]
    [Arguments("%2Fevents", "/events")]
    public async Task Login_Oidc_ChallengeRedirectUri_IsLocal(string encodedReturnUrl, string expectedRedirectUri)
    {
        var captured = new List<string?>();
        var client = await CreateOidcServerAsync(captured);

        var response = await client.GetAsync($"/authentication/login?returnUrl={encodedReturnUrl}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(captured).HasSingleItem();
        await Assert.That(captured[0]).IsEqualTo(expectedRedirectUri);
    }

    [Test]
    [Arguments("%2F%5Cevil.example")]
    [Arguments("%2F%2Fevil.example")]
    [Arguments("https%3A%2F%2Fevil.example")]
    public async Task SocialStart_OffSiteReturnUrl_FallsBackToSocialPage(string encodedReturnUrl)
    {
        var client = await CreateProviderNoneServerAsync();

        var response = await client.GetAsync($"/authentication/social/github/start?returnUrl={encodedReturnUrl}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location?.OriginalString)
            .StartsWith("/registration/social?socialError=", StringComparison.Ordinal);
    }

    [Test]
    public async Task SocialStart_LocalReturnUrl_IsKept()
    {
        var client = await CreateProviderNoneServerAsync();

        var response = await client.GetAsync("/authentication/social/github/start?returnUrl=%2Fregistration%2Fsocial%3Fstep%3D2");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location?.OriginalString)
            .StartsWith("/registration/social?step=2&socialError=", StringComparison.Ordinal);
    }

    private async Task<HttpClient> CreateProviderNoneServerAsync()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "None"
        });
        builder.AddSocialProfileConnectionAuthentication();
        builder.AddBethuyaWebAuthentication();

        var app = builder.Build();
        _apps.Add(app);
        app.MapBethuyaAuthEndpoints();
        app.MapSocialProfileConnectionEndpoints();

        return await StartAsync(app);
    }

    private async Task<HttpClient> CreateOidcServerAsync(List<string?> capturedRedirectUris)
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "Keycloak",
            ["Authentication:Keycloak:Authority"] = "https://idp.example/realms/bethuya",
            ["Authentication:Keycloak:ClientId"] = "bethuya-web",
            ["Authentication:Keycloak:ClientSecret"] = "test-secret"
        });
        builder.AddBethuyaWebAuthentication();
        builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, oidc =>
        {
            oidc.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                new OpenIdConnectConfiguration
                {
                    AuthorizationEndpoint = "https://idp.example/realms/bethuya/protocol/openid-connect/auth"
                });
            oidc.Events.OnRedirectToIdentityProvider = context =>
            {
                capturedRedirectUris.Add(context.Properties.RedirectUri);
                return Task.CompletedTask;
            };
        });

        var app = builder.Build();
        _apps.Add(app);
        app.MapBethuyaAuthEndpoints();

        return await StartAsync(app);
    }

    private static WebApplicationBuilder CreateBuilder(Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        return builder;
    }

    private async Task<HttpClient> StartAsync(WebApplication app)
    {
        await app.StartAsync();
        var client = app.GetTestClient();
        _clients.Add(client);
        return client;
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
}

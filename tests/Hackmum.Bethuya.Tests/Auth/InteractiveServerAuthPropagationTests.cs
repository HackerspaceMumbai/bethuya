using System.Net;
using System.Security.Claims;
using System.Text;
using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Web.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

public sealed class InteractiveServerAuthPropagationTests
{
    [Test]
    public async Task DevelopmentAuthStateProvider_HostCircuitStatePersists_WhenHttpContextIsNull()
    {
        DevelopmentPersonaCatalog.TryGet("Farah", out var farah);
        var farahPrincipal = DevelopmentAuthenticationDefaults.CreatePersonaPrincipal(farah!);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var provider = new DevelopmentAuthenticationStateProvider(accessor);
        provider.SetAuthenticationState(Task.FromResult(new AuthenticationState(farahPrincipal)));

        var state = await provider.GetAuthenticationStateAsync();

        await Assert.That(state.User.FindFirst("sub")?.Value).IsEqualTo("dev-persona-farah");
        await Assert.That(state.User.IsInRole(BethuyaRoles.Attendee)).IsTrue();
        await Assert.That(state.User.IsInRole(BethuyaRoles.Admin)).IsFalse();
    }

    [Test]
    public async Task DevPersonaPropagationHandler_WhenHttpContextIsNull_UsesCircuitPrincipal()
    {
        DevelopmentPersonaCatalog.TryGet("Farah", out var farah);
        var farahPrincipal = DevelopmentAuthenticationDefaults.CreatePersonaPrincipal(farah!);

        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns((HttpContext?)null);

        var circuitAccessor = new CircuitPrincipalAccessor
        {
            CurrentPrincipal = farahPrincipal
        };

        try
        {
            var handler = new DevPersonaPropagationHandler(httpAccessor, circuitAccessor)
            {
                InnerHandler = new StubOkHandler()
            };
            using var client = new HttpClient(handler);
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://backend/api/events");

            using var _ = await client.SendAsync(request);

            request.Headers.TryGetValues(DevelopmentPersonaCatalog.PersonaHeaderName, out var values);
            await Assert.That(values?.SingleOrDefault()).IsEqualTo("Farah");
        }
        finally
        {
            circuitAccessor.CurrentPrincipal = null;
        }
    }

    [Test]
    public async Task DevPersonaPropagationHandler_WhenHttpContextIsNull_PreservesUnknownPersonaFailClosed()
    {
        var unknownPrincipal = DevelopmentAuthenticationDefaults.CreateUnknownPersonaPrincipal("bad-key");

        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns((HttpContext?)null);

        var circuitAccessor = new CircuitPrincipalAccessor
        {
            CurrentPrincipal = unknownPrincipal
        };

        try
        {
            var handler = new DevPersonaPropagationHandler(httpAccessor, circuitAccessor)
            {
                InnerHandler = new StubOkHandler()
            };
            using var client = new HttpClient(handler);
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://backend/api/events");

            using var _ = await client.SendAsync(request);

            request.Headers.TryGetValues(DevelopmentPersonaCatalog.PersonaHeaderName, out var values);
            await Assert.That(values?.SingleOrDefault()).IsEqualTo("dev-persona-unknown");
        }
        finally
        {
            circuitAccessor.CurrentPrincipal = null;
        }
    }

    [Test]
    public async Task DevPersonaPropagationHandler_WhenHttpContextIsNullAndLegacyDefault_DoesNotAttachHeader()
    {
        var legacyPrincipal = DevelopmentAuthenticationDefaults.CreatePrincipal();

        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns((HttpContext?)null);

        var circuitAccessor = new CircuitPrincipalAccessor
        {
            CurrentPrincipal = legacyPrincipal
        };

        try
        {
            var handler = new DevPersonaPropagationHandler(httpAccessor, circuitAccessor)
            {
                InnerHandler = new StubOkHandler()
            };
            using var client = new HttpClient(handler);
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://backend/api/events");

            using var _ = await client.SendAsync(request);

            await Assert.That(request.Headers.Contains(DevelopmentPersonaCatalog.PersonaHeaderName)).IsFalse();
        }
        finally
        {
            circuitAccessor.CurrentPrincipal = null;
        }
    }

    [Test]
    public async Task HttpContextBackendAccessTokenProvider_WhenHttpContextIsNull_ResolvesFromCircuitTokenStore()
    {
        var now = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new AdjustableTimeProvider(now);
        var factory = new StubHttpClientFactory(new StubJsonHandler("""{"access_token":"refreshed"}"""));
        using var tokenStore = new InMemoryBackendTokenStore(
            factory,
            timeProvider,
            NullLogger<InMemoryBackendTokenStore>.Instance);

        tokenStore.Store(
            "sess-1",
            new BackendTokenEntry(
                AccessToken: "jwt-access-123",
                RefreshToken: "refresh-123",
                ExpiresAt: now.AddMinutes(10),
                TokenEndpoint: "https://idp.example.com/token",
                ClientId: "bethuya-web",
                ClientSecret: "secret"));

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "user-1"),
                new Claim(BackendTokenClaimTypes.TokenSessionId, "sess-1")
            ],
            "oidc");

        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns((HttpContext?)null);

        var circuitAccessor = new CircuitPrincipalAccessor
        {
            CurrentPrincipal = new ClaimsPrincipal(identity)
        };

        try
        {
            var provider = new HttpContextBackendAccessTokenProvider(httpAccessor, circuitAccessor, tokenStore);
            var token = await provider.GetAccessTokenAsync();

            await Assert.That(token).IsEqualTo("jwt-access-123");
        }
        finally
        {
            circuitAccessor.CurrentPrincipal = null;
        }
    }

    [Test]
    public async Task InMemoryBackendTokenStore_WhenExpiring_RefreshesOnceAcrossConcurrentCalls()
    {
        var now = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new AdjustableTimeProvider(now);
        var refreshHandler = new CountingRefreshHandler("""
            {
              "access_token": "new-access-token",
              "refresh_token": "new-refresh-token",
              "expires_in": 3600
            }
            """);
        var factory = new StubHttpClientFactory(refreshHandler);
        using var tokenStore = new InMemoryBackendTokenStore(
            factory,
            timeProvider,
            NullLogger<InMemoryBackendTokenStore>.Instance);

        tokenStore.Store(
            "sess-2",
            new BackendTokenEntry(
                AccessToken: "old-access-token",
                RefreshToken: "old-refresh-token",
                ExpiresAt: now.AddSeconds(30), // within 60s skew
                TokenEndpoint: "https://idp.example.com/token",
                ClientId: "bethuya-web",
                ClientSecret: "secret"));

        var results = await Task.WhenAll(
            tokenStore.GetValidAccessTokenAsync("sess-2").AsTask(),
            tokenStore.GetValidAccessTokenAsync("sess-2").AsTask(),
            tokenStore.GetValidAccessTokenAsync("sess-2").AsTask());

        await Assert.That(results.All(t => t == "new-access-token")).IsTrue();
        await Assert.That(refreshHandler.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task ClaimsCurrentUserService_WhenHttpContextIsNull_UsesCircuitPrincipal()
    {
        DevelopmentPersonaCatalog.TryGet("Priya", out var priya);
        var priyaPrincipal = DevelopmentAuthenticationDefaults.CreatePersonaPrincipal(priya!);

        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns((HttpContext?)null);

        var circuitAccessor = new CircuitPrincipalAccessor
        {
            CurrentPrincipal = priyaPrincipal
        };

        try
        {
            var service = new ClaimsCurrentUserService(httpAccessor, circuitAccessor);

            await Assert.That(service.IsAuthenticated).IsTrue();
            await Assert.That(service.UserId).IsEqualTo("dev-persona-priya");
            await Assert.That(service.Email).IsEqualTo("priya@bethuya.dev");
            await Assert.That(service.IsInRole(BethuyaRoles.Curator)).IsTrue();
            await Assert.That(service.IsInRole(BethuyaRoles.Admin)).IsFalse();
        }
        finally
        {
            circuitAccessor.CurrentPrincipal = null;
        }
    }

    [Test]
    public async Task BethuyaCircuitHandler_InboundActivity_FlowsPrincipalAcrossAwaitAndCleansUp()
    {
        DevelopmentPersonaCatalog.TryGet("Vikram", out var vikram);
        var vikramPrincipal = DevelopmentAuthenticationDefaults.CreatePersonaPrincipal(vikram!);
        var authStateProvider = Substitute.For<AuthenticationStateProvider>();
        authStateProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new AuthenticationState(vikramPrincipal)));

        var circuitAccessor = new CircuitPrincipalAccessor();
        var circuitHandler = new BethuyaCircuitHandler(authStateProvider, circuitAccessor);

        ClaimsPrincipal? observedInsideActivity = null;
        var wrapped = circuitHandler.CreateInboundActivityHandler(async _ =>
        {
            await Task.Yield();
            observedInsideActivity = circuitAccessor.CurrentPrincipal;
        });

        await wrapped(null!);

        await Assert.That(observedInsideActivity?.FindFirst("sub")?.Value).IsEqualTo("dev-persona-vikram");
        await Assert.That(circuitAccessor.CurrentPrincipal).IsNull();
    }

    [Test]
    public async Task EndToEnd_CircuitScopeToHttpClientFactory_EnforcesFarahDeniedAndVikramAllowed()
    {
        var backendBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        backendBuilder.WebHost.UseTestServer();
        backendBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "None"
        });
        backendBuilder.AddBethuyaApiAuthentication();
        backendBuilder.AddBethuyaAuthorization();

        await using var backendApp = backendBuilder.Build();
        backendApp.UseBethuyaAuthentication();
        backendApp.MapGet("/api/curation-probe", (HttpContext ctx) => Results.Ok(ctx.User.FindFirst("sub")?.Value))
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizerOrCurator);
        await backendApp.StartAsync();

        var backendHandler = backendApp.GetTestServer().CreateHandler();

        // Simulate Web DI container: IHttpClientFactory resolves DevPersonaPropagationHandler in its
        // own handler scope, separate from the Blazor circuit scope, while HttpContext is null.
        var webServices = new ServiceCollection();
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns((HttpContext?)null);
        webServices.AddSingleton(httpAccessor);
        webServices.AddSingleton<ICircuitPrincipalAccessor, CircuitPrincipalAccessor>();
        webServices.AddScoped<AuthenticationStateProvider, DevelopmentAuthenticationStateProvider>();
        webServices.AddScoped<CircuitHandler, BethuyaCircuitHandler>();
        webServices.AddTransient<DevPersonaPropagationHandler>();
        webServices
            .AddHttpClient("backend", client => client.BaseAddress = new Uri("http://backend"))
            .AddHttpMessageHandler<DevPersonaPropagationHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => backendHandler);

        await using var rootProvider = webServices.BuildServiceProvider();
        var httpClientFactory = rootProvider.GetRequiredService<IHttpClientFactory>();

        async Task<(HttpStatusCode Status, string Body)> InvokeFromSimulatedCircuitAsync(string personaKey)
        {
            DevelopmentPersonaCatalog.TryGet(personaKey, out var persona);
            var principal = DevelopmentAuthenticationDefaults.CreatePersonaPrincipal(persona!);

            await using var circuitScope = rootProvider.CreateAsyncScope();
            var authProvider = (DevelopmentAuthenticationStateProvider)circuitScope.ServiceProvider
                .GetRequiredService<AuthenticationStateProvider>();
            authProvider.SetAuthenticationState(Task.FromResult(new AuthenticationState(principal)));

            var circuitHandler = circuitScope.ServiceProvider.GetRequiredService<CircuitHandler>();
            HttpStatusCode status = default;
            string body = string.Empty;

            var activity = circuitHandler.CreateInboundActivityHandler(async _ =>
            {
                await Task.Yield();
                using var client = httpClientFactory.CreateClient("backend");
                using var response = await client.GetAsync("/api/curation-probe");
                status = response.StatusCode;
                body = await response.Content.ReadAsStringAsync();
            });

            await activity(null!);
            return (status, body);
        }

        var farahResult = await InvokeFromSimulatedCircuitAsync("Farah");
        var vikramResult = await InvokeFromSimulatedCircuitAsync("Vikram");

        await Assert.That(farahResult.Status).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(vikramResult.Status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(vikramResult.Body).Contains("dev-persona-vikram");
    }

    private sealed class StubOkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private sealed class StubJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private sealed class CountingRefreshHandler(string json) : HttpMessageHandler
    {
        private int _calls;
        public int CallCount => _calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(25, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

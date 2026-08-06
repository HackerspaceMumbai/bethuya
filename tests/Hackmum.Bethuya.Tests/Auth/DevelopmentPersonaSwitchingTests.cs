using System.Net;
using System.Net.Http.Json;
using Bethuya.Hybrid.Web.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// TUnit tests for Layer 2: Developer Identity Switching Infrastructure.
/// <para>
/// Covers: persona catalog completeness; principal claim shape; three-way resolution
/// (legacy default / catalog match / unknown-key fail-closed); environment+provider isolation;
/// <see cref="DevelopmentAuthenticationStateProvider"/> per-request reflection;
/// <see cref="DevPersonaPropagationHandler"/> header attachment; Farah-vs-Vikram policy proof;
/// structured-log verification via a real captured <see cref="Microsoft.Extensions.Logging.ILoggerProvider"/>
/// asserting EventId 3100 (DevPersonaResolved) and 3101 (DevPersonaUnknown) actually fire.
/// </para>
/// </summary>
public class DevelopmentPersonaSwitchingTests : IAsyncDisposable
{
    private readonly List<WebApplication> _apps = [];
    private readonly List<HttpClient> _clients = [];

    // ─────────────────────────────────────────────────────────────────────────
    // Catalog completeness
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Catalog_HasExactlySixPersonas()
    {
        await Assert.That(DevelopmentPersonaCatalog.All.Count).IsEqualTo(6);
    }

    [Test]
    public async Task Catalog_AnishIsAttendeeOnly()
    {
        DevelopmentPersonaCatalog.TryGet("Anish", out var p);
        await Assert.That(p).IsNotNull();
        await Assert.That(p!.Roles).Contains(BethuyaRoleNames.Attendee);
        await Assert.That(p.Roles.Count).IsEqualTo(1);
        await Assert.That(p.Subject).IsEqualTo("dev-persona-anish");
        await Assert.That(p.Email).IsEqualTo("anish@bethuya.dev");
    }

    [Test]
    public async Task Catalog_PriyaIsCuratorAndAttendee()
    {
        DevelopmentPersonaCatalog.TryGet("Priya", out var p);
        await Assert.That(p).IsNotNull();
        await Assert.That(p!.Roles).Contains(BethuyaRoleNames.Curator);
        await Assert.That(p.Roles).Contains(BethuyaRoleNames.Attendee);
        await Assert.That(p.Roles.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Catalog_RohanIsMayaIsFarahAreAttendeeOnly()
    {
        foreach (var key in new[] { "Rohan", "Maya", "Farah" })
        {
            DevelopmentPersonaCatalog.TryGet(key, out var p);
            await Assert.That(p).IsNotNull();
            await Assert.That(p!.Roles.Count).IsEqualTo(1);
            await Assert.That(p.Roles).Contains(BethuyaRoleNames.Attendee);
        }
    }

    [Test]
    public async Task Catalog_VikramHasAllFourRoles()
    {
        DevelopmentPersonaCatalog.TryGet("Vikram", out var p);
        await Assert.That(p).IsNotNull();
        await Assert.That(p!.Roles).Contains(BethuyaRoleNames.Admin);
        await Assert.That(p.Roles).Contains(BethuyaRoleNames.Organizer);
        await Assert.That(p.Roles).Contains(BethuyaRoleNames.Curator);
        await Assert.That(p.Roles).Contains(BethuyaRoleNames.Attendee);
        await Assert.That(p.Roles.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Catalog_TryGet_IsCaseInsensitive()
    {
        await Assert.That(DevelopmentPersonaCatalog.TryGet("anish", out _)).IsTrue();
        await Assert.That(DevelopmentPersonaCatalog.TryGet("ANISH", out _)).IsTrue();
        await Assert.That(DevelopmentPersonaCatalog.TryGet("AnIsH", out _)).IsTrue();
        await Assert.That(DevelopmentPersonaCatalog.TryGet("vikram", out _)).IsTrue();
        await Assert.That(DevelopmentPersonaCatalog.TryGet("VIKRAM", out _)).IsTrue();
    }

    [Test]
    public async Task Catalog_TryGet_ReturnsFalseForUnknownKeys()
    {
        await Assert.That(DevelopmentPersonaCatalog.TryGet("admin", out _)).IsFalse();
        await Assert.That(DevelopmentPersonaCatalog.TryGet("dev-user-001", out _)).IsFalse();
        await Assert.That(DevelopmentPersonaCatalog.TryGet("unknown-attacker", out _)).IsFalse();
        await Assert.That(DevelopmentPersonaCatalog.TryGet("", out _)).IsFalse();
        await Assert.That(DevelopmentPersonaCatalog.TryGet(null, out _)).IsFalse();
    }

    [Test]
    public async Task Catalog_NoPersonaHasNonPlatformRoles()
    {
        // Ensures no persona accidentally introduces a non-standard role.
        var allowedRoles = new HashSet<string>
        {
            BethuyaRoleNames.Admin,
            BethuyaRoleNames.Organizer,
            BethuyaRoleNames.Curator,
            BethuyaRoleNames.Attendee,
        };

        foreach (var persona in DevelopmentPersonaCatalog.All)
        {
            foreach (var role in persona.Roles)
            {
                await Assert.That(allowedRoles.Contains(role)).IsTrue();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Principal claim shape
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CreatePersonaPrincipal_UsesCorrectClaimConventions()
    {
        DevelopmentPersonaCatalog.TryGet("Anish", out var persona);
        var principal = DevelopmentAuthenticationDefaults.CreatePersonaPrincipal(persona!);

        await Assert.That(principal.FindFirst("sub")?.Value).IsEqualTo("dev-persona-anish");
        await Assert.That(principal.FindFirst("email")?.Value).IsEqualTo("anish@bethuya.dev");
        await Assert.That(principal.FindFirst("name")?.Value).IsEqualTo("Anish");
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Attendee)).IsTrue();
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Admin)).IsFalse();
        await Assert.That(principal.Identity?.IsAuthenticated).IsTrue();
        await Assert.That(principal.Identity?.AuthenticationType).IsEqualTo(DevelopmentAuthenticationDefaults.SchemeName);
    }

    [Test]
    public async Task CreatePersonaPrincipal_VikramHasAllRoles()
    {
        DevelopmentPersonaCatalog.TryGet("Vikram", out var persona);
        var principal = DevelopmentAuthenticationDefaults.CreatePersonaPrincipal(persona!);

        await Assert.That(principal.FindFirst("sub")?.Value).IsEqualTo("dev-persona-vikram");
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Admin)).IsTrue();
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Organizer)).IsTrue();
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Curator)).IsTrue();
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Attendee)).IsTrue();
    }

    [Test]
    public async Task CreateUnknownPersonaPrincipal_IsAuthenticatedButHasZeroRoles()
    {
        var principal = DevelopmentAuthenticationDefaults.CreateUnknownPersonaPrincipal("some-bad-key");

        await Assert.That(principal.Identity?.IsAuthenticated).IsTrue();
        await Assert.That(principal.Identity?.AuthenticationType).IsEqualTo(DevelopmentAuthenticationDefaults.SchemeName);
        // Must NOT be the fixed dev admin — fail-closed, not fail-open.
        await Assert.That(principal.FindFirst("sub")?.Value).IsNotEqualTo("dev-user-001");
        // Zero role claims.
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Admin)).IsFalse();
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Organizer)).IsFalse();
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Curator)).IsFalse();
        await Assert.That(principal.IsInRole(BethuyaRoleNames.Attendee)).IsFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TestServer: three-way resolution via header
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ApiAuthentication_NoPersonaHeader_ResolvesToLegacyDefault()
    {
        var (app, client) = await CreateApiTestServerAsync();

        var response = await client.GetAsync("/whoami");
        var body = await response.Content.ReadFromJsonAsync<WhoAmIResponse>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body!.Sub).IsEqualTo("dev-user-001");
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Admin);
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Organizer);
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Curator);
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Attendee);
    }

    [Test]
    public async Task ApiAuthentication_KnownPersonaHeader_ResolvesToCatalogPrincipal()
    {
        var (app, client) = await CreateApiTestServerAsync();
        client.DefaultRequestHeaders.Add(DevelopmentPersonaCatalog.PersonaHeaderName, "Priya");

        var response = await client.GetAsync("/whoami");
        var body = await response.Content.ReadFromJsonAsync<WhoAmIResponse>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body!.Sub).IsEqualTo("dev-persona-priya");
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Curator);
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Attendee);
        await Assert.That(body.Roles.Length).IsEqualTo(2);
    }

    [Test]
    public async Task ApiAuthentication_CaseInsensitivePersonaHeader_ResolvesCatalogPrincipal()
    {
        var (app, client) = await CreateApiTestServerAsync();
        client.DefaultRequestHeaders.Add(DevelopmentPersonaCatalog.PersonaHeaderName, "anish");

        var response = await client.GetAsync("/whoami");
        var body = await response.Content.ReadFromJsonAsync<WhoAmIResponse>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body!.Sub).IsEqualTo("dev-persona-anish");
    }

    [Test]
    public async Task ApiAuthentication_UnknownPersonaHeader_FailsClosed_NoRoles_403OnAllPolicies()
    {
        // This is the security-critical test: an unknown or tampered persona key must NOT
        // silently fall through to the fixed admin identity (fail-closed, not fail-open).
        // The principal is still authenticated (Development scheme "never anonymous" contract)
        // but carries zero roles, so every role-gated policy produces 403.
        var (app, client) = await CreateApiTestServerWithPoliciesAsync();
        client.DefaultRequestHeaders.Add(DevelopmentPersonaCatalog.PersonaHeaderName, "not-a-real-persona-xyz");

        // Whoami still responds (no authz policy) — confirms authentication succeeded.
        var whoamiResponse = await client.GetAsync("/whoami");
        var whoamiBody = await whoamiResponse.Content.ReadFromJsonAsync<WhoAmIResponse>();
        await Assert.That(whoamiResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(whoamiBody!.Sub).IsNotEqualTo("dev-user-001"); // NOT the fixed admin

        // Every role-gated endpoint returns 403.
        var adminResponse = await client.GetAsync("/admin-only");
        var organizerResponse = await client.GetAsync("/organizer-only");
        var curatorResponse = await client.GetAsync("/curator-only");
        var attendeeResponse = await client.GetAsync("/attendee-only");
        var orgOrCuratorResponse = await client.GetAsync("/org-or-curator-only");

        await Assert.That(adminResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(organizerResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(curatorResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(attendeeResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(orgOrCuratorResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Structured logging: EventId 3100 (resolved) / 3101 (unknown, fail-closed)
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ApiAuthentication_KnownPersonaHeader_LogsPersonaResolvedEvent()
    {
        // Verifies DevelopmentAuthenticationHandler actually emits the structured
        // "DevPersonaResolved" (EventId 3100) log entry containing the persona key
        // and resolved subject — not just that the principal is correct.
        var capturedLogs = new List<CapturedLogEntry>();
        var (app, client) = await CreateApiTestServerWithLoggingAsync(capturedLogs);

        client.DefaultRequestHeaders.Add(DevelopmentPersonaCatalog.PersonaHeaderName, "Priya");
        var response = await client.GetAsync("/whoami");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var resolvedEntry = capturedLogs.SingleOrDefault(e => e.EventId.Id == 3100);
        await Assert.That(resolvedEntry).IsNotNull();
        await Assert.That(resolvedEntry!.EventId.Name).IsEqualTo("DevPersonaResolved");
        await Assert.That(resolvedEntry.LogLevel).IsEqualTo(LogLevel.Information);
        await Assert.That(resolvedEntry.Message).Contains("Priya");
        await Assert.That(resolvedEntry.Message).Contains("dev-persona-priya");
        await Assert.That(capturedLogs.Any(e => e.EventId.Id == 3101)).IsFalse();
    }

    [Test]
    public async Task ApiAuthentication_UnknownPersonaHeader_LogsPersonaUnknownEvent()
    {
        // Verifies the fail-closed path emits the structured "DevPersonaUnknown"
        // (EventId 3101) warning — the observable audit trail for a tampered or
        // typo'd persona key that must never silently escalate to admin.
        var capturedLogs = new List<CapturedLogEntry>();
        var (app, client) = await CreateApiTestServerWithLoggingAsync(capturedLogs);

        client.DefaultRequestHeaders.Add(DevelopmentPersonaCatalog.PersonaHeaderName, "not-a-real-persona-xyz");
        var response = await client.GetAsync("/whoami");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var unknownEntry = capturedLogs.SingleOrDefault(e => e.EventId.Id == 3101);
        await Assert.That(unknownEntry).IsNotNull();
        await Assert.That(unknownEntry!.EventId.Name).IsEqualTo("DevPersonaUnknown");
        await Assert.That(unknownEntry.LogLevel).IsEqualTo(LogLevel.Warning);
        await Assert.That(unknownEntry.Message).Contains("not-a-real-persona-xyz");
        await Assert.That(capturedLogs.Any(e => e.EventId.Id == 3100)).IsFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Policy difference proof: Farah (Attendee) vs Vikram (all roles)
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task FarahVsVikram_OrganizerOrCuratorPolicy_DifferentOutcomes()
    {
        // This is the core "policy difference" proof: personas with different role sets
        // produce different authorization outcomes on the same endpoint.
        var (app, client) = await CreateApiTestServerWithPoliciesAsync();

        // Farah: Attendee only → must fail RequireOrganizerOrCurator.
        client.DefaultRequestHeaders.Remove(DevelopmentPersonaCatalog.PersonaHeaderName);
        client.DefaultRequestHeaders.Add(DevelopmentPersonaCatalog.PersonaHeaderName, "Farah");
        var farahResponse = await client.GetAsync("/org-or-curator-only");

        // Vikram: Admin + Organizer + Curator + Attendee → must pass.
        client.DefaultRequestHeaders.Remove(DevelopmentPersonaCatalog.PersonaHeaderName);
        client.DefaultRequestHeaders.Add(DevelopmentPersonaCatalog.PersonaHeaderName, "Vikram");
        var vikramResponse = await client.GetAsync("/org-or-curator-only");

        await Assert.That(farahResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(vikramResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task FarahAndPriya_AttendeePolicy_BothSucceed_ButFarahFailsCuratorPolicy()
    {
        var (app, client) = await CreateApiTestServerWithPoliciesAsync();

        // Farah: passes RequireAttendee but fails RequireCurator.
        client.DefaultRequestHeaders.Add(DevelopmentPersonaCatalog.PersonaHeaderName, "Farah");
        var farahAttendee = await client.GetAsync("/attendee-only");
        var farahCurator = await client.GetAsync("/curator-only");

        client.DefaultRequestHeaders.Remove(DevelopmentPersonaCatalog.PersonaHeaderName);
        client.DefaultRequestHeaders.Add(DevelopmentPersonaCatalog.PersonaHeaderName, "Priya");
        var priyaCurator = await client.GetAsync("/curator-only");

        await Assert.That(farahAttendee.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(farahCurator.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(priyaCurator.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Environment / Provider isolation
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ApiAuthentication_ProductionEnv_PersonaHeaderIgnored_LegacyDefault()
    {
        // Even when AllowInsecureDevAuth=true allows the handler to register in Production,
        // the handler must NOT honor any persona cookie or header outside Development.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "None",
            ["Authentication:AllowInsecureDevAuth"] = "true"
        });
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = builder.Build();
        _apps.Add(app);
        app.UseBethuyaAuthentication();
        app.MapGet("/whoami", (HttpContext ctx) => Results.Ok(new WhoAmIResponse(
            ctx.User.FindFirst("sub")?.Value,
            ctx.User.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToArray()
        ))).RequireAuthorization();

        await app.StartAsync();
        var client = app.GetTestClient();
        _clients.Add(client);

        // Send the Vikram persona header — it must be ignored.
        client.DefaultRequestHeaders.Add(DevelopmentPersonaCatalog.PersonaHeaderName, "Vikram");

        var response = await client.GetAsync("/whoami");
        var body = await response.Content.ReadFromJsonAsync<WhoAmIResponse>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        // Must still be the fixed legacy admin, not Vikram.
        await Assert.That(body!.Sub).IsEqualTo("dev-user-001");
        await Assert.That(body.Roles).Contains(BethuyaRoleNames.Admin);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DevelopmentAuthenticationStateProvider — per-request principal reflection
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task DevelopmentAuthStateProvider_ReflectsHttpContextUser_PerRequest()
    {
        // Two different HttpContexts → two different AuthenticationState results.
        DevelopmentPersonaCatalog.TryGet("Anish", out var anishPersona);
        DevelopmentPersonaCatalog.TryGet("Vikram", out var vikramPersona);

        var anishPrincipal = DevelopmentAuthenticationDefaults.CreatePersonaPrincipal(anishPersona!);
        var vikramPrincipal = DevelopmentAuthenticationDefaults.CreatePersonaPrincipal(vikramPersona!);

        var ctx1 = new DefaultHttpContext { User = anishPrincipal };
        var ctx2 = new DefaultHttpContext { User = vikramPrincipal };

        var accessor1 = Substitute.For<IHttpContextAccessor>();
        accessor1.HttpContext.Returns(ctx1);
        var accessor2 = Substitute.For<IHttpContextAccessor>();
        accessor2.HttpContext.Returns(ctx2);

        var provider1 = new DevelopmentAuthenticationStateProvider(accessor1);
        var provider2 = new DevelopmentAuthenticationStateProvider(accessor2);

        var state1 = await provider1.GetAuthenticationStateAsync();
        var state2 = await provider2.GetAuthenticationStateAsync();

        await Assert.That(state1.User.FindFirst("sub")?.Value).IsEqualTo("dev-persona-anish");
        await Assert.That(state2.User.FindFirst("sub")?.Value).IsEqualTo("dev-persona-vikram");
        await Assert.That(state1.User.FindFirst("sub")?.Value)
            .IsNotEqualTo(state2.User.FindFirst("sub")?.Value);
    }

    [Test]
    public async Task DevelopmentAuthStateProvider_FallsBackToLegacyWhenHttpContextIsNull()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var provider = new DevelopmentAuthenticationStateProvider(accessor);
        var state = await provider.GetAuthenticationStateAsync();

        await Assert.That(state.User.FindFirst("sub")?.Value).IsEqualTo("dev-user-001");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DevPersonaPropagationHandler
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task DevPersonaPropagationHandler_AttachesHeaderWhenCookiePresent()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{DevelopmentPersonaCatalog.PersonaCookieName}=Farah";

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var handler = new DevPersonaPropagationHandler(accessor)
        {
            InnerHandler = new TestInnerHandler()
        };
        using var client = new HttpClient(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://backend/api/test");
        using var response = await client.SendAsync(request);

        await Assert.That(
            request.Headers.TryGetValues(DevelopmentPersonaCatalog.PersonaHeaderName, out var values)
            && values.FirstOrDefault() == "Farah"
        ).IsTrue();
    }

    [Test]
    public async Task DevPersonaPropagationHandler_DoesNothingWhenNoCookiePresent()
    {
        var httpContext = new DefaultHttpContext(); // no cookies
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var handler = new DevPersonaPropagationHandler(accessor)
        {
            InnerHandler = new TestInnerHandler()
        };
        using var client = new HttpClient(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://backend/api/test");
        using var response = await client.SendAsync(request);

        await Assert.That(
            request.Headers.Contains(DevelopmentPersonaCatalog.PersonaHeaderName)
        ).IsFalse();
    }

    [Test]
    public async Task DevPersonaPropagationHandler_DoesNotOverwriteExistingHeader()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{DevelopmentPersonaCatalog.PersonaCookieName}=Farah";

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var handler = new DevPersonaPropagationHandler(accessor)
        {
            InnerHandler = new TestInnerHandler()
        };
        using var client = new HttpClient(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://backend/api/test");
        // Pre-set the header with a different value (caller already set it).
        request.Headers.TryAddWithoutValidation(DevelopmentPersonaCatalog.PersonaHeaderName, "Vikram");

        using var response = await client.SendAsync(request);

        // Header must still be Vikram, not overwritten with Farah from cookie.
        request.Headers.TryGetValues(DevelopmentPersonaCatalog.PersonaHeaderName, out var values);
        await Assert.That(values?.FirstOrDefault()).IsEqualTo("Vikram");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<(WebApplication App, HttpClient Client)> CreateApiTestServerAsync()
    {
        var builder = CreateBuilderWithProviderNone();
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = builder.Build();
        _apps.Add(app);

        app.UseBethuyaAuthentication();
        app.MapGet("/whoami", (HttpContext ctx) => Results.Ok(new WhoAmIResponse(
            ctx.User.FindFirst("sub")?.Value,
            ctx.User.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToArray()
        ))).RequireAuthorization();

        await app.StartAsync();
        var client = app.GetTestClient();
        _clients.Add(client);

        return (app, client);
    }

    private async Task<(WebApplication App, HttpClient Client)> CreateApiTestServerWithPoliciesAsync()
    {
        var builder = CreateBuilderWithProviderNone();
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = builder.Build();
        _apps.Add(app);

        app.UseBethuyaAuthentication();
        app.MapGet("/whoami", (HttpContext ctx) => Results.Ok(new WhoAmIResponse(
            ctx.User.FindFirst("sub")?.Value,
            ctx.User.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToArray()
        ))).RequireAuthorization();
        app.MapGet("/admin-only", () => Results.Ok("admin"))
            .RequireAuthorization(BethuyaPolicyNames.RequireAdmin);
        app.MapGet("/organizer-only", () => Results.Ok("organizer"))
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);
        app.MapGet("/curator-only", () => Results.Ok("curator"))
            .RequireAuthorization(BethuyaPolicyNames.RequireCurator);
        app.MapGet("/attendee-only", () => Results.Ok("attendee"))
            .RequireAuthorization(BethuyaPolicyNames.RequireAttendee);
        app.MapGet("/org-or-curator-only", () => Results.Ok("org-or-curator"))
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizerOrCurator);

        await app.StartAsync();
        var client = app.GetTestClient();
        _clients.Add(client);

        return (app, client);
    }

    private async Task<(WebApplication App, HttpClient Client)> CreateApiTestServerWithLoggingAsync(
        List<CapturedLogEntry> capturedLogs)
    {
        var builder = CreateBuilderWithProviderNone();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new CapturingLoggerProvider(capturedLogs));
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = builder.Build();
        _apps.Add(app);

        app.UseBethuyaAuthentication();
        app.MapGet("/whoami", (HttpContext ctx) => Results.Ok(new WhoAmIResponse(
            ctx.User.FindFirst("sub")?.Value,
            ctx.User.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToArray()
        ))).RequireAuthorization();

        await app.StartAsync();
        var client = app.GetTestClient();
        _clients.Add(client);

        return (app, client);
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

    private sealed record WhoAmIResponse(string? Sub, string[] Roles);

    /// <summary>Minimal stub inner handler so we can inspect outbound request headers.</summary>
    private sealed class TestInnerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    /// <summary>A single log entry captured by <see cref="CapturingLoggerProvider"/>.</summary>
    private sealed record CapturedLogEntry(LogLevel LogLevel, EventId EventId, string Message);

    /// <summary>
    /// Real <see cref="ILoggerProvider"/> that captures every log entry emitted during a test,
    /// used to assert that <see cref="DevelopmentAuthenticationHandler"/> actually emits the
    /// structured "DevPersonaResolved" (3100) / "DevPersonaUnknown" (3101) events — not a fake
    /// stand-in, but a real provider wired through <c>ILoggingBuilder.AddProvider</c>.
    /// </summary>
    private sealed class CapturingLoggerProvider(List<CapturedLogEntry> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(sink);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<CapturedLogEntry> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                sink.Add(new CapturedLogEntry(logLevel, eventId, formatter(state, exception)));
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DevPersonaEndpointExtensions — CSRF (Sec-Fetch-Site) and open-redirect guards
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task PersonaEndpoint_CrossSiteRequest_Returns403()
    {
        // A cross-origin page (<img>, <script>, etc.) sends Sec-Fetch-Site: cross-site.
        // The endpoint must reject it unconditionally — persona changes via cross-origin
        // requests are the silent-escalation vector the spec forbids.
        var (_, client) = await CreateWebPersonaEndpointServerAsync();
        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");

        var response = await client.GetAsync("/dev/persona/Vikram");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task PersonaEndpoint_CrossSiteRequest_ClearAlsoReturns403()
    {
        // The clear endpoint performs a state-changing side effect too (cookie deletion).
        var (_, client) = await CreateWebPersonaEndpointServerAsync();
        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");

        var response = await client.GetAsync("/dev/persona/clear");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task PersonaEndpoint_SameOriginRequest_SetsPersonaCookieAndRedirects()
    {
        // A same-origin request (developer navigating within the app) must succeed.
        var (_, client) = await CreateWebPersonaEndpointServerAsync();
        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");

        var response = await client.GetAsync("/dev/persona/Farah");

        // Should redirect (302) and set the persona cookie.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        response.Headers.TryGetValues("Set-Cookie", out var cookieValues);
        await Assert.That(
            cookieValues?.Any(v => v.StartsWith(DevelopmentPersonaCatalog.PersonaCookieName, StringComparison.Ordinal))
        ).IsTrue();
    }

    [Test]
    public async Task PersonaEndpoint_NoFetchSiteHeader_DirectNavigation_SetsPersonaCookieAndRedirects()
    {
        // Absent Sec-Fetch-Site header = typed URL or bookmark (direct navigation).
        // This is the primary developer UX and must never be blocked.
        var (_, client) = await CreateWebPersonaEndpointServerAsync();
        // No Sec-Fetch-Site header added.

        var response = await client.GetAsync("/dev/persona/Anish");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
    }

    [Test]
    public async Task PersonaEndpoint_AbsoluteReturnUrl_RedirectsToSafeRoot()
    {
        // Open redirect: attacker crafts a link that escalates persona AND sends dev to evil.example.
        // The endpoint must ignore the absolute URL and redirect to "/" instead.
        var (_, client) = await CreateWebPersonaEndpointServerAsync();

        var response = await client.GetAsync("/dev/persona/Anish?returnUrl=https://evil.example/phish");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? "";
        await Assert.That(location).IsEqualTo("/");
    }

    [Test]
    public async Task PersonaEndpoint_ProtocolRelativeReturnUrl_RedirectsToSafeRoot()
    {
        // Protocol-relative URLs (//evil.example) are also absolute-destination attacks.
        var (_, client) = await CreateWebPersonaEndpointServerAsync();

        // URL-encoded "//" so the router doesn't parse it as a path separator.
        var response = await client.GetAsync("/dev/persona/Anish?returnUrl=%2F%2Fevil.example");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? "";
        await Assert.That(location).IsEqualTo("/");
    }

    [Test]
    public async Task PersonaEndpoint_ValidLocalReturnUrl_RedirectsToIt()
    {
        // A legitimate local return URL must be honoured after persona selection.
        var (_, client) = await CreateWebPersonaEndpointServerAsync();

        var response = await client.GetAsync("/dev/persona/Anish?returnUrl=/events");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? "";
        await Assert.That(location).IsEqualTo("/events");
    }

    private async Task<(WebApplication App, HttpClient Client)> CreateWebPersonaEndpointServerAsync()
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

        var app = builder.Build();
        _apps.Add(app);

        app.MapDevPersonaEndpoints();

        await app.StartAsync();
        var client = app.GetTestClient();
        _clients.Add(client);

        return (app, client);
    }
}

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ServiceDefaults.Auth;
using ServiceDefaults.Auth.Observability;

namespace Hackmum.Bethuya.Tests.Auth.Observability;

/// <summary>
/// PR5: the backend <see cref="BethuyaAuthorizationAuditMiddlewareResultHandler"/> records framework-level
/// authorization outcomes — 401 (challenged/anonymous) vs 403 (forbidden/wrong-role) — on the
/// <c>authorization.outcomes</c> counter, dimensioned by route group, while leaving the HTTP behavior
/// (status codes) unchanged.
/// </summary>
public sealed class AuthOutcomeMetricsTests
{
    [Test]
    public async Task Anonymous_Records401_ForRouteGroup()
    {
        await using var app = await StartHostAsync();
        using var client = app.GetTestClient();

        var metrics = app.Services.GetRequiredService<AuthorizationMetrics>();
        var measurements = new ConcurrentQueue<MetricsTestHarness.Measurement>();
        using var listener = MetricsTestHarness.ListenToAuthorizationMeter(measurements, metrics);

        var response = await client.GetAsync(new Uri("/api/organizer/ping", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var outcome = measurements.Single(m => m.Instrument == "authorization.outcomes");
        await Assert.That(outcome.Tags["status_code"]).IsEqualTo(401);
        await Assert.That(outcome.Tags["route_group"]).IsEqualTo("organizer");
    }

    [Test]
    public async Task WrongRole_Records403_ForRouteGroup()
    {
        await using var app = await StartHostAsync();
        using var client = app.GetTestClient();

        var metrics = app.Services.GetRequiredService<AuthorizationMetrics>();
        var measurements = new ConcurrentQueue<MetricsTestHarness.Measurement>();
        using var listener = MetricsTestHarness.ListenToAuthorizationMeter(measurements, metrics);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/organizer/ping", UriKind.Relative));
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Attendee);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, "attendee-7");

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);

        var outcome = measurements.Single(m => m.Instrument == "authorization.outcomes");
        await Assert.That(outcome.Tags["status_code"]).IsEqualTo(403);
        await Assert.That(outcome.Tags["route_group"]).IsEqualTo("organizer");
    }

    [Test]
    public async Task Denial_AuditRecord_CarriesActualRoutePolicy_NotHardCodedAttendee()
    {
        var auditor = new RecordingAuditor();

        await using var app = await HeaderRoleAuthHost.StartAsync(
            a => a.MapGet("/api/organizer/ping", () => Results.Ok())
                .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer),
            enforceFallback: false,
            configureServices: services =>
            {
                services.AddSingleton<IAuthorizationAuditor>(auditor);
                services.AddSingleton<IAuthorizationMiddlewareResultHandler, BethuyaAuthorizationAuditMiddlewareResultHandler>();
            });
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/api/organizer/ping", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var record = auditor.Events.Single(e => e.Route == "/api/organizer/ping");
        await Assert.That(record.PolicyName).IsEqualTo(BethuyaPolicyNames.RequireOrganizer);
        await Assert.That(record.ResourceType).IsEqualTo("organizer");
        await Assert.That(record.OutcomeStatusCode).IsEqualTo(401);
    }

    private static Task<WebApplication> StartHostAsync() =>
        HeaderRoleAuthHost.StartAsync(
            a => a.MapGet("/api/organizer/ping", () => Results.Ok())
                .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer),
            enforceFallback: false,
            configureServices: services =>
                services.AddSingleton<IAuthorizationMiddlewareResultHandler, BethuyaAuthorizationAuditMiddlewareResultHandler>());
}

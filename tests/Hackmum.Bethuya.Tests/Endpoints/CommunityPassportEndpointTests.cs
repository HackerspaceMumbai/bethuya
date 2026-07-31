using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Tests.Endpoints;

public sealed class CommunityPassportEndpointTests : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly string _dbName = $"community-passport-endpoint-tests-{Guid.NewGuid():N}";

    [Before(Test)]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("RequireOrganizer", policy => policy.RequireRole("Organizer", "Admin")));
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddDbContext<BethuyaDbContext>(options =>
            options
                .UseInMemoryDatabase(_dbName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddScoped<CommunityPassportService>();
        builder.Services.AddScoped<ParticipationLedgerService>();
        builder.Services.AddScoped<CommunityJourneyReadModelService>();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapCommunityPassportEndpoints();
        await _app.StartAsync();

        _client = _app.GetTestClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
    }

    [After(Test)]
    public async Task Teardown()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Test]
    public async Task GetPassport_ReturnsProvisionedPassportForAuthenticatedUser()
    {
        var response = await _client.GetAsync("/api/community/passport");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var passport = await response.Content.ReadFromJsonAsync<CommunityPassportResponse>(JsonOptions);
        await Assert.That(passport).IsNotNull();
        await Assert.That(passport!.DisplayName).IsEqualTo("Passport Tester");
        await Assert.That(passport.Email).IsEqualTo("passport@example.com");
    }

    [Test]
    public async Task SavePrivacy_PersistsVisibilityChanges()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/community/passport/privacy",
            new UpdateCommunityPassportPrivacyRequest(
                Hackmum.Bethuya.Core.Enums.ProfileVisibilityScope.OrganizerOnly,
                ShareParticipationWithOrganizers: false,
                IsDiscoverableToCommunity: false));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var privacy = await response.Content.ReadFromJsonAsync<PassportPrivacyResponse>(JsonOptions);
        await Assert.That(privacy).IsNotNull();
        await Assert.That(privacy!.Visibility.ToString()).IsEqualTo("OrganizerOnly");
        await Assert.That(privacy.ShareParticipationWithOrganizers).IsFalse();
        await Assert.That(privacy.IsDiscoverableToCommunity).IsFalse();

        var persistedResponse = await _client.GetAsync("/api/community/passport");
        await Assert.That(persistedResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        await using var scope = _app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
        var hasPersistedPrivacy = await db.CommunityMembers.AnyAsync(member =>
            member.Visibility == Hackmum.Bethuya.Core.Enums.ProfileVisibilityScope.OrganizerOnly
            && !member.ShareParticipationWithOrganizers
            && !member.IsDiscoverableToCommunity);

        await Assert.That(hasPersistedPrivacy).IsTrue();
    }

    [Test]
    public async Task ParticipationEndpoints_WriteAndReadTimeline()
    {
        var now = new DateTimeOffset(2026, 7, 31, 12, 15, 0, TimeSpan.Zero);
        var writeResponse = await _client.PostAsJsonAsync(
            "/api/community/passport/participation",
            new UpsertParticipationEntriesRequest(
            [
                new ParticipationEntryWriteRequest(
                    Connector: ParticipationConnectorKind.Discord,
                    ExternalMemberKey: "discord:user:1",
                    Activity: ParticipationActivityKind.JoinedCommunity,
                    OccurredAt: now,
                    Evidence: "Joined #welcome",
                    ProvenanceKey: "discord:welcome:1")
            ]));

        await Assert.That(writeResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var writeResult = await writeResponse.Content.ReadFromJsonAsync<ParticipationEntryWriteResult>(JsonOptions);
        await Assert.That(writeResult).IsNotNull();
        await Assert.That(writeResult!.StoredCount).IsEqualTo(1);

        var timelineResponse = await _client.GetAsync("/api/community/passport/participation/timeline?limit=10");
        await Assert.That(timelineResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var timeline = await timelineResponse.Content.ReadFromJsonAsync<MemberParticipationTimelineResponse>(JsonOptions);
        await Assert.That(timeline).IsNotNull();
        await Assert.That(timeline!.Entries.Count).IsEqualTo(1);
        await Assert.That(timeline.Entries[0].ProvenanceKey).IsEqualTo("discord:welcome:1");
    }

    [Test]
    public async Task JourneyEndpoint_ReturnsLifecycleJourneyProjection()
    {
        var writeResponse = await _client.PostAsJsonAsync(
            "/api/community/passport/participation",
            new UpsertParticipationEntriesRequest(
            [
                new ParticipationEntryWriteRequest(
                    Connector: ParticipationConnectorKind.GitHub,
                    ExternalMemberKey: "github:passport-user",
                    Activity: ParticipationActivityKind.Volunteered,
                    OccurredAt: DateTimeOffset.UtcNow.AddDays(-2),
                    Evidence: "Helped with volunteer onboarding",
                    ProvenanceKey: "journey:test:volunteer:1")
            ]));

        await Assert.That(writeResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var journeyResponse = await _client.GetAsync("/api/community/passport/journey?timelineLimit=10");

        await Assert.That(journeyResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var journey = await journeyResponse.Content.ReadFromJsonAsync<CommunityJourneyProjectionResponse>(JsonOptions);
        await Assert.That(journey).IsNotNull();
        await Assert.That(journey!.JourneyScore).IsGreaterThan(0);
        await Assert.That(journey.Timeline.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task DashboardReadModel_ReturnsMetricsForOrganizer()
    {
        var response = await _client.GetAsync("/api/community/passport/dashboard/read-model?lookbackDays=90");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<CommunityHealthDashboardReadModelResponse>(JsonOptions);
        await Assert.That(dashboard).IsNotNull();
        await Assert.That(dashboard!.LookbackDays).IsEqualTo(90);
    }

    [Test]
    public async Task DashboardReadModel_ForbidsNonOrganizerRole()
    {
        using var attendeeRequest = new HttpRequestMessage(HttpMethod.Get, "/api/community/passport/dashboard/read-model");
        attendeeRequest.Headers.Add("x-test-role", "Attendee");

        var response = await _client.SendAsync(attendeeRequest);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers.TryGetValue("x-test-role", out var requestedRole)
                && !string.IsNullOrWhiteSpace(requestedRole)
                    ? requestedRole.ToString()
                    : "Organizer";

            Claim[] claims =
            [
                new Claim("sub", "passport-user"),
                new Claim("name", "Passport Tester"),
                new Claim(ClaimTypes.Email, "passport@example.com"),
                new Claim(ClaimTypes.Role, role)
            ];

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

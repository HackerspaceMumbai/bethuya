using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Tests.Endpoints;

public sealed class CommunityPassportParticipationAuthorizationTests
{
    [Test]
    public async Task ParticipationWrite_ForbidsAttendeeWithoutConnectorClaim()
    {
        await using var app = await CreateAppAsync(role: "Attendee", connectorIngestClaim: false, scopeClaim: null);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var response = await client.PostAsJsonAsync("/api/community/passport/participation", CreateRequest());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task ParticipationWrite_AllowsConnectorClaimWithoutOrganizerRole()
    {
        await using var app = await CreateAppAsync(role: "Attendee", connectorIngestClaim: true, scopeClaim: null);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var response = await client.PostAsJsonAsync("/api/community/passport/participation", CreateRequest());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ParticipationWrite_AllowsScopeClaimContainingConnectorIngestToken()
    {
        await using var app = await CreateAppAsync(
            role: "Attendee",
            connectorIngestClaim: false,
            scopeClaim: "openid profile connector.ingest");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var response = await client.PostAsJsonAsync("/api/community/passport/participation", CreateRequest());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private static UpsertParticipationEntriesRequest CreateRequest()
        => new(
        [
            new ParticipationEntryWriteRequest(
                Connector: ParticipationConnectorKind.Discord,
                ExternalMemberKey: "discord:user:99",
                Activity: ParticipationActivityKind.JoinedCommunity,
                OccurredAt: new DateTimeOffset(2026, 7, 31, 18, 45, 0, TimeSpan.Zero),
                Evidence: "Joined #welcome",
                ProvenanceKey: "discord:welcome:99")
        ]);

    private static async Task<WebApplication> CreateAppAsync(
        string role,
        bool connectorIngestClaim,
        string? scopeClaim)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddSingleton(new AuthorizationTestClaims(role, connectorIngestClaim, scopeClaim));
        builder.AddBethuyaAuthorization();
        builder.Services.AddDbContext<BethuyaDbContext>(options =>
            options
                .UseInMemoryDatabase($"community-passport-auth-tests-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddScoped<CommunityPassportService>();
        builder.Services.AddScoped<ParticipationLedgerService>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCommunityPassportEndpoints();
        await app.StartAsync();
        return app;
    }

    private sealed class AuthorizationTestClaims(string role, bool connectorIngestClaim, string? scopeClaim)
    {
        public string Role { get; } = role;
        public bool ConnectorIngestClaim { get; } = connectorIngestClaim;
        public string? ScopeClaim { get; } = scopeClaim;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AuthorizationTestClaims claimsConfig)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new("sub", "participation-auth-user"),
                new("name", "Participation Auth User"),
                new(ClaimTypes.Email, "participation-auth@example.com"),
                new(ClaimTypes.Role, claimsConfig.Role)
            };

            if (claimsConfig.ConnectorIngestClaim)
            {
                claims.Add(new Claim("connector.ingest", "true"));
            }

            if (!string.IsNullOrWhiteSpace(claimsConfig.ScopeClaim))
            {
                claims.Add(new Claim("scope", claimsConfig.ScopeClaim));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Tests.Endpoints;

public sealed class CommunityGraphEndpointTests
{
    [Test]
    public async Task Graph_RequiresAuthentication_AndRoundTripsTypedEvidenceWithoutPrivateFields()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, GraphAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        var databaseName = $"graph-api-{Guid.NewGuid()}";
        builder.Services.AddDbContext<BethuyaDbContext>(o => o.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<CommunityGraphService>();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCommunityGraphEndpoints();
        await app.StartAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
        var member = new CommunityMember { UserId = "graph-viewer", DisplayName = "Graph Viewer", Email = "private@example.com" };
        db.CommunityMembers.Add(member);
        db.ParticipationLedgerEntries.Add(new()
        {
            CommunityMemberId = member.Id, ExternalMemberKey = "private-external-key", ProvenanceKey = "private-provenance",
            IsVerified = true, Evidence = "Confirmed community membership", Activity = ParticipationActivityKind.JoinedCommunity,
            OccurredAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
        using var client = app.GetTestClient();
        using var denied = await client.GetAsync("/api/community/graph");
        await Assert.That(denied.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Add("x-graph-test", "yes");
        using var response = await client.GetAsync("/api/community/graph");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Headers.CacheControl!.NoStore).IsTrue();
        var payload = await response.Content.ReadAsStringAsync();
        await Assert.That(payload.Contains("private-", StringComparison.Ordinal)).IsFalse();
        await Assert.That(payload.Contains("private@example.com", StringComparison.Ordinal)).IsFalse();
        var graph = await response.Content.ReadFromJsonAsync<CommunityGraphSnapshot>();
        await Assert.That(graph!.Evidence.Single(p => p.EntryId == graph.Relationships.Single().EvidenceIds.Single()).Summary).IsEqualTo("Confirmed community membership");
    }

    private sealed class GraphAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            Request.Headers.ContainsKey("x-graph-test")
                ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "graph-viewer")], Scheme.Name)), Scheme.Name))
                : AuthenticateResult.NoResult());
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Hackmum.Bethuya.Agents.Workflows;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Endpoints;

public sealed class ApprovalEndpointAuthorizationTests
{
    private static readonly Guid DecisionId = Guid.Parse("01985f65-71aa-7e2b-9581-b3e8b0548b70");
    private static readonly Guid EntityId = Guid.Parse("01985f65-71aa-7e2b-9581-b3e8b0548b71");

    [Test]
    [Arguments("pending")]
    [Arguments("entity")]
    [Arguments("approve")]
    [Arguments("reject")]
    public async Task ApprovalRoutes_RejectAnonymousCallers(string route)
    {
        var repository = CreateRepository();
        await using var app = await CreateAppAsync(role: null, repository);
        using var client = app.GetTestClient();

        using var response = await SendAsync(client, route);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(repository.ReceivedCalls()).IsEmpty();
    }

    [Test]
    [Arguments(BethuyaRoleNames.Attendee, "pending")]
    [Arguments(BethuyaRoleNames.Attendee, "entity")]
    [Arguments(BethuyaRoleNames.Attendee, "approve")]
    [Arguments(BethuyaRoleNames.Attendee, "reject")]
    [Arguments(BethuyaRoleNames.Curator, "pending")]
    [Arguments(BethuyaRoleNames.Curator, "entity")]
    [Arguments(BethuyaRoleNames.Curator, "approve")]
    [Arguments(BethuyaRoleNames.Curator, "reject")]
    public async Task ApprovalRoutes_ForbidInsufficientRoles(string role, string route)
    {
        var repository = CreateRepository();
        await using var app = await CreateAppAsync(role, repository);
        using var client = CreateAuthenticatedClient(app);

        using var response = await SendAsync(client, route);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(repository.ReceivedCalls()).IsEmpty();
    }

    [Test]
    [Arguments(BethuyaRoleNames.Organizer, "pending")]
    [Arguments(BethuyaRoleNames.Organizer, "entity")]
    [Arguments(BethuyaRoleNames.Organizer, "approve")]
    [Arguments(BethuyaRoleNames.Organizer, "reject")]
    [Arguments(BethuyaRoleNames.Admin, "pending")]
    [Arguments(BethuyaRoleNames.Admin, "entity")]
    [Arguments(BethuyaRoleNames.Admin, "approve")]
    [Arguments(BethuyaRoleNames.Admin, "reject")]
    public async Task ApprovalRoutes_AllowOrganizerAndAdmin(string role, string route)
    {
        var repository = CreateRepository();
        await using var app = await CreateAppAsync(role, repository);
        using var client = CreateAuthenticatedClient(app);

        using var response = await SendAsync(client, route);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(repository.ReceivedCalls()).IsNotEmpty();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string route) => route switch
    {
        "pending" => client.GetAsync("/api/approvals/pending"),
        "entity" => client.GetAsync($"/api/approvals/registration/{EntityId}"),
        "approve" => client.PostAsJsonAsync($"/api/approvals/{DecisionId}/approve", new ApproveRequest("ok")),
        "reject" => client.PostAsJsonAsync($"/api/approvals/{DecisionId}/reject", new RejectRequest("no")),
        _ => throw new ArgumentOutOfRangeException(nameof(route), route, null)
    };

    private static IDecisionRepository CreateRepository()
    {
        var repository = Substitute.For<IDecisionRepository>();
        repository.GetPendingAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Decision>());
        repository.GetByEntityAsync("registration", EntityId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Decision>());
        repository.GetByIdAsync(DecisionId, Arg.Any<CancellationToken>())
            .Returns(new Decision
            {
                Id = DecisionId,
                EntityType = "registration",
                EntityId = EntityId,
                Status = DecisionStatus.Pending,
                DecidedBy = "organizer@example.com"
            });
        return repository;
    }

    private static HttpClient CreateAuthenticatedClient(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        return client;
    }

    private static async Task<WebApplication> CreateAppAsync(string? role, IDecisionRepository repository)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddSingleton(new TestRole(role));
        builder.AddBethuyaAuthorization();
        builder.Services.AddSingleton(repository);
        builder.Services.AddScoped<ApprovalWorkflow>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapApprovalEndpoints();
        await app.StartAsync();
        return app;
    }

    private sealed record TestRole(string? Value);

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestRole role)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (role.Value is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Claim[] claims =
            [
                new Claim("sub", "approval-role-test-user"),
                new Claim(ClaimTypes.Role, role.Value)
            ];

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}

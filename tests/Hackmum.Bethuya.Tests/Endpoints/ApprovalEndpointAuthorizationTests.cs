using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Hackmum.Bethuya.Agents.Workflows;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Endpoints;

public sealed class ApprovalEndpointAuthorizationTests
{
    private static readonly Guid TestDecisionId = Guid.Parse("01985f65-71aa-7e2b-9581-b3e8b0548b20");

    [Test]
    public async Task Pending_DeniesAnonymousRequests()
    {
        var repository = CreateRepository();
        await using var app = await CreateAppAsync(role: null, repository);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/approvals/pending");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await repository.DidNotReceive().GetPendingAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Pending_ForbidsAttendeeRole()
    {
        var repository = CreateRepository();
        await using var app = await CreateAppAsync(BethuyaRoleNames.Attendee, repository);
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/approvals/pending");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await repository.DidNotReceive().GetPendingAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Pending_AllowsCuratorRole()
    {
        var repository = CreateRepository();
        await using var app = await CreateAppAsync(BethuyaRoleNames.Curator, repository);
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/approvals/pending");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task EntityDecisions_DeniesAnonymousRequests()
    {
        var repository = CreateRepository();
        await using var app = await CreateAppAsync(role: null, repository);
        using var client = app.GetTestClient();

        var response = await client.GetAsync($"/api/approvals/Agenda/{TestDecisionId}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await repository.DidNotReceive().GetByEntityAsync("Agenda", TestDecisionId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EntityDecisions_ForbidsAttendeeRole()
    {
        var repository = CreateRepository();
        await using var app = await CreateAppAsync(BethuyaRoleNames.Attendee, repository);
        using var client = CreateClient(app);

        var response = await client.GetAsync($"/api/approvals/Agenda/{TestDecisionId}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task EntityDecisions_AllowsOrganizerRole()
    {
        var repository = CreateRepository();
        await using var app = await CreateAppAsync(BethuyaRoleNames.Organizer, repository);
        using var client = CreateClient(app);

        var response = await client.GetAsync($"/api/approvals/Agenda/{TestDecisionId}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Approve_DeniesAnonymousRequestsWithoutChangingDecision()
    {
        var decision = CreatePendingDecision();
        var repository = CreateRepository(decision);
        await using var app = await CreateAppAsync(role: null, repository);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync($"/api/approvals/{TestDecisionId}/approve", new { reason = "ok" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(decision.Status).IsEqualTo(DecisionStatus.Pending);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<Decision>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Approve_ForbidsCuratorRoleWithoutChangingDecision()
    {
        var decision = CreatePendingDecision();
        var repository = CreateRepository(decision);
        await using var app = await CreateAppAsync(BethuyaRoleNames.Curator, repository);
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync($"/api/approvals/{TestDecisionId}/approve", new { reason = "ok" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(decision.Status).IsEqualTo(DecisionStatus.Pending);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<Decision>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Approve_AllowsOrganizerRole()
    {
        var decision = CreatePendingDecision();
        var repository = CreateRepository(decision);
        await using var app = await CreateAppAsync(BethuyaRoleNames.Organizer, repository);
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync($"/api/approvals/{TestDecisionId}/approve", new { reason = "ok" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(decision.Status).IsEqualTo(DecisionStatus.Applied);
        await repository.Received(1).UpdateAsync(decision, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Reject_DeniesAnonymousRequestsWithoutChangingDecision()
    {
        var decision = CreatePendingDecision();
        var repository = CreateRepository(decision);
        await using var app = await CreateAppAsync(role: null, repository);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync($"/api/approvals/{TestDecisionId}/reject", new { reason = "no" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(decision.Status).IsEqualTo(DecisionStatus.Pending);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<Decision>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Reject_ForbidsAttendeeRoleWithoutChangingDecision()
    {
        var decision = CreatePendingDecision();
        var repository = CreateRepository(decision);
        await using var app = await CreateAppAsync(BethuyaRoleNames.Attendee, repository);
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync($"/api/approvals/{TestDecisionId}/reject", new { reason = "no" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(decision.Status).IsEqualTo(DecisionStatus.Pending);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<Decision>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Reject_AllowsOrganizerRole()
    {
        var decision = CreatePendingDecision();
        var repository = CreateRepository(decision);
        await using var app = await CreateAppAsync(BethuyaRoleNames.Organizer, repository);
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync($"/api/approvals/{TestDecisionId}/reject", new { reason = "no" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(decision.Status).IsEqualTo(DecisionStatus.Applied);
        await repository.Received(1).UpdateAsync(decision, Arg.Any<CancellationToken>());
    }

    private static Decision CreatePendingDecision() => new()
    {
        Id = TestDecisionId,
        EntityType = "Agenda",
        EntityId = Guid.Parse("01985f65-71aa-7e2b-9581-b3e8b0548b21"),
        DecidedBy = "agent",
        Status = DecisionStatus.Pending
    };

    private static IDecisionRepository CreateRepository(Decision? decision = null)
    {
        var repository = Substitute.For<IDecisionRepository>();
        repository.GetPendingAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Decision>());
        repository.GetByEntityAsync("Agenda", TestDecisionId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Decision>());
        repository.GetByIdAsync(TestDecisionId, Arg.Any<CancellationToken>()).Returns(decision);
        return repository;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        return client;
    }

    /// <summary>
    /// Builds a host that maps only the approval routes. A null role means the request carries no
    /// credentials, which exercises the anonymous path.
    /// </summary>
    private static async Task<WebApplication> CreateAppAsync(string? role, IDecisionRepository repository)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddSingleton(new TestRoleProvider(role));
        builder.AddBethuyaAuthorization();
        builder.Services.AddSingleton(repository);
        builder.Services.AddSingleton(new ApprovalWorkflow(
            NullLoggerFactory.Instance.CreateLogger<ApprovalWorkflow>()));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapApprovalEndpoints();
        await app.StartAsync();
        return app;
    }

    private sealed class TestRoleProvider(string? role)
    {
        public string? Role { get; } = role;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestRoleProvider roleProvider)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (string.IsNullOrWhiteSpace(roleProvider.Role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Claim[] claims =
            [
                new Claim("sub", "approval-role-test-user"),
                new Claim("name", "Approval Role Test User"),
                new Claim(ClaimTypes.Email, "approval-role-test@example.com"),
                new Claim(ClaimTypes.Role, roleProvider.Role)
            ];

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}

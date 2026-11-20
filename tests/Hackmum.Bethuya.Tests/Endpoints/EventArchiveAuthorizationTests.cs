using System.Security.Claims;
using System.Text.Encodings.Web;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServiceDefaults.Auth;
using System.Net.Http.Json;

namespace Hackmum.Bethuya.Tests.Endpoints;

public sealed class EventArchiveAuthorizationTests
{
    [Test]
    public async Task ArchiveLifecycleAndPublication_RequireOrganizerAndUseAuthenticatedActor()
    {
        var id = Guid.NewGuid();
        var orchestrator = Substitute.For<IEventLifecycleOrchestrator>();
        orchestrator.PreviewArchivePublicationAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new EventArchivePublicationPreview(
                id,
                new EventArchivePreview(
                    "events/2026/2026-11-28-community-summit",
                    "preview",
                    null,
                    "# Event",
                    null,
                    "title: Event",
                    true))));
        orchestrator.TransitionAsync(id, MeetupLifecycleState.Published, "authenticated-organizer", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new EventLifecycleOperationResult(
                id,
                MeetupLifecycleState.Published,
                null,
                null,
                "transitioned")));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(orchestrator);
        builder.Services.AddSingleton(Substitute.For<IEventRepository>());
        builder.Services.AddSingleton(Substitute.For<IImageUploadService>());
        builder.Services.AddDbContext<BethuyaDbContext>(options =>
            options.UseInMemoryDatabase($"archive-authorization-tests-{Guid.NewGuid():N}"));
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization(options =>
            options.AddPolicy(
                BethuyaPolicyNames.RequireOrganizer,
                policy => policy.RequireRole("Admin", "Organizer")));

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEventEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var archivePreviewPath = $"/api/events/{id}/archive/preview";

        var anonymousResponse = await client.GetAsync(archivePreviewPath);
        await Assert.That(anonymousResponse.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Add("X-Test-Role", "Attendee");
        var attendeeResponse = await client.GetAsync(archivePreviewPath);
        await Assert.That(attendeeResponse.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Remove("X-Test-Role");

        client.DefaultRequestHeaders.Add("X-Test-Role", "Organizer");
        var organizerResponse = await client.GetAsync(archivePreviewPath);
        await Assert.That(organizerResponse.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);

        var lifecycleResponse = await client.PostAsJsonAsync(
            $"/api/events/{id}/lifecycle",
            new EventLifecycleTransitionRequest(MeetupLifecycleState.Published, "spoofed-actor"));

        await Assert.That(lifecycleResponse.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
        await orchestrator.Received(1).TransitionAsync(
            id,
            MeetupLifecycleState.Published,
            "authenticated-organizer",
            Arg.Any<CancellationToken>());
        await orchestrator.DidNotReceive().TransitionAsync(
            id,
            MeetupLifecycleState.Published,
            "spoofed-actor",
            Arg.Any<CancellationToken>());
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim("sub", "authenticated-organizer"),
                new Claim(ClaimTypes.NameIdentifier, "authenticated-organizer"),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name, "name", ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

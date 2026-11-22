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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServiceDefaults.Auth;
using System.Net.Http.Json;

namespace Hackmum.Bethuya.Tests.Endpoints;

public sealed class EventArchiveAuthorizationTests
{
    [Test]
    public async Task EventMutations_RequireOrganizerAndPreservePublicReads()
    {
        var id = Guid.NewGuid();
        var repo = Substitute.For<IEventRepository>();
        var images = Substitute.For<IImageUploadService>();
        var ingestion = Substitute.For<ISessionIngestionService>();
        var evt = new Event { Id = id, Title = "Event", CreatedBy = "creator" };
        repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(evt);
        repo.GetByHashtagAsync("event", Arg.Any<CancellationToken>()).Returns(evt);
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { evt });
        repo.CreateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<Event>());
        ingestion.ImportSessionizeAsync(id, Arg.Any<CancellationToken>()).Returns(1);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(repo);
        builder.Services.AddSingleton(images);
        builder.Services.AddSingleton(ingestion);
        builder.Services.AddDbContext<BethuyaDbContext>(options => options
            .UseInMemoryDatabase($"event-auth-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        builder.Configuration["Authorization:EnforceAuthenticatedFallback"] = "false";
        builder.AddBethuyaAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEventEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();

        foreach (var role in new string?[] { null, "Attendee" })
        {
            if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
            foreach (var route in Enumerable.Range(0, 5))
            {
                using var response = await SendMutationAsync(client, id, route);
                await Assert.That(response.StatusCode).IsEqualTo(role is null
                    ? System.Net.HttpStatusCode.Unauthorized : System.Net.HttpStatusCode.Forbidden);
            }
            await Assert.That(repo.ReceivedCalls().Count()).IsEqualTo(0);
            await Assert.That(images.ReceivedCalls().Count()).IsEqualTo(0);
            await Assert.That(ingestion.ReceivedCalls().Count()).IsEqualTo(0);
            client.DefaultRequestHeaders.Remove("X-Test-Role");
        }

        foreach (var path in new[] { "/api/events", $"/api/events/{id}", "/api/events/slug/event", $"/api/events/{id}/fairness-targets", $"/api/events/{id}/sessionize/preview" })
        {
            using var response = await client.GetAsync(path);
            await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
        }

        foreach (var role in new[] { "Organizer", "Admin" })
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
            foreach (var route in Enumerable.Range(0, 5))
            {
                using var response = await SendMutationAsync(client, id, route);
                await Assert.That(response.StatusCode).IsEqualTo(route switch
                {
                    0 => System.Net.HttpStatusCode.Created,
                    3 => System.Net.HttpStatusCode.NoContent,
                    _ => System.Net.HttpStatusCode.OK
                });
            }
            client.DefaultRequestHeaders.Remove("X-Test-Role");
        }
        await repo.Received(2).CreateAsync(Arg.Is<Event>(e => e.CreatedBy == "authenticated-organizer"), Arg.Any<CancellationToken>());
        await repo.Received(4).UpdateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>());
        await repo.Received(2).DeleteAsync(id, Arg.Any<CancellationToken>());
        await ingestion.Received(2).ImportSessionizeAsync(id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateEvent_UsesStableSubjectAndRejectsMissingSubjectBeforeSideEffects()
    {
        var repo = Substitute.For<IEventRepository>();
        var images = Substitute.For<IImageUploadService>();
        repo.CreateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<Event>());
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(repo);
        builder.Services.AddSingleton(images);
        builder.Services.AddDbContext<BethuyaDbContext>(options => options
            .UseInMemoryDatabase($"event-subject-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        builder.Configuration["Authorization:EnforceAuthenticatedFallback"] = "false";
        builder.AddBethuyaAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEventEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Organizer");

        foreach (var mode in new[] { "missing", "blank", "blank-identifier", "name-only" })
        {
            client.DefaultRequestHeaders.Add("X-Test-Subject-Mode", mode);
            using var response = await client.PostAsJsonAsync("/api/events", CreateRequest() with
            {
                Hashtag = "event",
                CoverImageUrl = "https://res.cloudinary.com/demo/image/upload/test.png"
            });
            await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Unauthorized);
            await Assert.That(repo.ReceivedCalls().Count()).IsEqualTo(0);
            await Assert.That(images.ReceivedCalls().Count()).IsEqualTo(0);
            client.DefaultRequestHeaders.Remove("X-Test-Subject-Mode");
        }

        client.DefaultRequestHeaders.Add("X-Test-Subject-Mode", "nameidentifier");
        using var success = await client.PostAsJsonAsync("/api/events", CreateRequest() with { CreatedBy = "" });
        await Assert.That(success.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Created);
        await repo.Received(1).CreateAsync(Arg.Is<Event>(e => e.CreatedBy == "identifier-only"), Arg.Any<CancellationToken>());
    }

    private static PlanEventRequest CreateRequest() => new(
        "Event", null, EventType.Meetup, 42,
        new DateTimeOffset(2026, 11, 28, 10, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero),
        null, "spoofed-creator", null, null);

    private static Task<HttpResponseMessage> SendMutationAsync(HttpClient client, Guid id, int route) => route switch
    {
        0 => client.PostAsJsonAsync("/api/events", CreateRequest()),
        1 => client.PutAsJsonAsync($"/api/events/{id}", new UpdateEventRequest(
            "Updated", null, EventType.Meetup, 42, CreateRequest().StartDate, CreateRequest().EndDate,
            null, EventStatus.Draft, null)),
        2 => client.PutAsJsonAsync($"/api/events/{id}/fairness-targets", new EventFairnessTargetsContract()),
        3 => client.DeleteAsync($"/api/events/{id}"),
        4 => client.PostAsync($"/api/events/{id}/sessionize/import", null),
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

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

    internal sealed class TestAuthenticationHandler(
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

            var mode = Request.Headers["X-Test-Subject-Mode"].FirstOrDefault();
            var claims = new List<Claim> { new(ClaimTypes.Role, role) };
            if (mode == "nameidentifier") claims.Add(new Claim(ClaimTypes.NameIdentifier, "identifier-only"));
            else if (mode == "blank") claims.Add(new Claim("sub", " "));
            else if (mode == "blank-identifier") claims.Add(new Claim(ClaimTypes.NameIdentifier, " "));
            else if (mode == "name-only") claims.Add(new Claim(ClaimTypes.Name, "Display Name"));
            else if (mode != "missing") claims.Add(new Claim("sub", "authenticated-organizer"));
            var identity = new ClaimsIdentity(claims, Scheme.Name, "name", ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

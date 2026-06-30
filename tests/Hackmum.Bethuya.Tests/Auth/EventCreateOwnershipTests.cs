using System.Net;
using System.Net.Http.Json;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Tests.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR4 (Phase 4): <see cref="Event.CreatedBy"/> is server-set provenance derived from the validated
/// principal, not the spoofable <c>PlanEventRequest.CreatedBy</c> body field. A request that supplies a
/// forged <c>CreatedBy</c> is ignored, and a principal with no resolvable identity is rejected
/// with <c>401</c> before any persistence occurs.
/// </summary>
public sealed class EventCreateOwnershipTests
{
    [Test]
    public async Task Create_IgnoresSpoofedBodyCreatedBy_AndUsesPrincipalIdentity()
    {
        Event? captured = null;
        var eventRepository = Substitute.For<IEventRepository>();
        eventRepository.CreateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<Event>();
                return captured;
            });

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(eventRepository);
        builder.Services.AddSingleton(Substitute.For<IImageUploadService>());
        builder.AddTestAuthorization();
        builder.Services.AddBethuyaUserContext();
        builder.Services.AddDbContext<BethuyaDbContext>(options =>
            options
                .UseInMemoryDatabase($"event-ownership-tests-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        await using var app = builder.Build();
        app.UseTestAuthorization();
        app.MapEventEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/events", UriKind.Relative),
            BuildRequest(spoofedCreatedBy: "spoofed-by-client"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(captured).IsNotNull();
        // TestAuthHost authenticates an "test-admin" principal; the server-set CreatedBy must reflect
        // that validated identity, never the forged body value.
        await Assert.That(captured!.CreatedBy).IsEqualTo("test-admin");
        await Assert.That(captured.CreatedBy).IsNotEqualTo("spoofed-by-client");
    }

    [Test]
    public async Task Create_NoResolvableIdentity_IsUnauthorizedAndDoesNotPersist()
    {
        var eventRepository = Substitute.For<IEventRepository>();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.Sources.Clear();
        builder.Services.AddSingleton(eventRepository);
        builder.Services.AddSingleton(Substitute.For<IImageUploadService>());

        builder.Services.AddAuthentication(HeaderRoleAuthHandler.SchemeName)
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HeaderRoleAuthHandler>(
                HeaderRoleAuthHandler.SchemeName, _ => { });
        builder.AddBethuyaAuthorization();
        builder.Services.AddBethuyaUserContext();
        // Last registration wins: an authenticated organizer whose token carries no usable identity.
        builder.Services.AddScoped<IUserContext, NullIdentityUserContext>();
        builder.Services.AddDbContext<BethuyaDbContext>(options =>
            options
                .UseInMemoryDatabase($"event-ownership-noid-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEventEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/events", UriKind.Relative))
        {
            Content = JsonContent.Create(BuildRequest(spoofedCreatedBy: "spoofed"))
        };
        // Organizer role satisfies the route group; the identity gap is inside CreateEventAsync.
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Organizer);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, "organizer-1");

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await eventRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Test]
    public async Task Create_PersistsNonPiiSubject_NotEmailOrName()
    {
        Event? captured = null;
        var eventRepository = Substitute.For<IEventRepository>();
        eventRepository.CreateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<Event>();
                return captured;
            });

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.Sources.Clear();
        builder.Services.AddSingleton(eventRepository);
        builder.Services.AddSingleton(Substitute.For<IImageUploadService>());
        builder.Services.AddAuthentication(HeaderRoleAuthHandler.SchemeName)
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HeaderRoleAuthHandler>(
                HeaderRoleAuthHandler.SchemeName, _ => { });
        builder.AddBethuyaAuthorization();
        builder.Services.AddBethuyaUserContext();
        builder.Services.AddDbContext<BethuyaDbContext>(options =>
            options
                .UseInMemoryDatabase($"event-ownership-pii-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEventEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();

        const string subject = "organizer-7";
        const string email = "alice.organizer@example.com";
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/events", UriKind.Relative))
        {
            Content = JsonContent.Create(BuildRequest(spoofedCreatedBy: "spoofed"))
        };
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Organizer);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, subject);
        request.Headers.Add(HeaderRoleAuthHandler.EmailHeader, email);

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(captured).IsNotNull();
        // CreatedBy is surfaced on the anonymous /api/public/events reads, so it must persist the
        // non-PII subject identifier — never the organizer's email or display name.
        await Assert.That(captured!.CreatedBy).IsEqualTo(subject);
        await Assert.That(captured.CreatedBy).IsNotEqualTo(email);
    }

    private static PlanEventRequest BuildRequest(string spoofedCreatedBy) => new(
        Title: "Ownership provenance event",
        Description: "Validates server-set CreatedBy.",
        Type: EventType.Meetup,
        Capacity: 50,
        StartDate: new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
        EndDate: new DateTimeOffset(2026, 6, 1, 21, 0, 0, TimeSpan.Zero),
        Location: "Hackerspace",
        CreatedBy: spoofedCreatedBy,
        Hashtag: "ownershipprovenance",
        CoverImageUrl: null);

    private sealed class NullIdentityUserContext : IUserContext
    {
        public bool IsAuthenticated => true;
        public string? UserId => null;
        public string? Email => null;
        public string? Name => null;
    }
}

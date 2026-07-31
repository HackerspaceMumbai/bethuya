using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
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

public sealed class CurationEndpointAuthorizationTests
{
    [Test]
    public async Task CurationDashboard_ForbidsAttendeeRole()
    {
        await using var app = await CreateAppAsync("Attendee");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var response = await client.GetAsync($"/api/curation/{TestEventId}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task CurationDashboard_AllowsOrganizerRole()
    {
        await using var app = await CreateAppAsync("Organizer");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var response = await client.GetAsync($"/api/curation/{TestEventId}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private static readonly Guid TestEventId = Guid.Parse("01985f65-71aa-7e2b-9581-b3e8b0548b10");

    private static async Task<WebApplication> CreateAppAsync(string role)
    {
        var eventRepository = Substitute.For<IEventRepository>();
        eventRepository.GetByIdAsync(TestEventId, Arg.Any<CancellationToken>())
            .Returns(new Event
            {
                Id = TestEventId,
                Title = "Curation Security Test Event",
                CreatedBy = "organizer",
                Capacity = 25,
                StartDate = DateTimeOffset.UtcNow,
                EndDate = DateTimeOffset.UtcNow.AddHours(2)
            });

        var registrationRepository = Substitute.For<IRegistrationRepository>();
        registrationRepository.GetByEventIdAsync(TestEventId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Registration>());
        registrationRepository.GetHistoricalByEmailsAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, IReadOnlyList<Registration>>(StringComparer.OrdinalIgnoreCase));

        var attendeeProfileRepository = Substitute.For<IAttendeeProfileRepository>();
        attendeeProfileRepository.GetPublicSummariesByEmailAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, AttendeePublicSummary>(StringComparer.OrdinalIgnoreCase));
        var curatorAgent = Substitute.For<IAgent<CuratorRequest, CuratorResponse>>();
        var decisionRepository = Substitute.For<IDecisionRepository>();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddSingleton<ITestRoleProvider>(new TestRoleProvider(role));
        builder.AddBethuyaAuthorization();
        builder.Services.AddSingleton(eventRepository);
        builder.Services.AddSingleton(registrationRepository);
        builder.Services.AddSingleton(attendeeProfileRepository);
        builder.Services.AddSingleton(curatorAgent);
        builder.Services.AddSingleton(decisionRepository);
        builder.Services.AddSingleton<CurationFairnessService>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCurationEndpoints();
        await app.StartAsync();
        return app;
    }

    private interface ITestRoleProvider
    {
        string Role { get; }
    }

    private sealed class TestRoleProvider(string role) : ITestRoleProvider
    {
        public string Role { get; } = role;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITestRoleProvider roleProvider)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            Claim[] claims =
            [
                new Claim("sub", "curation-role-test-user"),
                new Claim("name", "Role Test User"),
                new Claim(ClaimTypes.Email, "role-test@example.com"),
                new Claim(ClaimTypes.Role, roleProvider.Role)
            ];

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}

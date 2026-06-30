using System.Net;
using System.Net.Http.Json;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR4 (Phase 4): a registration's owner is server-authoritative. <see cref="Registration.UserId"/> is
/// stamped from the validated principal's subject at creation — the request body has no owner field, so
/// the caller cannot register on another attendee's behalf. An authenticated principal with no usable
/// subject cannot own a registration and is rejected with <c>401</c>.
/// </summary>
public sealed class RegistrationCreateOwnershipTests
{
    [Test]
    public async Task Create_StampsUserIdFromCallerSubject()
    {
        const string callerSub = "caller-99";
        Registration? captured = null;

        var profileRepo = Substitute.For<IAttendeeProfileRepository>();
        var registrationRepo = Substitute.For<IRegistrationRepository>();
        registrationRepo.CreateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<Registration>();
                return captured;
            });

        await using var app = await HeaderRoleAuthHost.StartAsync(
            a => a.MapRegistrationEndpoints(),
            configureServices: services =>
            {
                services.AddSingleton(profileRepo);
                services.AddSingleton(registrationRepo);
                services.AddSingleton<InclusionSignalsNormalizer>();
                services.AddBethuyaUserContext();
            },
            configuration: new Dictionary<string, string?> { ["Onboarding:BypassMandatoryProfile"] = "true" });
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/attendee/registrations", UriKind.Relative))
        {
            Content = JsonContent.Create(BuildRequest())
        };
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Attendee);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, callerSub);

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.UserId).IsEqualTo(callerSub);
    }

    [Test]
    public async Task Create_AuthenticatedWithoutSubject_IsUnauthorizedAndDoesNotPersist()
    {
        var profileRepo = Substitute.For<IAttendeeProfileRepository>();
        var registrationRepo = Substitute.For<IRegistrationRepository>();

        await using var app = await HeaderRoleAuthHost.StartAsync(
            a => a.MapRegistrationEndpoints(),
            configureServices: services =>
            {
                services.AddSingleton(profileRepo);
                services.AddSingleton(registrationRepo);
                services.AddSingleton<InclusionSignalsNormalizer>();
                services.AddBethuyaUserContext();
                services.AddScoped<IUserContext, NullSubjectUserContext>();
            },
            configuration: new Dictionary<string, string?> { ["Onboarding:BypassMandatoryProfile"] = "true" });
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/attendee/registrations", UriKind.Relative))
        {
            Content = JsonContent.Create(BuildRequest())
        };
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Attendee);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, "ignored");

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await registrationRepo.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    private static CreateRegistrationRequest BuildRequest() => new(
        EventId: Guid.CreateVersion7(),
        FullName: "Test Attendee",
        Email: "caller99@bethuya.test",
        Bio: null,
        Interests: [],
        Intent: "I want to learn and contribute.",
        Goals: null,
        ContributionPreferences: [],
        ExperienceLevel: null,
        DietaryRequirements: null,
        AccessibilityNeeds: null);

    private sealed class NullSubjectUserContext : IUserContext
    {
        public bool IsAuthenticated => true;
        public string? UserId => null;
        public string? Email => null;
        public string? Name => null;
    }
}

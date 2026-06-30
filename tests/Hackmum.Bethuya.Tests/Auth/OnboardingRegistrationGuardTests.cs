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
/// PR3 (M1): the backend independently enforces mandatory-profile completion before allowing
/// attendee registration, regardless of the UI onboarding gate. The
/// <c>Onboarding:BypassMandatoryProfile</c> switch still opens the gate for demos.
/// </summary>
public sealed class OnboardingRegistrationGuardTests
{
    [Test]
    public async Task Register_WithIncompleteProfile_IsBlocked()
    {
        var status = await RegisterAsync(profile: BuildProfile("user-1", complete: false), bypass: false);
        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Register_WithMissingProfile_IsBlocked()
    {
        var status = await RegisterAsync(profile: null, bypass: false);
        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Register_WithCompleteProfile_IsAllowed()
    {
        var status = await RegisterAsync(profile: BuildProfile("user-1", complete: true), bypass: false);
        await Assert.That(status).IsEqualTo(HttpStatusCode.Created);
    }

    [Test]
    public async Task Register_AuthenticatedWithoutSubject_IsUnauthorized()
    {
        // An authenticated principal that carries no usable subject claim must be treated as an
        // authentication failure (401), not conflated with an incomplete-profile rejection (403).
        var profileRepo = Substitute.For<IAttendeeProfileRepository>();
        var registrationRepo = Substitute.For<IRegistrationRepository>();
        registrationRepo.CreateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Registration>());

        await using var app = await HeaderRoleAuthHost.StartAsync(
            a => a.MapRegistrationEndpoints(),
            configureServices: services =>
            {
                services.AddSingleton(profileRepo);
                services.AddSingleton(registrationRepo);
                services.AddSingleton<InclusionSignalsNormalizer>();
                services.AddBethuyaUserContext();
                // Last registration wins: simulate a validated-but-subjectless principal.
                services.AddScoped<IUserContext, NullSubjectUserContext>();
            },
            configuration: new Dictionary<string, string?> { ["Onboarding:BypassMandatoryProfile"] = "false" });
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/attendee/registrations", UriKind.Relative))
        {
            Content = JsonContent.Create(new CreateRegistrationRequest(
                EventId: Guid.CreateVersion7(),
                FullName: "Test Attendee",
                Email: "user1@bethuya.test",
                Bio: null,
                Interests: [],
                Intent: "I want to learn and contribute.",
                Goals: null,
                ContributionPreferences: [],
                ExperienceLevel: null,
                DietaryRequirements: null,
                AccessibilityNeeds: null))
        };
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Attendee);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, "user-1");

        var response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await profileRepo.DidNotReceive().GetByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Register_WithBypassEnabled_IsAllowedDespiteMissingProfile()
    {
        var status = await RegisterAsync(profile: null, bypass: true);
        await Assert.That(status).IsEqualTo(HttpStatusCode.Created);
    }

    private static async Task<HttpStatusCode> RegisterAsync(AttendeeProfile? profile, bool bypass)
    {
        var profileRepo = Substitute.For<IAttendeeProfileRepository>();
        profileRepo.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(profile);

        var registrationRepo = Substitute.For<IRegistrationRepository>();
        registrationRepo.CreateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Registration>());

        var config = new Dictionary<string, string?>
        {
            ["Onboarding:BypassMandatoryProfile"] = bypass ? "true" : "false"
        };

        await using var app = await HeaderRoleAuthHost.StartAsync(
            a => a.MapRegistrationEndpoints(),
            configureServices: services =>
            {
                services.AddSingleton(profileRepo);
                services.AddSingleton(registrationRepo);
                services.AddSingleton<InclusionSignalsNormalizer>();
                services.AddBethuyaUserContext();
            },
            configuration: config);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/attendee/registrations", UriKind.Relative))
        {
            Content = JsonContent.Create(new CreateRegistrationRequest(
                EventId: Guid.CreateVersion7(),
                FullName: "Test Attendee",
                Email: "user1@bethuya.test",
                Bio: null,
                Interests: [],
                Intent: "I want to learn and contribute.",
                Goals: null,
                ContributionPreferences: [],
                ExperienceLevel: null,
                DietaryRequirements: null,
                AccessibilityNeeds: null))
        };
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Attendee);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, "user-1");

        var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static AttendeeProfile BuildProfile(string userId, bool complete) => new()
    {
        UserId = userId,
        FirstName = "Test",
        LastName = "Attendee",
        Email = $"{userId}@bethuya.test",
        GovernmentPhotoIdType = "Passport",
        GovernmentIdLastFour = "1234",
        LinkedInMemberId = string.Empty,
        GitHubLogin = string.Empty,
        GitHubProfileUrl = string.Empty,
        IsProfileComplete = complete,
        ProfileCompletedAt = complete ? DateTimeOffset.UtcNow : null
    };

    /// <summary>An authenticated principal whose token carries no usable subject/identity claim.</summary>
    private sealed class NullSubjectUserContext : IUserContext
    {
        public bool IsAuthenticated => true;
        public string? UserId => null;
        public string? Email => null;
        public string? Name => null;
    }
}

using System.Net;
using System.Net.Http.Json;
using Bethuya.Hybrid.Web.Auth;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Auth;

public class DevelopmentAuthenticationTests : IAsyncDisposable
{
    private readonly List<WebApplication> _apps = [];
    private readonly List<HttpClient> _clients = [];

    [Test]
    public async Task WebAuthentication_NoneProvider_AllowsAuthorizedEndpoint()
    {
        var builder = CreateBuilderWithProviderNone();
        builder.AddBethuyaWebAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapGet("/protected", (HttpContext context) => Results.Ok(new
            {
                name = context.User.Identity?.Name,
                authenticated = context.User.Identity?.IsAuthenticated
            })).RequireAuthorization();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        var response = await client.GetAsync("/protected");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains("Dev User");
        await Assert.That(body).Contains("true");
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_UsesDevelopmentUserForProfileStatus()
    {
        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>())
            .Returns((AttendeeProfile?)null);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        var response = await client.GetAsync("/api/profile/completion-status");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains("\"isProfileComplete\":false");
        await repository.Received(1).GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_LoadsSavedMandatoryProfile()
    {
        var existingProfile = new AttendeeProfile
        {
            UserId = "dev-user-001",
            FirstName = "Dev",
            LastName = "User",
            Email = "dev@bethuya.local",
            MobileNumber = "+91 99999 11111",
            GovernmentPhotoIdType = "Passport",
            GovernmentIdLastFour = "1234",
            OccupationStatus = "Employee",
            CompanyName = "GitHub",
            City = "Mumbai",
            State = "Maharashtra",
            PostalCode = "400001",
            Country = "India",
            LinkedInMemberId = string.Empty,
            GitHubLogin = string.Empty,
            GitHubProfileUrl = string.Empty,
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        };

        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>())
            .Returns(existingProfile);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.GetAsync("/api/profile");
        var body = await response.Content.ReadFromJsonAsync<MandatoryProfileResponse>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.FirstName).IsEqualTo("Dev");
        await Assert.That(body.CompanyName).IsEqualTo("GitHub");
        await Assert.That(body.Country).IsEqualTo("India");
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_AllowsSavingMandatoryProfile()
    {
        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>())
            .Returns((AttendeeProfile?)null);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.PostAsJsonAsync("/api/profile", new SaveMandatoryProfileRequest(
            "Dev",
            "User",
            "dev@bethuya.local",
            null,
            "Passport",
            "1234",
            "Organizer",
            null,
            null,
            "Mumbai",
            "Maharashtra",
            "400001",
            "India"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await repository.Received(1).CreateAsync(
            Arg.Is<AttendeeProfile>(profile =>
                profile.UserId == "dev-user-001" &&
                profile.FirstName == "Dev" &&
                profile.GovernmentPhotoIdType == "Passport" &&
                profile.GovernmentIdLastFour == "1234" &&
                profile.LinkedInMemberId == string.Empty &&
                profile.GitHubLogin == string.Empty &&
                profile.GitHubProfileUrl == string.Empty &&
                profile.IsProfileComplete),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_AllowsSavingLinkedInOnlyForEmployee()
    {
        var existingProfile = new AttendeeProfile
        {
            UserId = "dev-user-001",
            FirstName = "Dev",
            LastName = "User",
            Email = "dev@bethuya.local",
            GovernmentPhotoIdType = "Passport",
            GovernmentIdLastFour = "1234",
            OccupationStatus = "Employee",
            LinkedInMemberId = string.Empty,
            GitHubLogin = string.Empty,
            GitHubProfileUrl = string.Empty,
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        };

        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>())
            .Returns(existingProfile);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.PostAsJsonAsync("/api/profile/social", new SaveSocialProfileRequest(
            "yrZCpj2Z12",
            "https://www.linkedin.com/in/dev-user",
            null,
            null));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await repository.Received(1).UpdateAsync(
            Arg.Is<AttendeeProfile>(profile =>
                profile.LinkedInMemberId == "yrZCpj2Z12" &&
                profile.LinkedInProfileUrl == "https://www.linkedin.com/in/dev-user" &&
                profile.GitHubLogin == string.Empty &&
                profile.GitHubProfileUrl == string.Empty),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_PersistsTypedLinkedInProfileUrlAlongsideVerifiedMemberId()
    {
        var existingProfile = new AttendeeProfile
        {
            UserId = "dev-user-001",
            FirstName = "Dev",
            LastName = "User",
            Email = "dev@bethuya.local",
            GovernmentPhotoIdType = "Passport",
            GovernmentIdLastFour = "1234",
            OccupationStatus = "Employee",
            LinkedInMemberId = string.Empty,
            GitHubLogin = string.Empty,
            GitHubProfileUrl = string.Empty,
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        };

        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>())
            .Returns(existingProfile);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.PostAsJsonAsync("/api/profile/social", new SaveSocialProfileRequest(
            "yrZCpj2Z12",
            "https://www.linkedin.com/in/dev-user-custom-url/",
            null,
            null));

        var status = await response.Content.ReadFromJsonAsync<ProfileCompletionStatusResponse>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(status).IsNotNull();
        await Assert.That(status!.IsSocialConnectionsComplete).IsTrue();
        await repository.Received(1).UpdateAsync(
            Arg.Is<AttendeeProfile>(profile =>
                profile.LinkedInMemberId == "yrZCpj2Z12" &&
                profile.LinkedInProfileUrl == "https://www.linkedin.com/in/dev-user-custom-url/" &&
                profile.GitHubLogin == string.Empty &&
                profile.GitHubProfileUrl == string.Empty),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_AllowsSavingGitHubOnlyForStudent()
    {
        var existingProfile = new AttendeeProfile
        {
            UserId = "dev-user-001",
            FirstName = "Dev",
            LastName = "User",
            Email = "dev@bethuya.local",
            GovernmentPhotoIdType = "Passport",
            GovernmentIdLastFour = "1234",
            OccupationStatus = "Student",
            LinkedInMemberId = string.Empty,
            GitHubLogin = string.Empty,
            GitHubProfileUrl = string.Empty,
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        };

        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>())
            .Returns(existingProfile);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.PostAsJsonAsync("/api/profile/social", new SaveSocialProfileRequest(
            null,
            null,
            "dev-user",
            "https://github.com/dev-user"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await repository.Received(1).UpdateAsync(
            Arg.Is<AttendeeProfile>(profile =>
                profile.LinkedInMemberId == string.Empty &&
                profile.GitHubLogin == "dev-user" &&
                profile.GitHubProfileUrl == "https://github.com/dev-user"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_LoadsSavedAideProfile()
    {
        var existingProfile = new AttendeeProfile
        {
            UserId = "dev-user-001",
            FirstName = "Dev",
            LastName = "User",
            Email = "dev@bethuya.local",
            GovernmentPhotoIdType = "Passport",
            GovernmentIdLastFour = "1234",
            LinkedInMemberId = string.Empty,
            GitHubLogin = string.Empty,
            GitHubProfileUrl = string.Empty,
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow,
            IsAideProfileComplete = true,
            AideProfileCompletedAt = DateTimeOffset.UtcNow,
            GenderIdentity = "Woman",
            Disability = "Physical / mobility",
            DisabilityDetails = "Wheelchair access",
            HowDidYouHear = "Friend or colleague"
        };

        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>())
            .Returns(existingProfile);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.GetAsync("/api/profile/aide");
        var body = await response.Content.ReadFromJsonAsync<AideProfileResponse>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.GenderIdentity).IsEqualTo("Woman");
        await Assert.That(body.DisabilityDetails).IsEqualTo("Wheelchair access");
        await Assert.That(body.HowDidYouHear).IsEqualTo("Friend or colleague");
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_EmployeeWithoutCompany_ReturnsValidationProblem()
    {
        var repository = Substitute.For<IAttendeeProfileRepository>();

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.PostAsJsonAsync("/api/profile", new SaveMandatoryProfileRequest(
            "Dev",
            "User",
            "dev@bethuya.local",
            null,
            "Passport",
            "1234",
            "Employee",
            null,
            null,
            "Mumbai",
            "Maharashtra",
            "400001",
            "India"));

        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(body).Contains("Company name is required when employment status is Employee.");
        await repository.DidNotReceive().CreateAsync(Arg.Any<AttendeeProfile>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_StudentWithoutGitHub_ReturnsValidationProblem()
    {
        var existingProfile = new AttendeeProfile
        {
            UserId = "dev-user-001",
            FirstName = "Dev",
            LastName = "User",
            Email = "dev@bethuya.local",
            GovernmentPhotoIdType = "Passport",
            GovernmentIdLastFour = "1234",
            OccupationStatus = "Student",
            LinkedInMemberId = string.Empty,
            GitHubLogin = string.Empty,
            GitHubProfileUrl = string.Empty,
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        };

        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>())
            .Returns(existingProfile);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.PostAsJsonAsync("/api/profile/social", new SaveSocialProfileRequest(
            "yrZCpj2Z12",
            "https://www.linkedin.com/in/dev-user",
            null,
            null));

        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(body).Contains("GitHub is required for students.");
        await repository.DidNotReceive().UpdateAsync(Arg.Any<AttendeeProfile>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_LinkedInUrlAloneDoesNotSatisfyEmployeeRequirement()
    {
        var existingProfile = new AttendeeProfile
        {
            UserId = "dev-user-001",
            FirstName = "Dev",
            LastName = "User",
            Email = "dev@bethuya.local",
            GovernmentPhotoIdType = "Passport",
            GovernmentIdLastFour = "1234",
            OccupationStatus = "Employee",
            LinkedInMemberId = string.Empty,
            GitHubLogin = string.Empty,
            GitHubProfileUrl = string.Empty,
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        };

        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>())
            .Returns(existingProfile);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.PostAsJsonAsync("/api/profile/social", new SaveSocialProfileRequest(
            null,
            "https://www.linkedin.com/in/dev-user-only-url/",
            null,
            null));

        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(body).Contains("LinkedIn is required for full-time employed applicants.");
        await repository.DidNotReceive().UpdateAsync(Arg.Any<AttendeeProfile>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_FullOnboardingRoundTrip_PersistsSavedStateForSubsequentReads()
    {
        AttendeeProfile? storedProfile = null;
        var repository = CreateStatefulRepository(() => storedProfile, profile => storedProfile = profile);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var mandatoryResponse = await client.PostAsJsonAsync("/api/profile", new SaveMandatoryProfileRequest(
            "Dev",
            "User",
            "dev@bethuya.local",
            "+91 98765 43210",
            "Passport",
            "1234",
            "Employee",
            "GitHub",
            null,
            "Mumbai",
            "Maharashtra",
            "400001",
            "India"));
        using var socialResponse = await client.PostAsJsonAsync("/api/profile/social", new SaveSocialProfileRequest(
            "yrZCpj2Z12",
            "https://www.linkedin.com/in/dev-user",
            "dev-user",
            "https://github.com/dev-user"));
        using var aideResponse = await client.PostAsJsonAsync("/api/profile/aide", new SaveAideProfileRequest(
            "Prefer to self-describe",
            "Non-binary femme",
            "25–34",
            null,
            null,
            "Physical / mobility",
            "Wheelchair ramp access",
            "Vegetarian",
            null,
            null,
            null,
            null,
            "Andheri",
            "Public transport",
            null,
            null,
            null,
            "English, Hindi",
            "Bachelor's degree",
            "Friend or colleague",
            "Quiet seating"));

        var status = await client.GetFromJsonAsync<ProfileCompletionStatusResponse>("/api/profile/completion-status");
        var social = await client.GetFromJsonAsync<SocialProfileResponse>("/api/profile/social");

        await Assert.That(mandatoryResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(socialResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(aideResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(status).IsNotNull();
        await Assert.That(status!.IsProfileComplete).IsTrue();
        await Assert.That(status.IsSocialConnectionsComplete).IsTrue();
        await Assert.That(status.IsAideProfileComplete).IsTrue();
        await Assert.That(social).IsNotNull();
        await Assert.That(social!.OccupationStatus).IsEqualTo("Employee");
        await Assert.That(social.LinkedInMemberId).IsEqualTo("yrZCpj2Z12");
        await Assert.That(social.LinkedInProfileUrl).IsEqualTo("https://www.linkedin.com/in/dev-user");
        await Assert.That(social.GitHubLogin).IsEqualTo("dev-user");
        await Assert.That(social.GitHubProfileUrl).IsEqualTo("https://github.com/dev-user");
        await Assert.That(storedProfile).IsNotNull();
        await Assert.That(storedProfile!.AdditionalSupport).IsEqualTo("Quiet seating");
        await Assert.That(storedProfile.DisabilityDetails).IsEqualTo("Wheelchair ramp access");
        await Assert.That(storedProfile.Neighborhood).IsEqualTo("Andheri");
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_EditingMandatoryProfile_DoesNotWipeSavedSocialOrAideState()
    {
        var profileCompletedAt = new DateTimeOffset(2026, 04, 15, 9, 30, 0, TimeSpan.Zero);
        var aideCompletedAt = new DateTimeOffset(2026, 04, 15, 9, 45, 0, TimeSpan.Zero);
        AttendeeProfile? storedProfile = new()
        {
            UserId = "dev-user-001",
            FirstName = "Dev",
            LastName = "User",
            Email = "dev@bethuya.local",
            MobileNumber = "+91 98765 43210",
            GovernmentPhotoIdType = "Passport",
            GovernmentIdLastFour = "1234",
            OccupationStatus = "Employee",
            CompanyName = "GitHub",
            LinkedInMemberId = "yrZCpj2Z12",
            LinkedInProfileUrl = "https://www.linkedin.com/in/dev-user",
            GitHubLogin = "dev-user",
            GitHubProfileUrl = "https://github.com/dev-user",
            City = "Mumbai",
            State = "Maharashtra",
            PostalCode = "400001",
            Country = "India",
            IsProfileComplete = true,
            ProfileCompletedAt = profileCompletedAt,
            IsAideProfileComplete = true,
            AideProfileCompletedAt = aideCompletedAt,
            GenderIdentity = "Prefer to self-describe",
            SelfDescribeGender = "Non-binary femme",
            Disability = "Physical / mobility",
            DisabilityDetails = "Wheelchair ramp access",
            AdditionalSupport = "Quiet seating"
        };

        var repository = CreateStatefulRepository(() => storedProfile, profile => storedProfile = profile);

        var builder = CreateBuilderWithProviderNone();
        builder.Services.AddSingleton(repository);
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseBethuyaAuthentication();
            app.MapProfileEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.PostAsJsonAsync("/api/profile", new SaveMandatoryProfileRequest(
            "Devika",
            "User",
            "devika@bethuya.local",
            "+91 91234 56789",
            "Passport",
            "9876",
            "Employee",
            "GitHub India",
            null,
            "Pune",
            "Maharashtra",
            "411001",
            "India"));

        var status = await response.Content.ReadFromJsonAsync<ProfileCompletionStatusResponse>();
        var social = await client.GetFromJsonAsync<SocialProfileResponse>("/api/profile/social");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(status).IsNotNull();
        await Assert.That(status!.IsProfileComplete).IsTrue();
        await Assert.That(status.IsSocialConnectionsComplete).IsTrue();
        await Assert.That(status.IsAideProfileComplete).IsTrue();
        await Assert.That(status.ProfileCompletedAt).IsEqualTo(profileCompletedAt);
        await Assert.That(status.AideProfileCompletedAt).IsEqualTo(aideCompletedAt);
        await Assert.That(social).IsNotNull();
        await Assert.That(social!.LinkedInMemberId).IsEqualTo("yrZCpj2Z12");
        await Assert.That(social.GitHubLogin).IsEqualTo("dev-user");
        await Assert.That(storedProfile).IsNotNull();
        await Assert.That(storedProfile!.FirstName).IsEqualTo("Devika");
        await Assert.That(storedProfile.Email).IsEqualTo("devika@bethuya.local");
        await Assert.That(storedProfile.CompanyName).IsEqualTo("GitHub India");
        await Assert.That(storedProfile.LinkedInProfileUrl).IsEqualTo("https://www.linkedin.com/in/dev-user");
        await Assert.That(storedProfile.GitHubProfileUrl).IsEqualTo("https://github.com/dev-user");
        await Assert.That(storedProfile.GenderIdentity).IsEqualTo("Prefer to self-describe");
        await Assert.That(storedProfile.DisabilityDetails).IsEqualTo("Wheelchair ramp access");
        await Assert.That(storedProfile.AdditionalSupport).IsEqualTo("Quiet seating");
    }

    [Test]
    public async Task SocialConnections_LinkedInStart_UsesOpenIdConnectScopesByDefault()
    {
        var builder = CreateBuilderWithLinkedInSocialConnections();
        builder.AddSocialProfileConnectionAuthentication();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseAuthentication();
            app.MapSocialProfileConnectionEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var response = await client.GetAsync("/authentication/social/linkedin/start?returnUrl=%2Fregistration%2Fsocial");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);

        var redirectUri = response.Headers.Location;
        await Assert.That(redirectUri).IsNotNull();

        var query = QueryHelpers.ParseQuery(redirectUri!.Query);
        await Assert.That(query["scope"].ToString()).IsEqualTo("openid profile");
        await Assert.That(query["redirect_uri"].ToString()).Contains("/oauth/linkedin/callback");
    }

    [Test]
    public async Task SocialConnections_LinkedInUnauthorizedScopeError_RedirectsBackToRegistrationSocial()
    {
        var builder = CreateBuilderWithLinkedInSocialConnections();
        builder.AddSocialProfileConnectionAuthentication();

        var app = await StartAppAsync(builder, app =>
        {
            app.UseAuthentication();
            app.MapSocialProfileConnectionEndpoints();
        });

        var client = app.GetTestClient();
        _clients.Add(client);

        using var startResponse = await client.GetAsync("/authentication/social/linkedin/start?returnUrl=%2Fregistration%2Fsocial");
        var authorizationRedirect = startResponse.Headers.Location;
        await Assert.That(authorizationRedirect).IsNotNull();

        var state = QueryHelpers.ParseQuery(authorizationRedirect!.Query)["state"].ToString();
        await Assert.That(state).IsNotEmpty();

        using var callbackResponse = await client.GetAsync($"/oauth/linkedin/callback?error=unauthorized_scope_error&error_description=Scope%20%22profile%22%20is%20not%20authorized%20for%20the%20application&state={Uri.EscapeDataString(state)}");

        await Assert.That(callbackResponse.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(callbackResponse.Headers.Location).IsNotNull();
        await Assert.That(callbackResponse.Headers.Location!.ToString()).Contains("/registration/social");
        await Assert.That(callbackResponse.Headers.Location!.ToString()).Contains("socialError=social-provider-scope-not-authorized");
        await Assert.That(callbackResponse.Headers.Location!.ToString()).Contains("socialProvider=linkedin");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        foreach (var app in _apps)
        {
            await app.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    private static WebApplicationBuilder CreateBuilderWithProviderNone()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "None"
        });

        return builder;
    }

    private static WebApplicationBuilder CreateBuilderWithLinkedInSocialConnections()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SocialConnections:LinkedIn:ClientId"] = "linkedin-client-id",
            ["SocialConnections:LinkedIn:ClientSecret"] = "linkedin-client-secret",
            ["SocialConnections:LinkedIn:CallbackPath"] = "/oauth/linkedin/callback"
        });

        return builder;
    }

    private static IAttendeeProfileRepository CreateStatefulRepository(Func<AttendeeProfile?> getStoredProfile, Action<AttendeeProfile> setStoredProfile)
    {
        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetByUserIdAsync("dev-user-001", Arg.Any<CancellationToken>())
            .Returns(_ => getStoredProfile());
        repository.When(x => x.CreateAsync(Arg.Any<AttendeeProfile>(), Arg.Any<CancellationToken>()))
            .Do(callInfo => setStoredProfile(callInfo.Arg<AttendeeProfile>()));
        repository.When(x => x.UpdateAsync(Arg.Any<AttendeeProfile>(), Arg.Any<CancellationToken>()))
            .Do(callInfo => setStoredProfile(callInfo.Arg<AttendeeProfile>()));
        return repository;
    }

    private async Task<WebApplication> StartAppAsync(WebApplicationBuilder builder, Action<WebApplication> configure)
    {
        var app = builder.Build();
        configure(app);
        await app.StartAsync();
        _apps.Add(app);
        return app;
    }
}

using System.Net;
using System.Net.Http.Json;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
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

    private async Task<WebApplication> StartAppAsync(WebApplicationBuilder builder, Action<WebApplication> configure)
    {
        var app = builder.Build();
        configure(app);
        await app.StartAsync();
        _apps.Add(app);
        return app;
    }
}

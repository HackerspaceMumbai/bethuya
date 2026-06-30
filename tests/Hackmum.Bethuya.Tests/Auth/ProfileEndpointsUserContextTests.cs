using System.Net;
using System.Net.Http;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR3 (M3): ProfileEndpoints resolve the caller identity from <see cref="IUserContext"/> instead of
/// hand-rolled claim parsing. An authenticated attendee's subject flows to the repository, and an
/// anonymous request is rejected by the attendee policy / default-deny.
/// </summary>
public sealed class ProfileEndpointsUserContextTests
{
    [Test]
    public async Task GetProfile_Authenticated_UsesUserContextSubject()
    {
        var repo = Substitute.For<IAttendeeProfileRepository>();
        repo.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(BuildProfile("user-1"));

        await using var app = await HeaderRoleAuthHost.StartAsync(
            a => a.MapProfileEndpoints(),
            configureServices: services =>
            {
                services.AddSingleton(repo);
                services.AddBethuyaUserContext();
            });
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/profile", UriKind.Relative));
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Attendee);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, "user-1");

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await repo.Received(1).GetByUserIdAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetProfile_Anonymous_IsRejected()
    {
        var repo = Substitute.For<IAttendeeProfileRepository>();

        await using var app = await HeaderRoleAuthHost.StartAsync(
            a => a.MapProfileEndpoints(),
            configureServices: services =>
            {
                services.AddSingleton(repo);
                services.AddBethuyaUserContext();
            });
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/api/profile", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await repo.DidNotReceiveWithAnyArgs().GetByUserIdAsync(default!, default);
    }

    private static AttendeeProfile BuildProfile(string userId) => new()
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
        IsProfileComplete = true,
        ProfileCompletedAt = DateTimeOffset.UtcNow
    };
}

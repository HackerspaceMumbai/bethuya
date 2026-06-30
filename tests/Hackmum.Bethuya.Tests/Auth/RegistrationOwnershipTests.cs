using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR4 (Phase 4): per-registration resource-ownership enforcement closes the horizontal-privilege /
/// IDOR gap left by PR3. The owning attendee can read/mutate/delete their registration; a different
/// attendee is denied with <c>404</c> (existence-hiding for a user-owned PII resource); operational
/// bypass roles (organizer/curator/admin) may act on any registration. The event-scoped list — which
/// exposes every registrant's PII — is restricted to bypass roles and a bare attendee receives
/// <c>403</c> (role-scope denial; the route plainly exists, so no existence to hide).
/// </summary>
public sealed class RegistrationOwnershipTests
{
    private const string OwnerSub = "owner-1";
    private const string OtherSub = "intruder-2";

    // Minimal valid PNG: the 8-byte signature is enough to pass the endpoint's content-signature check.
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];

    [Test]
    public async Task GetById_Owner_ReturnsOk()
    {
        var registrationId = Guid.CreateVersion7();
        var repo = Substitute.For<IRegistrationRepository>();
        repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(registrationId, OwnerSub));

        await using var app = await StartHostAsync(repo);
        using var client = app.GetTestClient();

        var response = await SendAsync(client, HttpMethod.Get, registrationId, BethuyaRoleNames.Attendee, OwnerSub);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetById_NonOwnerAttendee_ReturnsNotFound()
    {
        var registrationId = Guid.CreateVersion7();
        var repo = Substitute.For<IRegistrationRepository>();
        repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(registrationId, OwnerSub));

        await using var app = await StartHostAsync(repo);
        using var client = app.GetTestClient();

        var response = await SendAsync(client, HttpMethod.Get, registrationId, BethuyaRoleNames.Attendee, OtherSub);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetById_NonOwnerOrganizer_BypassesOwnershipAndReturnsOk()
    {
        var registrationId = Guid.CreateVersion7();
        var repo = Substitute.For<IRegistrationRepository>();
        repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(registrationId, OwnerSub));

        await using var app = await StartHostAsync(repo);
        using var client = app.GetTestClient();

        var response = await SendAsync(client, HttpMethod.Get, registrationId, BethuyaRoleNames.Organizer, OtherSub);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Delete_Owner_ReturnsNoContentAndDeletes()
    {
        var registrationId = Guid.CreateVersion7();
        var repo = Substitute.For<IRegistrationRepository>();
        repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(registrationId, OwnerSub));

        await using var app = await StartHostAsync(repo);
        using var client = app.GetTestClient();

        var response = await SendAsync(client, HttpMethod.Delete, registrationId, BethuyaRoleNames.Attendee, OwnerSub);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await repo.Received(1).DeleteAsync(registrationId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Delete_NonOwnerAttendee_ReturnsNotFoundAndDoesNotDelete()
    {
        var registrationId = Guid.CreateVersion7();
        var repo = Substitute.For<IRegistrationRepository>();
        repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(registrationId, OwnerSub));

        await using var app = await StartHostAsync(repo);
        using var client = app.GetTestClient();

        var response = await SendAsync(client, HttpMethod.Delete, registrationId, BethuyaRoleNames.Attendee, OtherSub);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await repo.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Test]
    public async Task UploadGovernmentId_Owner_ReturnsNoContent()
    {
        var registrationId = Guid.CreateVersion7();
        var repo = Substitute.For<IRegistrationRepository>();
        repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(registrationId, OwnerSub));

        await using var app = await StartHostAsync(repo);
        using var client = app.GetTestClient();

        var response = await SendGovernmentIdAsync(client, registrationId, BethuyaRoleNames.Attendee, OwnerSub);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await repo.Received(1).UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UploadGovernmentId_NonOwnerAttendee_ReturnsNotFoundAndDoesNotUpdate()
    {
        var registrationId = Guid.CreateVersion7();
        var repo = Substitute.For<IRegistrationRepository>();
        repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(registrationId, OwnerSub));

        await using var app = await StartHostAsync(repo);
        using var client = app.GetTestClient();

        var response = await SendGovernmentIdAsync(client, registrationId, BethuyaRoleNames.Attendee, OtherSub);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await repo.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Test]
    public async Task EventList_BareAttendee_ReturnsForbidden()
    {
        var eventId = Guid.CreateVersion7();
        var repo = Substitute.For<IRegistrationRepository>();

        await using var app = await StartHostAsync(repo);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/attendee/registrations/event/{eventId}", UriKind.Relative));
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Attendee);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, OwnerSub);

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await repo.DidNotReceiveWithAnyArgs().GetByEventIdAsync(default, default);
    }

    [Test]
    public async Task EventList_Organizer_ReturnsOk()
    {
        var eventId = Guid.CreateVersion7();
        var repo = Substitute.For<IRegistrationRepository>();
        repo.GetByEventIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns([BuildRegistration(Guid.CreateVersion7(), OwnerSub)]);

        await using var app = await StartHostAsync(repo);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/attendee/registrations/event/{eventId}", UriKind.Relative));
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Organizer);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, OtherSub);

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await repo.Received(1).GetByEventIdAsync(eventId, Arg.Any<CancellationToken>());
    }

    private static Task<WebApplication> StartHostAsync(IRegistrationRepository repo) =>
        HeaderRoleAuthHost.StartAsync(
            a => a.MapRegistrationEndpoints(),
            configureServices: services =>
            {
                services.AddSingleton(repo);
                // CreateRegistrationAsync is mapped by the same group, so its dependencies must resolve
                // for endpoint parameter inference even though these tests exercise the read/mutate/delete
                // ownership paths.
                services.AddSingleton(Substitute.For<IAttendeeProfileRepository>());
                services.AddSingleton<InclusionSignalsNormalizer>();
                services.AddBethuyaUserContext();
                services.AddDataProtection();
            });

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, Guid registrationId, string role, string sub)
    {
        using var request = new HttpRequestMessage(
            method, new Uri($"/api/attendee/registrations/{registrationId}", UriKind.Relative));
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, role);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, sub);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendGovernmentIdAsync(
        HttpClient client, Guid registrationId, string role, string sub)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(PngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "id.png");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/attendee/registrations/{registrationId}/government-id", UriKind.Relative))
        {
            Content = content
        };
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, role);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, sub);
        return await client.SendAsync(request);
    }

    private static Registration BuildRegistration(Guid id, string ownerSub) => new()
    {
        Id = id,
        EventId = Guid.CreateVersion7(),
        UserId = ownerSub,
        FullName = "Owner Attendee",
        Email = $"{ownerSub}@bethuya.test",
        Intent = "Learn and contribute."
    };
}

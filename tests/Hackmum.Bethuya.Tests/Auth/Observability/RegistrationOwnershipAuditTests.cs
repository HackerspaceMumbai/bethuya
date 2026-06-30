using System.Net;
using System.Net.Http;
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
using ServiceDefaults.Auth.Observability;

namespace Hackmum.Bethuya.Tests.Auth.Observability;

/// <summary>
/// PR5: every security-relevant ownership/IDOR decision on the registration endpoints emits exactly one
/// non-PII audit record carrying the governing policy, decision, resource type/id and the caller's
/// subject hash. Asserted against a recording <see cref="IAuthorizationAuditor"/> substituted into the
/// PR3/PR4 functional host.
/// </summary>
public sealed class RegistrationOwnershipAuditTests
{
    private const string OwnerSub = "owner-1";
    private const string IntruderSub = "intruder-2";

    [Test]
    public async Task NonOwnerGet_EmitsExactlyOneDenyRecord_WithIntruderSubjectHash_AndNoPii()
    {
        var registrationId = Guid.CreateVersion7();
        var recorder = new RecordingAuditor();

        await using var app = await StartHostAsync(registrationId, recorder);
        using var client = app.GetTestClient();

        var response = await GetAsync(client, registrationId, BethuyaRoleNames.Attendee, IntruderSub);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        var record = recorder.Events.Single();
        await Assert.That(record.Decision).IsEqualTo(AuthorizationDecision.Deny);
        await Assert.That(record.PolicyName).IsEqualTo(BethuyaPolicyNames.ResourceOwner);
        await Assert.That(record.ResourceType).IsEqualTo(BethuyaAuditResourceTypes.Registration);
        await Assert.That(record.ResourceId).IsEqualTo(registrationId.ToString());
        await Assert.That(record.OutcomeStatusCode).IsEqualTo(404);
        await Assert.That(record.SubjectHash).IsEqualTo(SubjectHash.Compute(IntruderSub));

        // Non-PII guarantee: the record exposes no slot for and never carries Email/Name/gov-id.
        await Assert.That(record.SubjectHash).IsNotEqualTo(IntruderSub);
    }

    [Test]
    public async Task OwnerGet_EmitsExactlyOneAllowRecord_WithOwnerSubjectHash()
    {
        var registrationId = Guid.CreateVersion7();
        var recorder = new RecordingAuditor();

        await using var app = await StartHostAsync(registrationId, recorder);
        using var client = app.GetTestClient();

        var response = await GetAsync(client, registrationId, BethuyaRoleNames.Attendee, OwnerSub);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var record = recorder.Events.Single();
        await Assert.That(record.Decision).IsEqualTo(AuthorizationDecision.Allow);
        await Assert.That(record.SubjectHash).IsEqualTo(SubjectHash.Compute(OwnerSub));
        await Assert.That(record.OutcomeStatusCode).IsEqualTo(200);
    }

    [Test]
    public async Task NonOwnerOrganizerGet_EmitsExactlyOneBypassRecord()
    {
        var registrationId = Guid.CreateVersion7();
        var recorder = new RecordingAuditor();

        await using var app = await StartHostAsync(registrationId, recorder);
        using var client = app.GetTestClient();

        var response = await GetAsync(client, registrationId, BethuyaRoleNames.Organizer, IntruderSub);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var record = recorder.Events.Single();
        await Assert.That(record.Decision).IsEqualTo(AuthorizationDecision.Bypass);
        await Assert.That(record.SubjectHash).IsEqualTo(SubjectHash.Compute(IntruderSub));
    }

    private static Task<WebApplication> StartHostAsync(Guid registrationId, IAuthorizationAuditor auditor)
    {
        var repo = Substitute.For<IRegistrationRepository>();
        repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(new Registration
            {
                Id = registrationId,
                EventId = Guid.CreateVersion7(),
                UserId = OwnerSub,
                FullName = "Owner Attendee",
                Email = $"{OwnerSub}@bethuya.test",
                Intent = "Learn and contribute."
            });

        return HeaderRoleAuthHost.StartAsync(
            a => a.MapRegistrationEndpoints(),
            configureServices: services =>
            {
                services.AddSingleton(repo);
                services.AddSingleton(Substitute.For<IAttendeeProfileRepository>());
                services.AddSingleton<InclusionSignalsNormalizer>();
                services.AddBethuyaUserContext();
                services.AddDataProtection();
                // Registered after AddBethuyaAuthorization's TryAddSingleton, so this recording auditor
                // wins for IAuthorizationAuditor resolution.
                services.AddSingleton(auditor);
            });
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, Guid registrationId, string role, string sub)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/attendee/registrations/{registrationId}", UriKind.Relative));
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, role);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, sub);
        return await client.SendAsync(request);
    }
}

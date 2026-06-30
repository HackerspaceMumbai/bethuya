using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR3: functional enforcement of the named role policies. Combined with the metadata-level
/// <see cref="RouteGroupAuthorizationTests"/> (which assert each real endpoint carries the right
/// policy), these prove the policies themselves reject anonymous/wrong-role principals and admit
/// correct-role principals, including the Admin-satisfies-all hierarchy.
/// </summary>
public sealed class PolicyEnforcementTests
{
    [Test]
    [Arguments(BethuyaPolicyNames.RequireAdmin, BethuyaRoleNames.Admin)]
    [Arguments(BethuyaPolicyNames.RequireOrganizer, BethuyaRoleNames.Organizer)]
    [Arguments(BethuyaPolicyNames.RequireOrganizer, BethuyaRoleNames.Admin)]
    [Arguments(BethuyaPolicyNames.RequireCurator, BethuyaRoleNames.Curator)]
    [Arguments(BethuyaPolicyNames.RequireCurator, BethuyaRoleNames.Admin)]
    [Arguments(BethuyaPolicyNames.RequireAttendee, BethuyaRoleNames.Attendee)]
    [Arguments(BethuyaPolicyNames.RequireAttendee, BethuyaRoleNames.Organizer)]
    [Arguments(BethuyaPolicyNames.RequireAttendee, BethuyaRoleNames.Admin)]
    public async Task Policy_AllowsCorrectRole(string policy, string role)
    {
        var status = await CallAsync(policy, role);
        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    [Arguments(BethuyaPolicyNames.RequireAdmin, BethuyaRoleNames.Organizer)]
    [Arguments(BethuyaPolicyNames.RequireAdmin, BethuyaRoleNames.Attendee)]
    [Arguments(BethuyaPolicyNames.RequireOrganizer, BethuyaRoleNames.Curator)]
    [Arguments(BethuyaPolicyNames.RequireOrganizer, BethuyaRoleNames.Attendee)]
    [Arguments(BethuyaPolicyNames.RequireCurator, BethuyaRoleNames.Organizer)]
    [Arguments(BethuyaPolicyNames.RequireCurator, BethuyaRoleNames.Attendee)]
    public async Task Policy_RejectsWrongRole(string policy, string role)
    {
        var status = await CallAsync(policy, role);
        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    [Arguments(BethuyaPolicyNames.RequireAdmin)]
    [Arguments(BethuyaPolicyNames.RequireOrganizer)]
    [Arguments(BethuyaPolicyNames.RequireCurator)]
    [Arguments(BethuyaPolicyNames.RequireAttendee)]
    public async Task Policy_RejectsAnonymous(string policy)
    {
        var status = await CallAsync(policy, role: null);
        await Assert.That(status).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpStatusCode> CallAsync(string policy, string? role)
    {
        await using var app = await HeaderRoleAuthHost.StartAsync(a =>
        {
            a.MapGet($"/{policy}", () => Results.Ok()).RequireAuthorization(policy);
        });
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/{policy}", UriKind.Relative));
        if (role is not null)
        {
            request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, role);
        }

        var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}

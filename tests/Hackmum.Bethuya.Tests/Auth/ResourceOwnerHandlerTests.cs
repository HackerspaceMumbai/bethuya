using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR4: unit coverage for <see cref="ResourceOwnerHandler"/>. The handler backs the
/// <see cref="BethuyaPolicyNames.ResourceOwner"/> policy: the caller must own the resource, unless they
/// hold a bypass role (Admin/Organizer/Curator). A null resource owner is never matched to a subject.
/// </summary>
public sealed class ResourceOwnerHandlerTests
{
    [Test]
    public async Task Owner_Succeeds()
    {
        var result = await EvaluateAsync(
            subject: "user-1",
            roles: [BethuyaRoleNames.Attendee],
            ownerUserId: "user-1");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task NonOwner_SameRole_Fails()
    {
        var result = await EvaluateAsync(
            subject: "user-2",
            roles: [BethuyaRoleNames.Attendee],
            ownerUserId: "user-1");

        await Assert.That(result).IsFalse();
    }

    [Test]
    [Arguments(BethuyaRoleNames.Admin)]
    [Arguments(BethuyaRoleNames.Organizer)]
    [Arguments(BethuyaRoleNames.Curator)]
    public async Task BypassRole_NonOwner_Succeeds(string role)
    {
        var result = await EvaluateAsync(
            subject: "staff-9",
            roles: [role],
            ownerUserId: "user-1");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task NullOwner_NonBypassRole_Fails()
    {
        var result = await EvaluateAsync(
            subject: "user-1",
            roles: [BethuyaRoleNames.Attendee],
            ownerUserId: null);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task NullOwner_BypassRole_Succeeds()
    {
        var result = await EvaluateAsync(
            subject: "staff-9",
            roles: [BethuyaRoleNames.Organizer],
            ownerUserId: null);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Unauthenticated_Fails()
    {
        var handler = new ResourceOwnerHandler();
        var requirement = new SameOwnerRequirement();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext(
            [requirement],
            anonymous,
            new ResourceOwnerContext("user-1"));

        await handler.HandleAsync(context);

        await Assert.That(context.HasSucceeded).IsFalse();
    }

    private static async Task<bool> EvaluateAsync(string subject, string[] roles, string? ownerUserId)
    {
        var handler = new ResourceOwnerHandler();
        var requirement = new SameOwnerRequirement();

        var claims = new List<Claim>
        {
            new("sub", subject),
            new(ClaimTypes.NameIdentifier, subject)
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var identity = new ClaimsIdentity(claims, "TestAuth", nameType: "name", roleType: "role");
        var principal = new ClaimsPrincipal(identity);

        var context = new AuthorizationHandlerContext(
            [requirement],
            principal,
            new ResourceOwnerContext(ownerUserId));

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }
}

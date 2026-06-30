using Bethuya.Hybrid.Shared.Auth;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR1 (L2): the canonical role and policy names live in ServiceDefaults (consumed by the actual
/// authorization registration). The UI-facing constants in Bethuya.Hybrid.Shared must stay in
/// lock-step with them; this test guards against drift across the layering boundary.
/// </summary>
public class AuthConstantsParityTests
{
    [Test]
    public async Task RoleNames_MatchSharedRoleConstants()
    {
        await Assert.That(BethuyaRoleNames.Admin).IsEqualTo(BethuyaRoles.Admin);
        await Assert.That(BethuyaRoleNames.Organizer).IsEqualTo(BethuyaRoles.Organizer);
        await Assert.That(BethuyaRoleNames.Curator).IsEqualTo(BethuyaRoles.Curator);
        await Assert.That(BethuyaRoleNames.Attendee).IsEqualTo(BethuyaRoles.Attendee);
    }

    [Test]
    public async Task PolicyNames_MatchSharedPolicyConstants()
    {
        await Assert.That(BethuyaPolicyNames.RequireAdmin).IsEqualTo(BethuyaAuthorizationPolicies.RequireAdmin);
        await Assert.That(BethuyaPolicyNames.RequireOrganizer).IsEqualTo(BethuyaAuthorizationPolicies.RequireOrganizer);
        await Assert.That(BethuyaPolicyNames.RequireCurator).IsEqualTo(BethuyaAuthorizationPolicies.RequireCurator);
        await Assert.That(BethuyaPolicyNames.RequireAttendee).IsEqualTo(BethuyaAuthorizationPolicies.RequireAttendee);
    }
}

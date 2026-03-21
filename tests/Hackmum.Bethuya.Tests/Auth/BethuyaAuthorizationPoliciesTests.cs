using Bethuya.Hybrid.Shared.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

public class BethuyaAuthorizationPoliciesTests
{
    [Test]
    public async Task AllPolicies_AreDistinct()
    {
        var policies = new[]
        {
            BethuyaAuthorizationPolicies.RequireAdmin,
            BethuyaAuthorizationPolicies.RequireOrganizer,
            BethuyaAuthorizationPolicies.RequireCurator,
            BethuyaAuthorizationPolicies.RequireAttendee
        };
        await Assert.That(policies.Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task AllPolicies_AreNonEmpty()
    {
        var policies = new[]
        {
            BethuyaAuthorizationPolicies.RequireAdmin,
            BethuyaAuthorizationPolicies.RequireOrganizer,
            BethuyaAuthorizationPolicies.RequireCurator,
            BethuyaAuthorizationPolicies.RequireAttendee
        };
        foreach (var policy in policies)
        {
            await Assert.That(policy).IsNotNull().And.IsNotEmpty();
        }
    }
}

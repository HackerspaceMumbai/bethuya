using Bethuya.Hybrid.Shared.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

public class BethuyaRolesTests
{
    [Test]
    public async Task AllRoles_AreDistinct()
    {
        var roles = new[] { BethuyaRoles.Admin, BethuyaRoles.Organizer, BethuyaRoles.Curator, BethuyaRoles.Attendee };
        await Assert.That(roles.Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task AllRoles_AreNonEmpty()
    {
        var roles = new[] { BethuyaRoles.Admin, BethuyaRoles.Organizer, BethuyaRoles.Curator, BethuyaRoles.Attendee };
        foreach (var role in roles)
        {
            await Assert.That(role).IsNotNull().And.IsNotEmpty();
        }
    }
}

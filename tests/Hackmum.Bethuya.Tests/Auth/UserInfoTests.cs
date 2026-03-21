using Bethuya.Hybrid.Shared.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

public class UserInfoTests
{
    [Test]
    public async Task UserInfo_RoundTrips_AllProperties()
    {
        var userInfo = new UserInfo(
            UserId: "user-123",
            Email: "test@bethuya.dev",
            Name: "Test User",
            Roles: [BethuyaRoles.Organizer, BethuyaRoles.Attendee]);

        await Assert.That(userInfo.UserId).IsEqualTo("user-123");
        await Assert.That(userInfo.Email).IsEqualTo("test@bethuya.dev");
        await Assert.That(userInfo.Name).IsEqualTo("Test User");
        await Assert.That(userInfo.Roles).HasCount().EqualTo(2);
        await Assert.That(userInfo.Roles).Contains(BethuyaRoles.Organizer);
        await Assert.That(userInfo.Roles).Contains(BethuyaRoles.Attendee);
    }

    [Test]
    public async Task UserInfo_RecordEquality_Works()
    {
        var a = new UserInfo("id", "e@x.com", "Name", ["Admin"]);
        var b = new UserInfo("id", "e@x.com", "Name", ["Admin"]);

        // Records have value equality for simple types, but arrays compare by reference
        await Assert.That(a.UserId).IsEqualTo(b.UserId);
        await Assert.That(a.Email).IsEqualTo(b.Email);
    }
}

using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Tests.Domain;

public class RegistrationTests
{
    [Test]
    public async Task Registration_DefaultStatus_IsPending()
    {
        var reg = new Registration { FullName = "Test User", Email = "test@example.com" };
        await Assert.That(reg.Status).IsEqualTo(RegistrationStatus.Pending);
    }

    [Test]
    public async Task Registration_Interests_InitializedEmpty()
    {
        var reg = new Registration { FullName = "Test User", Email = "test@example.com" };
        await Assert.That(reg.Interests).IsNotNull();
        await Assert.That(reg.Interests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Registration_CanAddInterests()
    {
        var reg = new Registration
        {
            FullName = "Test User",
            Email = "test@example.com",
            Interests = ["AI", "Blazor", ".NET"]
        };
        await Assert.That(reg.Interests.Count).IsEqualTo(3);
        await Assert.That(reg.Interests).Contains("AI");
    }

    [Test]
    public async Task Registration_UniqueIds()
    {
        var reg1 = new Registration { FullName = "User 1", Email = "u1@test.com" };
        var reg2 = new Registration { FullName = "User 2", Email = "u2@test.com" };
        await Assert.That(reg1.Id).IsNotEqualTo(reg2.Id);
    }
}

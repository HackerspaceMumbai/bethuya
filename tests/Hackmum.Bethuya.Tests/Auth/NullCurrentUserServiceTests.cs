using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Services;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>Tests that the <see cref="ICurrentUserService"/> contract is well-defined.</summary>
public class CurrentUserServiceContractTests
{
    [Test]
    public async Task UnauthenticatedUser_ReturnsExpectedDefaults()
    {
        ICurrentUserService svc = new StubUnauthenticatedUserService();

        await Assert.That(svc.UserId).IsNull();
        await Assert.That(svc.Email).IsNull();
        await Assert.That(svc.IsAuthenticated).IsFalse();
        await Assert.That(svc.IsInRole(BethuyaRoles.Admin)).IsFalse();
    }

    [Test]
    public async Task AuthenticatedUser_ReturnsExpectedValues()
    {
        ICurrentUserService svc = new StubAuthenticatedUserService("u1", "a@b.com", [BethuyaRoles.Organizer]);

        await Assert.That(svc.UserId).IsEqualTo("u1");
        await Assert.That(svc.Email).IsEqualTo("a@b.com");
        await Assert.That(svc.IsAuthenticated).IsTrue();
        await Assert.That(svc.IsInRole(BethuyaRoles.Organizer)).IsTrue();
        await Assert.That(svc.IsInRole(BethuyaRoles.Admin)).IsFalse();
    }

    private sealed class StubUnauthenticatedUserService : ICurrentUserService
    {
        public string? UserId => null;
        public string? Email => null;
        public bool IsAuthenticated => false;
        public bool IsInRole(string role) => false;
    }

    private sealed class StubAuthenticatedUserService(string userId, string email, string[] roles) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Email => email;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => roles.Contains(role);
    }
}

using Microsoft.Extensions.Hosting;

namespace Hackmum.Bethuya.Tests.Auth;

public class AuthProviderTypeTests
{
    [Test]
    public async Task None_IsDefaultValue()
    {
        var options = new BethuyaAuthOptions();
        await Assert.That(options.Provider).IsEqualTo(AuthProviderType.None);
    }

    [Test]
    public async Task AllProviders_AreDistinct()
    {
        var values = Enum.GetValues<AuthProviderType>();
        await Assert.That(values.Length).IsEqualTo(4);
        await Assert.That(values.Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    [Arguments(AuthProviderType.None)]
    [Arguments(AuthProviderType.Entra)]
    [Arguments(AuthProviderType.Auth0)]
    [Arguments(AuthProviderType.Keycloak)]
    public async Task Provider_CanBeAssigned(AuthProviderType provider)
    {
        var options = new BethuyaAuthOptions { Provider = provider };
        await Assert.That(options.Provider).IsEqualTo(provider);
    }
}

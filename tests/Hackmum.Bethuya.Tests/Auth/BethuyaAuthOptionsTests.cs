using Microsoft.Extensions.Hosting;

namespace Hackmum.Bethuya.Tests.Auth;

public class BethuyaAuthOptionsTests
{
    [Test]
    public async Task Defaults_ProviderIsNone()
    {
        var options = new BethuyaAuthOptions();
        await Assert.That(options.Provider).IsEqualTo(AuthProviderType.None);
    }

    [Test]
    public async Task Defaults_EntraOptionsAreEmpty()
    {
        var options = new BethuyaAuthOptions();
        await Assert.That(options.Entra.TenantId).IsEqualTo("");
        await Assert.That(options.Entra.ClientId).IsEqualTo("");
        await Assert.That(options.Entra.ClientSecret).IsEqualTo("");
        await Assert.That(options.Entra.CallbackPath).IsEqualTo("/signin-oidc");
    }

    [Test]
    public async Task Defaults_Auth0OptionsAreEmpty()
    {
        var options = new BethuyaAuthOptions();
        await Assert.That(options.Auth0.Domain).IsEqualTo("");
        await Assert.That(options.Auth0.ClientId).IsEqualTo("");
        await Assert.That(options.Auth0.ClientSecret).IsEqualTo("");
        await Assert.That(options.Auth0.CallbackPath).IsEqualTo("/callback");
    }

    [Test]
    public async Task Defaults_KeycloakOptionsAreEmpty()
    {
        var options = new BethuyaAuthOptions();
        await Assert.That(options.Keycloak.Authority).IsEqualTo("");
        await Assert.That(options.Keycloak.ClientId).IsEqualTo("");
        await Assert.That(options.Keycloak.ClientSecret).IsEqualTo("");
        await Assert.That(options.Keycloak.CallbackPath).IsEqualTo("/signin-oidc");
    }

    [Test]
    public async Task SectionName_IsNotEmpty()
    {
        var sectionName = BethuyaAuthOptions.SectionName;
        await Assert.That(sectionName).IsNotNull().And.IsNotEmpty();
    }
}

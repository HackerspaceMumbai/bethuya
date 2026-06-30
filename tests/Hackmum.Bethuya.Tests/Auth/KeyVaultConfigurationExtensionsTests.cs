using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hackmum.Bethuya.Tests.Auth;

public class KeyVaultConfigurationExtensionsTests
{
    [Test]
    public async Task AddBethuyaKeyVaultConfiguration_Development_DoesNotRegisterHostedValidation()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });

        builder.AddBethuyaKeyVaultConfiguration();

        var hasValidationHostedService = builder.Services.Any(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType?.Name == "KeyVaultStartupValidationHostedService");

        await Assert.That(hasValidationHostedService).IsFalse();
    }

    [Test]
    public async Task AddBethuyaKeyVaultConfiguration_HostedWithoutUri_Throws()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production
        });

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddBethuyaKeyVaultConfiguration());

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("KEY_VAULT_URI");
        await Assert.That(exception.Message).Contains("VAULT_URI");
    }

    [Test]
    public async Task AddBethuyaKeyVaultConfiguration_HostedWithVaultUri_UsesVaultUriKey()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VAULT_URI"] = "not-a-valid-uri"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddBethuyaKeyVaultConfiguration());
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("not-a-valid-uri");
    }
}

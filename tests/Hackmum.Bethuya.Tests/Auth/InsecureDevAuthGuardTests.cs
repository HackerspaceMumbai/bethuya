using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR1 (C2): the insecure development authentication handler (Provider=None) must fail closed
/// outside the Development environment unless an explicit opt-in flag is set.
/// </summary>
public class InsecureDevAuthGuardTests
{
    [Test]
    public async Task ApiAuthentication_NoneProvider_Production_WithoutOptIn_Throws()
    {
        var builder = CreateBuilder(Environments.Production, optIn: false);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddBethuyaApiAuthentication());

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("AllowInsecureDevAuth");
    }

    [Test]
    public async Task WebAuthentication_NoneProvider_Production_WithoutOptIn_Throws()
    {
        var builder = CreateBuilder(Environments.Production, optIn: false);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddBethuyaWebAuthentication());

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("AllowInsecureDevAuth");
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_Production_WithOptIn_RegistersDevelopmentScheme()
    {
        var builder = CreateBuilder(Environments.Production, optIn: true);

        builder.AddBethuyaApiAuthentication();

        await AssertDevelopmentSchemeRegistered(builder);
    }

    [Test]
    public async Task ApiAuthentication_NoneProvider_Development_RegistersDevelopmentScheme()
    {
        var builder = CreateBuilder(Environments.Development, optIn: false);

        builder.AddBethuyaApiAuthentication();

        await AssertDevelopmentSchemeRegistered(builder);
    }

    [Test]
    public async Task WebAuthentication_NoneProvider_Production_WithOptIn_RegistersDevelopmentScheme()
    {
        var builder = CreateBuilder(Environments.Production, optIn: true);

        builder.AddBethuyaWebAuthentication();

        await AssertDevelopmentSchemeRegistered(builder);
    }

    [Test]
    public async Task WebAuthentication_NoneProvider_Development_RegistersDevelopmentScheme()
    {
        var builder = CreateBuilder(Environments.Development, optIn: false);

        builder.AddBethuyaWebAuthentication();

        await AssertDevelopmentSchemeRegistered(builder);
    }

    private static HostApplicationBuilder CreateBuilder(string environment, bool optIn)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment
        });

        var settings = new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "None"
        };

        if (optIn)
        {
            settings["Authentication:AllowInsecureDevAuth"] = "true";
        }

        builder.Configuration.AddInMemoryCollection(settings);
        return builder;
    }

    private static async Task AssertDevelopmentSchemeRegistered(HostApplicationBuilder builder)
    {
        using var provider = builder.Services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetSchemeAsync(DevelopmentAuthenticationDefaults.SchemeName);

        await Assert.That(scheme).IsNotNull();
    }
}

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR1 (H4): the Backend JWT Bearer authentication must validate issuer, audience, lifetime and
/// signing key, and require HTTPS metadata outside Development.
/// </summary>
public class JwtBearerHardeningTests
{
    [Test]
    public async Task ApiAuthentication_RealProvider_Production_HardensTokenValidation()
    {
        var options = BuildJwtBearerOptions(Environments.Production);

        await Assert.That(options.RequireHttpsMetadata).IsTrue();
        await Assert.That(options.TokenValidationParameters.ValidateIssuer).IsTrue();
        await Assert.That(options.TokenValidationParameters.ValidateAudience).IsTrue();
        await Assert.That(options.TokenValidationParameters.ValidateLifetime).IsTrue();
        await Assert.That(options.TokenValidationParameters.ValidateIssuerSigningKey).IsTrue();
        await Assert.That(options.TokenValidationParameters.RoleClaimType).IsEqualTo("realm_access");
        await Assert.That(options.TokenValidationParameters.NameClaimType).IsEqualTo("name");
        await Assert.That(options.TokenValidationParameters.ClockSkew).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task ApiAuthentication_RealProvider_Development_AllowsHttpMetadata()
    {
        var options = BuildJwtBearerOptions(Environments.Development);

        await Assert.That(options.RequireHttpsMetadata).IsFalse();
        await Assert.That(options.TokenValidationParameters.ValidateIssuer).IsTrue();
        await Assert.That(options.TokenValidationParameters.ValidateAudience).IsTrue();
    }

    private static JwtBearerOptions BuildJwtBearerOptions(string environment)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "Keycloak",
            ["Authentication:Keycloak:Authority"] = "https://keycloak.example.com/realms/bethuya",
            ["Authentication:Keycloak:ClientId"] = "bethuya-api"
        });

        builder.AddBethuyaApiAuthentication();

        using var provider = builder.Services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        return monitor.Get(JwtBearerDefaults.AuthenticationScheme);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR1 (C1): a config-gated default-deny fallback policy. When enabled, every endpoint without an
/// explicit policy requires an authenticated user. Disabled by default so it can be rolled out
/// independently after route separation marks public endpoints anonymous.
/// </summary>
public class AuthorizationFallbackPolicyTests
{
    [Test]
    public async Task AddBethuyaAuthorization_Default_HasNoFallbackPolicy()
    {
        var builder = CreateBuilder(enforceFallback: null);

        builder.AddBethuyaAuthorization();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        await Assert.That(options.FallbackPolicy).IsNull();
    }

    [Test]
    public async Task AddBethuyaAuthorization_FallbackEnabled_RequiresAuthenticatedUser()
    {
        var builder = CreateBuilder(enforceFallback: true);

        builder.AddBethuyaAuthorization();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        await Assert.That(options.FallbackPolicy).IsNotNull();
        await Assert.That(options.FallbackPolicy!.Requirements).IsNotEmpty();
    }

    [Test]
    public async Task AddBethuyaAuthorization_AlwaysRegistersNamedRolePolicies()
    {
        var builder = CreateBuilder(enforceFallback: null);

        builder.AddBethuyaAuthorization();

        using var provider = builder.Services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        await Assert.That(await policyProvider.GetPolicyAsync(BethuyaPolicyNames.RequireOrganizer)).IsNotNull();
        await Assert.That(await policyProvider.GetPolicyAsync(BethuyaPolicyNames.RequireCurator)).IsNotNull();
        await Assert.That(await policyProvider.GetPolicyAsync(BethuyaPolicyNames.RequireAttendee)).IsNotNull();
        await Assert.That(await policyProvider.GetPolicyAsync(BethuyaPolicyNames.RequireAdmin)).IsNotNull();
    }

    private static HostApplicationBuilder CreateBuilder(bool? enforceFallback)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });

        if (enforceFallback is not null)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:EnforceAuthenticatedFallback"] = enforceFallback.Value ? "true" : "false"
            });
        }

        return builder;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR3 (C1/C4): the default-deny fallback policy is now ON by default. Every endpoint without an
/// explicit policy requires an authenticated user. The <c>Authorization:EnforceAuthenticatedFallback</c>
/// key is retained purely as an emergency escape hatch to disable default-deny.
/// </summary>
public class AuthorizationFallbackPolicyTests
{
    [Test]
    public async Task AddBethuyaAuthorization_Default_RequiresAuthenticatedUser()
    {
        var builder = CreateBuilder(enforceFallback: null);

        builder.AddBethuyaAuthorization();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        await Assert.That(options.FallbackPolicy).IsNotNull();
        await Assert.That(options.FallbackPolicy!.Requirements
            .Any(r => r is DenyAnonymousAuthorizationRequirement)).IsTrue();
    }

    [Test]
    public async Task AddBethuyaAuthorization_EscapeHatchDisabled_HasNoFallbackPolicy()
    {
        var builder = CreateBuilder(enforceFallback: false);

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
        await Assert.That(options.FallbackPolicy.Requirements
            .Any(r => r is DenyAnonymousAuthorizationRequirement)).IsTrue();
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

        // Clear ambient configuration (a referenced app project flows an appsettings.json into the
        // test output that may set Authorization:EnforceAuthenticatedFallback). This keeps the
        // "default" case a true unset so the production default-on behavior is exercised.
        builder.Configuration.Sources.Clear();

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

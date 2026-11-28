using AppHost.Infrastructure;
using Aspire.Hosting;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Infrastructure;

public class EnvironmentalPoliciesTests
{
    [Test]
    public async Task ShouldEnableOnboardingBypass_LocalDevelopment_DefaultFlagFalse_ReturnsTrue()
    {
        var builder = CreateBuilder(DistributedApplicationOperation.Run);

        var shouldBypass = builder.ShouldEnableOnboardingBypass(enableOnboardingFlowInDevelopment: false);

        await Assert.That(shouldBypass).IsTrue();
    }

    [Test]
    public async Task ShouldEnableOnboardingBypass_LocalDevelopment_FlagTrue_ReturnsFalse()
    {
        var builder = CreateBuilder(DistributedApplicationOperation.Run);

        var shouldBypass = builder.ShouldEnableOnboardingBypass(enableOnboardingFlowInDevelopment: true);

        await Assert.That(shouldBypass).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldEnableOnboardingBypass_PublishMode_AlwaysReturnsFalse(bool enableOnboardingFlowInDevelopment)
    {
        var builder = CreateBuilder(DistributedApplicationOperation.Publish);

        var shouldBypass = builder.ShouldEnableOnboardingBypass(enableOnboardingFlowInDevelopment);

        await Assert.That(shouldBypass).IsFalse();
    }

    [Test]
    public async Task ResolveBuildConfiguration_MatchesCurrentBuildConfiguration()
    {
        var configuration = EnvironmentalPolicies.ResolveBuildConfiguration();

#if DEBUG
        await Assert.That(configuration).IsEqualTo("Debug");
#else
        await Assert.That(configuration).IsEqualTo("Release");
#endif
    }

    [Test]
    [Arguments("Debug")]
    [Arguments("Release")]
    public async Task ResolveWebStaticAssetsManifestPath_UsesProvidedConfiguration(string configuration)
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "bethuya-test-repo");
        var appHostDirectory = Path.Combine(repoRoot, "AppHost", "AppHost");

        var manifestPath = EnvironmentalPolicies.ResolveWebStaticAssetsManifestPath(appHostDirectory, configuration);

        var expectedPath = Path.GetFullPath(Path.Combine(
            repoRoot,
            "src",
            "Bethuya.Hybrid",
            "Bethuya.Hybrid.Web",
            "obj",
            configuration,
            "net10.0",
            "staticwebassets.development.json"));
        await Assert.That(manifestPath).IsEqualTo(expectedPath);
    }

    private static IDistributedApplicationBuilder CreateBuilder(DistributedApplicationOperation operation)
    {
        var builder = Substitute.For<IDistributedApplicationBuilder>();
        builder.ExecutionContext.Returns(new DistributedApplicationExecutionContext(operation));
        return builder;
    }
}

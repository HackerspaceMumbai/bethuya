using GitHub.Copilot;
using Hackmum.Bethuya.AI.CopilotSdk;

namespace Hackmum.Bethuya.Tests.Agents;

public class DateRecommendationSessionConfigTests
{
    [Test]
    public async Task CreateSessionConfig_ExcludesEveryToolSource()
    {
        var config = DateRecommendationService.CreateSessionConfig("gpt-4.1");

        await Assert.That(config.ExcludedTools).IsNotNull();
        await Assert.That(config.ExcludedTools!).Contains("builtin:*");
        await Assert.That(config.ExcludedTools!).Contains("mcp:*");
        await Assert.That(config.ExcludedTools!).Contains("custom:*");
    }

    [Test]
    public async Task CreateSessionConfig_RejectsShellPermissionRequest()
    {
        var config = DateRecommendationService.CreateSessionConfig("gpt-4.1");
        var request = new PermissionRequestShell
        {
            CanOfferSessionApproval = false,
            Commands = [],
            FullCommandText = "cat /etc/passwd",
            HasWriteFileRedirection = false,
            Intention = "Read a file",
            PossiblePaths = ["/etc/passwd"],
            PossibleUrls = []
        };

        await Assert.That(config.OnPermissionRequest).IsNotNull();
        var decision = await config.OnPermissionRequest!(request, new PermissionInvocation { SessionId = "test" });

        await Assert.That(decision.Kind).IsEqualTo("reject");
    }
}

using Hackmum.Bethuya.AI.Configuration;
using Hackmum.Bethuya.AI.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Tests.Agents;

public class AIRouterTests
{
    private const string FoundryLocalEndpoint = "http://localhost:5272/";
    private const string OllamaEndpoint = "http://localhost:11434/";
    private const string FoundryCloudEndpoint = "https://api.microsoft.com/foundry/v1";
    private const string OpenAIEndpoint = "https://api.openai.com/v1";

    [Test]
    public async Task Sensitive_UsesConfiguredLocalProvider()
    {
        var router = CreateRouter(new AIRoutingOptions
        {
            Providers = Providers(FoundryLocal(), FoundryCloud(), OpenAI())
        });

        var endpoint = GetEndpoint(router.GetChatClient(DataSensitivity.Sensitive));

        await Assert.That(endpoint).IsEqualTo(FoundryLocalEndpoint);
    }

    [Test]
    public async Task Sensitive_WhenLocalPrimaryMissing_FallsBackToLocalProviderOnly()
    {
        var router = CreateRouter(new AIRoutingOptions
        {
            Providers = Providers(FoundryCloud(), Ollama(), OpenAI())
        });

        var endpoint = GetEndpoint(router.GetChatClient(DataSensitivity.Sensitive));

        await Assert.That(endpoint).IsEqualTo(OllamaEndpoint);
    }

    [Test]
    public async Task Sensitive_WhenNoLocalProviderConfigured_ThrowsInsteadOfUsingCloud()
    {
        var router = CreateRouter(new AIRoutingOptions
        {
            Providers = Providers(FoundryCloud(), OpenAI())
        });

        await Assert.That(() => router.GetChatClient(DataSensitivity.Sensitive))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Sensitive_SkipsCloudProviderConfiguredAsSensitivePrimary()
    {
        var router = CreateRouter(new AIRoutingOptions
        {
            SensitiveProvider = "OpenAI",
            Providers = Providers(OpenAI(), Ollama())
        });

        var endpoint = GetEndpoint(router.GetChatClient(DataSensitivity.Sensitive));

        await Assert.That(endpoint).IsEqualTo(OllamaEndpoint);
    }

    [Test]
    public async Task Sensitive_SkipsCloudProviderConfiguredAsFallback()
    {
        var router = CreateRouter(new AIRoutingOptions
        {
            FallbackProvider = "OpenAI",
            Providers = Providers(OpenAI())
        });

        await Assert.That(() => router.GetChatClient(DataSensitivity.Sensitive))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task NonSensitive_StillUsesCloudProvider()
    {
        var router = CreateRouter(new AIRoutingOptions
        {
            Providers = Providers(FoundryLocal(), FoundryCloud(), Ollama())
        });

        var endpoint = GetEndpoint(router.GetChatClient(DataSensitivity.NonSensitive));

        await Assert.That(endpoint).IsEqualTo(FoundryCloudEndpoint);
    }

    private static AIRouter CreateRouter(AIRoutingOptions options) =>
        new(Options.Create(options), NullLogger<AIRouter>.Instance);

    private static string? GetEndpoint(IChatClient client) =>
        client.GetService<ChatClientMetadata>()?.ProviderUri?.ToString();

    private static Dictionary<string, AIProviderOptions> Providers(params AIProviderOptions[] providers) =>
        providers.ToDictionary(p => p.Name);

    private static AIProviderOptions FoundryLocal() =>
        new() { Name = "FoundryLocal", Endpoint = FoundryLocalEndpoint, IsLocal = true };

    private static AIProviderOptions Ollama() =>
        new() { Name = "Ollama", Endpoint = OllamaEndpoint, ModelId = "llama3.2", IsLocal = true };

    private static AIProviderOptions FoundryCloud() =>
        new() { Name = "Foundry", Endpoint = FoundryCloudEndpoint, ApiKey = "test-key", ModelId = "gpt-4o" };

    private static AIProviderOptions OpenAI() =>
        new() { Name = "OpenAI", Endpoint = OpenAIEndpoint, ApiKey = "test-key", ModelId = "gpt-4o" };
}

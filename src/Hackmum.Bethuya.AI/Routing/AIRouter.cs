using Hackmum.Bethuya.AI.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.AI.Routing;

/// <summary>
/// Privacy-aware AI provider router.
/// Routes Sensitive data to local providers (Foundry Local) and non-sensitive to cloud.
/// </summary>
public sealed partial class AIRouter(
    IOptions<AIRoutingOptions> options,
    ILogger<AIRouter> logger) : IAIRouter
{
    private readonly AIRoutingOptions _options = options.Value;
    private static readonly string[] SensitiveFallbacks = ["Foundry", "Ollama", "OpenAI"];
    private static readonly string[] NonSensitiveFallbacks = ["Ollama", "OpenAI"];

    public IChatClient GetChatClient(DataSensitivity sensitivity)
    {
        foreach (var providerName in GetProviderCandidates(sensitivity))
        {
            LogRouting(logger, sensitivity, providerName);

            if (!_options.Providers.TryGetValue(providerName, out var providerOptions))
            {
                LogProviderMissing(logger, providerName);
                continue;
            }

            // Skip cloud providers that are not configured with keys.
            if (string.IsNullOrWhiteSpace(providerOptions.ApiKey) && !providerOptions.IsLocal)
            {
                LogMissingApiKey(logger, providerName);
                continue;
            }

            return CreateChatClient(providerName, providerOptions);
        }

        throw new InvalidOperationException(
            $"No configured AI provider was available for '{sensitivity}'. Checked: {string.Join(", ", GetProviderCandidates(sensitivity))}.");
    }

    public string GetProviderName(DataSensitivity sensitivity) => sensitivity switch
    {
        DataSensitivity.Sensitive => _options.SensitiveProvider,
        DataSensitivity.NonSensitive => _options.NonSensitiveProvider,
        DataSensitivity.Public => _options.PublicProvider,
        _ => _options.FallbackProvider
    };

    private List<string> GetProviderCandidates(DataSensitivity sensitivity)
    {
        var configuredPrimary = GetProviderName(sensitivity);
        var fallbacks = sensitivity switch
        {
            DataSensitivity.Sensitive => SensitiveFallbacks,
            DataSensitivity.NonSensitive => NonSensitiveFallbacks,
            DataSensitivity.Public => NonSensitiveFallbacks,
            _ => NonSensitiveFallbacks
        };

        // Keep configured primary first, then deterministic chain, then configurable fallback if set.
        var candidates = new List<string> { configuredPrimary };
        candidates.AddRange(fallbacks);
        candidates.Add(_options.FallbackProvider);

        return candidates
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IChatClient CreateChatClient(string providerName, AIProviderOptions providerOptions)
    {
        // All supported providers (Foundry, FoundryLocal, Ollama, Azure OpenAI, OpenAI) expose
        // an OpenAI-compatible chat completions API. Foundry is now primary; others are fallbacks.
        var endpoint = new Uri(providerOptions.Endpoint);
        var modelId = providerOptions.ModelId ?? "default";

        LogCreatingClient(logger, providerName, endpoint, modelId);

        var client = new OpenAI.OpenAIClient(
            new System.ClientModel.ApiKeyCredential(providerOptions.ApiKey ?? "not-required"),
            new OpenAI.OpenAIClientOptions { Endpoint = endpoint });

        return new ChatClientBuilder(client.GetChatClient(modelId).AsIChatClient())
            .UseFunctionInvocation()
            .UseOpenTelemetry(configure: t => t.EnableSensitiveData = false)
            .Build();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Routing {Sensitivity} request to provider: {Provider}")]
    private static partial void LogRouting(ILogger logger, DataSensitivity sensitivity, string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Provider {Provider} not configured, skipping")]
    private static partial void LogProviderMissing(ILogger logger, string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Provider {Provider} missing API key, skipping")]
    private static partial void LogMissingApiKey(ILogger logger, string provider);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating chat client for {Provider} at {Endpoint} with model {Model}")]
    private static partial void LogCreatingClient(ILogger logger, string provider, Uri endpoint, string model);
}

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

    public IChatClient GetChatClient(DataSensitivity sensitivity)
    {
        var providerName = GetProviderName(sensitivity);
        LogRouting(logger, sensitivity, providerName);

        if (!_options.Providers.TryGetValue(providerName, out var providerOptions))
        {
            LogFallback(logger, providerName, _options.FallbackProvider);
            providerName = _options.FallbackProvider;

            if (!_options.Providers.TryGetValue(providerName, out providerOptions))
            {
                throw new InvalidOperationException(
                    $"Neither provider '{GetProviderName(sensitivity)}' nor fallback '{_options.FallbackProvider}' is configured.");
            }
        }

        return CreateChatClient(providerName, providerOptions);
    }

    public string GetProviderName(DataSensitivity sensitivity) => sensitivity switch
    {
        DataSensitivity.Sensitive => _options.SensitiveProvider,
        DataSensitivity.NonSensitive => _options.NonSensitiveProvider,
        DataSensitivity.Public => _options.PublicProvider,
        _ => _options.FallbackProvider
    };

    private IChatClient CreateChatClient(string providerName, AIProviderOptions providerOptions)
    {
        // All supported providers (Foundry Local, Ollama, Azure OpenAI, OpenAI) expose
        // an OpenAI-compatible chat completions API, so we use a single factory.
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Provider {Provider} not configured, falling back to {Fallback}")]
    private static partial void LogFallback(ILogger logger, string provider, string fallback);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating chat client for {Provider} at {Endpoint} with model {Model}")]
    private static partial void LogCreatingClient(ILogger logger, string provider, Uri endpoint, string model);
}

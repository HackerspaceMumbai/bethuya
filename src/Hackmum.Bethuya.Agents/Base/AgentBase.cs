using Hackmum.Bethuya.AI.Routing;
using Hackmum.Bethuya.Core.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Base;

/// <summary>
/// Base class for all Bethuya agents. Handles AI routing and logging.
/// Subclasses implement BuildPrompt and ParseResponse.
/// </summary>
public abstract partial class AgentBase<TRequest, TResponse>(
    IAIRouter router,
    ILogger logger) : IAgent<TRequest, TResponse>
    where TRequest : IAgentRequest
    where TResponse : IAgentResponse
{
    protected ILogger Logger => logger;

    public abstract string Name { get; }

    public async Task<TResponse> DraftAsync(TRequest request, CancellationToken ct = default)
    {
        LogDrafting(logger, Name, request.RequestedBy ?? "system", request.Sensitivity);

        var sensitivity = MapSensitivity(request.Sensitivity);
        var client = router.GetChatClient(sensitivity);
        var prompt = BuildPrompt(request);

        var response = await client.GetResponseAsync(prompt, cancellationToken: ct);

        var providerName = router.GetProviderName(sensitivity);
        LogDraftComplete(logger, Name, providerName);

        return ParseResponse(response, request);
    }

    /// <summary>Build the prompt messages for the AI provider.</summary>
    protected abstract IList<ChatMessage> BuildPrompt(TRequest request);

    /// <summary>Parse the AI response into a typed agent response.</summary>
    protected abstract TResponse ParseResponse(ChatResponse response, TRequest request);

    /// <summary>
    /// Maps Core.Enums.DataSensitivity to AI.Routing.DataSensitivity.
    /// Both enums have the same members but live in different namespaces.
    /// </summary>
    private static DataSensitivity MapSensitivity(Core.Enums.DataSensitivity sensitivity) =>
        sensitivity switch
        {
            Core.Enums.DataSensitivity.Sensitive => DataSensitivity.Sensitive,
            Core.Enums.DataSensitivity.NonSensitive => DataSensitivity.NonSensitive,
            Core.Enums.DataSensitivity.Public => DataSensitivity.Public,
            _ => DataSensitivity.NonSensitive
        };

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[{Agent}] Drafting for request by {User} (sensitivity: {Sensitivity})")]
    private static partial void LogDrafting(ILogger logger, string agent, string user, Core.Enums.DataSensitivity sensitivity);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[{Agent}] Draft complete. Provider: {Provider}")]
    private static partial void LogDraftComplete(ILogger logger, string agent, string provider);
}

using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.AI.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Implementations;

/// <summary>
/// Scout Agent (stub) — gathers external context for planning.
/// </summary>
public sealed class ScoutAgent(IAIRouter router, ILogger<ScoutAgent> logger)
    : AgentBase<ScoutRequest, ScoutResponse>(router, logger)
{
    public override string Name => "Scout";

    protected override IList<ChatMessage> BuildPrompt(ScoutRequest request)
    {
        return
        [
            new ChatMessage(ChatRole.System, "You are the Scout Agent for Bethuya."),
            new ChatMessage(ChatRole.User, $"Query type: {request.QueryType}")
        ];
    }

    protected override ScoutResponse ParseResponse(ChatResponse response, ScoutRequest request)
    {
        return new ScoutResponse(
            QueryType: request.QueryType,
            Data: new Dictionary<string, object>());
    }
}

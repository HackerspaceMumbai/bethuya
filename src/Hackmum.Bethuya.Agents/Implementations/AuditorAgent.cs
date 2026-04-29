using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.AI.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Implementations;

/// <summary>
/// Auditor Agent (stub) — maintains append-only audit log.
/// </summary>
public sealed class AuditorAgent(IAIRouter router, ILogger<AuditorAgent> logger)
    : AgentBase<AuditorRequest, AuditorResponse>(router, logger)
{
    public override string Name => "Auditor";

    protected override IList<ChatMessage> BuildPrompt(AuditorRequest request)
    {
        return
        [
            new ChatMessage(ChatRole.System, "You are the Auditor Agent for Bethuya."),
            new ChatMessage(ChatRole.User, $"Action: {request.Action}")
        ];
    }

    protected override AuditorResponse ParseResponse(ChatResponse response, AuditorRequest request)
    {
        return new AuditorResponse(
            Action: request.Action,
            Result: "Success");
    }
}

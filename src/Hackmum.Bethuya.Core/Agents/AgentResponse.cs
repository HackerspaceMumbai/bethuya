namespace Hackmum.Bethuya.Core.Agents;

public abstract record AgentResponse(
    bool RequiresHumanApproval = true,
    string? AgentReasoning = null) : IAgentResponse
{
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

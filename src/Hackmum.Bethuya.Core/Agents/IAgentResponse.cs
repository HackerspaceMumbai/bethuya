namespace Hackmum.Bethuya.Core.Agents;

public interface IAgentResponse
{
    bool RequiresHumanApproval { get; }
    string? AgentReasoning { get; }
    DateTimeOffset GeneratedAt { get; }
}

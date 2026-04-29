using Hackmum.Bethuya.Core.Agents;
using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Agents.Contracts;

/// <summary>
/// Request to spawn a domain agent (Planner, Curator, Reporter).
/// </summary>
/// <param name="EventId">Event identifier.</param>
/// <param name="AgentType">Type of agent to spawn (Planner, Curator, Reporter).</param>
/// <param name="Context">Optional context for the agent.</param>
public sealed record SpawnAgentRequest(
    Guid EventId,
    string AgentType,
    string? Context) : IAgentRequest
{
    public string AgentName => "Orchestrator";
    public DataSensitivity Sensitivity => DataSensitivity.NonSensitive;
    public string? RequestedBy => null;
}

/// <summary>
/// Response from spawning an agent.
/// </summary>
/// <param name="AgentName">Name of the spawned agent.</param>
/// <param name="EventId">Event identifier.</param>
/// <param name="Status">Spawn status.</param>
public sealed record SpawnAgentResponse(
    string AgentName,
    Guid EventId,
    string Status) : IAgentResponse
{
    public bool RequiresHumanApproval => false;
    public string? AgentReasoning => null;
    public DateTimeOffset GeneratedAt => DateTimeOffset.UtcNow;
}

/// <summary>
/// Request to advance workflow to next phase.
/// </summary>
/// <param name="EventId">Event identifier.</param>
/// <param name="TargetPhase">Target workflow phase.</param>
public sealed record AdvanceWorkflowRequest(
    Guid EventId,
    WorkflowPhase TargetPhase) : IAgentRequest
{
    public string AgentName => "Orchestrator";
    public DataSensitivity Sensitivity => DataSensitivity.NonSensitive;
    public string? RequestedBy => null;
}

/// <summary>
/// Response from workflow advancement.
/// </summary>
/// <param name="EventId">Event identifier.</param>
/// <param name="CurrentPhase">Current workflow phase after transition.</param>
/// <param name="Status">Workflow status.</param>
/// <param name="RequiresApproval">Whether this phase requires human approval.</param>
public sealed record AdvanceWorkflowResponse(
    Guid EventId,
    WorkflowPhase CurrentPhase,
    WorkflowStatus Status,
    bool RequiresApproval) : IAgentResponse
{
    public bool RequiresHumanApproval => RequiresApproval;
    public string? AgentReasoning => null;
    public DateTimeOffset GeneratedAt => DateTimeOffset.UtcNow;
}

/// <summary>
/// Request to orchestrate complete event workflow.
/// </summary>
/// <param name="EventId">Event identifier.</param>
/// <param name="WorkflowType">Type of workflow to execute.</param>
public sealed record OrchestratorRequest(
    Guid EventId,
    string WorkflowType) : IAgentRequest
{
    public string AgentName => "Orchestrator";
    public DataSensitivity Sensitivity => DataSensitivity.NonSensitive;
    public string? RequestedBy => null;
}

/// <summary>
/// Response from orchestrator.
/// </summary>
/// <param name="EventId">Event identifier.</param>
/// <param name="CurrentPhase">Current workflow phase.</param>
/// <param name="Status">Workflow status.</param>
/// <param name="Message">Human-readable status message.</param>
public sealed record OrchestratorResponse(
    Guid EventId,
    WorkflowPhase CurrentPhase,
    WorkflowStatus Status,
    string Message) : IAgentResponse
{
    public bool RequiresHumanApproval => Status == WorkflowStatus.PendingApproval;
    public string? AgentReasoning => Message;
    public DateTimeOffset GeneratedAt => DateTimeOffset.UtcNow;
}

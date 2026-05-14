using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Backend.Agents;

/// <summary>
/// Stable abstraction for invoking planner agents (Responses now, A2A later).
/// </summary>
public interface IAgentInvoker
{
    Task<PlannerInvocationResult> InvokePlannerAsync(
        PlannerInvocationInput input,
        string conversationId,
        string workItemId,
        string? traceParent,
        string? correlationId,
        CancellationToken ct = default);
}

/// <summary>
/// Input payload sent to planner invokers.
/// </summary>
public sealed record PlannerInvocationInput(
    Guid EventId,
    string Title,
    string Date,
    string Timezone,
    string? Location,
    int Capacity,
    string? Constraints,
    string? PriorEventsContext,
    string? HumanEditedMarkdown);

/// <summary>
/// Normalized planner output returned by invokers.
/// </summary>
public sealed record PlannerInvocationResult(
    string MarkdownAgenda,
    PlanningAgendaJson AgendaJson,
    string? ResponseId,
    string AgentName,
    string? AgentVersionTag);


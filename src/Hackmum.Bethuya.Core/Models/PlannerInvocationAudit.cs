namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Immutable audit trail for every planner invocation attempt/result.
/// </summary>
public sealed class PlannerInvocationAudit
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid PlanningCycleId { get; init; }
    public Guid EventId { get; init; }
    public required string WorkItemId { get; init; }
    public required string ConversationId { get; init; }
    public required string InputHash { get; init; }
    public string? ResponseId { get; init; }
    public string? AgentName { get; init; }
    public string? AgentVersionTag { get; init; }
    public required string MarkdownAgenda { get; init; }
    public required string AgendaJson { get; init; }
    public string? TraceParent { get; init; }
    public string? CorrelationId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public PlanningCycle? PlanningCycle { get; init; }
}


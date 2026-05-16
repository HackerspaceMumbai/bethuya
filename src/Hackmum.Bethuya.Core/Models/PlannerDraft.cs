namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Versioned draft output captured from Planner for a planning cycle.
/// </summary>
public sealed class PlannerDraft
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid PlanningCycleId { get; init; }
    public required string WorkItemId { get; init; }
    public required string InputHash { get; init; }
    public required string MarkdownAgenda { get; set; }
    public required string AgendaJson { get; set; }
    public string? HumanEditedMarkdown { get; set; }
    public string? HumanDiff { get; set; }
    public bool IsApproved { get; set; }
    public string? ApprovalDecision { get; set; }
    public string? ResponseId { get; set; }
    public string? AgentName { get; set; }
    public string? AgentVersionTag { get; set; }
    public string? TraceParent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public PlanningCycle? PlanningCycle { get; init; }
}


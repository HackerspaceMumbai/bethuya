namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Immutable published schedule snapshot captured when a planning cycle is finalized.
/// </summary>
public sealed class PublishedScheduleSnapshot
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public Guid PlanningCycleId { get; init; }
    public Guid PlannerDraftId { get; init; }
    public required string MarkdownAgenda { get; init; }
    public required string AgendaJson { get; init; }
    public required string PublishedBy { get; init; }
    public string? AgentVersionTag { get; init; }
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
}


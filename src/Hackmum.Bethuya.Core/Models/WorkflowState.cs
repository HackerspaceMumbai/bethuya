using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Tracks workflow state machine for event orchestration (planning → curation → reporting).
/// Redundant with Redis for durability.
/// </summary>
public sealed record WorkflowState
{
    public Guid EventId { get; set; }
    public WorkflowPhase CurrentPhase { get; set; }
    public WorkflowStatus Status { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

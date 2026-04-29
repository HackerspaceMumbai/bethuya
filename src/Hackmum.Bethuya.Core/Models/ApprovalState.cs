using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Tracks human approval state for agent outputs (agenda, curation, report).
/// </summary>
public sealed class ApprovalState
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EventId { get; init; }
    public string WorkflowPhase { get; init; } = string.Empty;
    public ApprovalStatus Status { get; init; }
    public string? Approver { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? Edits { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

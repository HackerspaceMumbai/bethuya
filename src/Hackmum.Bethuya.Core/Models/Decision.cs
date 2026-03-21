using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Human-in-the-loop decision with full audit trail.
/// </summary>
public sealed class Decision
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string EntityType { get; init; }
    public Guid EntityId { get; init; }
    public DecisionType Type { get; set; }
    public DecisionStatus Status { get; set; } = DecisionStatus.Pending;
    public required string DecidedBy { get; init; }
    public string? Reason { get; set; }
    public string? Diff { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

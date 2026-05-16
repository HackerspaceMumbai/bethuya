using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Planning thread for a single event schedule cycle. One Foundry conversation is scoped to one cycle.
/// </summary>
public sealed class PlanningCycle
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public required string ConversationId { get; init; }
    public PlanningCycleStatus Status { get; set; } = PlanningCycleStatus.Drafting;
    public Guid? ActiveDraftId { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }

    public Event? Event { get; init; }
    public List<PlannerDraft> Drafts { get; init; } = [];
    public List<PlannerInvocationAudit> InvocationAudits { get; init; } = [];
}


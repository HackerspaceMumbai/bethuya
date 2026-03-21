using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

public sealed class EventReport
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public string? Summary { get; set; }
    public List<string> Highlights { get; set; } = [];
    public List<string> ActionItems { get; set; } = [];
    public ProposalStatus Status { get; set; } = ProposalStatus.Draft;
    public string? DraftedByAgent { get; init; }
    public string? EditedBy { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Event? Event { get; init; }
}

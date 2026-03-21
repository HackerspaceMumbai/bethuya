using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

public sealed class WaitlistProposal
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public List<Guid> WaitlistedRegistrationIds { get; set; } = [];
    public string? Reason { get; set; }
    public ProposalStatus Status { get; set; } = ProposalStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public Event? Event { get; init; }
}

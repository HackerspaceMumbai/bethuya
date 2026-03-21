using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

public sealed class AttendanceProposal
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public List<Guid> ProposedAttendeeIds { get; set; } = [];
    public CurationInsights? Insights { get; set; }
    public FairnessBudget? Budget { get; set; }
    public ProposalStatus Status { get; set; } = ProposalStatus.Draft;
    public string? ReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Event? Event { get; init; }
}

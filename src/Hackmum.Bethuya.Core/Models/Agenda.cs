using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

public sealed class Agenda
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public List<AgendaSession> Sessions { get; set; } = [];
    public AgendaStatus Status { get; set; } = AgendaStatus.Draft;
    public string? CreatedByAgent { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Event? Event { get; init; }
}

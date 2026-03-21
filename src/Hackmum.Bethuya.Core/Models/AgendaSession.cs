namespace Hackmum.Bethuya.Core.Models;

public sealed class AgendaSession
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AgendaId { get; init; }
    public required string Title { get; set; }
    public string? Speaker { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
}

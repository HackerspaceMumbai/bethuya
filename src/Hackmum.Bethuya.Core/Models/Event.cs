using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

public sealed class Event
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Title { get; set; }
    public string? Description { get; set; }
    public EventType Type { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public int Capacity { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string? Location { get; set; }
    public string? Hashtag { get; set; }
    public string? CoverImageUrl { get; set; }
    public required string CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Registration> Registrations { get; init; } = [];
    public Agenda? Agenda { get; set; }
}

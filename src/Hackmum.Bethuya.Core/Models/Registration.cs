using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

public sealed class Registration
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public string? Bio { get; set; }
    public List<string> Interests { get; set; } = [];
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Event? Event { get; init; }
}

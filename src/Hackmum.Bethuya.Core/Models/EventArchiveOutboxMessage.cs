namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Durable request to project a public event snapshot to an archive destination.
/// </summary>
public sealed class EventArchiveOutboxMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public required string Destination { get; init; }
    public required string FolderPath { get; init; }
    public required string ReadmeMarkdown { get; init; }
    public required string MetadataJson { get; init; }
    public required string IdempotencyKey { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public int AttemptCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}

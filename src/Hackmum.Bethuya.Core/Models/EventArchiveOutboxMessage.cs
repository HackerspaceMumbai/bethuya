using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Durable request to project a public event snapshot to an archive destination.
/// </summary>
public sealed class EventArchiveOutboxMessage
{
    /// <summary>Gets the durable identifier for this outbox message.</summary>
    public EventArchiveOutboxMessageId Id { get; init; } =
        EventArchiveOutboxMessageId.From(Guid.CreateVersion7());

    /// <summary>Gets the event whose public archive projection is being published.</summary>
    public EventId EventId { get; init; }

    /// <summary>Gets the archive destination identifier.</summary>
    public required string Destination { get; init; }

    /// <summary>Gets the destination folder path for the event projection.</summary>
    public required string FolderPath { get; init; }

    /// <summary>Gets the rendered README content.</summary>
    public required string ReadmeMarkdown { get; init; }

    /// <summary>Gets the serialized event metadata content.</summary>
    public required string MetadataJson { get; init; }

    /// <summary>Gets the idempotency key for this projection.</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Gets the time when this outbox message was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the earliest time when this message may be processed.</summary>
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the number of processing attempts.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Gets or sets the lease expiration time for the current processor.</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>Gets or sets the time when this message was successfully processed.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Gets or sets the most recent processing error.</summary>
    public string? LastError { get; set; }
}

using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Unified participation ledger row used to project member history across connectors.
/// </summary>
public sealed class ParticipationLedgerEntry
{
    /// <summary>True only when a trusted ingestion source has verified this participation.</summary>
    public bool IsVerified { get; init; }
    /// <summary>Structured target type; never inferred from evidence text.</summary>
    public string? TargetKind { get; init; }
    /// <summary>Stable source target key, scoped to connector for external entities.</summary>
    public string? TargetKey { get; init; }
    /// <summary>Source-supplied display label for the target.</summary>
    public string? TargetLabel { get; init; }
    /// <summary>
    /// Unique ledger entry identifier.
    /// </summary>
    public ParticipationLedgerEntryId Id { get; init; } = ParticipationLedgerEntryId.From(Guid.CreateVersion7());

    /// <summary>
    /// Owning community member identifier.
    /// </summary>
    public CommunityMemberId CommunityMemberId { get; init; }

    /// <summary>
    /// Source connector that produced this signal.
    /// </summary>
    public ParticipationConnectorKind Connector { get; init; }

    /// <summary>
    /// Connector-specific member key used for provenance and reconciliation.
    /// </summary>
    public required string ExternalMemberKey { get; init; }

    /// <summary>
    /// Optional internal event identifier mapped during ingestion.
    /// </summary>
    public Guid? EventId { get; init; }

    /// <summary>
    /// Optional external platform event identifier.
    /// </summary>
    public string? ExternalEventId { get; init; }

    /// <summary>
    /// Optional external source record identifier.
    /// </summary>
    public string? ExternalRecordId { get; init; }

    /// <summary>
    /// Canonical signal kind.
    /// </summary>
    public ParticipationActivityKind Activity { get; init; }

    /// <summary>
    /// Human-readable evidence summary shown in timeline projections.
    /// </summary>
    public required string Evidence { get; init; }

    /// <summary>
    /// Idempotency key scoped to connector provenance.
    /// </summary>
    public required string ProvenanceKey { get; init; }

    /// <summary>
    /// Optional correlation token from source webhooks/sync runs.
    /// </summary>
    public string? SourceCorrelationId { get; init; }

    /// <summary>
    /// Source occurrence timestamp.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Persistence timestamp for this entry.
    /// </summary>
    public DateTimeOffset IngestedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Navigation to owning member.
    /// </summary>
    public CommunityMember? CommunityMember { get; init; }
}

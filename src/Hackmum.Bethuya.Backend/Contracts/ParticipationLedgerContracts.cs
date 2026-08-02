using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Backend.Contracts;

/// <summary>
/// Batched participation ingestion request payload.
/// </summary>
public sealed record UpsertParticipationEntriesRequest(
    IReadOnlyList<ParticipationEntryWriteRequest> Entries);

/// <summary>
/// One normalized participation entry submitted to the unified ledger.
/// </summary>
public sealed record ParticipationEntryWriteRequest(
    ParticipationConnectorKind Connector,
    string ExternalMemberKey,
    ParticipationActivityKind Activity,
    DateTimeOffset OccurredAt,
    string Evidence,
    string ProvenanceKey,
    Guid? EventId = null,
    string? ExternalEventId = null,
    string? ExternalRecordId = null,
    string? SourceCorrelationId = null);

/// <summary>
/// Result returned after writing participation entries.
/// </summary>
public sealed record ParticipationEntryWriteResult(
    int ReceivedCount,
    int StoredCount,
    int DuplicateCount);

/// <summary>
/// Member timeline projection backed by the participation ledger.
/// </summary>
public sealed record MemberParticipationTimelineResponse(
    IReadOnlyList<MemberParticipationTimelineEntryResponse> Entries);

/// <summary>
/// One item in the member participation timeline projection.
/// </summary>
public sealed record MemberParticipationTimelineEntryResponse(
    Guid EntryId,
    string Connector,
    string Activity,
    DateTimeOffset OccurredAt,
    string Evidence,
    string ProvenanceKey,
    Guid? EventId,
    string? EventTitle);

using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Canonical participation ingestion payload emitted by connector adapters and webhooks.
/// </summary>
public sealed record NormalizedParticipationEntry(
    ParticipationConnectorKind Connector,
    string ExternalMemberKey,
    ParticipationActivityKind Activity,
    DateTimeOffset OccurredAt,
    string Evidence,
    string ProvenanceKey,
    EventId? EventId = null,
    string? ExternalEventId = null,
    string? ExternalRecordId = null,
    string? SourceCorrelationId = null);

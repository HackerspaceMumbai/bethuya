using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Connector contract for external participation sources and webhook ingestion.
/// </summary>
public interface IParticipationConnector
{
    /// <summary>
    /// Connector identity.
    /// </summary>
    ParticipationConnectorKind ConnectorKind { get; }

    /// <summary>
    /// Pulls normalized participation signals via incremental sync.
    /// </summary>
    Task<IReadOnlyList<NormalizedParticipationEntry>> SyncAsync(ParticipationSyncRequest request, CancellationToken ct = default);

    /// <summary>
    /// Parses and normalizes one webhook payload from the external source.
    /// </summary>
    Task<IReadOnlyList<NormalizedParticipationEntry>> ParseWebhookAsync(ParticipationWebhookEnvelope webhook, CancellationToken ct = default);
}

/// <summary>
/// Incremental connector sync request envelope.
/// </summary>
/// <param name="CommunitySlug">Target community tenant slug.</param>
/// <param name="Since">Optional lower watermark for incremental sync.</param>
/// <param name="Cursor">Optional opaque checkpoint token.</param>
/// <param name="BatchSize">Optional source batch size hint.</param>
public sealed record ParticipationSyncRequest(
    string CommunitySlug,
    DateTimeOffset? Since = null,
    string? Cursor = null,
    int? BatchSize = null);

/// <summary>
/// Source webhook envelope passed to connector adapters.
/// </summary>
/// <param name="Connector">Connector expected to parse this payload.</param>
/// <param name="ReceivedAt">When the webhook was received.</param>
/// <param name="Signature">Provider signature/token for authenticity checks.</param>
/// <param name="PayloadJson">Raw JSON payload.</param>
/// <param name="EventType">Optional source event type string.</param>
/// <param name="DeliveryId">Optional source delivery id for traceability.</param>
public sealed record ParticipationWebhookEnvelope(
    ParticipationConnectorKind Connector,
    DateTimeOffset ReceivedAt,
    string Signature,
    string PayloadJson,
    string? EventType = null,
    string? DeliveryId = null);

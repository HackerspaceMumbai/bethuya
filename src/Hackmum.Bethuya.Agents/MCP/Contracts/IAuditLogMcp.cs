using Refit;

namespace Hackmum.Bethuya.Agents.MCP.Contracts;

/// <summary>
/// MCP tool for audit log operations.
/// Provides append-only audit entry storage for compliance and analytics.
/// All entries are sanitized (no PII).
/// </summary>
public interface IAuditLogMcp
{
    /// <summary>Append a new audit entry to the immutable log.</summary>
    [Post("/api/audit-log/entries")]
    Task AppendAuditEntryAsync(
        [Body] AuditLogEntry entry,
        CancellationToken ct = default);

    /// <summary>Query audit entries by event and optional filters.</summary>
    [Get("/api/audit-log/entries")]
    Task<List<AuditLogEntry>> QueryAuditEntriesAsync(
        [Query] Guid eventId,
        [Query(CollectionFormat.Multi)] List<string>? actions = null,
        [Query] DateTimeOffset? startDate = null,
        [Query] DateTimeOffset? endDate = null,
        CancellationToken ct = default);
}

/// <summary>
/// Sanitized audit log entry (no PII, no personal identifiers).
/// </summary>
public sealed record AuditLogEntry(
    Guid Id,
    Guid EventId,
    string AgentName,
    string Action,
    DateTimeOffset Timestamp,
    string Reason,
    int AffectedCount,
    string OrganizerEmail,
    Dictionary<string, string> Metadata)
{
    /// <summary>
    /// Factory method to create a new audit entry with auto-generated ID.
    /// </summary>
    public static AuditLogEntry Create(
        Guid eventId,
        string agentName,
        string action,
        string reason,
        int affectedCount,
        string organizerEmail,
        Dictionary<string, string>? metadata = null)
    {
        return new AuditLogEntry(
            Id: Guid.CreateVersion7(),
            EventId: eventId,
            AgentName: agentName,
            Action: action,
            Timestamp: DateTimeOffset.UtcNow,
            Reason: reason,
            AffectedCount: affectedCount,
            OrganizerEmail: organizerEmail,
            Metadata: metadata ?? []);
    }
}

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Append-only audit log for all agent actions (curation, approval, workflow transitions).
/// Retained for 2 years for compliance and analytics.
/// </summary>
public sealed class AuditLog
{
    public long Id { get; init; }
    public Guid EventId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Actor { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

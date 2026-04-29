namespace Bethuya.Hybrid.Shared.Models;

/// <summary>
/// PII-safe curation proposal containing only aggregate statistics.
/// NO individual attendee details, names, or emails.
/// </summary>
public sealed record CurationProposalDto(
    Guid EventId,
    List<Guid> AcceptedIds,
    List<Guid> WaitlistIds,
    string CurationRationale,
    FairnessMetricsDto? FairnessScore);

/// <summary>
/// Aggregate fairness metrics (no PII).
/// Shows distribution percentages and alignment scores, never individual attendee data.
/// </summary>
public sealed record FairnessMetricsDto(
    decimal OverallScore,
    List<DeiMetricDto> DeiMetrics,
    Dictionary<string, int> ThemeDistribution);

/// <summary>
/// DEI category representation metrics (percentage distribution only).
/// </summary>
public sealed record DeiMetricDto(
    string Category,
    int SelectedPercentage,
    int WaitlistPercentage,
    int TargetPercentage);

/// <summary>
/// Request to approve a curation proposal.
/// </summary>
public sealed record CurationApprovalRequest(
    Guid EventId,
    string ApprovedBy,
    string Status,
    DateTimeOffset Timestamp);

/// <summary>
/// Request to reject a curation proposal and request changes.
/// </summary>
public sealed record CurationRejectionRequest(
    Guid EventId,
    string RejectedBy,
    string Reason,
    DateTimeOffset Timestamp);

/// <summary>
/// Audit log entry for curation decisions (no attendee PII).
/// </summary>
public sealed record CurationAuditEntryDto(
    Guid EventId,
    DateTimeOffset Timestamp,
    string Organizer,
    string Action,
    int AffectedCount,
    int AcceptedCount,
    int WaitlistCount,
    decimal FairnessScore,
    string Reason,
    Dictionary<string, string> Metadata);

/// <summary>
/// Audit log response (collection of curation decisions with aggregate stats only).
/// </summary>
public sealed record CurationAuditLogDto(
    Guid EventId,
    string EventName,
    List<CurationAuditEntryDto> Entries);

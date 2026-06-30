namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// Immutable, append-only record of a single security-relevant authorization decision. By construction
/// it carries <b>only non-PII fields</b> — there is no slot for Email, Name, government-id, or any raw
/// attendee data. The caller is identified solely by <see cref="SubjectHash"/> (see
/// <see cref="Observability.SubjectHash"/>), so the record can flow to logs, metrics, and traces that
/// leave the trust boundary without leaking PII.
/// </summary>
public sealed record AuthorizationAuditEvent
{
    /// <summary>The decision outcome (allow / deny / role-bypass grant).</summary>
    public required AuthorizationDecision Decision { get; init; }

    /// <summary>
    /// The governing policy, a <see cref="BethuyaPolicyNames"/> constant — never a string literal.
    /// </summary>
    public required string PolicyName { get; init; }

    /// <summary>The coarse resource category (a <see cref="BethuyaAuditResourceTypes"/> value).</summary>
    public required string ResourceType { get; init; }

    /// <summary>The specific resource id (e.g. registration/event GUID), or <see langword="null"/>.</summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// The non-PII subject hash of the caller (<see cref="Observability.SubjectHash.Compute"/>), or
    /// <see langword="null"/> when no identity is resolvable (e.g. an anonymous request being denied).
    /// </summary>
    public string? SubjectHash { get; init; }

    /// <summary>The request route/path the decision was made on, or <see langword="null"/>.</summary>
    public string? Route { get; init; }

    /// <summary>The HTTP status the decision maps to (e.g. 200/201/204/401/403/404), or <see langword="null"/>.</summary>
    public int? OutcomeStatusCode { get; init; }

    /// <summary>When the decision was recorded (UTC).</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

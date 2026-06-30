namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// Sink for authorization audit records. The default implementation
/// (<see cref="AuthorizationAuditor"/>) fans a single <see cref="AuthorizationAuditEvent"/> out to a
/// structured log, an OpenTelemetry metric, and a trace span — all carrying only non-PII fields. Tests
/// substitute a recording fake to assert exactly-once emission.
/// </summary>
public interface IAuthorizationAuditor
{
    /// <summary>Records one security-relevant authorization decision.</summary>
    void Record(AuthorizationAuditEvent auditEvent);
}

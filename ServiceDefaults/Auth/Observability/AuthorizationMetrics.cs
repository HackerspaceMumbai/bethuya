using System.Diagnostics.Metrics;

namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// Owns the authorization <see cref="Meter"/> and its instruments. Registered as a singleton and wired
/// into OpenTelemetry via <c>metrics.AddMeter(<see cref="MeterName"/>)</c> so authorization outcomes
/// surface on the Aspire dashboard. All dimensions are non-PII and low-cardinality.
/// </summary>
public sealed class AuthorizationMetrics : IDisposable
{
    /// <summary>The OpenTelemetry meter name; registered in <c>ConfigureOpenTelemetry</c>.</summary>
    public const string MeterName = "Bethuya.Authorization";

    private readonly Meter _meter;
    private readonly Counter<long> _decisions;
    private readonly Counter<long> _outcomes;

    /// <summary>The underlying meter instance. Exposed to tests for instance-scoped listening.</summary>
    internal Meter Meter => _meter;

    /// <summary>Creates the meter and its instruments.</summary>
    public AuthorizationMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);

        _decisions = _meter.CreateCounter<long>(
            "authorization.decisions",
            unit: "{decision}",
            description: "Count of security-relevant authorization decisions by policy, decision, and resource type.");

        _outcomes = _meter.CreateCounter<long>(
            "authorization.outcomes",
            unit: "{outcome}",
            description: "Count of authorization middleware outcomes (401 vs 403) by route group.");
    }

    /// <summary>
    /// Records one resource/ownership authorization decision. Increments
    /// <c>authorization.decisions</c> tagged with policy, decision, and resource type.
    /// </summary>
    public void RecordDecision(AuthorizationAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        _decisions.Add(
            1,
            new KeyValuePair<string, object?>("policy", auditEvent.PolicyName),
            new KeyValuePair<string, object?>("decision", auditEvent.Decision.ToString()),
            new KeyValuePair<string, object?>("resource_type", auditEvent.ResourceType));
    }

    /// <summary>
    /// Records one authorization middleware outcome (a challenge/forbid). Increments
    /// <c>authorization.outcomes</c> tagged with the route group and HTTP status code (401/403).
    /// </summary>
    public void RecordOutcome(string routeGroup, int statusCode)
    {
        _outcomes.Add(
            1,
            new KeyValuePair<string, object?>("route_group", routeGroup),
            new KeyValuePair<string, object?>("status_code", statusCode));
    }

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}

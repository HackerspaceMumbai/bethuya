using System.Diagnostics;

namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// Owns the authorization <see cref="ActivitySource"/>. Each recorded decision starts a short internal
/// span (nested under the current request span) carrying non-PII attributes, so the Aspire dashboard's
/// trace view shows why access was granted/denied. Wired into OpenTelemetry via
/// <c>tracing.AddSource(<see cref="SourceName"/>)</c>.
/// </summary>
public static class AuthorizationActivity
{
    /// <summary>The OpenTelemetry activity source name; registered in <c>ConfigureOpenTelemetry</c>.</summary>
    public const string SourceName = "Bethuya.Authorization";

    /// <summary>The span name used for authorization-decision activities.</summary>
    public const string DecisionActivityName = "authorization.decision";

    /// <summary>The shared activity source. Disposal is owned by the process lifetime.</summary>
    public static readonly ActivitySource Source = new(SourceName);
}

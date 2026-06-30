using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// Default <see cref="IAuthorizationAuditor"/>. Fans each decision out to three observability sinks that
/// all flow through the existing OpenTelemetry pipeline to the Aspire dashboard:
/// <list type="number">
///   <item>a structured <see cref="ILogger"/> record (Warning for denials, Information otherwise);</item>
///   <item>the <see cref="AuthorizationMetrics"/> decision counter;</item>
///   <item>a short internal trace span (<see cref="AuthorizationActivity"/>) with non-PII tags.</item>
/// </list>
/// Every field written is non-PII by construction (see <see cref="AuthorizationAuditEvent"/>): the caller
/// is identified only by <see cref="AuthorizationAuditEvent.SubjectHash"/> — never Email, Name, or
/// government-id.
/// </summary>
public sealed partial class AuthorizationAuditor(
    ILogger<AuthorizationAuditor> logger,
    AuthorizationMetrics metrics) : IAuthorizationAuditor
{
    /// <inheritdoc />
    public void Record(AuthorizationAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        metrics.RecordDecision(auditEvent);
        EmitLog(auditEvent);
        EmitSpan(auditEvent);
    }

    private void EmitLog(AuthorizationAuditEvent auditEvent)
    {
        if (auditEvent.Decision is AuthorizationDecision.Deny)
        {
            LogDenied(
                logger,
                auditEvent.PolicyName,
                auditEvent.ResourceType,
                auditEvent.ResourceId,
                auditEvent.SubjectHash,
                auditEvent.Route,
                auditEvent.OutcomeStatusCode);
        }
        else
        {
            var decision = auditEvent.Decision.ToString();
            LogGranted(
                logger,
                decision,
                auditEvent.PolicyName,
                auditEvent.ResourceType,
                auditEvent.ResourceId,
                auditEvent.SubjectHash,
                auditEvent.Route,
                auditEvent.OutcomeStatusCode);
        }
    }

    private static void EmitSpan(AuthorizationAuditEvent auditEvent)
    {
        using var activity = AuthorizationActivity.Source.StartActivity(
            AuthorizationActivity.DecisionActivityName,
            ActivityKind.Internal);

        if (activity is null)
        {
            return;
        }

        activity.SetTag("authorization.decision", auditEvent.Decision.ToString());
        activity.SetTag("authorization.policy", auditEvent.PolicyName);
        activity.SetTag("authorization.resource_type", auditEvent.ResourceType);
        activity.SetTag("authorization.resource_id", auditEvent.ResourceId);
        activity.SetTag("authorization.subject_hash", auditEvent.SubjectHash);
        activity.SetTag("authorization.route", auditEvent.Route);
        activity.SetTag("authorization.outcome_status_code", auditEvent.OutcomeStatusCode);
    }

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Warning,
        Message = "Authorization denied: policy={Policy} resourceType={ResourceType} resourceId={ResourceId} subjectHash={SubjectHash} route={Route} status={StatusCode}")]
    private static partial void LogDenied(
        ILogger logger,
        string policy,
        string resourceType,
        string? resourceId,
        string? subjectHash,
        string? route,
        int? statusCode);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Information,
        Message = "Authorization {Decision}: policy={Policy} resourceType={ResourceType} resourceId={ResourceId} subjectHash={SubjectHash} route={Route} status={StatusCode}")]
    private static partial void LogGranted(
        ILogger logger,
        string decision,
        string policy,
        string resourceType,
        string? resourceId,
        string? subjectHash,
        string? route,
        int? statusCode);
}

using System.Collections.Concurrent;
using ServiceDefaults.Auth;
using ServiceDefaults.Auth.Observability;

namespace Hackmum.Bethuya.Tests.Auth.Observability;

/// <summary>
/// PR5: the default <see cref="AuthorizationAuditor"/> fans one decision out to three sinks (log, metric,
/// span). These tests assert each sink fires exactly once, that denials log at Warning and grants at
/// Information, and that the span carries only non-PII attributes — all via the real framework listeners
/// the OpenTelemetry pipeline uses.
/// </summary>
public sealed class AuthorizationAuditorTests
{
    private static AuthorizationAuditEvent DenyEvent() => new()
    {
        Decision = AuthorizationDecision.Deny,
        PolicyName = BethuyaPolicyNames.ResourceOwner,
        ResourceType = BethuyaAuditResourceTypes.Registration,
        SubjectHash = "abc123def456",
        Route = "/api/attendee/registrations/123",
        OutcomeStatusCode = 404
    };

    [Test]
    public async Task Record_Denial_EmitsOneWarningLog_AndIncrementsMetric()
    {
        var (metrics, provider) = MetricsTestHarness.CreateMetrics();
        await using var _ = provider;
        using var __ = metrics;

        var sink = new RecordingLogSink();
        var auditor = new AuthorizationAuditor(new RecordingLogger<AuthorizationAuditor>(sink), metrics);

        var measurements = new ConcurrentQueue<MetricsTestHarness.Measurement>();
        using var listener = MetricsTestHarness.ListenToAuthorizationMeter(measurements, metrics);

        auditor.Record(DenyEvent());

        var entry = sink.Entries.Single();
        await Assert.That(entry.Level).IsEqualTo(Microsoft.Extensions.Logging.LogLevel.Warning);
        await Assert.That(measurements.Count(m => m.Instrument == "authorization.decisions")).IsEqualTo(1);
    }

    [Test]
    public async Task Record_Grant_EmitsOneInformationLog()
    {
        var (metrics, provider) = MetricsTestHarness.CreateMetrics();
        await using var _ = provider;
        using var __ = metrics;

        var sink = new RecordingLogSink();
        var auditor = new AuthorizationAuditor(new RecordingLogger<AuthorizationAuditor>(sink), metrics);

        auditor.Record(DenyEvent() with { Decision = AuthorizationDecision.Bypass, OutcomeStatusCode = 200 });

        var entry = sink.Entries.Single();
        await Assert.That(entry.Level).IsEqualTo(Microsoft.Extensions.Logging.LogLevel.Information);
    }

    [Test]
    public async Task Record_StartsOneSpan_WithNonPiiTags()
    {
        var (metrics, provider) = MetricsTestHarness.CreateMetrics();
        await using var _ = provider;
        using var __ = metrics;

        var sink = new RecordingLogSink();
        var auditor = new AuthorizationAuditor(new RecordingLogger<AuthorizationAuditor>(sink), metrics);

        var (activityListener, activities) = ActivityTestHarness.Listen();
        using var __l = activityListener;

        // The ActivitySource is process-global, so concurrent tests share it; filter captured spans by a
        // unique subject-hash sentinel so this assertion only sees the span this test produced.
        var subjectHash = $"hash-{Guid.NewGuid():N}";
        auditor.Record(DenyEvent() with { SubjectHash = subjectHash });

        var activity = activities.Single(a => (string?)a.GetTagItem("authorization.subject_hash") == subjectHash);
        await Assert.That(activity.OperationName).IsEqualTo(AuthorizationActivity.DecisionActivityName);
        await Assert.That(activity.GetTagItem("authorization.decision")).IsEqualTo(nameof(AuthorizationDecision.Deny));
        await Assert.That(activity.GetTagItem("authorization.policy")).IsEqualTo(BethuyaPolicyNames.ResourceOwner);
        await Assert.That(activity.GetTagItem("authorization.resource_type")).IsEqualTo(BethuyaAuditResourceTypes.Registration);
        await Assert.That(activity.GetTagItem("authorization.subject_hash")).IsEqualTo(subjectHash);
    }
}

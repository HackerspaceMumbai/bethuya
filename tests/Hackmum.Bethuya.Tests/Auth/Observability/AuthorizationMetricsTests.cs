using System.Collections.Concurrent;
using ServiceDefaults.Auth;
using ServiceDefaults.Auth.Observability;

namespace Hackmum.Bethuya.Tests.Auth.Observability;

/// <summary>
/// PR5: <see cref="AuthorizationMetrics"/> emits the authorization counters that flow to the Aspire
/// dashboard. Verified end-to-end via a real <see cref="System.Diagnostics.Metrics.MeterListener"/> so
/// the instrument names and (low-cardinality, non-PII) tag dimensions are asserted exactly as the
/// OpenTelemetry exporter will observe them.
/// </summary>
public sealed class AuthorizationMetricsTests
{
    [Test]
    public async Task RecordDecision_IncrementsDecisionsCounter_WithPolicyDecisionResourceTags()
    {
        var (metrics, provider) = MetricsTestHarness.CreateMetrics();
        await using var _ = provider;
        using var __ = metrics;

        var measurements = new ConcurrentQueue<MetricsTestHarness.Measurement>();
        using var listener = MetricsTestHarness.ListenToAuthorizationMeter(measurements, metrics);

        metrics.RecordDecision(new AuthorizationAuditEvent
        {
            Decision = AuthorizationDecision.Deny,
            PolicyName = BethuyaPolicyNames.ResourceOwner,
            ResourceType = BethuyaAuditResourceTypes.Registration,
            SubjectHash = "deadbeefcafe",
            Route = "/api/attendee/registrations/x",
            OutcomeStatusCode = 404
        });

        var decision = measurements.Single(m => m.Instrument == "authorization.decisions");
        await Assert.That(decision.Value).IsEqualTo(1L);
        await Assert.That(decision.Tags["policy"]).IsEqualTo(BethuyaPolicyNames.ResourceOwner);
        await Assert.That(decision.Tags["decision"]).IsEqualTo(nameof(AuthorizationDecision.Deny));
        await Assert.That(decision.Tags["resource_type"]).IsEqualTo(BethuyaAuditResourceTypes.Registration);
    }

    [Test]
    public async Task RecordOutcome_IncrementsOutcomesCounter_WithRouteGroupAndStatus()
    {
        var (metrics, provider) = MetricsTestHarness.CreateMetrics();
        await using var _ = provider;
        using var __ = metrics;

        var measurements = new ConcurrentQueue<MetricsTestHarness.Measurement>();
        using var listener = MetricsTestHarness.ListenToAuthorizationMeter(measurements, metrics);

        metrics.RecordOutcome("attendee", 403);

        var outcome = measurements.Single(m => m.Instrument == "authorization.outcomes");
        await Assert.That(outcome.Value).IsEqualTo(1L);
        await Assert.That(outcome.Tags["route_group"]).IsEqualTo("attendee");
        await Assert.That(outcome.Tags["status_code"]).IsEqualTo(403);
    }

    [Test]
    [Arguments(AuthorizationDecision.Allow)]
    [Arguments(AuthorizationDecision.Deny)]
    [Arguments(AuthorizationDecision.Bypass)]
    public async Task RecordDecision_TagsDecisionDimension_PerOutcome(AuthorizationDecision decision)
    {
        var (metrics, provider) = MetricsTestHarness.CreateMetrics();
        await using var _ = provider;
        using var __ = metrics;

        var measurements = new ConcurrentQueue<MetricsTestHarness.Measurement>();
        using var listener = MetricsTestHarness.ListenToAuthorizationMeter(measurements, metrics);

        metrics.RecordDecision(new AuthorizationAuditEvent
        {
            Decision = decision,
            PolicyName = BethuyaPolicyNames.ResourceOwner,
            ResourceType = BethuyaAuditResourceTypes.Registration
        });

        var recorded = measurements.Single(m => m.Instrument == "authorization.decisions");
        await Assert.That(recorded.Tags["decision"]).IsEqualTo(decision.ToString());
    }
}


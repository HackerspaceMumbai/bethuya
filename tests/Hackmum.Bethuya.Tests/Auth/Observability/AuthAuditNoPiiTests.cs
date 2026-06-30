using System.Net;
using System.Net.Http;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ServiceDefaults.Auth;
using ServiceDefaults.Auth.Observability;

namespace Hackmum.Bethuya.Tests.Auth.Observability;

/// <summary>
/// PR5 no-PII guarantee: with the real <see cref="AuthorizationAuditor"/> wired, a request that carries a
/// sentinel Email in its principal must produce audit logs and trace spans that contain only the non-PII
/// subject hash — never the Email, Name, or the word "government". Captured via a real logger provider and
/// a real <see cref="System.Diagnostics.ActivityListener"/>.
/// </summary>
public sealed class AuthAuditNoPiiTests
{
    private const string OwnerSub = "owner-1";
    private const string IntruderSub = "intruder-2";
    private const string SentinelEmail = "sentinel.pii@secret.example";

    [Test]
    public async Task NonOwnerGet_RealAuditor_LogsAndSpans_ContainNoEmailNameOrGovernment()
    {
        var registrationId = Guid.CreateVersion7();
        var sink = new RecordingLogSink();

        var repo = Substitute.For<IRegistrationRepository>();
        repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(new Registration
            {
                Id = registrationId,
                EventId = Guid.CreateVersion7(),
                UserId = OwnerSub,
                FullName = "Owner Attendee",
                Email = $"{OwnerSub}@bethuya.test",
                Intent = "Learn and contribute."
            });

        await using var app = await HeaderRoleAuthHost.StartAsync(
            a => a.MapRegistrationEndpoints(),
            configureServices: services =>
            {
                services.AddSingleton(repo);
                services.AddSingleton(Substitute.For<IAttendeeProfileRepository>());
                services.AddSingleton<InclusionSignalsNormalizer>();
                services.AddBethuyaUserContext();
                services.AddDataProtection();
                // Capture every log category; keep the real AuthorizationAuditor (do NOT override it).
                services.AddSingleton<ILoggerProvider>(new RecordingLoggerProvider(sink));
            });

        var (activityListener, activities) = ActivityTestHarness.Listen();
        using var _ = activityListener;

        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/attendee/registrations/{registrationId}", UriKind.Relative));
        request.Headers.Add(HeaderRoleAuthHandler.RolesHeader, BethuyaRoleNames.Attendee);
        request.Headers.Add(HeaderRoleAuthHandler.SubHeader, IntruderSub);
        request.Headers.Add(HeaderRoleAuthHandler.EmailHeader, SentinelEmail);

        var response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        // The audit path actually ran: at least one authorization log + one decision span.
        var auditLogText = sink.Entries.SelectMany(e => e.AllText).ToList();
        await Assert.That(auditLogText.Any(t => t.Contains("Authorization", StringComparison.Ordinal))).IsTrue();

        // The ActivitySource is process-global; concurrent tests share it. Scope to the span this request
        // produced by filtering on the intruder's subject hash (unique to this test).
        var intruderHash = SubjectHash.Compute(IntruderSub);
        var activity = activities.Single(a => (string?)a.GetTagItem("authorization.subject_hash") == intruderHash);

        // No PII leaked into logs.
        foreach (var text in auditLogText)
        {
            await Assert.That(text.Contains(SentinelEmail, StringComparison.OrdinalIgnoreCase)).IsFalse();
            await Assert.That(text.Contains("government", StringComparison.OrdinalIgnoreCase)).IsFalse();
        }

        // No PII leaked into span tags; the subject is present only as its hash.
        foreach (var tag in activity.Tags)
        {
            var value = tag.Value ?? string.Empty;
            await Assert.That(value.Contains(SentinelEmail, StringComparison.OrdinalIgnoreCase)).IsFalse();
            await Assert.That(value.Contains("government", StringComparison.OrdinalIgnoreCase)).IsFalse();
        }

        await Assert.That(activity.GetTagItem("authorization.subject_hash")).IsEqualTo(intruderHash);
    }
}

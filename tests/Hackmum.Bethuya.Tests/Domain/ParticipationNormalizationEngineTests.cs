using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;

namespace Hackmum.Bethuya.Tests.Domain;

public sealed class ParticipationNormalizationEngineTests
{
    [Test]
    public async Task Normalize_TrimsFields_AndPreservesCanonicalValues()
    {
        var input = new NormalizedParticipationEntry(
            Connector: ParticipationConnectorKind.Discord,
            ExternalMemberKey: "  member-123  ",
            Activity: ParticipationActivityKind.MessageEngaged,
            OccurredAt: new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero),
            Evidence: "  Reacted with :rocket: in #events  ",
            ProvenanceKey: "  discord:msg:42  ",
            ExternalEventId: "  event-77  ",
            ExternalRecordId: "  msg-42  ",
            SourceCorrelationId: "  delivery-abc  ");

        var normalized = ParticipationNormalizationEngine.Normalize(input);

        await Assert.That(normalized.ExternalMemberKey).IsEqualTo("member-123");
        await Assert.That(normalized.Evidence).IsEqualTo("Reacted with :rocket: in #events");
        await Assert.That(normalized.ProvenanceKey).IsEqualTo("discord:msg:42");
        await Assert.That(normalized.ExternalEventId).IsEqualTo("event-77");
        await Assert.That(normalized.ExternalRecordId).IsEqualTo("msg-42");
        await Assert.That(normalized.SourceCorrelationId).IsEqualTo("delivery-abc");
        await Assert.That(normalized.Activity).IsEqualTo(ParticipationActivityKind.MessageEngaged);
    }

    [Test]
    public async Task Normalize_MissingProvenance_ThrowsArgumentException()
    {
        var input = new NormalizedParticipationEntry(
            Connector: ParticipationConnectorKind.GitHub,
            ExternalMemberKey: "octocat",
            Activity: ParticipationActivityKind.Volunteered,
            OccurredAt: DateTimeOffset.UtcNow,
            Evidence: "Opened a volunteer issue",
            ProvenanceKey: " ");

        var exception = Assert.Throws<ArgumentException>(() => ParticipationNormalizationEngine.Normalize(input));
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("Provenance key is required.");
    }
}

using Hackmum.Bethuya.Infrastructure.Services;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class EventArchiveContentMergerTests
{
    [Test]
    [Arguments(">-")]
    [Arguments("|-")]
    [Arguments("|+")]
    [Arguments(">+")]
    [Arguments("|2-")]
    [Arguments("|-2")]
    [Arguments(">+2 # folded value with comment")]
    [Arguments(">2+")]
    [Arguments("| # literal value with comment")]
    public async Task MergeMetadata_BlockScalarValue_RefusesReplacement(string blockHeader)
    {
        var existing = $$"""
            title: "Existing title"
            date: 2026-11-28
            status: upcoming
            description: {{blockHeader}}
              Existing description body
              Another body line
            venue: "Community Hall"
            """;

        var action = () => EventArchiveContentMerger.MergeMetadata(existing, ProposedMetadata);

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("Old description", "  continued plain text")]
    [Arguments("\"Old description", "  continued quoted text\"")]
    [Arguments("'Old description", "  continued quoted text'")]
    [Arguments("[first item,", "second item]")]
    [Arguments("{first: value,", "second: value}")]
    [Arguments("", "  nested: value")]
    public async Task MergeMetadata_MultilineValue_RefusesReplacement(string firstLineValue, string continuation)
    {
        var existing = $"""
            title: "Existing title"
            date: 2026-11-28
            status: upcoming
            description: {firstLineValue}
            {continuation}
            venue: "Community Hall"
            """;

        var action = () => EventArchiveContentMerger.MergeMetadata(existing, ProposedMetadata);

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task MergeMetadata_SimpleScalar_ReplacesManagedValueAndPreservesCommunityFields()
    {
        const string existing = """
            title: "Existing title"
            date: 2026-11-28
            status: upcoming
            description: "Existing description"
            venue: "Community Hall"
            """;

        var merged = EventArchiveContentMerger.MergeMetadata(existing, ProposedMetadata);

        await Assert.That(merged).Contains("description: \"New description\"");
        await Assert.That(merged).Contains("venue: \"Community Hall\"");
        await Assert.That(merged).DoesNotContain("Existing description");
    }

    [Test]
    [Arguments("bethuya: {}")]
    [Arguments("bethuya: {eventId: other-event}")]
    [Arguments("bethuya: *communityMapping")]
    [Arguments("\"bethuya\":")]
    public async Task MergeMetadata_UnsupportedProvenanceMapping_RefusesCandidate(string mapping)
    {
        var existing = $"title: Old title\ndate: 2026-11-28\nstatus: upcoming\ndescription: Old description\n{mapping}\n";

        await Assert.That(() => EventArchiveContentMerger.MergeMetadata(existing, ProposedMetadata))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("\"title\"")]
    [Arguments("'title'")]
    [Arguments("\"ti\\u0074le\"")]
    public async Task MergeMetadata_QuotedRootKey_RefusesRatherThanAddingDuplicate(string key)
    {
        var existing = $"{key}: Old title\ndate: 2026-11-28\nstatus: upcoming\ndescription: Old description\n";

        await Assert.That(() => EventArchiveContentMerger.MergeMetadata(existing, ProposedMetadata))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task MergeMetadata_CommunityBlockScalar_PreservesParagraphBreaks()
    {
        const string notes = "notes: |\n  First paragraph.\n\n  Second paragraph.\n";
        var existing = $"title: Old title\ndate: 2026-11-28\nstatus: upcoming\ndescription: Old description\n{notes}";

        var merged = EventArchiveContentMerger.MergeMetadata(existing, ProposedMetadata);

        await Assert.That(merged).Contains(notes);
    }

    [Test]
    public async Task MergeMetadata_FourSpaceProvenance_UpdatesWithoutDuplicateIdentity()
    {
        const string existing = """
            title: Old title
            date: 2026-11-28
            status: upcoming
            description: Old description
            bethuya:
                eventId: aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
                projectionVersion: old-version
                community: keep
            """;

        var merged = EventArchiveContentMerger.MergeMetadata(existing, ProposedMetadata);

        await Assert.That(merged.Split("eventId:", StringSplitOptions.None).Length).IsEqualTo(2);
        await Assert.That(merged).Contains("    projectionVersion: 1");
        await Assert.That(merged).Contains("    community: keep");
    }

    private const string ProposedMetadata = """
        title: "New title"
        date: 2026-11-28
        status: published
        description: "New description"
        bethuya:
          eventId: aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
          projectionVersion: 1
        """;

    [Test]
    [Arguments("&archiveTitle \"Old title\"")]
    [Arguments("!!str \"Old title\"")]
    [Arguments("*archiveTitle")]
    public async Task MergeMetadata_ManagedAnchorOrTag_RefusesRatherThanChangingCommunityReferences(string value)
    {
        var existing = $"title: {value}\ndisplayTitle: *archiveTitle\ndate: 2026-11-28\nstatus: upcoming\ndescription: old\n";

        await Assert.That(() => EventArchiveContentMerger.MergeMetadata(existing, ProposedMetadata))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("```\n<!-- bethuya:managed:start -->\nCommunity example\n<!-- bethuya:managed:end -->\n```")]
    [Arguments("~~~markdown\n<!-- bethuya:managed:start -->\nCommunity example\n<!-- bethuya:managed:end -->\n~~~")]
    [Arguments("    <!-- bethuya:managed:start -->\n    Community example\n    <!-- bethuya:managed:end -->")]
    [Arguments("Example `<!-- bethuya:managed:start -->` and `<!-- bethuya:managed:end -->`.")]
    public async Task MergeReadme_LiteralMarkers_RefusesRatherThanReplacingCommunityExample(string existing)
    {
        await Assert.That(() => EventArchiveContentMerger.MergeReadme(existing, "# New projection"))
            .Throws<InvalidOperationException>();
    }
}

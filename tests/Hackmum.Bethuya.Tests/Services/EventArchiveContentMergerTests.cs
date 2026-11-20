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

    private const string ProposedMetadata = """
        title: "New title"
        date: 2026-11-28
        status: published
        description: "New description"
        bethuya:
          eventId: aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
          projectionVersion: 1
        """;
}

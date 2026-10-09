using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Hackmum.Bethuya.Backend.Contracts;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Produces the canonical versioned Community Passport portability archive.
/// </summary>
public sealed class CommunityPassportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public byte[] BuildArchive(CommunityPassportExperienceResponse passport)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "passport.json", JsonSerializer.Serialize(passport, JsonOptions));
            WriteEntry(archive, "contributions.csv", BuildContributionsCsv(passport.Contributions));
            WriteEntry(archive, "opportunities.csv", BuildOpportunitiesCsv(passport.Opportunities));
            WriteEntry(archive, "relationships.csv", BuildRelationshipsCsv(passport.Connections));
            WriteEntry(archive, "portfolio.csv", BuildPortfolioCsv(passport.Portfolio));
            WriteEntry(archive, "community-story.md",
                $"# Community Story{Environment.NewLine}{Environment.NewLine}{passport.CommunityStory.Narrative}{Environment.NewLine}");
            WriteEntry(archive, "passport-summary.html", BuildHtml(passport));
        }
        return stream.ToArray();
    }

    private static string BuildContributionsCsv(IEnumerable<PassportContributionResponse> items)
        => BuildCsv(
            ["id", "type", "title", "description", "community", "impact_area", "occurred_at", "verified", "event_id"],
            items.Select(item => new[]
            {
                item.Id.ToString(), item.Type, item.Title, item.Description, item.Community,
                item.ImpactArea, item.OccurredAt.ToString("O"), item.IsVerified.ToString(),
                item.EventId?.ToString() ?? string.Empty
            }));

    private static string BuildOpportunitiesCsv(IEnumerable<PassportOpportunityResponse> items)
        => BuildCsv(
            ["id", "kind", "title", "status", "offered_at", "outcome"],
            items.Select(item => new[]
            {
                item.Id.ToString(), item.Kind.ToString(), item.Title, item.CurrentStatus.ToString(),
                item.OfferedAt.ToString("O"), item.Outcome ?? string.Empty
            }));

    private static string BuildRelationshipsCsv(IEnumerable<PassportConnectionResponse> items)
        => BuildCsv(
            ["member_id", "display_name", "kind", "context", "explanation"],
            items.Select(item => new[]
            {
                item.MemberId.ToString(), item.DisplayName, item.Kind.ToString(), item.Context, item.Explanation
            }));

    private static string BuildPortfolioCsv(PassportPortfolioResponse portfolio)
        => BuildCsv(
            ["kind", "title", "description", "featured", "links", "evidence"],
            portfolio.VerifiedHighlights.Select(item => new[]
            {
                "verified", item.Title, item.Description, "false", string.Empty, item.Evidence
            }).Concat(portfolio.Entries.Select(item => new[]
            {
                "curated", item.Title, item.Description, item.IsFeatured.ToString(),
                string.Join(' ', item.Links.Select(link => link.Url)),
                string.Join(' ', item.EvidenceEntryIds)
            })));

    private static string BuildCsv(IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(EscapeCsv)));
        }
        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        var safeValue = value.Length > 0 && value[0] is '=' or '+' or '-' or '@'
            ? $"'{value}"
            : value;
        return $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string BuildHtml(CommunityPassportExperienceResponse passport)
    {
        var encoder = HtmlEncoder.Default;
        var signalList = string.Join(
            string.Empty,
            passport.Signals.Select(signal =>
                $"<li><strong>{encoder.Encode(signal.Label)}</strong> — {encoder.Encode(signal.Explanation)}</li>"));
        var contributionList = string.Join(
            string.Empty,
            passport.Contributions.Take(12).Select(contribution =>
                $"<li>{encoder.Encode(contribution.Title)} <small>{contribution.OccurredAt:dd MMM yyyy}</small></li>"));
        var portfolioList = string.Join(
            string.Empty,
            passport.Portfolio.Entries.Take(8).Select(entry =>
                $"<li><strong>{encoder.Encode(entry.Title)}</strong> — {encoder.Encode(entry.Description)}</li>"));

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>{{encoder.Encode(passport.Identity.DisplayName)}} — Community Passport</title>
              <style>body{font:16px/1.6 Georgia,serif;max-width:860px;margin:3rem auto;padding:0 1.5rem;color:#211f1a}h1,h2{line-height:1.15}small{color:#68645a}</style>
            </head>
            <body>
              <header><p>{{encoder.Encode(passport.Identity.CommunityName)}}</p><h1>{{encoder.Encode(passport.Identity.DisplayName)}}</h1></header>
              <section><h2>Community Story</h2><p>{{encoder.Encode(passport.CommunityStory.Narrative)}}</p></section>
              <section><h2>Community Signals</h2><ul>{{signalList}}</ul></section>
              <section><h2>Contribution Highlights</h2><ul>{{contributionList}}</ul></section>
              <section><h2>Portfolio Highlights</h2><ul>{{portfolioList}}</ul></section>
            </body>
            </html>
            """;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}

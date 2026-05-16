using System.Globalization;
using System.Text.RegularExpressions;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Planning;

/// <summary>
/// Validates and reconciles planner hybrid outputs.
/// </summary>
public static partial class PlanningAgendaValidator
{
    private static readonly HashSet<string> AllowedFormats =
    [
        "talk",
        "panel",
        "workshop",
        "networking",
        "break",
        "other"
    ];

    public static IReadOnlyList<string> Validate(PlanningAgendaJson? agenda)
    {
        var errors = new List<string>();
        if (agenda is null)
        {
            errors.Add("agenda_json is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(agenda.AgendaVersion))
        {
            errors.Add("agenda_json.agendaVersion is required.");
        }

        if (agenda.Event is null)
        {
            errors.Add("agenda_json.event is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(agenda.Event.EventId))
        {
            errors.Add("agenda_json.event.eventId is required.");
        }

        if (string.IsNullOrWhiteSpace(agenda.Event.Title))
        {
            errors.Add("agenda_json.event.title is required.");
        }

        if (!DateOnly.TryParseExact(agenda.Event.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            errors.Add("agenda_json.event.date must be ISO yyyy-MM-dd.");
        }

        if (string.IsNullOrWhiteSpace(agenda.Event.Timezone))
        {
            errors.Add("agenda_json.event.timezone is required.");
        }

        if (agenda.Agenda is null)
        {
            errors.Add("agenda_json.agenda is required.");
            return errors;
        }

        if (agenda.Agenda.Blocks.Count == 0)
        {
            errors.Add("agenda_json.agenda.blocks must contain at least one block.");
            return errors;
        }

        var computedDuration = 0.0;
        foreach (var block in agenda.Agenda.Blocks)
        {
            if (!TryParseTime(block.Start, out var start))
            {
                errors.Add($"agenda_json block '{block.BlockId}' has invalid start '{block.Start}'.");
            }

            if (!TryParseTime(block.End, out var end))
            {
                errors.Add($"agenda_json block '{block.BlockId}' has invalid end '{block.End}'.");
            }

            if (string.IsNullOrWhiteSpace(block.Title))
            {
                errors.Add($"agenda_json block '{block.BlockId}' title is required.");
            }

            if (string.IsNullOrWhiteSpace(block.Description))
            {
                errors.Add($"agenda_json block '{block.BlockId}' description is required.");
            }

            if (!AllowedFormats.Contains(block.Format))
            {
                errors.Add($"agenda_json block '{block.BlockId}' format '{block.Format}' is invalid.");
            }

            if (TryParseTime(block.Start, out start) && TryParseTime(block.End, out end))
            {
                var duration = (end - start).TotalMinutes;
                if (duration <= 0)
                {
                    errors.Add($"agenda_json block '{block.BlockId}' must have end after start.");
                }
                else
                {
                    computedDuration += duration;
                }
            }
        }

        if (Math.Abs(computedDuration - agenda.Agenda.TotalDurationMinutes) > 0.5)
        {
            errors.Add("agenda_json.agenda.totalDurationMinutes must equal sum of block durations.");
        }

        return errors;
    }

    public static PlanningAgendaJson ReconcileFromMarkdown(PlanningAgendaJson agenda, string markdownAgenda)
    {
        var lines = markdownAgenda.Split(Environment.NewLine, StringSplitOptions.TrimEntries);
        var timeline = new List<(string Start, string End, string Title)>();
        foreach (var line in lines)
        {
            var match = TimelineLineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            timeline.Add((match.Groups["start"].Value, match.Groups["end"].Value, match.Groups["title"].Value.Trim()));
        }

        if (timeline.Count == 0)
        {
            return agenda;
        }

        for (var i = 0; i < agenda.Agenda.Blocks.Count && i < timeline.Count; i++)
        {
            agenda.Agenda.Blocks[i].Start = timeline[i].Start;
            agenda.Agenda.Blocks[i].End = timeline[i].End;
            agenda.Agenda.Blocks[i].Title = timeline[i].Title;
        }

        agenda.Agenda.TotalDurationMinutes = agenda.Agenda.Blocks
            .Where(b => TryParseTime(b.Start, out _) && TryParseTime(b.End, out _))
            .Sum(b =>
            {
                var hasStart = TryParseTime(b.Start, out var start);
                var hasEnd = TryParseTime(b.End, out var end);
                if (!hasStart || !hasEnd)
                {
                    return 0;
                }
                return (end - start).TotalMinutes;
            });

        return agenda;
    }

    public static IReadOnlyList<string> ValidateMarkdownConsistency(PlanningAgendaJson agenda, string markdownAgenda)
    {
        var errors = new List<string>();
        foreach (var block in agenda.Agenda.Blocks)
        {
            if (!markdownAgenda.Contains(block.Title, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Markdown is missing block title '{block.Title}'.");
            }

            var hasRangeFormat = markdownAgenda.Contains($"{block.Start} - {block.End}", StringComparison.OrdinalIgnoreCase);
            var hasTableFormat = markdownAgenda.Contains($"| {block.Start} | {block.End} |", StringComparison.OrdinalIgnoreCase);
            if (!hasRangeFormat && !hasTableFormat)
            {
                errors.Add($"Markdown is missing block time '{block.Start} - {block.End}'.");
            }
        }

        return errors;
    }

    private static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    [GeneratedRegex(@"^\|\s*(?<start>\d{2}:\d{2})\s*\|\s*(?<end>\d{2}:\d{2})\s*\|\s*(?<title>.+?)\s*\|?$", RegexOptions.Compiled)]
    private static partial Regex TimelineLineRegex();
}


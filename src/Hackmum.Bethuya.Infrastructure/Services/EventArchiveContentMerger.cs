using System.Text.RegularExpressions;

namespace Hackmum.Bethuya.Infrastructure.Services;

internal static partial class EventArchiveContentMerger
{
    private const string ReadmeStart = "<!-- bethuya:managed:start -->";
    private const string ReadmeEnd = "<!-- bethuya:managed:end -->";
    private static readonly string[] ManagedMetadataKeys = ["title", "date", "status", "description"];
    private static readonly string[] ManagedBethuyaKeys = ["eventId", "projectionVersion"];

    public static string MergeReadme(string? existing, string managedContent)
    {
        var managed = $"{ReadmeStart}\n{managedContent.TrimEnd()}\n{ReadmeEnd}";
        if (string.IsNullOrEmpty(existing))
        {
            return managed + "\n";
        }

        var startIndex = existing.IndexOf(ReadmeStart, StringComparison.Ordinal);
        var endIndex = existing.IndexOf(ReadmeEnd, StringComparison.Ordinal);
        if (startIndex < 0 && endIndex < 0)
        {
            return $"{existing.TrimEnd()}\n\n{managed}\n";
        }

        if (startIndex < 0
            || endIndex < 0
            || existing.IndexOf(ReadmeStart, startIndex + ReadmeStart.Length, StringComparison.Ordinal) >= 0
            || existing.IndexOf(ReadmeEnd, endIndex + ReadmeEnd.Length, StringComparison.Ordinal) >= 0
            || endIndex < startIndex)
        {
            throw new InvalidOperationException("The existing README has ambiguous Bethuya-managed section markers; publication was refused.");
        }

        var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        managed = managed.Replace("\n", newline, StringComparison.Ordinal);
        return $"{existing[..startIndex]}{managed}{existing[(endIndex + ReadmeEnd.Length)..]}";
    }

    public static string MergeMetadata(string? existing, string proposed)
    {
        if (string.IsNullOrEmpty(existing))
        {
            return proposed;
        }

        var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = existing.Split(newline, StringSplitOptions.None).ToList();
        var proposedLines = proposed.Split('\n');

        foreach (var key in ManagedMetadataKeys)
        {
            var candidate = FindScalar(proposedLines, key, 0)
                ?? throw new InvalidOperationException($"Bethuya projection is missing managed metadata key '{key}'.");
            SetScalar(lines, key, 0, candidate);
        }

        var bethuyaSections = FindKeyLines(lines, "bethuya", 0);
        if (bethuyaSections.Count > 1)
        {
            throw new InvalidOperationException("The existing event.yml has multiple bethuya sections; publication was refused.");
        }

        if (bethuyaSections.Count == 0)
        {
            var bethuyaStart = proposedLines.ToList().FindIndex(line => IsScalarLine(line, "bethuya", 0));
            if (bethuyaStart < 0)
            {
                throw new InvalidOperationException("Bethuya projection is missing its provenance section.");
            }

            var append = proposedLines.Skip(bethuyaStart).Select(line => line.Replace("\n", string.Empty, StringComparison.Ordinal));
            lines.RemoveAll(string.IsNullOrEmpty);
            lines.AddRange(append);
        }
        else
        {
            var sectionIndex = bethuyaSections[0];
            var sectionEnd = lines.FindIndex(sectionIndex + 1, line =>
                !string.IsNullOrWhiteSpace(line)
                && !line.StartsWith(' ')
                && !line.StartsWith('#'));
            if (sectionEnd < 0)
            {
                sectionEnd = lines.Count;
            }

            foreach (var key in ManagedBethuyaKeys)
            {
                var candidate = FindScalar(proposedLines, key, 2)
                    ?? throw new InvalidOperationException($"Bethuya projection is missing provenance key '{key}'.");
                SetScalar(lines, key, 2, candidate, sectionIndex + 1, sectionEnd);
                sectionEnd = lines.FindIndex(sectionIndex + 1, line =>
                    !string.IsNullOrWhiteSpace(line)
                    && !line.StartsWith(' ')
                    && !line.StartsWith('#'));
                if (sectionEnd < 0)
                {
                    sectionEnd = lines.Count;
                }
            }
        }

        return string.Join(newline, lines);
    }

    private static void SetScalar(List<string> lines, string key, int indentation, string candidate, int start = 0, int end = -1)
    {
        end = end < 0 ? lines.Count : end;
        var matches = FindKeyLines(lines, key, indentation, start, end);
        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"The existing event.yml has duplicate managed key '{key}'; publication was refused.");
        }

        if (matches.Count == 1)
        {
            var lineIndex = matches[0];
            var existingValue = ScalarValueRegex().Match(lines[lineIndex]);
            var value = existingValue.Groups["value"].Value;
            if (!existingValue.Success
                || value.Trim() is "[]" or "{}"
                || IsBlockScalarValue(value)
                || HasUnterminatedQuotedScalar(value)
                || HasUnclosedFlowCollection(value)
                || HasIndentedContinuation(lines, lineIndex, indentation, end))
            {
                throw new InvalidOperationException($"The existing event.yml uses an ambiguous value for managed key '{key}'; publication was refused.");
            }

            lines[matches[0]] = candidate;
            return;
        }

        if (indentation == 0)
        {
            var section = FindKeyLines(lines, "bethuya", 0);
            if (section.Count > 1)
            {
                throw new InvalidOperationException("The existing event.yml has multiple bethuya sections; publication was refused.");
            }

            lines.Insert(section.Count == 1 ? section[0] : lines.Count, candidate);
        }
        else
        {
            lines.Insert(end, candidate);
        }
    }

    private static bool IsBlockScalarValue(string value)
    {
        var trimmedValue = value.TrimStart();
        return trimmedValue.StartsWith('|') || trimmedValue.StartsWith('>');
    }

    private static bool HasUnterminatedQuotedScalar(string value)
    {
        var trimmedValue = value.TrimStart();
        if (trimmedValue.StartsWith('"'))
        {
            for (var index = 1; index < trimmedValue.Length; index++)
            {
                if (trimmedValue[index] == '\\')
                {
                    index++;
                }
                else if (trimmedValue[index] == '"')
                {
                    return false;
                }
            }

            return true;
        }

        if (!trimmedValue.StartsWith('\''))
        {
            return false;
        }

        for (var index = 1; index < trimmedValue.Length; index++)
        {
            if (trimmedValue[index] != '\'')
            {
                continue;
            }

            if (index + 1 < trimmedValue.Length && trimmedValue[index + 1] == '\'')
            {
                index++;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasUnclosedFlowCollection(string value)
    {
        var trimmedValue = value.TrimStart();
        if (trimmedValue.Length == 0 || trimmedValue[0] is not ('[' or '{'))
        {
            return false;
        }

        var delimiters = new Stack<char>();
        var quote = '\0';
        for (var index = 0; index < trimmedValue.Length; index++)
        {
            var character = trimmedValue[index];
            if (quote == '"')
            {
                if (character == '\\')
                {
                    index++;
                }
                else if (character == '"')
                {
                    quote = '\0';
                }

                continue;
            }

            if (quote == '\'')
            {
                if (character == '\'' && index + 1 < trimmedValue.Length && trimmedValue[index + 1] == '\'')
                {
                    index++;
                }
                else if (character == '\'')
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                quote = character;
            }
            else if (character == '#' && (index == 0 || char.IsWhiteSpace(trimmedValue[index - 1])))
            {
                break;
            }
            else if (character == '[')
            {
                delimiters.Push(']');
            }
            else if (character == '{')
            {
                delimiters.Push('}');
            }
            else if (character is ']' or '}')
            {
                if (delimiters.Count == 0 || delimiters.Pop() != character)
                {
                    return true;
                }
            }
        }

        return delimiters.Count > 0 || quote != '\0';
    }

    private static bool HasIndentedContinuation(
        List<string> lines,
        int scalarIndex,
        int indentation,
        int end)
    {
        for (var index = scalarIndex + 1; index < end; index++)
        {
            var line = lines[index];
            var trimmedLine = line.TrimStart();
            if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            var lineIndentation = line.Length - line.TrimStart().Length;
            return lineIndentation > indentation;
        }

        return false;
    }

    private static string? FindScalar(string[] lines, string key, int indentation)
    {
        var matches = FindKeyLines(lines, key, indentation);
        return matches.Count == 1 ? lines[matches[0]] : null;
    }

    private static List<int> FindKeyLines(IReadOnlyList<string> lines, string key, int indentation, int start = 0, int end = -1)
    {
        end = end < 0 ? lines.Count : Math.Min(end, lines.Count);
        var matches = new List<int>();
        for (var index = start; index < end; index++)
        {
            if (IsScalarLine(lines[index], key, indentation))
            {
                matches.Add(index);
            }
        }

        return matches;
    }

    private static bool IsScalarLine(string line, string key, int indentation)
        => ScalarValueRegex().Match(line) is { Success: true } match
            && match.Groups["indent"].Value.Length == indentation
            && match.Groups["key"].Value.Equals(key, StringComparison.Ordinal);

    [GeneratedRegex(@"^(?<indent> *)(?<key>[A-Za-z0-9_-]+)\s*:\s*(?<value>.*)$")]
    private static partial Regex ScalarValueRegex();
}

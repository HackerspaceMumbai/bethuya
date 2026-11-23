using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>Conservatively reads and merges Bethuya-owned archive content.</summary>
public static partial class EventArchiveContentMerger
{
    private const string ReadmeStart = "<!-- bethuya:managed:start -->";
    private const string ReadmeEnd = "<!-- bethuya:managed:end -->";
    private static readonly string[] ManagedMetadataKeys = ["title", "date", "status", "description"];
    private static readonly string[] ManagedBethuyaKeys = ["eventId", "projectionVersion"];

    /// <summary>Replaces only the marked README section, preserving community content.</summary>
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

        ValidateReadmeMarkers(existing);
        var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        managed = managed.Replace("\n", newline, StringComparison.Ordinal);
        return $"{existing[..startIndex]}{managed}{existing[(endIndex + ReadmeEnd.Length)..]}";
    }

    private static void ValidateReadmeMarkers(string existing)
    {
        var fenceCharacter = '\0';
        var fenceLength = 0;
        string? htmlBlockEnd = null;
        foreach (var rawLine in existing.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Contains(ReadmeStart, StringComparison.Ordinal) || line.Contains(ReadmeEnd, StringComparison.Ordinal))
            {
                if (fenceCharacter != '\0' || htmlBlockEnd is not null || line != ReadmeStart && line != ReadmeEnd)
                {
                    throw new InvalidOperationException("README markers must be standalone comments outside literal Markdown blocks; resolve ambiguous ownership before preview.");
                }
            }

            if (htmlBlockEnd is not null)
            {
                if (htmlBlockEnd.Length == 0 ? string.IsNullOrWhiteSpace(line)
                    : line.Contains(htmlBlockEnd, StringComparison.OrdinalIgnoreCase))
                {
                    htmlBlockEnd = null;
                }
                continue;
            }

            if (fenceCharacter == '\0' && FindHtmlBlockEnd(line) is { } end)
            {
                htmlBlockEnd = end.Length != 0 && line.Contains(end, StringComparison.OrdinalIgnoreCase) ? null : end;
                continue;
            }

            var trimmed = line.TrimStart(' ');
            if (line.Length - trimmed.Length > 3 || trimmed.Length < 3 || trimmed[0] is not ('`' or '~'))
            {
                continue;
            }

            var length = trimmed.TakeWhile(character => character == trimmed[0]).Count();
            if (length < 3)
            {
                continue;
            }

            if (fenceCharacter == '\0')
            {
                fenceCharacter = trimmed[0];
                fenceLength = length;
            }
            else if (trimmed[0] == fenceCharacter && length >= fenceLength && string.IsNullOrWhiteSpace(trimmed[length..]))
            {
                fenceCharacter = '\0';
            }
        }
    }

    private static string? FindHtmlBlockEnd(string line)
    {
        var trimmed = line.TrimStart(' ');
        if (line.Length - trimmed.Length > 3)
        {
            return null;
        }
        if (trimmed.StartsWith("<!--", StringComparison.Ordinal))
        {
            return "-->";
        }
        if (trimmed.StartsWith("<?", StringComparison.Ordinal))
        {
            return "?>";
        }
        if (trimmed.StartsWith("<![CDATA[", StringComparison.Ordinal))
        {
            return "]]>";
        }
        if (trimmed.Length > 2 && trimmed.StartsWith("<!", StringComparison.Ordinal) && char.IsAsciiLetterUpper(trimmed[2]))
        {
            return ">";
        }
        var literalTag = LiteralHtmlTagRegex().Match(trimmed);
        if (literalTag.Success)
        {
            return $"</{literalTag.Groups["tag"].Value}>";
        }

        // Ambiguous/custom HTML blocks are refused until a blank line establishes a Markdown boundary.
        return StandaloneHtmlTagRegex().IsMatch(trimmed) ? string.Empty : null;
    }

    /// <summary>Merges owned scalars and refuses unsupported mapping structures.</summary>
    public static string MergeMetadata(string? existing, string proposed)
    {
        if (string.IsNullOrEmpty(existing))
        {
            return proposed;
        }

        var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = existing.Split(newline, StringSplitOptions.None).ToList();
        var proposedLines = proposed.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

        foreach (var key in ManagedMetadataKeys)
        {
            var candidate = FindScalar(proposedLines, key, 0)
                ?? throw new InvalidOperationException($"Bethuya projection is missing managed metadata key '{key}'.");
            SetScalar(lines, key, 0, candidate);
        }

        var mapping = FindBethuyaMapping(lines);
        if (mapping is null)
        {
            var bethuyaStart = proposedLines.ToList().FindIndex(line => IsScalarLine(line, "bethuya", 0));
            if (bethuyaStart < 0)
            {
                throw new InvalidOperationException("Bethuya projection is missing its provenance section.");
            }

            var append = proposedLines.Skip(bethuyaStart).Select(line => line.Replace("\n", string.Empty, StringComparison.Ordinal));
            lines.AddRange(append);
        }
        else
        {
            var section = mapping.Value;
            foreach (var key in ManagedBethuyaKeys)
            {
                var candidate = FindScalar(proposedLines, key, 2)
                    ?? throw new InvalidOperationException($"Bethuya projection is missing provenance key '{key}'.");
                SetScalar(lines, key, section.Indentation,
                    new string(' ', section.Indentation) + candidate.TrimStart(),
                    section.Start, section.End);
                section = FindBethuyaMapping(lines)
                    ?? throw new InvalidOperationException("The Bethuya provenance mapping could not be retained.");
            }
        }

        return string.Join(newline, lines);
    }

    /// <summary>Reads the direct provenance identity, refusing ambiguous or unsupported YAML.</summary>
    public static string? ReadBethuyaEventId(string metadata)
    {
        var lines = metadata.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var mapping = FindBethuyaMapping(lines);
        if (mapping is null)
        {
            return null;
        }

        var section = mapping.Value;
        var matches = FindKeyLines(lines, "eventId", section.Indentation, section.Start, section.End);
        if (matches.Count > 1)
        {
            throw new InvalidOperationException("The archive has duplicate Bethuya identities; adoption was refused.");
        }

        return matches.Count == 0
            ? null
            : DecodeIdentityScalar(ReadSimpleScalar(lines, matches[0], "eventId", section.Indentation, section.End));
    }

    private static string DecodeIdentityScalar(string value)
    {
        var quote = '\0';
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (quote == '"' && character == '\\'
                || quote == '\'' && character == '\'' && index + 1 < value.Length && value[index + 1] == '\'')
            {
                index++;
            }
            else if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
            }
            else if (character is '"' or '\'')
            {
                quote = character;
            }
            else if (character == '#' && (index == 0 || char.IsWhiteSpace(value[index - 1])))
            {
                value = value[..index];
                break;
            }
        }

        value = value.Trim();
        if (value.StartsWith('"'))
        {
            try
            {
                return JsonSerializer.Deserialize<string>(value)
                    ?? throw new InvalidOperationException("The Bethuya identity must be a non-null scalar.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("The quoted Bethuya identity uses unsupported YAML; resolve it before preview or adoption.", exception);
            }
        }
        if (!value.StartsWith('\''))
        {
            return value;
        }
        if (value.Length < 2 || !value.EndsWith('\''))
        {
            throw new InvalidOperationException("The Bethuya identity uses an ambiguous quoted scalar; resolve it before preview or adoption.");
        }
        return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
    }

    private static (int Start, int End, int Indentation)? FindBethuyaMapping(List<string> lines)
    {
        ValidateMappingKeys(lines, 0, lines.Count, 0);
        var sections = FindKeyLines(lines, "bethuya", 0);
        if (sections.Count > 1)
        {
            throw new InvalidOperationException("The existing event.yml has multiple bethuya sections; publication was refused.");
        }

        if (sections.Count == 0)
        {
            return null;
        }

        var index = sections[0];
        var value = ScalarValueRegex().Match(lines[index]).Groups["value"].Value.TrimStart();
        if (value.Length != 0 && !value.StartsWith('#'))
        {
            throw new InvalidOperationException("The bethuya section must be a plain-key block mapping; resolve its structure before preview or adoption.");
        }

        var end = index + 1;
        while (end < lines.Count && (IsIgnorableLine(lines[end]) || Indentation(lines[end]) > 0))
        {
            end++;
        }

        var firstChild = Enumerable.Range(index + 1, end - index - 1)
            .FirstOrDefault(child => !IsIgnorableLine(lines[child]), -1);
        var indentation = firstChild < 0 ? 2 : Indentation(lines[firstChild]);
        ValidateMappingKeys(lines, index + 1, end, indentation);
        return (index + 1, end, indentation);
    }

    private static void ValidateMappingKeys(List<string> lines, int start, int end, int indentation)
    {
        for (var index = start; index < end; index++)
        {
            if (IsIgnorableLine(lines[index]))
            {
                continue;
            }

            var actualIndentation = Indentation(lines[index]);
            if (actualIndentation < indentation
                || actualIndentation == indentation && !ScalarValueRegex().IsMatch(lines[index]))
            {
                throw new InvalidOperationException("The event.yml uses unsupported mapping keys or indentation; resolve it to a plain-key block mapping before preview or adoption.");
            }
        }
    }

    private static int Indentation(string line) => line.Length - line.TrimStart(' ').Length;

    private static bool IsIgnorableLine(string line)
        => string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#');

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
            _ = ReadSimpleScalar(lines, lineIndex, key, indentation, end);
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

    private static string ReadSimpleScalar(List<string> lines, int lineIndex, string key, int indentation, int end)
    {
        var existingValue = ScalarValueRegex().Match(lines[lineIndex]);
        var value = existingValue.Groups["value"].Value;
        if (!existingValue.Success
            || value.TrimStart().StartsWith('&')
            || value.TrimStart().StartsWith('*')
            || value.TrimStart().StartsWith('!')
            || value.TrimStart().StartsWith('[')
            || value.TrimStart().StartsWith('{')
            || IsBlockScalarValue(value)
            || HasUnterminatedQuotedScalar(value)
            || HasUnclosedFlowCollection(value)
            || HasIndentedContinuation(lines, lineIndex, indentation, end))
        {
            throw new InvalidOperationException($"The existing event.yml uses an ambiguous value for managed key '{key}'; publication was refused.");
        }

        return value;
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

    [GeneratedRegex(@"^(?<indent> *)(?<key>[A-Za-z0-9_-]+)[ \t]*:(?=[ \t]|\r?$)[ \t]*(?<value>.*)$")]
    private static partial Regex ScalarValueRegex();

    [GeneratedRegex(@"^<(?<tag>script|pre|style|textarea)(?=[\s>]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex LiteralHtmlTagRegex();

    [GeneratedRegex(@"^</?[A-Za-z][A-Za-z0-9-]*(?:[ \t]+[^<>]*)?[ \t]*/?>[ \t]*$")]
    private static partial Regex StandaloneHtmlTagRegex();
}

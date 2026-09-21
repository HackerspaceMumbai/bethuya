namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// A parsed CSV/XLSX file in its original, source-native form: headers in file order and one
/// row per data row, keyed by the original column header. No mapping or normalization applied.
/// </summary>
public sealed record ParsedImportFile(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

/// <summary>
/// Parses import file bytes into a <see cref="ParsedImportFile"/>. Implementations are pure
/// (no DB access) and must not throw for well-formed-but-empty files.
/// </summary>
public interface IImportFileParser
{
    /// <summary>File extensions (including the leading dot, lower-case) this parser supports.</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    ParsedImportFile Parse(Stream content);
}

/// <summary>Thrown when an uploaded file cannot be parsed (corrupt, unsupported, or empty of headers).</summary>
public sealed class ImportFileParseException(string message, Exception? innerException = null)
    : Exception(message, innerException);

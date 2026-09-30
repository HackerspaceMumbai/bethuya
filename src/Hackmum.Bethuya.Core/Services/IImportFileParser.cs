namespace Hackmum.Bethuya.Core.Services;

/// <summary>Resource limits applied consistently to CSV and XLSX imports.</summary>
public static class ImportFileLimits
{
    /// <summary>Maximum number of source columns permitted in an import file.</summary>
    public const int MaxColumns = 100;

    /// <summary>Maximum number of data rows permitted in one import batch.</summary>
    public const int MaxRows = 10_000;

    /// <summary>Maximum number of characters permitted in one source cell.</summary>
    public const int MaxCellLength = 16_384;

    /// <summary>Maximum number of ZIP entries permitted in an XLSX package (zip-bomb guard).</summary>
    public const int MaxZipEntries = 1_000;

    /// <summary>Maximum total declared uncompressed size of an XLSX package (zip-bomb guard).</summary>
    public const long MaxUncompressedBytes = 200L * 1024 * 1024;

    /// <summary>
    /// Rejects header rows containing columns that collide after trimming and case-folding. Rows are
    /// keyed by header case-insensitively, so such collisions would silently drop source data.
    /// </summary>
    /// <param name="headers">Trimmed header names in file order.</param>
    /// <param name="fileKind">A display label for error messages, e.g. <c>CSV</c>.</param>
    public static void EnsureUniqueHeaders(IEnumerable<string> headers, string fileKind)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            if (!seen.Add(header))
            {
                throw new ImportFileParseException(
                    $"The {fileKind} file has more than one column named '{header}'. Rename or remove the duplicate column and upload again.");
            }
        }
    }
}

/// <summary>
/// A parsed CSV/XLSX file in its original, source-native form: headers in file order and one
/// row per data row, keyed by the original column header. No mapping or normalization applied.
/// </summary>
public sealed record ParsedImportFile(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

/// <summary>
/// Parses import file bytes into a <see cref="ParsedImportFile"/>. Implementations are pure
/// (no DB access) and reject files that do not contain a usable header row.
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

using Hackmum.Bethuya.Core.Services;

namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>
/// Resolves the appropriate <see cref="IImportFileParser"/> by file extension.
/// </summary>
public sealed class ImportFileParserResolver(IEnumerable<IImportFileParser> parsers)
{
    public IImportFileParser Resolve(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var parser = parsers.FirstOrDefault(p => p.SupportedExtensions.Contains(extension));

        return parser ?? throw new ImportFileParseException(
            $"Unsupported file type '{extension}'. Only CSV and XLSX files are supported.");
    }
}

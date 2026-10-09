using CsvHelper;
using CsvHelper.Configuration;
using Hackmum.Bethuya.Core.Services;
using System.Globalization;

namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>CSV implementation of <see cref="IImportFileParser"/>.</summary>
public sealed class CsvImportFileParser : IImportFileParser
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".csv"];

    public ParsedImportFile Parse(Stream content)
    {
        try
        {
            using var reader = new StreamReader(content);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
            {
                throw new ImportFileParseException("The CSV file does not contain a header row.");
            }

            if (csv.HeaderRecord.Length > ImportFileLimits.MaxColumns)
            {
                throw new ImportFileParseException(
                    $"The CSV file has more than {ImportFileLimits.MaxColumns} columns.");
            }

            // Blank header columns carry no mappable data, mirroring the XLSX parser.
            var headerColumns = csv.HeaderRecord
                .Select((header, index) => (Index: index, Header: header.Trim()))
                .Where(pair => pair.Header.Length > 0)
                .ToList();
            if (headerColumns.Count == 0)
            {
                throw new ImportFileParseException("The CSV file does not contain a header row.");
            }

            var headers = headerColumns.Select(pair => pair.Header).ToList();
            ImportFileLimits.EnsureUniqueHeaders(headers, "CSV");

            var rows = new List<IReadOnlyDictionary<string, string?>>();

            while (csv.Read())
            {
                if (rows.Count == ImportFileLimits.MaxRows)
                {
                    throw new ImportFileParseException(
                        $"The CSV file has more than {ImportFileLimits.MaxRows} data rows.");
                }

                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (columnIndex, header) in headerColumns)
                {
                    var value = csv.TryGetField<string>(columnIndex, out var field) ? field : null;
                    if (value?.Length > ImportFileLimits.MaxCellLength)
                    {
                        throw new ImportFileParseException(
                            $"A CSV cell exceeds the {ImportFileLimits.MaxCellLength}-character limit.");
                    }

                    row[header] = value;
                }

                rows.Add(row);
            }

            return new ParsedImportFile(headers, rows);
        }
        catch (ImportFileParseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ImportFileParseException("The CSV file could not be parsed. Please check that it is a valid, well-formed CSV export.", ex);
        }
    }
}

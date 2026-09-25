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

            var headers = csv.HeaderRecord.ToList();
            if (headers.Count > ImportFileLimits.MaxColumns)
            {
                throw new ImportFileParseException(
                    $"The CSV file has more than {ImportFileLimits.MaxColumns} columns.");
            }

            var rows = new List<IReadOnlyDictionary<string, string?>>();

            while (csv.Read())
            {
                if (rows.Count == ImportFileLimits.MaxRows)
                {
                    throw new ImportFileParseException(
                        $"The CSV file has more than {ImportFileLimits.MaxRows} data rows.");
                }

                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in headers)
                {
                    var value = csv.GetField(header);
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

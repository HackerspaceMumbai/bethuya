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
            var rows = new List<IReadOnlyDictionary<string, string?>>();

            while (csv.Read())
            {
                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in headers)
                {
                    row[header] = csv.GetField(header);
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

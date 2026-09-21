using ClosedXML.Excel;
using Hackmum.Bethuya.Core.Services;

namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>XLSX implementation of <see cref="IImportFileParser"/>. Reads the first worksheet's used range.</summary>
public sealed class XlsxImportFileParser : IImportFileParser
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".xlsx"];

    public ParsedImportFile Parse(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var worksheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new ImportFileParseException("The XLSX file does not contain any worksheets.");

            var usedRange = worksheet.RangeUsed();
            if (usedRange is null)
            {
                throw new ImportFileParseException("The XLSX file's first worksheet is empty.");
            }

            var rowsUsed = usedRange.RowsUsed().ToList();
            if (rowsUsed.Count == 0)
            {
                throw new ImportFileParseException("The XLSX file does not contain a header row.");
            }

            var headerRow = rowsUsed[0];
            var headers = headerRow.Cells()
                .Select(cell => cell.GetString().Trim())
                .Where(header => !string.IsNullOrWhiteSpace(header))
                .ToList();

            if (headers.Count == 0)
            {
                throw new ImportFileParseException("The XLSX file does not contain a header row.");
            }

            var rows = new List<IReadOnlyDictionary<string, string?>>();
            foreach (var dataRow in rowsUsed.Skip(1))
            {
                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
                {
                    var cell = dataRow.Cell(columnIndex + 1);
                    row[headers[columnIndex]] = cell.IsEmpty() ? null : cell.GetString();
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
            throw new ImportFileParseException("The XLSX file could not be parsed. Please check that it is a valid, well-formed spreadsheet export.", ex);
        }
    }
}

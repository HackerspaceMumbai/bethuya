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

            var rowsUsed = usedRange.RowsUsed();
            var firstRow = rowsUsed.FirstOrDefault();
            if (firstRow is null)
            {
                throw new ImportFileParseException("The XLSX file does not contain a header row.");
            }

            var headerRow = firstRow;
            var headers = headerRow.Cells()
                .Select(cell => cell.GetString().Trim())
                .Where(header => !string.IsNullOrWhiteSpace(header))
                .ToList();

            if (headers.Count == 0)
            {
                throw new ImportFileParseException("The XLSX file does not contain a header row.");
            }

            if (headers.Count > ImportFileLimits.MaxColumns)
            {
                throw new ImportFileParseException(
                    $"The XLSX file has more than {ImportFileLimits.MaxColumns} columns.");
            }

            var rows = new List<IReadOnlyDictionary<string, string?>>();
            foreach (var dataRow in rowsUsed.Skip(1))
            {
                if (rows.Count == ImportFileLimits.MaxRows)
                {
                    throw new ImportFileParseException(
                        $"The XLSX file has more than {ImportFileLimits.MaxRows} data rows.");
                }

                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
                {
                    var cell = dataRow.Cell(columnIndex + 1);
                    var value = cell.IsEmpty() ? null : cell.GetString();
                    if (value?.Length > ImportFileLimits.MaxCellLength)
                    {
                        throw new ImportFileParseException(
                            $"An XLSX cell exceeds the {ImportFileLimits.MaxCellLength}-character limit.");
                    }

                    row[headers[columnIndex]] = value;
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

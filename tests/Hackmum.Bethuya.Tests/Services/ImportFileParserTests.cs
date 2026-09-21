using System.Text;
using ClosedXML.Excel;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Services;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class ImportFileParserTests
{
    [Test]
    public async Task CsvImportFileParser_ParsesHeadersAndRows()
    {
        var parser = new CsvImportFileParser();
        var csv = "Name,Email\nAda Lovelace,ada@example.com\nAlan Turing,alan@example.com\n";

        var parsed = parser.Parse(ToStream(csv));

        await Assert.That(parsed.Headers).IsEquivalentTo(["Name", "Email"]);
        await Assert.That(parsed.Rows.Count).IsEqualTo(2);
        await Assert.That(parsed.Rows[0]["Email"]).IsEqualTo("ada@example.com");
    }

    [Test]
    public async Task CsvImportFileParser_MissingHeaderRow_Throws()
    {
        var parser = new CsvImportFileParser();

        await Assert.That(() => parser.Parse(ToStream(string.Empty)))
            .Throws<ImportFileParseException>();
    }

    [Test]
    public async Task CsvImportFileParser_RaggedRows_DoesNotThrow()
    {
        var parser = new CsvImportFileParser();
        // Second data row is missing the Email column entirely.
        var csv = "Name,Email\nAda Lovelace,ada@example.com\nAlan Turing\n";

        var parsed = parser.Parse(ToStream(csv));

        await Assert.That(parsed.Rows.Count).IsEqualTo(2);
        await Assert.That(parsed.Rows[1]["Email"]).IsNull();
    }

    [Test]
    public async Task XlsxImportFileParser_ParsesHeadersAndRows()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            worksheet.Cell(1, 1).Value = "Name";
            worksheet.Cell(1, 2).Value = "Email";
            worksheet.Cell(2, 1).Value = "Ada Lovelace";
            worksheet.Cell(2, 2).Value = "ada@example.com";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new XlsxImportFileParser();
        var parsed = parser.Parse(stream);

        await Assert.That(parsed.Headers).IsEquivalentTo(["Name", "Email"]);
        await Assert.That(parsed.Rows.Count).IsEqualTo(1);
        await Assert.That(parsed.Rows[0]["Email"]).IsEqualTo("ada@example.com");
    }

    [Test]
    public async Task XlsxImportFileParser_EmptyWorksheet_Throws()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            workbook.Worksheets.Add("Sheet1");
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new XlsxImportFileParser();

        await Assert.That(() => parser.Parse(stream)).Throws<ImportFileParseException>();
    }

    [Test]
    public async Task ImportFileParserResolver_ResolvesByExtension()
    {
        var resolver = new ImportFileParserResolver([new CsvImportFileParser(), new XlsxImportFileParser()]);

        var csvParser = resolver.Resolve("luma-export.CSV");
        var xlsxParser = resolver.Resolve("mlh-export.xlsx");

        await Assert.That(csvParser).IsTypeOf<CsvImportFileParser>();
        await Assert.That(xlsxParser).IsTypeOf<XlsxImportFileParser>();
    }

    [Test]
    public async Task ImportFileParserResolver_UnsupportedExtension_Throws()
    {
        var resolver = new ImportFileParserResolver([new CsvImportFileParser(), new XlsxImportFileParser()]);

        await Assert.That(() => resolver.Resolve("export.pdf")).Throws<ImportFileParseException>();
    }

    private static MemoryStream ToStream(string content) => new(Encoding.UTF8.GetBytes(content));
}

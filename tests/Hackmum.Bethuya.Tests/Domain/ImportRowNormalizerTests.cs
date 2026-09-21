using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;

namespace Hackmum.Bethuya.Tests.Domain;

public sealed class ImportRowNormalizerTests
{
    [Test]
    public async Task NormalizeAll_ValidRegistrationRow_MapsFieldsAndLowercasesEmail()
    {
        var template = CreateTemplate(ImportKind.Registration,
            ("Name", ImportTargetField.FullName),
            ("Email", ImportTargetField.Email),
            ("Registered At", ImportTargetField.OccurredAt));

        var file = new ParsedImportFile(
            ["Name", "Email", "Registered At"],
            [
                new Dictionary<string, string?>
                {
                    ["Name"] = "Ada Lovelace",
                    ["Email"] = "Ada@Example.com",
                    ["Registered At"] = "2026-07-01T10:00:00Z"
                }
            ]);

        var rows = ImportRowNormalizer.NormalizeAll(file, template);

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].IsValid).IsTrue();
        await Assert.That(rows[0].Email).IsEqualTo("ada@example.com");
        await Assert.That(rows[0].FullName).IsEqualTo("Ada Lovelace");
        await Assert.That(rows[0].OccurredAt).IsNotNull();
    }

    [Test]
    public async Task NormalizeAll_MissingEmail_ProducesRequiredError()
    {
        var template = CreateTemplate(ImportKind.Registration,
            ("Name", ImportTargetField.FullName),
            ("Email", ImportTargetField.Email));

        var file = new ParsedImportFile(
            ["Name", "Email"],
            [new Dictionary<string, string?> { ["Name"] = "No Email", ["Email"] = "" }]);

        var rows = ImportRowNormalizer.NormalizeAll(file, template);

        await Assert.That(rows[0].IsValid).IsFalse();
        await Assert.That(rows[0].ValidationErrors).Contains("Email is required.");
    }

    [Test]
    public async Task NormalizeAll_MalformedEmail_ProducesValidationErrorAndNullsEmail()
    {
        var template = CreateTemplate(ImportKind.Registration,
            ("Name", ImportTargetField.FullName),
            ("Email", ImportTargetField.Email));

        var file = new ParsedImportFile(
            ["Name", "Email"],
            [new Dictionary<string, string?> { ["Name"] = "Bad Email", ["Email"] = "not-an-email" }]);

        var rows = ImportRowNormalizer.NormalizeAll(file, template);

        await Assert.That(rows[0].IsValid).IsFalse();
        await Assert.That(rows[0].Email).IsNull();
    }

    [Test]
    public async Task NormalizeAll_MissingFullName_OnlyRequiredForRegistrationImports()
    {
        var registrationTemplate = CreateTemplate(ImportKind.Registration, ("Email", ImportTargetField.Email));
        var attendanceTemplate = CreateTemplate(ImportKind.Attendance, ("Email", ImportTargetField.Email));

        var file = new ParsedImportFile(
            ["Email"],
            [new Dictionary<string, string?> { ["Email"] = "someone@example.com" }]);

        var registrationRows = ImportRowNormalizer.NormalizeAll(file, registrationTemplate);
        var attendanceRows = ImportRowNormalizer.NormalizeAll(file, attendanceTemplate);

        await Assert.That(registrationRows[0].IsValid).IsFalse();
        await Assert.That(registrationRows[0].ValidationErrors).Contains("Full name is required for registration imports.");
        await Assert.That(attendanceRows[0].IsValid).IsTrue();
    }

    [Test]
    public async Task NormalizeAll_InvalidOccurredAt_ProducesValidationError()
    {
        var template = CreateTemplate(ImportKind.Attendance,
            ("Email", ImportTargetField.Email),
            ("Check-in", ImportTargetField.OccurredAt));

        var file = new ParsedImportFile(
            ["Email", "Check-in"],
            [new Dictionary<string, string?> { ["Email"] = "a@example.com", ["Check-in"] = "not-a-date" }]);

        var rows = ImportRowNormalizer.NormalizeAll(file, template);

        await Assert.That(rows[0].IsValid).IsFalse();
        await Assert.That(rows[0].ValidationErrors).Contains("'not-a-date' is not a valid date/time value.");
    }

    [Test]
    public async Task NormalizeAll_DuplicateEmailsInFile_FlagsBothRowsAsErrors()
    {
        var template = CreateTemplate(ImportKind.Attendance, ("Email", ImportTargetField.Email));

        var file = new ParsedImportFile(
            ["Email"],
            [
                new Dictionary<string, string?> { ["Email"] = "dup@example.com" },
                new Dictionary<string, string?> { ["Email"] = "DUP@example.com" }
            ]);

        var rows = ImportRowNormalizer.NormalizeAll(file, template);

        await Assert.That(rows[0].IsValid).IsFalse();
        await Assert.That(rows[1].IsValid).IsFalse();
        await Assert.That(rows[0].ValidationErrors.Any(e => e.Contains("appears more than once", StringComparison.Ordinal))).IsTrue();
        await Assert.That(rows[1].ValidationErrors.Any(e => e.Contains("appears more than once", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task NormalizeAll_UnmappedHeadersAreIgnored()
    {
        var template = CreateTemplate(ImportKind.Attendance, ("Email", ImportTargetField.Email));

        var file = new ParsedImportFile(
            ["Email", "Unmapped Column"],
            [new Dictionary<string, string?> { ["Email"] = "a@example.com", ["Unmapped Column"] = "ignored" }]);

        var rows = ImportRowNormalizer.NormalizeAll(file, template);

        await Assert.That(rows[0].IsValid).IsTrue();
        await Assert.That(rows[0].Notes).IsNull();
    }

    private static ImportTemplate CreateTemplate(ImportKind importKind, params (string SourceColumnName, ImportTargetField TargetField)[] mappings)
    {
        var template = new ImportTemplate
        {
            Name = "Test Template",
            Scope = ImportTemplateScope.System,
            SourceKind = ImportSourceKind.Custom,
            ImportKind = importKind
        };

        foreach (var (sourceColumnName, targetField) in mappings)
        {
            template.ColumnMappings.Add(new ImportColumnMapping
            {
                ImportTemplateId = template.Id,
                SourceColumnName = sourceColumnName,
                TargetField = targetField
            });
        }

        return template;
    }
}

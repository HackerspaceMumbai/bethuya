using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using System.Net.Mail;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Applies an <see cref="ImportTemplate"/>'s column mappings to a <see cref="ParsedImportFile"/>
/// and validates each row. Pure and deeply testable — no DB access. Identity resolution
/// (matching against existing Bethuya data) happens separately in the dry-run service, since
/// that requires the database.
/// </summary>
public static class ImportRowNormalizer
{
    /// <summary>
    /// Normalizes every row and flags in-file duplicate emails as validation errors on every
    /// row that shares the duplicated email (per the "duplicate emails within a file are
    /// validation errors" decision).
    /// </summary>
    public static IReadOnlyList<NormalizedImportRow> NormalizeAll(ParsedImportFile file, ImportTemplate template)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(template);

        var mappingByHeader = template.ColumnMappings
            .GroupBy(mapping => mapping.SourceColumnName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().TargetField, StringComparer.OrdinalIgnoreCase);

        var rows = new List<NormalizedImportRow>(file.Rows.Count);
        for (var i = 0; i < file.Rows.Count; i++)
        {
            rows.Add(NormalizeRow(i, file.Rows[i], mappingByHeader, template.ImportKind));
        }

        FlagDuplicateEmails(rows);

        return rows;
    }

    private static NormalizedImportRow NormalizeRow(
        int rowIndex,
        IReadOnlyDictionary<string, string?> rawRow,
        Dictionary<string, ImportTargetField> mappingByHeader,
        ImportKind importKind)
    {
        var values = new Dictionary<ImportTargetField, string?>();
        foreach (var (header, rawValue) in rawRow)
        {
            if (mappingByHeader.TryGetValue(header.Trim(), out var targetField))
            {
                values[targetField] = rawValue;
            }
        }

        var errors = new List<string>();

        var email = TrimToNull(values.GetValueOrDefault(ImportTargetField.Email));
        if (email is null)
        {
            errors.Add("Email is required.");
        }
        else if (!IsValidEmail(email))
        {
            errors.Add($"'{email}' is not a valid email address.");
            email = null;
        }

        else
        {
            email = email.ToLowerInvariant();
        }

        var fullName = TrimToNull(values.GetValueOrDefault(ImportTargetField.FullName));
        if (importKind == ImportKind.Registration && fullName is null)
        {
            errors.Add("Full name is required for registration imports.");
        }

        DateTimeOffset? occurredAt = null;
        var occurredAtRaw = TrimToNull(values.GetValueOrDefault(ImportTargetField.OccurredAt));
        if (occurredAtRaw is not null)
        {
            if (DateTimeOffset.TryParse(occurredAtRaw, out var parsed))
            {
                occurredAt = parsed;
            }
            else
            {
                errors.Add($"'{occurredAtRaw}' is not a valid date/time value.");
            }
        }

        return new NormalizedImportRow
        {
            RowIndex = rowIndex,
            Email = email,
            FullName = fullName,
            OccurredAt = occurredAt,
            Notes = TrimToNull(values.GetValueOrDefault(ImportTargetField.Notes)),
            Intent = TrimToNull(values.GetValueOrDefault(ImportTargetField.Intent)),
            Goals = TrimToNull(values.GetValueOrDefault(ImportTargetField.Goals)),
            ExperienceLevel = TrimToNull(values.GetValueOrDefault(ImportTargetField.ExperienceLevel)),
            DietaryRequirements = TrimToNull(values.GetValueOrDefault(ImportTargetField.DietaryRequirements)),
            AccessibilityNeeds = TrimToNull(values.GetValueOrDefault(ImportTargetField.AccessibilityNeeds)),
            ExternalRecordId = TrimToNull(values.GetValueOrDefault(ImportTargetField.ExternalRecordId)),
            ValidationErrors = errors
        };
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email);
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void FlagDuplicateEmails(List<NormalizedImportRow> rows)
    {
        var duplicateEmails = rows
            .Where(row => row.Email is not null)
            .GroupBy(row => row.Email!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (duplicateEmails.Count == 0)
        {
            return;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Email is not null && duplicateEmails.Contains(row.Email))
            {
                row.ValidationErrors.Add($"Email '{row.Email}' appears more than once in this file.");
            }
        }
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

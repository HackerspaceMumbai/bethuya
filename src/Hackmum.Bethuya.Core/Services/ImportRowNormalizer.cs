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
    // Mirrors the HasMaxLength(...) database constraints in RegistrationConfiguration and
    // ParticipationLedgerEntryConfiguration. A row that looks valid here but exceeds these
    // limits would otherwise pass Dry Run only to fail later, at commit time, against the
    // database — so these are enforced as validation errors before IsValid is ever set.
    private const int EmailMaxLength = 200;
    private const int FullNameMaxLength = 200;
    private const int IntentMaxLength = 4000;
    private const int GoalsMaxLength = 1000;
    private const int ExperienceLevelMaxLength = 50;
    private const int DietaryRequirementsMaxLength = 500;
    private const int AccessibilityNeedsMaxLength = 1000;
    private const int ExternalRecordIdMaxLength = 200;

    // Notes is persisted as Registration.Bio (max 2000) for registrations, or as
    // ParticipationLedgerEntry.Evidence (max 600) for attendance.
    private const int RegistrationNotesMaxLength = 2000;
    private const int AttendanceNotesMaxLength = 600;

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
        else if (email.Length > EmailMaxLength)
        {
            errors.Add($"Email must be {EmailMaxLength} characters or fewer.");
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
        CheckLength(fullName, FullNameMaxLength, "Full name", errors);

        var notes = TrimToNull(values.GetValueOrDefault(ImportTargetField.Notes));
        CheckLength(
            notes,
            importKind == ImportKind.Registration ? RegistrationNotesMaxLength : AttendanceNotesMaxLength,
            "Notes",
            errors);

        var intent = TrimToNull(values.GetValueOrDefault(ImportTargetField.Intent));
        CheckLength(intent, IntentMaxLength, "Intent", errors);

        var goals = TrimToNull(values.GetValueOrDefault(ImportTargetField.Goals));
        CheckLength(goals, GoalsMaxLength, "Goals", errors);

        var experienceLevel = TrimToNull(values.GetValueOrDefault(ImportTargetField.ExperienceLevel));
        CheckLength(experienceLevel, ExperienceLevelMaxLength, "Experience level", errors);

        var dietaryRequirements = TrimToNull(values.GetValueOrDefault(ImportTargetField.DietaryRequirements));
        CheckLength(dietaryRequirements, DietaryRequirementsMaxLength, "Dietary requirements", errors);

        var accessibilityNeeds = TrimToNull(values.GetValueOrDefault(ImportTargetField.AccessibilityNeeds));
        CheckLength(accessibilityNeeds, AccessibilityNeedsMaxLength, "Accessibility needs", errors);

        var externalRecordId = TrimToNull(values.GetValueOrDefault(ImportTargetField.ExternalRecordId));
        CheckLength(externalRecordId, ExternalRecordIdMaxLength, "External record id", errors);

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
            Notes = notes,
            Intent = intent,
            Goals = goals,
            ExperienceLevel = experienceLevel,
            DietaryRequirements = dietaryRequirements,
            AccessibilityNeeds = accessibilityNeeds,
            ExternalRecordId = externalRecordId,
            ValidationErrors = errors
        };
    }

    /// <summary>Adds a validation error if <paramref name="value"/> exceeds the given persistence limit.</summary>
    private static void CheckLength(string? value, int maxLength, string fieldLabel, List<string> errors)
    {
        if (value is not null && value.Length > maxLength)
        {
            errors.Add($"{fieldLabel} must be {maxLength} characters or fewer.");
        }
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

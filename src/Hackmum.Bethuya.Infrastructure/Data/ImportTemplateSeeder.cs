using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Data;

/// <summary>
/// Idempotently seeds the built-in System <see cref="ImportTemplate"/> rows (Luma and MLH,
/// Registration and Attendance) so organizers have a ready-to-use mapping for the platforms
/// most Bethuya-supported events already use. System templates are configuration data, not
/// hardcoded parser logic, so format changes do not require code deployments.
/// </summary>
public static class ImportTemplateSeeder
{
    public static async Task EnsureSeededAsync(BethuyaDbContext db, CancellationToken ct = default)
    {
        var existingNames = await db.ImportTemplates
            .Where(t => t.Scope == ImportTemplateScope.System)
            .Select(t => t.Name)
            .ToListAsync(ct);
        var existing = existingNames.ToHashSet(StringComparer.Ordinal);

        foreach (var template in BuildSystemTemplates())
        {
            if (!existing.Contains(template.Name))
            {
                db.ImportTemplates.Add(template);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static IEnumerable<ImportTemplate> BuildSystemTemplates()
    {
        yield return CreateTemplate(
            "Luma Standard Registration Export",
            ImportSourceKind.Luma,
            ImportKind.Registration,
            [
                ("Name", ImportTargetField.FullName),
                ("Email", ImportTargetField.Email),
                ("Registered At", ImportTargetField.OccurredAt),
            ]);

        yield return CreateTemplate(
            "Luma Attendance Export",
            ImportSourceKind.Luma,
            ImportKind.Attendance,
            [
                ("Name", ImportTargetField.FullName),
                ("Email", ImportTargetField.Email),
                ("Check-in Time", ImportTargetField.OccurredAt),
            ]);

        yield return CreateTemplate(
            "MLH Registration Export",
            ImportSourceKind.MLH,
            ImportKind.Registration,
            [
                ("Full Name", ImportTargetField.FullName),
                ("Email Address", ImportTargetField.Email),
                ("Application Date", ImportTargetField.OccurredAt),
                ("School", ImportTargetField.Notes),
            ]);

        yield return CreateTemplate(
            "MLH Attendance Export",
            ImportSourceKind.MLH,
            ImportKind.Attendance,
            [
                ("Full Name", ImportTargetField.FullName),
                ("Email Address", ImportTargetField.Email),
                ("Checked In At", ImportTargetField.OccurredAt),
            ]);
    }

    private static ImportTemplate CreateTemplate(
        string name,
        ImportSourceKind sourceKind,
        ImportKind importKind,
        IReadOnlyList<(string SourceColumnName, ImportTargetField TargetField)> mappings)
    {
        var template = new ImportTemplate
        {
            Name = name,
            Scope = ImportTemplateScope.System,
            SourceKind = sourceKind,
            ImportKind = importKind,
            OwnerUserId = null
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

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
        var existingTemplates = await db.ImportTemplates
            .Include(t => t.ColumnMappings)
            .Where(t => t.Scope == ImportTemplateScope.System)
            .ToListAsync(ct);

        foreach (var template in BuildSystemTemplates())
        {
            var existing = existingTemplates.SingleOrDefault(t => t.Name == template.Name);
            if (existing is null)
            {
                db.ImportTemplates.Add(template);
            }
            else if (existing.ColumnMappings.Count != template.ColumnMappings.Count ||
                 template.ColumnMappings.Any(mapping => !existing.ColumnMappings.Any(current =>
                     current.SourceColumnName == mapping.SourceColumnName && current.TargetField == mapping.TargetField)))
            {
                db.ImportColumnMappings.RemoveRange(existing.ColumnMappings);
                foreach (var mapping in template.ColumnMappings)
                {
                    db.ImportColumnMappings.Add(new ImportColumnMapping
                    {
                        ImportTemplateId = existing.Id,
                        SourceColumnName = mapping.SourceColumnName,
                        TargetField = mapping.TargetField
                    });
                }
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
                ("name", ImportTargetField.FullName),
                ("email", ImportTargetField.Email),
                ("created_at", ImportTargetField.OccurredAt),
                ("approval_status", ImportTargetField.ApprovalStatus),
            ]);

        yield return CreateTemplate(
            "Luma Attendance Export",
            ImportSourceKind.Luma,
            ImportKind.Attendance,
            [
                ("name", ImportTargetField.FullName),
                ("email", ImportTargetField.Email),
                ("checked_in_at", ImportTargetField.CheckedInAt),
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
                ("Checked In At", ImportTargetField.CheckedInAt),
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

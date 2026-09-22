using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>A single source column -&gt; target field mapping supplied when creating/editing a template.</summary>
public sealed record ImportColumnMappingInput(string SourceColumnName, ImportTargetField TargetField);

/// <summary>
/// CRUD for <see cref="ImportTemplate"/>s. System templates (seeded for Luma/MLH) are read-only;
/// organizers clone them into an editable User template. User templates are editable by their
/// owner or an Admin — enforced here since <c>RequireOrganizer</c> alone does not distinguish
/// ownership. Authorization for "is this caller an Admin" is passed in by the caller (endpoint),
/// which already has the <see cref="System.Security.Claims.ClaimsPrincipal"/>.
/// </summary>
public sealed class ImportTemplateService(BethuyaDbContext db)
{
    public async Task<IReadOnlyList<ImportTemplate>> ListAsync(
        string requestingUserId,
        ImportKind? importKind = null,
        bool requestingUserIsAdmin = false,
        CancellationToken ct = default)
    {
        var query = db.ImportTemplates
            .Include(t => t.ColumnMappings)
            .Where(t => requestingUserIsAdmin ||
                t.Scope == ImportTemplateScope.System ||
                t.OwnerUserId == requestingUserId);

        if (importKind is not null)
        {
            query = query.Where(t => t.ImportKind == importKind);
        }

        return await query
            .OrderBy(t => t.Scope)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);
    }

    public async Task<ImportTemplate> GetAsync(Guid templateId, CancellationToken ct = default)
        => await db.ImportTemplates
            .Include(t => t.ColumnMappings)
            .SingleOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new InvalidOperationException($"Import template '{templateId}' was not found.");

    public async Task<ImportTemplate> GetForUserAsync(
        Guid templateId,
        string requestingUserId,
        bool requestingUserIsAdmin,
        CancellationToken ct = default)
    {
        var template = await GetAsync(templateId, ct);
        if (template.Scope == ImportTemplateScope.System ||
            requestingUserIsAdmin ||
            string.Equals(template.OwnerUserId, requestingUserId, StringComparison.Ordinal))
        {
            return template;
        }

        throw new UnauthorizedAccessException("Only the template's owner or an Admin can access it.");
    }

    public async Task<ImportTemplate> CreateAsync(
        string name,
        ImportSourceKind sourceKind,
        ImportKind importKind,
        string ownerUserId,
        IReadOnlyList<ImportColumnMappingInput> mappings,
        CancellationToken ct = default)
    {
        var template = new ImportTemplate
        {
            Name = name,
            Scope = ImportTemplateScope.User,
            SourceKind = sourceKind,
            ImportKind = importKind,
            OwnerUserId = ownerUserId
        };

        ApplyMappings(db, template, mappings);

        db.ImportTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return template;
    }

    /// <summary>Clones any template (System or User) into a new, independently-editable User template owned by the caller.</summary>
    public async Task<ImportTemplate> CloneAsync(
        Guid sourceTemplateId,
        string ownerUserId,
        bool requestingUserIsAdmin,
        string? newName = null,
        CancellationToken ct = default)
    {
        var source = await GetForUserAsync(sourceTemplateId, ownerUserId, requestingUserIsAdmin, ct);

        var clone = new ImportTemplate
        {
            Name = newName ?? $"{source.Name} (copy)",
            Scope = ImportTemplateScope.User,
            SourceKind = source.SourceKind,
            ImportKind = source.ImportKind,
            OwnerUserId = ownerUserId,
            ClonedFromTemplateId = source.Id
        };

        foreach (var mapping in source.ColumnMappings)
        {
            clone.ColumnMappings.Add(new ImportColumnMapping
            {
                ImportTemplateId = clone.Id,
                SourceColumnName = mapping.SourceColumnName,
                TargetField = mapping.TargetField
            });
        }

        db.ImportTemplates.Add(clone);
        await db.SaveChangesAsync(ct);
        return clone;
    }

    public async Task<ImportTemplate> UpdateAsync(
        Guid templateId,
        string requestingUserId,
        bool requestingUserIsAdmin,
        string name,
        IReadOnlyList<ImportColumnMappingInput> mappings,
        CancellationToken ct = default)
    {
        await ImportMutationGate.Instance.WaitAsync(ct);
        try
        {
            var template = await GetAsync(templateId, ct);

            if (template.Scope == ImportTemplateScope.System)
            {
                throw new InvalidOperationException("System templates are read-only. Clone this template to customize it.");
            }

            if (!requestingUserIsAdmin && !string.Equals(template.OwnerUserId, requestingUserId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Only the template's owner or an Admin can edit it.");
            }

            if (await db.ImportBatches.AnyAsync(
                batch => batch.ImportTemplateId == templateId &&
                    batch.Status == ImportBatchStatus.Committed,
                ct))
            {
                throw new InvalidOperationException(
                    "Templates referenced by a committed import batch are immutable. Clone the template to make changes.");
            }

            template.Name = name;
            template.UpdatedAt = DateTimeOffset.UtcNow;

        // Remove/add mappings directly through the DbSet rather than mutating the
        // template.ColumnMappings navigation collection. Touching that collection while an old
        // mapping is simultaneously marked Deleted triggers a relationship-fixup "reference
        // changed" notification on the same entity, which throws a DbUpdateConcurrencyException
        // against the InMemory provider.
            db.ImportColumnMappings.RemoveRange(template.ColumnMappings.ToList());
            ApplyMappings(db, template, mappings);

            await db.SaveChangesAsync(ct);
            return template;
        }
        finally
        {
            ImportMutationGate.Instance.Release();
        }
    }

    private static void ApplyMappings(BethuyaDbContext db, ImportTemplate template, IReadOnlyList<ImportColumnMappingInput> mappings)
    {
        var duplicateTarget = mappings
            .GroupBy(mapping => mapping.TargetField)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTarget is not null)
        {
            throw new InvalidOperationException(
                $"Only one source column can map to '{duplicateTarget.Key}'.");
        }

        foreach (var mapping in mappings)
        {
            db.ImportColumnMappings.Add(new ImportColumnMapping
            {
                ImportTemplateId = template.Id,
                SourceColumnName = mapping.SourceColumnName,
                TargetField = mapping.TargetField
            });
        }
    }
}

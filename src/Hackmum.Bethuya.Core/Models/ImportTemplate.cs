using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// A reusable column-mapping configuration for importing a CSV/XLSX export into Bethuya.
/// System templates (seeded for Luma/MLH) are read-only; organizers clone them into editable
/// User templates. Phase 1 lifecycle is Create/Clone/Edit/Save only — no version history.
/// </summary>
public sealed class ImportTemplate
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public ImportTemplateScope Scope { get; set; } = ImportTemplateScope.User;
    public ImportSourceKind SourceKind { get; set; } = ImportSourceKind.Custom;
    public ImportKind ImportKind { get; set; } = ImportKind.Registration;

    /// <summary>
    /// Owning organizer's stable user id. Null for System-scoped templates.
    /// User templates are editable by their owner or an Admin.
    /// </summary>
    public string? OwnerUserId { get; set; }

    /// <summary>
    /// The template this one was cloned from, if any (for traceability only; not a version chain).
    /// </summary>
    public Guid? ClonedFromTemplateId { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ImportColumnMapping> ColumnMappings { get; init; } = [];
}

using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Maps one source column header to a canonical <see cref="ImportTargetField"/> for a given
/// <see cref="ImportTemplate"/>.
/// </summary>
public sealed class ImportColumnMapping
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ImportTemplateId { get; init; }
    public required string SourceColumnName { get; set; }
    public ImportTargetField TargetField { get; set; }

    public ImportTemplate? ImportTemplate { get; init; }
}

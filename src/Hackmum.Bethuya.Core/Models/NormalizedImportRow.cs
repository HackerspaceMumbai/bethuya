namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// A single import row after column mapping and field-level validation, but before comparing
/// against existing Bethuya data (member/registration diffing happens in the dry-run service).
/// </summary>
public sealed record NormalizedImportRow
{
    public required int RowIndex { get; init; }
    public string? Email { get; init; }
    public string? FullName { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
    public string? Notes { get; init; }
    public string? Intent { get; init; }
    public string? Goals { get; init; }
    public string? ExperienceLevel { get; init; }
    public string? DietaryRequirements { get; init; }
    public string? AccessibilityNeeds { get; init; }
    public string? ExternalRecordId { get; init; }

    public List<string> ValidationErrors { get; init; } = [];

    public bool IsValid => ValidationErrors.Count == 0;
}

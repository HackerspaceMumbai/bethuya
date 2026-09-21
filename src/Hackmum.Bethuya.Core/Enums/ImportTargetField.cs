namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Canonical fields an import column can be mapped to. Not every field applies to every
/// <see cref="ImportKind"/>; <see cref="Services.ImportRowNormalizer"/> enforces which fields
/// are required per kind.
/// </summary>
public enum ImportTargetField
{
    /// <summary>Required for every import kind; the identity-resolution key.</summary>
    Email,
    /// <summary>Required for Registration imports; used as the display name for new members.</summary>
    FullName,
    /// <summary>Optional. When absent, defaults to the time the row is processed.</summary>
    OccurredAt,
    /// <summary>Free-form note. Registration: bio. Attendance: check-in/evidence note.</summary>
    Notes,
    Intent,
    Goals,
    ExperienceLevel,
    DietaryRequirements,
    AccessibilityNeeds,
    /// <summary>Optional external record identifier from the source platform, used for provenance.</summary>
    ExternalRecordId
}

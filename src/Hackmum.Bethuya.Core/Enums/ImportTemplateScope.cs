namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Ownership/editability scope of an <see cref="Models.ImportTemplate"/>. System templates are
/// seeded, read-only, and shared; User templates are organizer-created (optionally cloned from
/// a System or another User template) and editable by their owner or an Admin.
/// </summary>
public enum ImportTemplateScope
{
    System,
    User
}

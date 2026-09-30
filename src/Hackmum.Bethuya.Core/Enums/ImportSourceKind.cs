namespace Hackmum.Bethuya.Core.Enums;

/// <summary>Identifies which external export format an <see cref="Models.ImportTemplate"/> targets.</summary>
public enum ImportSourceKind
{
    Luma,
    MLH,
    /// <summary>Organizer-defined mapping for a sponsor sheet, spreadsheet, or any other export.</summary>
    Custom
}

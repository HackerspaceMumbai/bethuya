namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Per-row disposition shown to an organizer in a Dry Run preview.
/// </summary>
public enum ImportRowDisposition
{
    /// <summary>Row is valid and its email does not match an existing Bethuya member/registration; a new record will be created.</summary>
    WillCreate,
    /// <summary>Row is valid and its email matches an existing Bethuya member/registration; that record will be updated.</summary>
    WillUpdate,
    /// <summary>Row failed validation and will block commit until fixed.</summary>
    Error
}

/// <summary>One row's contribution to an <see cref="ImportPreviewReport"/>.</summary>
public sealed record ImportRowPreview(
    int RowIndex,
    string? Email,
    ImportRowDisposition Disposition,
    IReadOnlyList<string> ValidationErrors);

/// <summary>
/// The Dry Run result an organizer reviews before committing: totals, validation errors,
/// duplicate-email-in-file rows, and records to be created vs. updated. Never mutates
/// production data.
/// </summary>
public sealed record ImportPreviewReport(
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    int RowsToCreate,
    int RowsToUpdate,
    IReadOnlyList<ImportRowPreview> Rows)
{
    /// <summary>Commit may only proceed when this is true (zero validation errors across all rows).</summary>
    public bool CanCommit => TotalRows > 0 && ErrorRows == 0;
}

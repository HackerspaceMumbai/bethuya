using Refit;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Refit-generated typed HTTP client for the Bethuya Registration/Attendance Import API.</summary>
public interface IImportApi
{
    [Multipart]
    [Post("/api/import/batches")]
    Task<ImportBatchDto> UploadAndRunDryRunAsync(
        [AliasAs("eventId")] Guid eventId,
        [AliasAs("importTemplateId")] Guid importTemplateId,
        [AliasAs("importKind")] string importKind,
        [AliasAs("file")] StreamPart file,
        CancellationToken ct = default);

    [Post("/api/import/batches/{importBatchId}/dry-run")]
    Task<ImportBatchDto> RerunDryRunAsync(Guid importBatchId, CancellationToken ct = default);

    [Get("/api/import/batches/{importBatchId}")]
    Task<ImportBatchDto> GetBatchAsync(Guid importBatchId, CancellationToken ct = default);

    [Get("/api/import/batches/{importBatchId}/preview")]
    Task<ImportPreviewReportDto> GetPreviewAsync(Guid importBatchId, CancellationToken ct = default);

    [Post("/api/import/batches/{importBatchId}/commit")]
    Task<ImportBatchDto> CommitAsync(Guid importBatchId, CancellationToken ct = default);

    [Get("/api/import/events/{eventId}/batches")]
    Task<List<ImportBatchDto>> ListBatchesForEventAsync(Guid eventId, CancellationToken ct = default);

    [Get("/api/import/templates")]
    Task<List<ImportTemplateDto>> ListTemplatesAsync([Query] string? importKind = null, CancellationToken ct = default);
}

/// <summary>Import batch status/progress returned from the API.</summary>
public sealed record ImportBatchDto(
    Guid Id,
    Guid EventId,
    string ImportKind,
    Guid ImportTemplateId,
    ImportBatchStatusDto Status,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    int RowsToCreate,
    int RowsToUpdate,
    string? FailureReason,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DryRunCompletedAt,
    DateTimeOffset? CommittedAt,
    string? FileName)
{
    /// <summary>Whether the Dry Run preview reports zero validation errors, so Commit is allowed.</summary>
    public bool CanCommit => Status == ImportBatchStatusDto.DryRunCompleted && TotalRows > 0 && ErrorRows == 0;
}

/// <summary>Lifecycle status of an import batch.</summary>
public enum ImportBatchStatusDto
{
    Draft,
    DryRunCompleted,
    Committed,
    Failed
}

/// <summary>Import template returned from the API (System templates are read-only).</summary>
public sealed record ImportTemplateDto(
    Guid Id,
    string Name,
    string Scope,
    string SourceKind,
    string ImportKind,
    string? OwnerUserId,
    Guid? ClonedFromTemplateId,
    List<ImportColumnMappingDto> ColumnMappings);

public sealed record ImportColumnMappingDto(string SourceColumnName, string TargetField);

/// <summary>Per-row disposition in a Dry Run preview.</summary>
public sealed record ImportRowPreviewDto(
    int RowIndex,
    string? Email,
    string Disposition,
    List<string> ValidationErrors);

/// <summary>Dry Run result an organizer reviews before committing.</summary>
public sealed record ImportPreviewReportDto(
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    int RowsToCreate,
    int RowsToUpdate,
    List<ImportRowPreviewDto> Rows);

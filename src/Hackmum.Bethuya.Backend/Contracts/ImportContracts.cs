using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Backend.Contracts;

/// <summary>Response shape for an <see cref="ImportBatch"/>, safe to return to the organizer UI.</summary>
public sealed record ImportBatchResponse(
    Guid Id,
    Guid EventId,
    ImportKind ImportKind,
    Guid ImportTemplateId,
    ImportBatchStatus Status,
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
    public static ImportBatchResponse FromModel(ImportBatch batch, string? fileName = null) => new(
        batch.Id,
        batch.EventId,
        batch.ImportKind,
        batch.ImportTemplateId,
        batch.Status,
        batch.TotalRows,
        batch.ValidRows,
        batch.ErrorRows,
        batch.RowsToCreate,
        batch.RowsToUpdate,
        batch.FailureReason,
        batch.CreatedByUserId,
        batch.CreatedAt,
        batch.DryRunCompletedAt,
        batch.CommittedAt,
        fileName ?? batch.ImportArtifact?.FileName);
}

/// <summary>Response shape for an <see cref="ImportTemplate"/>.</summary>
public sealed record ImportTemplateResponse(
    Guid Id,
    string Name,
    ImportTemplateScope Scope,
    ImportSourceKind SourceKind,
    ImportKind ImportKind,
    string? OwnerUserId,
    Guid? ClonedFromTemplateId,
    IReadOnlyList<ImportColumnMappingResponse> ColumnMappings)
{
    public static ImportTemplateResponse FromModel(ImportTemplate template) => new(
        template.Id,
        template.Name,
        template.Scope,
        template.SourceKind,
        template.ImportKind,
        template.OwnerUserId,
        template.ClonedFromTemplateId,
        template.ColumnMappings
            .Select(m => new ImportColumnMappingResponse(m.SourceColumnName, m.TargetField))
            .ToList());
}

public sealed record ImportColumnMappingResponse(string SourceColumnName, ImportTargetField TargetField);

public sealed record CreateImportTemplateRequest(
    string Name,
    ImportSourceKind SourceKind,
    ImportKind ImportKind,
    IReadOnlyList<ImportColumnMappingResponse> ColumnMappings);

public sealed record UpdateImportTemplateRequest(
    string Name,
    IReadOnlyList<ImportColumnMappingResponse> ColumnMappings);

public sealed record CloneImportTemplateRequest(string? NewName);

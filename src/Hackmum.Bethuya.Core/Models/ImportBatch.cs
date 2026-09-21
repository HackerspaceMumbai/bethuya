using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// One import attempt: an uploaded (or replayed) file, scoped to one Bethuya event, parsed and
/// validated (Dry Run), then optionally committed. Status only moves forward: Draft -&gt;
/// DryRunCompleted -&gt; Committed | Failed. Commit is only reachable from DryRunCompleted with
/// zero validation errors, and is atomic (all rows or none).
/// </summary>
public sealed class ImportBatch
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public ImportKind ImportKind { get; init; }
    public Guid ImportTemplateId { get; init; }
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Draft;

    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorRows { get; set; }
    public int RowsToCreate { get; set; }
    public int RowsToUpdate { get; set; }

    public string? FailureReason { get; set; }

    public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DryRunCompletedAt { get; set; }
    public DateTimeOffset? CommittedAt { get; set; }

    public Event? Event { get; init; }
    public ImportTemplate? ImportTemplate { get; init; }
    public ImportArtifact? ImportArtifact { get; init; }
    public List<ImportRawRow> RawRows { get; init; } = [];
}

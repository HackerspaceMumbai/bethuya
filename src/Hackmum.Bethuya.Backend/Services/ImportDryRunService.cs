using System.Security.Cryptography;
using System.Text.Json;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Runs Dry Run validation for a CSV/XLSX import: parses and persists the original file
/// (an <see cref="ImportArtifact"/>) and its rows (<see cref="ImportRawRow"/>), applies the
/// selected <see cref="ImportTemplate"/>'s column mappings, and reports what would happen on
/// commit. Never writes to <see cref="Registration"/> or <see cref="ParticipationLedgerEntry"/>.
/// </summary>
public sealed class ImportDryRunService(
    BethuyaDbContext db,
    ImportFileParserResolver parserResolver,
    IImportArtifactStore artifactStore)
{
    /// <summary>Uploads a new file and runs its first Dry Run, creating a new <see cref="ImportBatch"/>.</summary>
    public async Task<ImportBatch> StartAsync(
        Guid eventId,
        Guid importTemplateId,
        ImportKind importKind,
        string fileName,
        string contentType,
        byte[] fileBytes,
        string createdByUserId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        var template = await LoadTemplateAsync(importTemplateId, ct);
        if (template.ImportKind != importKind)
        {
            throw new InvalidOperationException(
                $"Template '{template.Name}' is configured for {template.ImportKind} imports, not {importKind}.");
        }

        var eventExists = await db.Events.AnyAsync(e => e.Id == eventId, ct);
        if (!eventExists)
        {
            throw new InvalidOperationException($"Event '{eventId}' was not found.");
        }

        var parser = parserResolver.Resolve(fileName);
        using var contentStream = new MemoryStream(fileBytes, writable: false);
        var parsed = parser.Parse(contentStream);

        var storageKey = await artifactStore.SaveAsync(fileBytes, fileName, ct);

        try
        {
            var checksum = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();

            var batch = new ImportBatch
            {
                EventId = eventId,
                ImportKind = importKind,
                ImportTemplateId = importTemplateId,
                CreatedByUserId = createdByUserId
            };

            var artifact = new ImportArtifact
            {
                ImportBatchId = batch.Id,
                FileName = fileName,
                StorageKey = storageKey,
                ContentType = contentType,
                SizeBytes = fileBytes.LongLength,
                Sha256Checksum = checksum
            };

            var rawRows = parsed.Rows
                .Select((row, index) => new ImportRawRow
                {
                    ImportBatchId = batch.Id,
                    RowIndex = index,
                    RawDataJson = JsonSerializer.Serialize(row)
                })
                .ToList();

            db.ImportBatches.Add(batch);
            db.ImportArtifacts.Add(artifact);
            db.ImportRawRows.AddRange(rawRows);

            await ApplyDryRunAsync(batch, template, parsed.Rows, ct);
            await db.SaveChangesAsync(ct);

            return batch;
        }
        catch
        {
            // The artifact bytes were already persisted to storage before the database write.
            // If anything after that write fails, the file would otherwise be orphaned with no
            // ImportArtifact record and no audit trail. Clean it up before propagating.
            await artifactStore.DeleteAsync(storageKey, ct);
            throw;
        }
    }

    /// <summary>
    /// Re-validates an existing batch's already-persisted raw rows (e.g. after editing the
    /// underlying template's mappings). Does not re-upload or re-parse the original file.
    /// </summary>
    public async Task<ImportBatch> RerunAsync(Guid importBatchId, CancellationToken ct = default)
    {
        var batch = await db.ImportBatches
            .Include(b => b.RawRows)
            .SingleOrDefaultAsync(b => b.Id == importBatchId, ct)
            ?? throw new InvalidOperationException($"Import batch '{importBatchId}' was not found.");

        if (batch.Status == ImportBatchStatus.Committed)
        {
            throw new InvalidOperationException(
                "This import has already been committed and its data is locked. Start a new import to make changes.");
        }

        var template = await LoadTemplateAsync(batch.ImportTemplateId, ct);
        var rows = DeserializeRawRows(batch.RawRows);

        await ApplyDryRunAsync(batch, template, rows, ct);
        await db.SaveChangesAsync(ct);

        return batch;
    }

    /// <summary>Recomputes the full per-row preview for an existing batch, without mutating it.</summary>
    public async Task<ImportPreviewReport> GetPreviewAsync(Guid importBatchId, CancellationToken ct = default)
    {
        var batch = await db.ImportBatches
            .Include(b => b.RawRows)
            .SingleOrDefaultAsync(b => b.Id == importBatchId, ct)
            ?? throw new InvalidOperationException($"Import batch '{importBatchId}' was not found.");

        var template = await LoadTemplateAsync(batch.ImportTemplateId, ct);
        var rows = DeserializeRawRows(batch.RawRows);
        var normalizedRows = ImportRowNormalizer.NormalizeAll(new ParsedImportFile([], rows), template);

        return await ComputePreviewAsync(batch.EventId, batch.ImportKind, normalizedRows, ct);
    }

    private static List<IReadOnlyDictionary<string, string?>> DeserializeRawRows(IEnumerable<ImportRawRow> rawRows)
        => rawRows
            .OrderBy(r => r.RowIndex)
            .Select(r => (IReadOnlyDictionary<string, string?>)(
                JsonSerializer.Deserialize<Dictionary<string, string?>>(r.RawDataJson) ?? []))
            .ToList();

    private async Task<ImportTemplate> LoadTemplateAsync(Guid templateId, CancellationToken ct)
        => await db.ImportTemplates
            .Include(t => t.ColumnMappings)
            .SingleOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new InvalidOperationException($"Import template '{templateId}' was not found.");

    private async Task ApplyDryRunAsync(
        ImportBatch batch,
        ImportTemplate template,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rawRows,
        CancellationToken ct)
    {
        var normalizedRows = ImportRowNormalizer.NormalizeAll(new ParsedImportFile([], rawRows), template);
        var preview = await ComputePreviewAsync(batch.EventId, batch.ImportKind, normalizedRows, ct);

        batch.TotalRows = preview.TotalRows;
        batch.ValidRows = preview.ValidRows;
        batch.ErrorRows = preview.ErrorRows;
        batch.RowsToCreate = preview.RowsToCreate;
        batch.RowsToUpdate = preview.RowsToUpdate;
        batch.Status = ImportBatchStatus.DryRunCompleted;
        batch.DryRunCompletedAt = DateTimeOffset.UtcNow;
        batch.FailureReason = null;
    }

    /// <summary>
    /// Compares valid rows against existing Bethuya data to classify each as a create or update.
    /// For Registration imports this checks <see cref="Registration"/> by (event, email). For
    /// Attendance imports — an append-only ledger — "update" instead means an attendance record
    /// already exists for that person and event, so committing this row would be a no-op.
    /// </summary>
    private async Task<ImportPreviewReport> ComputePreviewAsync(
        Guid eventId,
        ImportKind importKind,
        IReadOnlyList<NormalizedImportRow> normalizedRows,
        CancellationToken ct)
    {
        var validEmails = normalizedRows
            .Where(row => row.IsValid)
            .Select(row => row.Email!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        HashSet<string> existingKeys;
        if (importKind == ImportKind.Registration)
        {
            existingKeys = validEmails.Length == 0
                ? []
                : (await db.Registrations
                    .Where(r => r.EventId == eventId)
                    .Select(r => r.Email)
                    .ToListAsync(ct))
                    .Select(email => email.Trim().ToLowerInvariant())
                    .Where(email => validEmails.Contains(email))
                    .ToHashSet(StringComparer.Ordinal);
        }
        else
        {
            var provenanceKeys = validEmails
                .Select(email => ImportProvenanceKeyBuilder.BuildAttendanceProvenanceKey(eventId, email))
                .ToArray();
            existingKeys = provenanceKeys.Length == 0
                ? []
                : (await db.ParticipationLedgerEntries
                    .Where(e => e.EventId == eventId && provenanceKeys.Contains(e.ProvenanceKey))
                    .Select(e => e.ProvenanceKey)
                    .ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);
        }

        var rowPreviews = new List<ImportRowPreview>(normalizedRows.Count);
        var createCount = 0;
        var updateCount = 0;

        foreach (var row in normalizedRows)
        {
            if (!row.IsValid)
            {
                rowPreviews.Add(new ImportRowPreview(row.RowIndex, row.Email, ImportRowDisposition.Error, row.ValidationErrors));
                continue;
            }

            var matchKey = importKind == ImportKind.Registration
                ? row.Email!
                : ImportProvenanceKeyBuilder.BuildAttendanceProvenanceKey(eventId, row.Email!);

            if (existingKeys.Contains(matchKey))
            {
                updateCount++;
                rowPreviews.Add(new ImportRowPreview(row.RowIndex, row.Email, ImportRowDisposition.WillUpdate, []));
            }
            else
            {
                createCount++;
                rowPreviews.Add(new ImportRowPreview(row.RowIndex, row.Email, ImportRowDisposition.WillCreate, []));
            }
        }

        var errorCount = normalizedRows.Count(row => !row.IsValid);
        var validCount = normalizedRows.Count - errorCount;

        return new ImportPreviewReport(
            TotalRows: normalizedRows.Count,
            ValidRows: validCount,
            ErrorRows: errorCount,
            RowsToCreate: createCount,
            RowsToUpdate: updateCount,
            Rows: rowPreviews);
    }
}

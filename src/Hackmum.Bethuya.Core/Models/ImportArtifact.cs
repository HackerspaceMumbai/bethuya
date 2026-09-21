namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Metadata for the original uploaded import file. Actual bytes are stored behind
/// <see cref="Services.IImportArtifactStore"/>, never in the relational database.
/// </summary>
public sealed class ImportArtifact
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ImportBatchId { get; init; }
    public required string FileName { get; set; }

    /// <summary>Opaque key used to retrieve the file bytes from <see cref="Services.IImportArtifactStore"/>.</summary>
    public required string StorageKey { get; set; }

    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256Checksum { get; set; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;

    public ImportBatch? ImportBatch { get; init; }
}

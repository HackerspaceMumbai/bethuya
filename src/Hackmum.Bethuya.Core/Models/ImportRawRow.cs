namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// One parsed row from an import file's original, source-native form — exactly as parsed,
/// before any column mapping or normalization. Kept separate from normalized/validated values
/// so a batch can be replayed if mapping/normalization rules change later.
/// </summary>
public sealed class ImportRawRow
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ImportBatchId { get; init; }
    public int RowIndex { get; init; }

    /// <summary>JSON object of source column name -&gt; raw cell value (string), as parsed.</summary>
    public required string RawDataJson { get; set; }

    public ImportBatch? ImportBatch { get; init; }
}

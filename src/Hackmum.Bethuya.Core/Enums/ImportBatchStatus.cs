namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Lifecycle status of an <see cref="Models.ImportBatch"/>. A batch only moves forward:
/// Draft -&gt; DryRunCompleted -&gt; Committed. Commit is only reachable from DryRunCompleted
/// with zero validation errors. Failed can be reached from DryRunCompleted if a commit attempt
/// throws.
/// </summary>
public enum ImportBatchStatus
{
    Draft,
    DryRunCompleted,
    Committed,
    Failed
}

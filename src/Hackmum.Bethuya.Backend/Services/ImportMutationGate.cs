namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Serializes import commits and template mutations so a committed batch always retains the
/// mapping version that produced its persisted data.
/// </summary>
/// <remarks>
/// This is a <em>process-local</em> guard only: it coordinates concurrent requests within a
/// single backend instance and does not protect against a template update on one replica
/// racing a commit on another when the backend is scaled out to multiple replicas. Two
/// mitigations keep that gap bounded for this phase:
/// <list type="bullet">
/// <item>
/// <see cref="ImportCommitService"/> re-normalizes every row against the template's *current*
/// mapping at commit time (not the mapping captured during Dry Run), so a mapping change never
/// causes stale-mapped data to be written silently &#8212; a row that becomes invalid under the
/// new mapping fails the commit outright instead.
/// </item>
/// <item>
/// <see cref="ImportTemplateService.UpdateAsync"/> still refuses to edit a template referenced
/// by any Committed batch, so the two operations cannot disagree once either has fully landed
/// &#8212; the residual race window is the brief overlap between a commit's status transition
/// and a concurrent update's read of that status.
/// </item>
/// </list>
/// Replacing this with true cross-replica coordination (e.g. Postgres row locking or an
/// immutable per-batch mapping snapshot) is tracked as follow-up work once the backend runs
/// with more than one replica; it is not required for the current single-replica local/Aspire
/// deployment target.
/// </remarks>
internal static class ImportMutationGate
{
    internal static SemaphoreSlim Instance { get; } = new(1, 1);
}

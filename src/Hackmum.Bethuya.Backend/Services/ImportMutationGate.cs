namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Serializes import commits and template mutations so a committed batch always retains the
/// mapping version that produced its persisted data.
/// </summary>
internal static class ImportMutationGate
{
    internal static SemaphoreSlim Instance { get; } = new(1, 1);
}

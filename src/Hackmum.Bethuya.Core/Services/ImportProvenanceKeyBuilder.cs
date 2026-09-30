namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Builds the deterministic idempotency key used to dedupe imported attendance records against
/// the unified participation ledger. Keyed by event + normalized email (not by import batch), so
/// re-importing the same attendee for the same event in a later batch is recognized as a
/// duplicate rather than a second attendance record.
/// </summary>
public static class ImportProvenanceKeyBuilder
{
    public static string BuildAttendanceProvenanceKey(Guid eventId, string normalizedEmail)
        => $"import:attendance:{eventId:N}:{normalizedEmail}";
}

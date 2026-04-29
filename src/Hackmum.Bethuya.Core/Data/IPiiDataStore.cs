using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Data;

/// <summary>
/// Local-only PII storage for registrants during curation phase.
/// CRITICAL: Data is stored in local SQLite only, never in cloud or main database.
/// All PII is deleted immediately after curation approval.
/// </summary>
public interface IPiiDataStore
{
    /// <summary>Load PII for registrants of an event (local SQLite only).</summary>
    Task<List<Registration>> GetRegistrantsAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Store registrant PII temporarily during curation phase.</summary>
    Task UpsertRegistrantAsync(Registration record, CancellationToken ct = default);

    /// <summary>Delete all PII for an event after curation approval (CRITICAL).</summary>
    Task DeleteEventPiiAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Query by interest tag (for fairness budget computation).</summary>
    Task<List<Registration>> GetByInterestAsync(Guid eventId, string interest, CancellationToken ct = default);

    /// <summary>Verify that all PII for an event has been deleted.</summary>
    Task<bool> IsEventPiiDeletedAsync(Guid eventId, CancellationToken ct = default);
}

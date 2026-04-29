using Hackmum.Bethuya.Core.Data;
using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Infrastructure.Data.PII;

/// <summary>
/// Local SQLite implementation of PII data store.
/// CRITICAL: All PII stays local; deleted immediately after curation approval.
/// </summary>
public sealed partial class PiiDataStore(
    IDbContextFactory<PiiDbContext> contextFactory,
    ILogger<PiiDataStore> logger) : IPiiDataStore
{
    public async Task<List<Registration>> GetRegistrantsAsync(Guid eventId, CancellationToken ct = default)
    {
        using var context = await contextFactory.CreateDbContextAsync(ct);
        var registrants = await context.PiiRegistrations
            .Where(r => r.EventId == eventId)
            .ToListAsync(ct);

        LogRetrievedRegistrants(logger, registrants.Count, eventId);
        return registrants;
    }

    public async Task UpsertRegistrantAsync(Registration record, CancellationToken ct = default)
    {
        using var context = await contextFactory.CreateDbContextAsync(ct);
        var existing = await context.PiiRegistrations
            .FirstOrDefaultAsync(r => r.Id == record.Id, ct);

        if (existing != null)
        {
            context.PiiRegistrations.Remove(existing);
        }

        context.PiiRegistrations.Add(record);
        await context.SaveChangesAsync(ct);

        LogUpsertedRegistrant(logger, record.Id, record.EventId);
    }

    public async Task DeleteEventPiiAsync(Guid eventId, CancellationToken ct = default)
    {
        using var context = await contextFactory.CreateDbContextAsync(ct);
        var entries = await context.PiiRegistrations
            .Where(r => r.EventId == eventId)
            .ToListAsync(ct);

        if (entries.Count == 0)
        {
            LogNoEntriesFound(logger, eventId);
            return;
        }

        context.PiiRegistrations.RemoveRange(entries);
        await context.SaveChangesAsync(ct);

        LogDeletedPii(logger, entries.Count, eventId);
    }

    public async Task<List<Registration>> GetByInterestAsync(Guid eventId, string interest, CancellationToken ct = default)
    {
        using var context = await contextFactory.CreateDbContextAsync(ct);
        var registrants = await context.PiiRegistrations
            .Where(r => r.EventId == eventId && r.Interests.Contains(interest))
            .ToListAsync(ct);

        LogRetrievedByInterest(logger, registrants.Count, interest, eventId);
        return registrants;
    }

    public async Task<bool> IsEventPiiDeletedAsync(Guid eventId, CancellationToken ct = default)
    {
        using var context = await contextFactory.CreateDbContextAsync(ct);
        var count = await context.PiiRegistrations
            .Where(r => r.EventId == eventId)
            .CountAsync(ct);

        return count == 0;
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "[PiiDataStore] Retrieved {Count} registrants for event {EventId}")]
    private static partial void LogRetrievedRegistrants(
        ILogger logger, int count, Guid eventId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "[PiiDataStore] Upserted PII for registrant {RegistrationId} (event {EventId})")]
    private static partial void LogUpsertedRegistrant(
        ILogger logger, Guid registrationId, Guid eventId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[PiiDataStore] No PII entries found for event {EventId} to delete")]
    private static partial void LogNoEntriesFound(
        ILogger logger, Guid eventId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[PiiDataStore] DELETED {Count} PII records for event {EventId} after curation approval")]
    private static partial void LogDeletedPii(
        ILogger logger, int count, Guid eventId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "[PiiDataStore] Retrieved {Count} registrants with interest '{Interest}' for event {EventId}")]
    private static partial void LogRetrievedByInterest(
        ILogger logger, int count, string interest, Guid eventId);
}

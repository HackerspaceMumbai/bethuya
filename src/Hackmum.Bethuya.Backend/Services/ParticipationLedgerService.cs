using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Writes normalized participation entries and projects member timelines from the unified ledger.
/// </summary>
public sealed class ParticipationLedgerService(
    BethuyaDbContext db,
    CommunityPassportService communityPassportService)
{
    private const int MaxTimelineLimit = 200;
    private const int DefaultTimelineLimit = 25;

    public async Task<ParticipationEntryWriteResult> WriteAsync(
        CommunitySubjectContext subject,
        UpsertParticipationEntriesRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entries = request.Entries ?? [];
        var normalizedEntries = entries
            .Select(ToNormalized)
            .Select(ParticipationNormalizationEngine.Normalize)
            .ToArray();

        if (normalizedEntries.Length == 0)
        {
            return new ParticipationEntryWriteResult(ReceivedCount: 0, StoredCount: 0, DuplicateCount: 0);
        }

        var memberId = await EnsureMemberAsync(subject, ct);
        var strategy = db.Database.CreateExecutionStrategy();

        var storedCount = 0;
        var duplicateCount = 0;

        await strategy.ExecuteAsync(async () =>
        {
            var attemptStoredCount = 0;
            var attemptDuplicateCount = 0;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var provenanceKeys = normalizedEntries
                .Select(entry => entry.ProvenanceKey)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var existingKeys = await db.ParticipationLedgerEntries
                .AsNoTracking()
                .Where(entry => provenanceKeys.Contains(entry.ProvenanceKey))
                .Select(entry => entry.ProvenanceKey)
                .ToHashSetAsync(StringComparer.Ordinal, ct);

            List<ParticipationLedgerEntry> pendingEntries = [];
            foreach (var normalized in normalizedEntries)
            {
                if (existingKeys.Contains(normalized.ProvenanceKey))
                {
                    attemptDuplicateCount++;
                    continue;
                }

                pendingEntries.Add(new ParticipationLedgerEntry
                {
                    CommunityMemberId = memberId,
                    Connector = normalized.Connector,
                    ExternalMemberKey = normalized.ExternalMemberKey,
                    EventId = normalized.EventId,
                    ExternalEventId = normalized.ExternalEventId,
                    ExternalRecordId = normalized.ExternalRecordId,
                    Activity = normalized.Activity,
                    Evidence = normalized.Evidence,
                    ProvenanceKey = normalized.ProvenanceKey,
                    SourceCorrelationId = normalized.SourceCorrelationId,
                    OccurredAt = normalized.OccurredAt
                });
                existingKeys.Add(normalized.ProvenanceKey);
            }

            if (pendingEntries.Count > 0)
            {
                try
                {
                    db.ParticipationLedgerEntries.AddRange(pendingEntries);
                    await db.SaveChangesAsync(ct);
                    attemptStoredCount += pendingEntries.Count;
                }
                catch (DbUpdateException)
                {
                    foreach (var pendingEntry in pendingEntries)
                    {
                        db.Entry(pendingEntry).State = EntityState.Detached;
                    }

                    foreach (var pendingEntry in pendingEntries)
                    {
                        db.ParticipationLedgerEntries.Add(pendingEntry);

                        try
                        {
                            await db.SaveChangesAsync(ct);
                            attemptStoredCount++;
                        }
                        catch (DbUpdateException)
                        {
                            db.Entry(pendingEntry).State = EntityState.Detached;

                            var keyExists = await db.ParticipationLedgerEntries
                                .AsNoTracking()
                                .AnyAsync(entry => entry.ProvenanceKey == pendingEntry.ProvenanceKey, ct);
                            if (!keyExists)
                            {
                                throw;
                            }

                            attemptDuplicateCount++;
                        }
                    }
                }
            }

            await transaction.CommitAsync(ct);
            storedCount = attemptStoredCount;
            duplicateCount = attemptDuplicateCount;
        });

        return new ParticipationEntryWriteResult(
            ReceivedCount: normalizedEntries.Length,
            StoredCount: storedCount,
            DuplicateCount: duplicateCount);
    }

    public async Task<MemberParticipationTimelineResponse> ReadTimelineAsync(
        CommunitySubjectContext subject,
        int limit = DefaultTimelineLimit,
        CancellationToken ct = default)
    {
        var constrainedLimit = Math.Clamp(limit, 1, MaxTimelineLimit);
        var memberId = await EnsureMemberAsync(subject, ct);

        var ledgerEntries = await db.ParticipationLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.CommunityMemberId == memberId)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.IngestedAt)
            .Take(constrainedLimit)
            .ToListAsync(ct);

        var eventIds = ledgerEntries
            .Where(entry => entry.EventId.HasValue)
            .Select(entry => entry.EventId!.Value)
            .Distinct()
            .ToArray();

        var eventsById = eventIds.Length == 0
            ? new Dictionary<Guid, Event>()
            : await db.Events
                .AsNoTracking()
                .Where(evt => eventIds.Contains(evt.Id))
                .ToDictionaryAsync(evt => evt.Id, ct);

        var timelineEntries = ledgerEntries
            .Select(entry => new MemberParticipationTimelineEntryResponse(
                EntryId: entry.Id.Value,
                Connector: entry.Connector.ToString(),
                Activity: entry.Activity.ToString(),
                OccurredAt: entry.OccurredAt,
                Evidence: entry.Evidence,
                ProvenanceKey: entry.ProvenanceKey,
                EventId: entry.EventId,
                EventTitle: entry.EventId.HasValue && eventsById.TryGetValue(entry.EventId.Value, out var evt)
                    ? evt.Title
                    : null))
            .ToList();

        return new MemberParticipationTimelineResponse(timelineEntries);
    }

    private static NormalizedParticipationEntry ToNormalized(ParticipationEntryWriteRequest entry)
        => new(
            Connector: entry.Connector,
            ExternalMemberKey: entry.ExternalMemberKey,
            Activity: entry.Activity,
            OccurredAt: entry.OccurredAt,
            Evidence: entry.Evidence,
            ProvenanceKey: entry.ProvenanceKey,
            EventId: entry.EventId,
            ExternalEventId: entry.ExternalEventId,
            ExternalRecordId: entry.ExternalRecordId,
            SourceCorrelationId: entry.SourceCorrelationId);

    private async Task<Hackmum.Bethuya.Core.ValueObjects.CommunityMemberId> EnsureMemberAsync(
        CommunitySubjectContext subject,
        CancellationToken ct)
    {
        _ = await communityPassportService.GetPassportAsync(subject, ct);

        return await db.CommunityMembers
            .AsNoTracking()
            .Where(member => member.UserId == subject.UserId)
            .Select(member => member.Id)
            .SingleAsync(ct);
    }
}

using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Core.ValueObjects;
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
        if (entries.Count == 0)
        {
            return new ParticipationEntryWriteResult(ReceivedCount: 0, StoredCount: 0, DuplicateCount: 0);
        }

        var fallbackMemberId = await EnsureMemberAsync(subject, ct);
        var resolvedEntries = await ResolveEntriesAsync(entries, fallbackMemberId, ct);
        var strategy = db.Database.CreateExecutionStrategy();

        var storedCount = 0;
        var duplicateCount = 0;

        await strategy.ExecuteAsync(async () =>
        {
            var attemptStoredCount = 0;
            var attemptDuplicateCount = 0;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var provenanceKeys = resolvedEntries
                .Select(entry => entry.Normalized.ProvenanceKey)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var connectors = resolvedEntries
                .Select(entry => entry.Normalized.Connector)
                .Distinct()
                .ToArray();
            var memberIds = resolvedEntries
                .Select(entry => entry.MemberId)
                .Distinct()
                .ToArray();

            var existingKeys = await db.ParticipationLedgerEntries
                .AsNoTracking()
                .Where(entry =>
                    provenanceKeys.Contains(entry.ProvenanceKey)
                    && connectors.Contains(entry.Connector)
                    && memberIds.Contains(entry.CommunityMemberId))
                .Select(entry => new DedupeKey(entry.CommunityMemberId, entry.Connector, entry.ProvenanceKey))
                .ToListAsync(ct);
            var existingDedupeKeys = existingKeys.ToHashSet();

            foreach (var resolvedEntry in resolvedEntries)
            {
                var normalized = resolvedEntry.Normalized;
                var dedupeKey = new DedupeKey(resolvedEntry.MemberId, normalized.Connector, normalized.ProvenanceKey);
                if (existingDedupeKeys.Contains(dedupeKey))
                {
                    attemptDuplicateCount++;
                    continue;
                }

                var ledgerEntry = new ParticipationLedgerEntry
                {
                    CommunityMemberId = resolvedEntry.MemberId,
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
                };

                db.ParticipationLedgerEntries.Add(ledgerEntry);

                try
                {
                    await db.SaveChangesAsync(ct);
                    existingDedupeKeys.Add(dedupeKey);
                    attemptStoredCount++;
                }
                catch (DbUpdateException)
                {
                    db.Entry(ledgerEntry).State = EntityState.Detached;

                    var keyExists = await db.ParticipationLedgerEntries
                        .AsNoTracking()
                        .AnyAsync(entry =>
                            entry.CommunityMemberId == resolvedEntry.MemberId
                            && entry.Connector == normalized.Connector
                            && entry.ProvenanceKey == normalized.ProvenanceKey, ct);
                    if (!keyExists)
                    {
                        throw;
                    }

                    existingDedupeKeys.Add(dedupeKey);
                    attemptDuplicateCount++;
                }
            }

            await transaction.CommitAsync(ct);
            storedCount = attemptStoredCount;
            duplicateCount = attemptDuplicateCount;
        });

        return new ParticipationEntryWriteResult(
            ReceivedCount: resolvedEntries.Length,
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

    private async Task<ResolvedParticipationEntry[]> ResolveEntriesAsync(
        IReadOnlyList<ParticipationEntryWriteRequest> entries,
        CommunityMemberId fallbackMemberId,
        CancellationToken ct)
    {
        var normalizedEntries = new List<NormalizedParticipationEntry>(entries.Count);
        var validationErrors = new List<string>();

        for (var index = 0; index < entries.Count; index++)
        {
            try
            {
                var normalized = ParticipationNormalizationEngine.Normalize(ToNormalized(entries[index]));
                normalizedEntries.Add(normalized);
            }
            catch (ArgumentException ex)
            {
                validationErrors.Add($"entries[{index}]: {ex.Message}");
            }
        }

        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(string.Join(' ', validationErrors));
        }

        var resolutions = await ResolveMemberResolutionsAsync(normalizedEntries, fallbackMemberId, ct);
        return normalizedEntries
            .Select(normalized => new ResolvedParticipationEntry(
                normalized,
                resolutions[ToResolutionKey(normalized.Connector, normalized.ExternalMemberKey)]))
            .ToArray();
    }

    private async Task<Dictionary<MemberResolutionKey, CommunityMemberId>> ResolveMemberResolutionsAsync(
        IReadOnlyList<NormalizedParticipationEntry> normalizedEntries,
        CommunityMemberId fallbackMemberId,
        CancellationToken ct)
    {
        var resolutions = normalizedEntries
            .Select(entry => ToResolutionKey(entry.Connector, entry.ExternalMemberKey))
            .Distinct()
            .ToDictionary(key => key, _ => fallbackMemberId);

        var groupedByProvider = normalizedEntries
            .Select(entry => new
            {
                Key = ToResolutionKey(entry.Connector, entry.ExternalMemberKey),
                Provider = TryMapProvider(entry.Connector),
                Variants = GetExternalMemberKeyVariants(entry.ExternalMemberKey)
            })
            .Where(item => item.Provider is not null)
            .GroupBy(item => item.Provider!.Value);

        foreach (var group in groupedByProvider)
        {
            var provider = group.Key;
            var providerLookupKeys = group
                .SelectMany(item => item.Variants)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (providerLookupKeys.Count == 0)
            {
                continue;
            }

            var identities = await db.ExternalIdentities
                .AsNoTracking()
                .Where(identity =>
                    identity.Provider == provider
                    && (providerLookupKeys.Contains(identity.Subject.ToLowerInvariant())
                        || identity.Username != null && providerLookupKeys.Contains(identity.Username.ToLowerInvariant())))
                .Select(identity => new IdentityLink(identity.CommunityMemberId, identity.Subject, identity.Username))
                .ToListAsync(ct);

            var identityLookup = BuildIdentityLookup(identities);

            foreach (var item in group)
            {
                var resolved = item.Variants
                    .Select(variant => identityLookup.TryGetValue(variant, out var memberId) ? memberId : (CommunityMemberId?)null)
                    .FirstOrDefault(memberId => memberId is not null);

                if (resolved is not null)
                {
                    resolutions[item.Key] = resolved.Value;
                }
            }
        }

        return resolutions;
    }

    private static Dictionary<string, CommunityMemberId> BuildIdentityLookup(
        IReadOnlyList<IdentityLink> identities)
    {
        var identityLookup = new Dictionary<string, CommunityMemberId>(StringComparer.Ordinal);

        foreach (var identity in identities)
        {
            foreach (var key in GetExternalMemberKeyVariants((string)identity.Subject))
            {
                TryAddIdentityLookup(identityLookup, key, identity.CommunityMemberId);
            }

            if (identity.Username is string username)
            {
                foreach (var key in GetExternalMemberKeyVariants(username))
                {
                    TryAddIdentityLookup(identityLookup, key, identity.CommunityMemberId);
                }
            }
        }

        return identityLookup;
    }

    private static void TryAddIdentityLookup(
        Dictionary<string, CommunityMemberId> lookup,
        string key,
        CommunityMemberId memberId)
    {
        if (lookup.TryGetValue(key, out var existing) && existing != memberId)
        {
            throw new InvalidOperationException($"External member key '{key}' maps to multiple community members.");
        }

        lookup[key] = memberId;
    }

    private static List<string> GetExternalMemberKeyVariants(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        var variants = new HashSet<string>(StringComparer.Ordinal)
        {
            normalized
        };

        var firstSeparator = normalized.IndexOf(':');
        if (firstSeparator >= 0 && firstSeparator < normalized.Length - 1)
        {
            variants.Add(normalized[(firstSeparator + 1)..]);
        }

        var lastSeparator = normalized.LastIndexOf(':');
        if (lastSeparator >= 0 && lastSeparator < normalized.Length - 1)
        {
            variants.Add(normalized[(lastSeparator + 1)..]);
        }

        return variants.ToList();
    }

    private static IdentityProviderKind? TryMapProvider(ParticipationConnectorKind connector)
        => connector switch
        {
            ParticipationConnectorKind.GitHub => IdentityProviderKind.GitHub,
            ParticipationConnectorKind.Discord => IdentityProviderKind.Discord,
            ParticipationConnectorKind.Meetup => IdentityProviderKind.Meetup,
            ParticipationConnectorKind.Luma => IdentityProviderKind.Luma,
            ParticipationConnectorKind.Eventbrite => IdentityProviderKind.Eventbrite,
            _ => null
        };

    private static MemberResolutionKey ToResolutionKey(
        ParticipationConnectorKind connector,
        string externalMemberKey)
        => new(connector, externalMemberKey.Trim().ToLowerInvariant());

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

    private sealed record ResolvedParticipationEntry(
        NormalizedParticipationEntry Normalized,
        CommunityMemberId MemberId);

    private sealed record MemberResolutionKey(
        ParticipationConnectorKind Connector,
        string ExternalMemberKey);

    private sealed record DedupeKey(
        CommunityMemberId CommunityMemberId,
        ParticipationConnectorKind Connector,
        string ProvenanceKey);

    private sealed record IdentityLink(
        CommunityMemberId CommunityMemberId,
        string Subject,
        string? Username);
}

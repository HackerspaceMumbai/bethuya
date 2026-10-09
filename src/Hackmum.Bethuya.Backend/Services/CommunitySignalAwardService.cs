using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Manages the narrowly scoped organizer-awarded Champion recognition.
/// </summary>
public sealed class CommunitySignalAwardService(
    BethuyaDbContext db,
    CommunityPassportAccessPolicy accessPolicy)
{
    public async Task<CommunitySignalResponse> AwardChampionAsync(
        CommunityMemberId memberId,
        AwardChampionSignalRequest request,
        string awardedBy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.EvidenceEntryIds);
        if (string.IsNullOrWhiteSpace(request.Rationale) || request.Rationale.Trim().Length > 1000)
        {
            throw new ArgumentException("Champion rationale is required and must be 1,000 characters or fewer.", nameof(request));
        }

        var member = await db.CommunityMembers.SingleOrDefaultAsync(candidate => candidate.Id == memberId, ct)
            ?? throw new KeyNotFoundException("Community member not found.");
        if (!accessPolicy.CanOrganizerView(member))
        {
            throw new UnauthorizedAccessException("This passport is private.");
        }

        await ValidateEvidenceAsync(memberId, request.EvidenceEntryIds, ct);
        var existing = await db.CommunitySignalAwards
            .SingleOrDefaultAsync(award =>
                award.CommunityMemberId == memberId
                && award.Kind == CommunitySignalKind.Champion
                && award.RevokedAt == null, ct);
        if (existing is null)
        {
            existing = new CommunitySignalAward
            {
                CommunityMemberId = memberId,
                Rationale = request.Rationale.Trim(),
                EvidenceEntryIds = request.EvidenceEntryIds.Distinct().ToArray(),
                AwardedBy = awardedBy
            };
            db.CommunitySignalAwards.Add(existing);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                db.Entry(existing).State = EntityState.Detached;
                existing = await db.CommunitySignalAwards.AsNoTracking()
                    .SingleOrDefaultAsync(award =>
                        award.CommunityMemberId == memberId
                        && award.Kind == CommunitySignalKind.Champion
                        && award.RevokedAt == null, ct);
                if (existing is null)
                {
                    throw new InvalidOperationException(
                        "Champion recognition could not be persisted.",
                        ex);
                }
            }
        }

        return ToResponse(existing);
    }

    public async Task RevokeChampionAsync(
        CommunityMemberId memberId,
        RevokeChampionSignalRequest request,
        string revokedBy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
        {
            throw new ArgumentException("Revocation reason is required and must be 1,000 characters or fewer.", nameof(request));
        }

        var member = await db.CommunityMembers.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == memberId, ct)
            ?? throw new KeyNotFoundException("Community member not found.");
        if (!accessPolicy.CanOrganizerView(member))
        {
            throw new UnauthorizedAccessException("This passport is private.");
        }

        var award = await db.CommunitySignalAwards.SingleOrDefaultAsync(candidate =>
            candidate.CommunityMemberId == memberId
            && candidate.Kind == CommunitySignalKind.Champion
            && candidate.RevokedAt == null, ct)
            ?? throw new KeyNotFoundException("Active Champion recognition not found.");
        award.RevokedBy = revokedBy;
        award.RevocationReason = request.Reason.Trim();
        award.RevokedAt = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new KeyNotFoundException("Active Champion recognition not found.");
        }
    }

    private async Task ValidateEvidenceAsync(
        CommunityMemberId memberId,
        IReadOnlyList<Guid> evidenceIds,
        CancellationToken ct)
    {
        var distinctIds = evidenceIds.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return;
        }
        var typedIds = distinctIds.Select(ParticipationLedgerEntryId.From).ToArray();
        var count = await db.ParticipationLedgerEntries.AsNoTracking()
            .CountAsync(entry => entry.CommunityMemberId == memberId && typedIds.Contains(entry.Id), ct);
        if (count != distinctIds.Length)
        {
            throw new ArgumentException("Champion evidence must belong to the recognized member.", nameof(evidenceIds));
        }
    }

    private static CommunitySignalResponse ToResponse(CommunitySignalAward award)
        => new(
            CommunitySignalKind.Champion,
            "Champion",
            true,
            award.Rationale,
            award.EvidenceEntryIds.Count == 0
                ? ["Explicit organizer recognition"]
                : [$"{award.EvidenceEntryIds.Count} verified evidence record(s)"]);
}

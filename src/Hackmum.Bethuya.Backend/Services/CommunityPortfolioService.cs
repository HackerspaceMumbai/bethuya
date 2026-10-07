using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Manages member-curated Community Portfolio entries.
/// </summary>
public sealed class CommunityPortfolioService(
    BethuyaDbContext db,
    CommunityPassportService passportService)
{
    public async Task<PortfolioEntryResponse> UpsertAsync(
        CommunitySubjectContext subject,
        CommunityPortfolioEntryId? entryId,
        UpsertPortfolioEntryRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var member = await passportService.EnsureMemberProvisionedAsync(subject, ct);
        await ValidateEvidenceAsync(member.Id, request.EvidenceEntryIds, ct);

        CommunityPortfolioEntry entry;
        if (entryId is not { } existingEntryId)
        {
            entry = new CommunityPortfolioEntry
            {
                CommunityMemberId = member.Id,
                Title = request.Title.Trim(),
                Description = request.Description.Trim()
            };
            db.CommunityPortfolioEntries.Add(entry);
        }
        else
        {
            entry = await db.CommunityPortfolioEntries
                .SingleOrDefaultAsync(candidate =>
                    candidate.Id == existingEntryId && candidate.CommunityMemberId == member.Id, ct)
                ?? throw new KeyNotFoundException("Portfolio entry not found.");
            entry.Title = request.Title.Trim();
            entry.Description = request.Description.Trim();
        }

        entry.IsFeatured = request.IsFeatured;
        entry.DisplayOrder = Math.Max(0, request.DisplayOrder);
        entry.Links = request.Links.Select(link =>
            new PortfolioLink(link.Kind, link.Url.Trim(), link.Label?.Trim())).ToArray();
        entry.EvidenceEntryIds = request.EvidenceEntryIds.Distinct().ToArray();
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(entry);
    }

    public async Task DeleteAsync(
        CommunitySubjectContext subject,
        CommunityPortfolioEntryId entryId,
        CancellationToken ct = default)
    {
        var member = await passportService.EnsureMemberProvisionedAsync(subject, ct);
        var entry = await db.CommunityPortfolioEntries
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == entryId && candidate.CommunityMemberId == member.Id, ct)
            ?? throw new KeyNotFoundException("Portfolio entry not found.");
        db.CommunityPortfolioEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReorderAsync(
        CommunitySubjectContext subject,
        ReorderPortfolioEntriesRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.EntryIds);
        var member = await passportService.EnsureMemberProvisionedAsync(subject, ct);
        var requestedIds = request.EntryIds.Distinct().ToArray();
        var typedIds = requestedIds.Select(CommunityPortfolioEntryId.From).ToArray();
        var entries = await db.CommunityPortfolioEntries
            .Where(entry => entry.CommunityMemberId == member.Id && typedIds.Contains(entry.Id))
            .ToListAsync(ct);
        if (entries.Count != requestedIds.Length)
        {
            throw new ArgumentException("Every portfolio entry must belong to the current member.", nameof(request));
        }

        var order = requestedIds.Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index);
        foreach (var entry in entries)
        {
            entry.DisplayOrder = order[entry.Id.Value];
            entry.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateEvidenceAsync(
        CommunityMemberId memberId,
        IReadOnlyList<Guid> evidenceEntryIds,
        CancellationToken ct)
    {
        var distinctIds = evidenceEntryIds.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return;
        }

        var typedIds = distinctIds.Select(ParticipationLedgerEntryId.From).ToArray();
        var validCount = await db.ParticipationLedgerEntries.AsNoTracking()
            .CountAsync(entry => entry.CommunityMemberId == memberId && typedIds.Contains(entry.Id), ct);
        if (validCount != distinctIds.Length)
        {
            throw new ArgumentException("Portfolio evidence must reference the current member's ledger.", nameof(evidenceEntryIds));
        }
    }

    private static void ValidateRequest(UpsertPortfolioEntryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Links);
        ArgumentNullException.ThrowIfNull(request.EvidenceEntryIds);
        if (request.Links.Any(link => link is null))
        {
            throw new ArgumentException("Portfolio links cannot contain null entries.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 160)
        {
            throw new ArgumentException("Portfolio title is required and must be 160 characters or fewer.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 2000)
        {
            throw new ArgumentException("Portfolio description is required and must be 2,000 characters or fewer.", nameof(request));
        }
        if (request.Links.Count > 12)
        {
            throw new ArgumentException("A portfolio entry supports at most 12 links.", nameof(request));
        }
        foreach (var link in request.Links)
        {
            if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || link.Url.Length > 2048)
            {
                throw new ArgumentException("Portfolio links must be valid HTTPS URLs up to 2,048 characters.", nameof(request));
            }
        }
    }

    private static PortfolioEntryResponse ToResponse(CommunityPortfolioEntry entry)
        => new(
            entry.Id.Value,
            entry.Title,
            entry.Description,
            entry.IsFeatured,
            entry.DisplayOrder,
            entry.Links.Select(link => new PortfolioLinkResponse(link.Kind, link.Url, link.Label)).ToArray(),
            entry.EvidenceEntryIds);
}

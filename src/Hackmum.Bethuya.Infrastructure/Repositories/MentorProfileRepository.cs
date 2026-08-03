using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Repositories;

/// <summary>
/// EF Core-backed repository for community mentor profiles.
/// </summary>
public sealed class MentorProfileRepository(BethuyaDbContext db) : IMentorProfileRepository
{
    public async Task<MentorProfile?> GetByMemberIdAsync(CommunityMemberId memberId, CancellationToken ct = default)
        => await db.MentorProfiles
            .Include(profile => profile.Member)
            .FirstOrDefaultAsync(profile => profile.MemberId == memberId, ct);

    public async Task<List<MentorProfile>> GetDiscoverableMentorsAsync(
        IReadOnlyList<MentorExpertiseArea>? filterAreas,
        int limit,
        CancellationToken ct = default)
    {
        limit = Math.Max(1, limit);

        var query = db.MentorProfiles
            .Include(profile => profile.Member)
            .Where(profile =>
                profile.Status == MentorshipStatus.OptedIn &&
                profile.IsDiscoverable &&
                profile.Member != null &&
                profile.Member.IsDiscoverableToCommunity)
            .AsNoTracking();

        // Push expertise-area prefiltering when supported by the provider, then
        // apply exact in-memory enum matching to handle JSON ordering variance.
        var canApplyServerAreaFilter = !string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

        if (filterAreas is { Count: > 0 } && canApplyServerAreaFilter)
        {
            var areaNames = filterAreas.Select(a => a.ToString()).ToList();
            query = query.Where(profile => areaNames.Any(area => profile.ExpertiseAreasJson.Contains(area)));
        }

        var profiles = await query
            .OrderBy(profile => profile.OptedInAt)
            .Take(limit)
            .ToListAsync(ct);

        // Re-apply exact in-memory match to drop false positives from the LIKE check.
        if (filterAreas is { Count: > 0 })
        {
            profiles = profiles
                .Where(profile => profile.ExpertiseAreas.Any(filterAreas.Contains))
                .ToList();
        }

        return profiles;
    }

    public async Task<MentorProfile> UpsertAsync(MentorProfile profile, CancellationToken ct = default)
    {
        var existing = await db.MentorProfiles
            .FirstOrDefaultAsync(p => p.MemberId == profile.MemberId, ct);

        if (existing is null)
        {
            db.MentorProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            return profile;
        }

        existing.Status = profile.Status;
        existing.ExpertiseAreasJson = profile.ExpertiseAreasJson;
        existing.IntroductionBio = profile.IntroductionBio;
        existing.AvailabilityHoursPerMonth = profile.AvailabilityHoursPerMonth;
        existing.IsDiscoverable = profile.IsDiscoverable;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return existing;
    }
}

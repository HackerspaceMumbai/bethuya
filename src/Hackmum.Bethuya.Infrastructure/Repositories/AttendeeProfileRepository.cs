using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Repositories;

public sealed class AttendeeProfileRepository(BethuyaDbContext db) : IAttendeeProfileRepository
{
    public async Task<AttendeeProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default)
        => await db.AttendeeProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task<AttendeeProfile> CreateAsync(AttendeeProfile profile, CancellationToken ct = default)
    {
        db.AttendeeProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task UpdateAsync(AttendeeProfile profile, CancellationToken ct = default)
    {
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        db.AttendeeProfiles.Update(profile);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AttendeeInclusionSource?> GetInclusionSourceByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.ToLowerInvariant();

#pragma warning disable CA1304, CA1311, CA1862 // Npgsql translates ToLower() to lower(); ToLowerInvariant() and string.Equals(..., StringComparison) are not translatable.
        return await db.AttendeeProfiles
            .AsNoTracking()
            .Where(p => p.Email.ToLower() == normalizedEmail)
            .Select(p => new AttendeeInclusionSource(
                p.Neighborhood,
                p.LanguageProficiency,
                p.EducationalBackground,
                p.SocioeconomicBackground,
                p.GenderIdentity))
            .FirstOrDefaultAsync(ct);
#pragma warning restore CA1304, CA1311, CA1862
    }

    public async Task<IReadOnlyDictionary<string, AttendeePublicSummary>> GetPublicSummariesByEmailAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken ct = default)
    {
        if (emails.Count == 0)
        {
            return new Dictionary<string, AttendeePublicSummary>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedEmails = emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

#pragma warning disable CA1304, CA1311 // Npgsql translates ToLower() to lower(); ToLowerInvariant() is not translatable.
        return await db.AttendeeProfiles
            .AsNoTracking()
            .Where(p => normalizedEmails.Contains(p.Email.ToLower()))
            .GroupBy(p => p.Email.ToLower())
            .Select(group => group
                .OrderByDescending(profile => profile.UpdatedAt)
                .Select(profile => new
                {
                    profile.Email,
                    Summary = new AttendeePublicSummary(
                        profile.OccupationStatus,
                        profile.CompanyName,
                        profile.EducationInstitute,
                        GitHubRepoCount: null,
                        IsGitHubLinked: !string.IsNullOrWhiteSpace(profile.GitHubLogin) || !string.IsNullOrWhiteSpace(profile.GitHubProfileUrl),
                        IsLinkedInVerified: !string.IsNullOrWhiteSpace(profile.LinkedInMemberId) || !string.IsNullOrWhiteSpace(profile.LinkedInProfileUrl),
                        MemberSinceYear: profile.CreatedAt.Year)
                })
                .First())
            .ToDictionaryAsync(
                item => item.Email,
                item => item.Summary,
                StringComparer.OrdinalIgnoreCase,
                ct);
#pragma warning restore CA1304, CA1311
    }
}

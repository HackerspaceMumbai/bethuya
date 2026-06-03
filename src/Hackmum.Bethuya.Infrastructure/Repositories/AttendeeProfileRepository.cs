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
        => await db.AttendeeProfiles
            .AsNoTracking()
            .Where(p => p.Email == email)
            .Select(p => new AttendeeInclusionSource(
                p.Neighborhood,
                p.LanguageProficiency,
                p.EducationalBackground,
                p.SocioeconomicBackground,
                p.GenderIdentity))
            .FirstOrDefaultAsync(ct);

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
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return await db.AttendeeProfiles
            .AsNoTracking()
            .Where(p => normalizedEmails.Contains(p.Email))
            .GroupBy(p => p.Email)
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
    }
}

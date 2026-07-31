using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Projects the vNext Community Passport while reusing the current attendee-profile and registration history.
/// </summary>
public sealed class CommunityPassportService(BethuyaDbContext db)
{
    private const string DefaultCommunitySlug = "hackerspace-mumbai";
    private const string DefaultResidencyRegion = "South India";
    private const string EuropeanUnionResidencyRegion = "European Union";

    private static readonly HashSet<string> EuJurisdictions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Germany",
        "France",
        "Netherlands",
        "Spain",
        "Italy",
        "Belgium",
        "Ireland",
        "Portugal",
        "Poland",
        "Sweden",
        "Finland",
        "Denmark",
        "Austria",
        "Czech Republic"
    };

    public async Task<CommunityPassportResponse> GetPassportAsync(CommunitySubjectContext subject, CancellationToken ct = default)
    {
        var member = await GetOrProvisionMemberAsync(subject, ct);
        return await BuildPassportAsync(member, ct);
    }

    public async Task<PassportPrivacyResponse> UpdatePrivacyAsync(
        CommunitySubjectContext subject,
        UpdateCommunityPassportPrivacyRequest request,
        CancellationToken ct = default)
    {
        var member = await GetOrProvisionMemberAsync(subject, ct);

        member.Visibility = request.Visibility;
        member.ShareParticipationWithOrganizers = request.ShareParticipationWithOrganizers;
        member.IsDiscoverableToCommunity = request.IsDiscoverableToCommunity;
        member.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return new PassportPrivacyResponse(
            member.Visibility,
            member.ShareParticipationWithOrganizers,
            member.IsDiscoverableToCommunity);
    }

    private async Task<CommunityPassportResponse> BuildPassportAsync(CommunityMember member, CancellationToken ct)
    {
        var registrationsQuery = db.Registrations.AsNoTracking();
        if (db.Database.IsNpgsql())
        {
            registrationsQuery = registrationsQuery
                .Where(registration => EF.Functions.ILike(registration.Email, member.Email));
        }
        else
        {
            registrationsQuery = registrationsQuery
                .Where(registration => string.Equals(registration.Email, member.Email, StringComparison.OrdinalIgnoreCase));
        }

        var registrations = await registrationsQuery
            .OrderByDescending(registration => registration.UpdatedAt)
            .ToListAsync(ct);

        var eventIds = registrations
            .Select(registration => registration.EventId)
            .Distinct()
            .ToList();

        var eventsById = eventIds.Count == 0
            ? new Dictionary<Guid, Event>()
            : await db.Events
                .AsNoTracking()
                .Where(evt => eventIds.Contains(evt.Id))
                .ToDictionaryAsync(evt => evt.Id, ct);

        var volunteerSignals = registrations.Count(HasVolunteerSignal);
        var attendedCount = registrations.Count(registration => registration.Status == RegistrationStatus.CheckedIn);
        var waitlistedCount = registrations.Count(registration => registration.Status == RegistrationStatus.Waitlisted);
        var milestoneCount = CalculateMilestones(member.ExternalIdentities.Count, attendedCount, volunteerSignals);

        var timeline = registrations
            .Select(registration =>
            {
                var eventTitle = eventsById.TryGetValue(registration.EventId, out var evt)
                    ? evt.Title
                    : "Unknown event";

                return new PassportTimelineEntryResponse(
                    registration.EventId,
                    eventTitle,
                    MapRegistrationStatus(registration.Status),
                    registration.UpdatedAt,
                    $"Registration status recorded as {MapRegistrationStatus(registration.Status)}.");
            })
            .Take(12)
            .ToList();

        var identities = member.ExternalIdentities
            .OrderBy(identity => identity.Provider)
            .Select(identity => new PassportIdentityResponse(
                identity.Provider,
                identity.Subject,
                identity.Username,
                identity.ProfileUrl,
                identity.IsVerified,
                identity.LinkedAt))
            .ToList();

        return new CommunityPassportResponse(
            member.DisplayName,
            member.Email,
            member.OccupationStatus,
            member.CompanyName,
            member.EducationInstitute,
            DetermineTier(attendedCount, volunteerSignals, identities.Count),
            new PassportMetricsResponse(
                EventsRegistered: registrations.Count,
                EventsAttended: attendedCount,
                EventsWaitlisted: waitlistedCount,
                VolunteerSignals: volunteerSignals,
                MilestonesEarned: milestoneCount),
            new PassportPrivacyResponse(
                member.Visibility,
                member.ShareParticipationWithOrganizers,
                member.IsDiscoverableToCommunity),
            new PassportResidencyResponse(
                member.ResidencyRegion,
                member.ResidencyMode,
                member.ComplianceProfile),
            identities,
            timeline);
    }

    private async Task<CommunityMember> GetOrProvisionMemberAsync(CommunitySubjectContext subject, CancellationToken ct)
    {
        var member = await db.CommunityMembers
            .Include(existing => existing.ExternalIdentities)
            .FirstOrDefaultAsync(existing => existing.UserId == subject.UserId, ct);

        var profile = await db.AttendeeProfiles.FirstOrDefaultAsync(existing => existing.UserId == subject.UserId, ct);

        if (member is null)
        {
            member = new CommunityMember
            {
                UserId = subject.UserId,
                DisplayName = BuildDisplayName(profile, subject),
                Email = ResolveEmail(profile, subject),
                CommunitySlug = DefaultCommunitySlug,
                OccupationStatus = profile?.OccupationStatus,
                CompanyName = profile?.CompanyName,
                EducationInstitute = profile?.EducationInstitute,
                ResidencyRegion = ResolveResidencyRegion(profile),
                ResidencyMode = ResolveResidencyMode(profile),
                ComplianceProfile = ResolveComplianceProfile(profile)
            };

            EnsureLinkedIdentity(
                member,
                IdentityProviderKind.Platform,
                subject.UserId,
                subject.DisplayName,
                profileUrl: null);
            SyncLegacyIdentities(member, profile);

            db.CommunityMembers.Add(member);

            try
            {
                await db.SaveChangesAsync(ct);
                return member;
            }
            catch (DbUpdateException)
            {
                if (await TryLoadProvisionedMemberAsync(subject.UserId, ct) is { } provisioned)
                {
                    return provisioned;
                }

                throw;
            }
        }

        var changed = false;
        changed |= ApplyProfileSync(member, profile, subject);
        changed |= SyncLegacyIdentities(member, profile);

        if (changed)
        {
            member.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return member;
    }

    private async Task<CommunityMember?> TryLoadProvisionedMemberAsync(string userId, CancellationToken ct)
    {
        if (db.ChangeTracker.Entries<CommunityMember>().FirstOrDefault(entry =>
                string.Equals(entry.Entity.UserId, userId, StringComparison.Ordinal)) is { } memberEntry)
        {
            memberEntry.State = EntityState.Detached;
        }

        return await db.CommunityMembers
            .Include(existing => existing.ExternalIdentities)
            .FirstOrDefaultAsync(existing => existing.UserId == userId, ct);
    }

    private static bool ApplyProfileSync(CommunityMember member, AttendeeProfile? profile, CommunitySubjectContext subject)
    {
        var changed = false;
        var displayName = BuildDisplayName(profile, subject);
        var email = ResolveEmail(profile, subject);
        var region = ResolveResidencyRegion(profile);
        var residencyMode = ResolveResidencyMode(profile);
        var complianceProfile = ResolveComplianceProfile(profile);

        if (!string.Equals(member.DisplayName, displayName, StringComparison.Ordinal))
        {
            member.DisplayName = displayName;
            changed = true;
        }

        if (!string.Equals(member.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            member.Email = email;
            changed = true;
        }

        changed |= ReplaceIfDifferent(member.OccupationStatus, profile?.OccupationStatus, value => member.OccupationStatus = value);
        changed |= ReplaceIfDifferent(member.CompanyName, profile?.CompanyName, value => member.CompanyName = value);
        changed |= ReplaceIfDifferent(member.EducationInstitute, profile?.EducationInstitute, value => member.EducationInstitute = value);

        if (!string.Equals(member.ResidencyRegion, region, StringComparison.Ordinal))
        {
            member.ResidencyRegion = region;
            changed = true;
        }

        if (member.ResidencyMode != residencyMode)
        {
            member.ResidencyMode = residencyMode;
            changed = true;
        }

        if (!string.Equals(member.ComplianceProfile, complianceProfile, StringComparison.Ordinal))
        {
            member.ComplianceProfile = complianceProfile;
            changed = true;
        }

        if (EnsureLinkedIdentity(member, IdentityProviderKind.Platform, subject.UserId, subject.DisplayName, profileUrl: null))
        {
            changed = true;
        }

        return changed;
    }

    private static bool ReplaceIfDifferent(
        string? currentValue,
        string? nextValue,
        Action<string?> setter)
    {
        if (string.IsNullOrWhiteSpace(nextValue))
        {
            return false;
        }

        var trimmedValue = nextValue.Trim();
        if (string.Equals(currentValue, trimmedValue, StringComparison.Ordinal))
        {
            return false;
        }

        setter(trimmedValue);
        return true;
    }

    private static bool SyncLegacyIdentities(CommunityMember member, AttendeeProfile? profile)
    {
        if (profile is null)
        {
            return false;
        }

        var changed = false;

        if (!string.IsNullOrWhiteSpace(profile.LinkedInMemberId))
        {
            changed |= EnsureLinkedIdentity(
                member,
                IdentityProviderKind.LinkedIn,
                profile.LinkedInMemberId,
                profile.LinkedInMemberId,
                profile.LinkedInProfileUrl);
        }

        if (!string.IsNullOrWhiteSpace(profile.GitHubLogin))
        {
            changed |= EnsureLinkedIdentity(
                member,
                IdentityProviderKind.GitHub,
                profile.GitHubLogin,
                profile.GitHubLogin,
                profile.GitHubProfileUrl);
        }

        return changed;
    }

    private static bool EnsureLinkedIdentity(
        CommunityMember member,
        IdentityProviderKind provider,
        string subject,
        string? username,
        string? profileUrl)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return false;
        }

        var trimmedSubject = subject.Trim();
        var identity = member.ExternalIdentities.FirstOrDefault(existing =>
            existing.Provider == provider &&
            string.Equals(existing.Subject, trimmedSubject, StringComparison.OrdinalIgnoreCase));

        if (identity is null)
        {
            member.ExternalIdentities.Add(new ExternalIdentity
            {
                CommunityMemberId = member.Id,
                Provider = provider,
                Subject = trimmedSubject,
                Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim(),
                ProfileUrl = string.IsNullOrWhiteSpace(profileUrl) ? null : profileUrl.Trim(),
                LastVerifiedAt = DateTimeOffset.UtcNow
            });
            return true;
        }

        var changed = false;
        if (!string.IsNullOrWhiteSpace(username) && !string.Equals(identity.Username, username.Trim(), StringComparison.Ordinal))
        {
            identity.Username = username.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(profileUrl) && !string.Equals(identity.ProfileUrl, profileUrl.Trim(), StringComparison.Ordinal))
        {
            identity.ProfileUrl = profileUrl.Trim();
            changed = true;
        }

        if (!identity.IsVerified)
        {
            identity.IsVerified = true;
            changed = true;
        }

        if (changed)
        {
            identity.LastVerifiedAt = DateTimeOffset.UtcNow;
        }

        return changed;
    }

    private static string BuildDisplayName(AttendeeProfile? profile, CommunitySubjectContext subject)
    {
        if (profile is not null)
        {
            var fullName = $"{profile.FirstName} {profile.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }
        }

        if (!string.IsNullOrWhiteSpace(subject.DisplayName))
        {
            return subject.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(subject.Email))
        {
            return subject.Email.Split('@', StringSplitOptions.RemoveEmptyEntries)[0];
        }

        return "Community Member";
    }

    private static string ResolveEmail(AttendeeProfile? profile, CommunitySubjectContext subject)
    {
        if (!string.IsNullOrWhiteSpace(profile?.Email))
        {
            return profile.Email.Trim();
        }

        if (!string.IsNullOrWhiteSpace(subject.Email))
        {
            return subject.Email.Trim();
        }

        return $"{subject.UserId}@unknown.local";
    }

    private static string ResolveResidencyRegion(AttendeeProfile? profile)
    {
        var country = NormalizeCountry(profile?.Country);
        return country switch
        {
            not null when country.Equals("India", StringComparison.OrdinalIgnoreCase) => DefaultResidencyRegion,
            not null when EuJurisdictions.Contains(country) => EuropeanUnionResidencyRegion,
            { Length: > 0 } => "Jurisdiction policy required",
            _ => DefaultResidencyRegion
        };
    }

    private static SensitiveDataResidencyMode ResolveResidencyMode(AttendeeProfile? profile)
    {
        var country = NormalizeCountry(profile?.Country);
        return country switch
        {
            not null when EuJurisdictions.Contains(country) => SensitiveDataResidencyMode.JurisdictionLocked,
            _ => SensitiveDataResidencyMode.SovereignRegion
        };
    }

    private static string ResolveComplianceProfile(AttendeeProfile? profile)
    {
        var country = NormalizeCountry(profile?.Country);
        return country switch
        {
            not null when country.Equals("India", StringComparison.OrdinalIgnoreCase) => "DPDP-ready",
            not null when EuJurisdictions.Contains(country) => "GDPR-ready",
            { Length: > 0 } => "Jurisdiction policy review required",
            _ => "DPDP-ready"
        };
    }

    private static string? NormalizeCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return null;
        }

        return country.Trim();
    }

    private static bool HasVolunteerSignal(Registration registration)
        => registration.ContributionPreferences.Any(preference =>
               preference.Contains("volunteer", StringComparison.OrdinalIgnoreCase))
           || (!string.IsNullOrWhiteSpace(registration.Intent)
               && registration.Intent.Contains("volunteer", StringComparison.OrdinalIgnoreCase))
           || (!string.IsNullOrWhiteSpace(registration.Goals)
               && registration.Goals.Contains("volunteer", StringComparison.OrdinalIgnoreCase));

    private static int CalculateMilestones(int identityCount, int attendedCount, int volunteerSignals)
    {
        var milestones = 0;
        if (identityCount > 0)
        {
            milestones++;
        }

        if (identityCount > 1)
        {
            milestones++;
        }

        if (attendedCount > 0)
        {
            milestones++;
        }

        if (volunteerSignals > 0)
        {
            milestones++;
        }

        return milestones;
    }

    private static string DetermineTier(int attendedCount, int volunteerSignals, int identityCount)
        => (attendedCount, volunteerSignals, identityCount) switch
        {
            (_, > 0, _) => "Volunteer Track",
            (>= 3, _, _) => "Active Contributor",
            (> 0, _, >= 2) => "Verified Builder",
            (_, _, >= 2) => "Verified Member",
            _ => "New Member"
        };

    private static string MapRegistrationStatus(RegistrationStatus status)
        => status switch
        {
            RegistrationStatus.CheckedIn => "Attended",
            RegistrationStatus.Accepted => "Accepted",
            RegistrationStatus.Waitlisted => "Waitlisted",
            RegistrationStatus.Rejected => "Rejected",
            RegistrationStatus.Cancelled => "Cancelled",
            _ => "Registered"
        };
}

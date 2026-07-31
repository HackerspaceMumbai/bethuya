using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class CommunityPassportServiceTests
{
    [Test]
    public async Task GetPassportAsync_ProvisionsMemberFromExistingProfileAndRegistrations()
    {
        await using var db = CreateDbContext();
        var userId = "user-123";

        db.AttendeeProfiles.Add(new AttendeeProfile
        {
            UserId = userId,
            FirstName = "Augustine",
            LastName = "Correa",
            Email = "aug@example.com",
            GovernmentPhotoIdType = "PAN",
            GovernmentIdLastFour = "1234",
            OccupationStatus = "Working Professional",
            CompanyName = "Hackerspace Mumbai",
            LinkedInMemberId = "aug-li",
            LinkedInProfileUrl = "https://linkedin.com/in/aug",
            GitHubLogin = "indcoder",
            GitHubProfileUrl = "https://github.com/indcoder",
            Country = "India",
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        });

        var eventA = new Event
        {
            Title = "Copilot Dev Day",
            Type = EventType.Meetup,
            Capacity = 100,
            StartDate = new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 7, 1, 21, 0, 0, TimeSpan.Zero),
            CreatedBy = "organizer"
        };

        var eventB = new Event
        {
            Title = "AI Sprint",
            Type = EventType.Meetup,
            Capacity = 80,
            StartDate = new DateTimeOffset(2026, 7, 10, 18, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 7, 10, 21, 0, 0, TimeSpan.Zero),
            CreatedBy = "organizer"
        };

        db.Events.AddRange(eventA, eventB);
        db.Registrations.AddRange(
            new Registration
            {
                EventId = eventA.Id,
                FullName = "Augustine Correa",
                Email = "aug@example.com",
                Status = RegistrationStatus.CheckedIn,
                ContributionPreferences = ["Volunteer desk"]
            },
            new Registration
            {
                EventId = eventB.Id,
                FullName = "Augustine Correa",
                Email = "aug@example.com",
                Status = RegistrationStatus.Waitlisted
            });

        await db.SaveChangesAsync();

        var service = new CommunityPassportService(db);

        var passport = await service.GetPassportAsync(
            new CommunitySubjectContext(userId, "Augustine Correa", "aug@example.com"));

        await Assert.That(passport.DisplayName).IsEqualTo("Augustine Correa");
        await Assert.That(passport.CurrentTier).IsEqualTo("Volunteer Track");
        await Assert.That(passport.Metrics.EventsRegistered).IsEqualTo(2);
        await Assert.That(passport.Metrics.EventsAttended).IsEqualTo(1);
        await Assert.That(passport.Metrics.EventsWaitlisted).IsEqualTo(1);
        await Assert.That(passport.Metrics.VolunteerSignals).IsEqualTo(1);
        await Assert.That(passport.LinkedIdentities.Select(identity => identity.Provider))
            .Contains(IdentityProviderKind.Platform);
        await Assert.That(passport.LinkedIdentities.Select(identity => identity.Provider))
            .Contains(IdentityProviderKind.GitHub);
        await Assert.That(passport.LinkedIdentities.Select(identity => identity.Provider))
            .Contains(IdentityProviderKind.LinkedIn);
        await Assert.That(passport.Residency.Region).IsEqualTo("South India");
        await Assert.That(passport.Residency.ComplianceProfile).IsEqualTo("DPDP-ready");
    }

    [Test]
    public async Task GetPassportAsync_MatchesRegistrationsCaseInsensitively()
    {
        await using var db = CreateDbContext();
        var userId = "user-case-match";

        db.AttendeeProfiles.Add(new AttendeeProfile
        {
            UserId = userId,
            FirstName = "Casey",
            LastName = "Matcher",
            Email = "casey@example.com",
            GovernmentPhotoIdType = "PAN",
            GovernmentIdLastFour = "4321",
            LinkedInMemberId = "casey-li",
            GitHubLogin = "casey-gh",
            GitHubProfileUrl = "https://github.com/casey-gh",
            Country = "INDIA",
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        });

        var evt = new Event
        {
            Title = "Case Match Event",
            Type = EventType.Meetup,
            Capacity = 20,
            StartDate = DateTimeOffset.UtcNow.AddDays(3),
            EndDate = DateTimeOffset.UtcNow.AddDays(3).AddHours(2),
            CreatedBy = "organizer"
        };

        db.Events.Add(evt);
        db.Registrations.Add(new Registration
        {
            EventId = evt.Id,
            FullName = "Casey Matcher",
            Email = "CASEY@EXAMPLE.COM",
            Status = RegistrationStatus.Accepted
        });

        await db.SaveChangesAsync();

        var service = new CommunityPassportService(db);
        var passport = await service.GetPassportAsync(new CommunitySubjectContext(userId, "Casey Matcher", "casey@example.com"));

        await Assert.That(passport.Metrics.EventsRegistered).IsEqualTo(1);
        await Assert.That(passport.Residency.Region).IsEqualTo("South India");
        await Assert.That(passport.Residency.ComplianceProfile).IsEqualTo("DPDP-ready");
    }

    [Test]
    public async Task UpdatePrivacyAsync_PersistsRequestedControls()
    {
        await using var db = CreateDbContext();
        var service = new CommunityPassportService(db);
        var subject = new CommunitySubjectContext("user-privacy", "Privacy User", "privacy@example.com");

        await service.GetPassportAsync(subject);

        var updated = await service.UpdatePrivacyAsync(
            subject,
            new UpdateCommunityPassportPrivacyRequest(
                ProfileVisibilityScope.OrganizerOnly,
                ShareParticipationWithOrganizers: false,
                IsDiscoverableToCommunity: false));

        await Assert.That(updated.Visibility).IsEqualTo(ProfileVisibilityScope.OrganizerOnly);
        await Assert.That(updated.ShareParticipationWithOrganizers).IsFalse();
        await Assert.That(updated.IsDiscoverableToCommunity).IsFalse();

        var stored = await db.CommunityMembers.SingleAsync(member => member.UserId == subject.UserId);
        await Assert.That(stored.Visibility).IsEqualTo(ProfileVisibilityScope.OrganizerOnly);
        await Assert.That(stored.ShareParticipationWithOrganizers).IsFalse();
        await Assert.That(stored.IsDiscoverableToCommunity).IsFalse();
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"community-passport-tests-{Guid.NewGuid():N}")
            .Options;

        return new BethuyaDbContext(options);
    }
}

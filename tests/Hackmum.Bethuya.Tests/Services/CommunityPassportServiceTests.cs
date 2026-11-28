using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class CommunityPassportServiceTests
{
    [Test]
    public async Task GetPassportAsync_DoesNotClaimExistingRegistrationsByMutableEmail()
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
                ApprovalObservedAt = new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero),
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
        await Assert.That(passport.Metrics.EventsRegistered).IsEqualTo(0);
        await Assert.That(passport.Metrics.EventsAttended).IsEqualTo(0);
        await Assert.That(passport.Metrics.EventsWaitlisted).IsEqualTo(0);
        await Assert.That(passport.Metrics.VolunteerSignals).IsEqualTo(0);
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
    public async Task GetPassportAsync_UsesImmutableRegistrationMemberLink()
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

        var member = new CommunityMember
        {
            UserId = userId,
            DisplayName = "Casey Matcher",
            Email = "casey@example.com"
        };
        db.AddRange(evt, member);
        db.Registrations.Add(new Registration
        {
            EventId = evt.Id,
            CommunityMemberId = member.Id,
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

    [Test]
    public async Task EnsureMemberProvisionedAsync_SocialHandleLinkedToAnotherMember_DoesNotThrowOrMoveIdentity()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var db = CreateSqliteDbContext(connection);
        var service = new CommunityPassportService(db);
        var owner = new CommunitySubjectContext("user-owner", "Owner", "owner@example.com");
        var squatter = new CommunitySubjectContext("user-squatter", "Squatter", "squatter@example.com");

        await service.EnsureMemberProvisionedAsync(owner);

        db.AttendeeProfiles.Add(BuildProfile(squatter.UserId, "squatter@example.com", gitHubLogin: "owner-gh", linkedInMemberId: "owner-li"));
        await db.SaveChangesAsync();
        await service.EnsureMemberProvisionedAsync(squatter);

        db.AttendeeProfiles.Add(BuildProfile(owner.UserId, "owner@example.com", gitHubLogin: "owner-gh", linkedInMemberId: "owner-li"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var member = await service.EnsureMemberProvisionedAsync(owner);
        db.ChangeTracker.Clear();
        var memberAgain = await service.EnsureMemberProvisionedAsync(owner);

        await Assert.That(member.ExternalIdentities.Select(identity => identity.Provider))
            .IsEquivalentTo(new[] { IdentityProviderKind.Platform });
        await Assert.That(memberAgain.ExternalIdentities.Count).IsEqualTo(1);

        var squatterMember = await db.CommunityMembers.SingleAsync(existing => existing.UserId == squatter.UserId);
        var gitHubIdentity = await db.ExternalIdentities.SingleAsync(identity =>
            identity.Provider == IdentityProviderKind.GitHub && identity.Subject == "owner-gh");
        await Assert.That(gitHubIdentity.CommunityMemberId).IsEqualTo(squatterMember.Id);
    }

    [Test]
    public async Task GetPassportAsync_SocialIdentitiesFromProfile_AreNotMarkedVerified()
    {
        await using var db = CreateDbContext();
        var service = new CommunityPassportService(db);
        var subject = new CommunitySubjectContext("user-social", "Social User", "social@example.com");
        db.AttendeeProfiles.Add(BuildProfile(subject.UserId, "social@example.com", gitHubLogin: "social-gh", linkedInMemberId: "social-li"));
        await db.SaveChangesAsync();

        await service.GetPassportAsync(subject);
        var passport = await service.GetPassportAsync(subject);

        var identities = passport.LinkedIdentities.ToDictionary(identity => identity.Provider);
        await Assert.That(identities[IdentityProviderKind.Platform].IsVerified).IsTrue();
        await Assert.That(identities[IdentityProviderKind.GitHub].IsVerified).IsFalse();
        await Assert.That(identities[IdentityProviderKind.LinkedIn].IsVerified).IsFalse();
    }

    [Test]
    public async Task GetPassportAsync_LegacyVerifiedProfileIdentities_AreDowngradedAndPlatformStaysVerified()
    {
        await using var db = CreateDbContext();
        var service = new CommunityPassportService(db);
        var subject = new CommunitySubjectContext("user-legacy", "Legacy User", "legacy@example.com");
        var verifiedAt = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        var member = new CommunityMember { UserId = subject.UserId, DisplayName = "Legacy User", Email = "legacy@example.com" };
        member.ExternalIdentities.AddRange(
        [
            new ExternalIdentity { CommunityMemberId = member.Id, Provider = IdentityProviderKind.Platform, Subject = subject.UserId, Username = subject.DisplayName, IsVerified = true, LastVerifiedAt = verifiedAt },
            new ExternalIdentity { CommunityMemberId = member.Id, Provider = IdentityProviderKind.GitHub, Subject = "legacy-gh", Username = "legacy-gh", ProfileUrl = "https://github.com/legacy-gh", IsVerified = true, LastVerifiedAt = verifiedAt },
            new ExternalIdentity { CommunityMemberId = member.Id, Provider = IdentityProviderKind.LinkedIn, Subject = "legacy-li", Username = "legacy-li", IsVerified = true, LastVerifiedAt = verifiedAt }
        ]);
        db.CommunityMembers.Add(member);
        db.AttendeeProfiles.Add(BuildProfile(subject.UserId, "legacy@example.com", gitHubLogin: "legacy-gh", linkedInMemberId: "legacy-li"));
        await db.SaveChangesAsync();

        await service.GetPassportAsync(subject);

        var identities = await db.ExternalIdentities.ToDictionaryAsync(identity => identity.Provider);
        await Assert.That(identities[IdentityProviderKind.Platform].IsVerified).IsTrue();
        await Assert.That(identities[IdentityProviderKind.Platform].LastVerifiedAt).IsEqualTo(verifiedAt);
        await Assert.That(identities[IdentityProviderKind.GitHub].IsVerified).IsFalse();
        await Assert.That(identities[IdentityProviderKind.GitHub].LastVerifiedAt).IsNull();
        await Assert.That(identities[IdentityProviderKind.LinkedIn].IsVerified).IsFalse();
        await Assert.That(identities[IdentityProviderKind.LinkedIn].LastVerifiedAt).IsNull();
    }

    [Test]
    public async Task GetPassportAsync_ProfileUrlChange_DoesNotRefreshLastVerifiedAt()
    {
        await using var db = CreateDbContext();
        var service = new CommunityPassportService(db);
        var subject = new CommunitySubjectContext("user-url", "Url User", "url@example.com");
        var member = new CommunityMember { UserId = subject.UserId, DisplayName = "Url User", Email = "url@example.com" };
        member.ExternalIdentities.Add(new ExternalIdentity
        {
            CommunityMemberId = member.Id,
            Provider = IdentityProviderKind.GitHub,
            Subject = "url-gh",
            Username = "url-gh",
            ProfileUrl = "https://github.com/old-url",
            IsVerified = true,
            LastVerifiedAt = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)
        });
        db.CommunityMembers.Add(member);
        db.AttendeeProfiles.Add(BuildProfile(subject.UserId, "url@example.com", gitHubLogin: "url-gh", linkedInMemberId: "url-li"));
        await db.SaveChangesAsync();

        await service.GetPassportAsync(subject);
        var gitHub = await db.ExternalIdentities.SingleAsync(identity => identity.Provider == IdentityProviderKind.GitHub);
        gitHub.ProfileUrl = "https://github.com/older-url";
        await db.SaveChangesAsync();
        await service.GetPassportAsync(subject);

        await Assert.That(gitHub.ProfileUrl).IsEqualTo("https://github.com/url-gh");
        await Assert.That(gitHub.IsVerified).IsFalse();
        await Assert.That(gitHub.LastVerifiedAt).IsNull();
    }

    private static AttendeeProfile BuildProfile(string userId, string email, string gitHubLogin, string linkedInMemberId)
        => new()
        {
            UserId = userId,
            FirstName = "Test",
            LastName = userId,
            Email = email,
            GovernmentPhotoIdType = "PAN",
            GovernmentIdLastFour = "1234",
            LinkedInMemberId = linkedInMemberId,
            GitHubLogin = gitHubLogin,
            GitHubProfileUrl = $"https://github.com/{gitHubLogin}",
            Country = "India",
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        };

    private static BethuyaDbContext CreateSqliteDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new BethuyaDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"community-passport-tests-{Guid.NewGuid():N}")
            .Options;

        return new BethuyaDbContext(options);
    }
}

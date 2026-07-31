using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hackmum.Bethuya.Tests.Services;

/// <summary>
/// TUnit tests for mentor opt-in lifecycle, matching/discovery, recommendation drafts, and policy-bound state transitions.
/// </summary>
public sealed class MentorshipServiceTests
{
    // ─── Opt-in ───────────────────────────────────────────────────────────────

    [Test]
    public async Task OptIn_CreatesMentorProfile_WithDiscoverableByDefault()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var subject = SeedSubject();

        var profile = await service.OptInAsync(subject, new MentorOptInRequest(
            ExpertiseAreas: [MentorExpertiseArea.SoftwareEngineering, MentorExpertiseArea.OpenSource],
            IntroductionBio: "Happy to help with OSS contributions.",
            AvailabilityHoursPerMonth: 4));

        await Assert.That(profile.Status).IsEqualTo(MentorshipStatus.OptedIn);
        await Assert.That(profile.IsDiscoverable).IsTrue();
        await Assert.That(profile.ExpertiseAreas).Contains(MentorExpertiseArea.SoftwareEngineering);
        await Assert.That(profile.AvailabilityHoursPerMonth).IsEqualTo(4);
        await Assert.That(db.MentorProfiles.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task OptIn_WhenAlreadyOptedIn_UpdatesExistingProfile()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var subject = SeedSubject();

        await service.OptInAsync(subject, new MentorOptInRequest(
            ExpertiseAreas: [MentorExpertiseArea.Design],
            AvailabilityHoursPerMonth: 2));

        // Opt in again with updated details
        var updated = await service.OptInAsync(subject, new MentorOptInRequest(
            ExpertiseAreas: [MentorExpertiseArea.Design, MentorExpertiseArea.ProductManagement],
            IntroductionBio: "Now also mentoring on PM topics.",
            AvailabilityHoursPerMonth: 6));

        await Assert.That(db.MentorProfiles.Count()).IsEqualTo(1);
        await Assert.That(updated.ExpertiseAreas).Contains(MentorExpertiseArea.ProductManagement);
        await Assert.That(updated.AvailabilityHoursPerMonth).IsEqualTo(6);
        await Assert.That(updated.IntroductionBio).IsEqualTo("Now also mentoring on PM topics.");
    }

    [Test]
    public async Task GetMyProfile_ReturnsNull_WhenNotOptedIn()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var subject = SeedSubject();

        var profile = await service.GetMyProfileAsync(subject);

        await Assert.That(profile).IsNull();
    }

    [Test]
    public async Task GetMyProfile_ReturnsProfile_AfterOptIn()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var subject = SeedSubject();

        await service.OptInAsync(subject, new MentorOptInRequest(
            ExpertiseAreas: [MentorExpertiseArea.CommunityBuilding]));

        var profile = await service.GetMyProfileAsync(subject);

        await Assert.That(profile).IsNotNull();
        await Assert.That(profile!.Status).IsEqualTo(MentorshipStatus.OptedIn);
    }

    // ─── Status transitions ───────────────────────────────────────────────────

    [Test]
    public async Task UpdateStatus_ToPaused_HidesFromDiscovery()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var subject = SeedSubject();

        await service.OptInAsync(subject, new MentorOptInRequest(
            ExpertiseAreas: [MentorExpertiseArea.DevOps]));

        var paused = await service.UpdateStatusAsync(subject, new MentorStatusUpdateRequest(MentorshipStatus.Paused));

        await Assert.That(paused.Status).IsEqualTo(MentorshipStatus.Paused);
        await Assert.That(paused.IsDiscoverable).IsFalse();
    }

    [Test]
    public async Task UpdateStatus_ToOptedOut_RemovesFromDiscovery()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var subject = SeedSubject();

        await service.OptInAsync(subject, new MentorOptInRequest(
            ExpertiseAreas: [MentorExpertiseArea.Research]));

        var optedOut = await service.UpdateStatusAsync(subject, new MentorStatusUpdateRequest(MentorshipStatus.OptedOut));

        await Assert.That(optedOut.Status).IsEqualTo(MentorshipStatus.OptedOut);
        await Assert.That(optedOut.IsDiscoverable).IsFalse();
    }

    [Test]
    public async Task UpdateStatus_WithoutPriorOptIn_ThrowsInvalidOperationException()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var subject = SeedSubject();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.UpdateStatusAsync(subject, new MentorStatusUpdateRequest(MentorshipStatus.Paused)));
    }

    // ─── Discovery ────────────────────────────────────────────────────────────

    [Test]
    public async Task DiscoverMentors_ReturnsOnlyOptedInDiscoverableMembers()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var mentor1 = SeedSubject("mentor-1@test.com");
        var mentor2 = SeedSubject("mentor-2@test.com");
        var mentor3 = SeedSubject("mentor-3@test.com");

        await service.OptInAsync(mentor1, new MentorOptInRequest([MentorExpertiseArea.SoftwareEngineering]));
        await service.OptInAsync(mentor2, new MentorOptInRequest([MentorExpertiseArea.Design]));
        await service.OptInAsync(mentor3, new MentorOptInRequest([MentorExpertiseArea.SoftwareEngineering]));

        // Pause mentor3 - should not appear in discovery
        await service.UpdateStatusAsync(mentor3, new MentorStatusUpdateRequest(MentorshipStatus.Paused));

        var results = await service.DiscoverMentorsAsync(filterAreas: null, limit: 10);

        await Assert.That(results.Count).IsEqualTo(2);
    }

    [Test]
    public async Task DiscoverMentors_FiltersOnExpertiseArea()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var mentor1 = SeedSubject("filter-mentor-1@test.com");
        var mentor2 = SeedSubject("filter-mentor-2@test.com");

        await service.OptInAsync(mentor1, new MentorOptInRequest([MentorExpertiseArea.SoftwareEngineering]));
        await service.OptInAsync(mentor2, new MentorOptInRequest([MentorExpertiseArea.Design]));

        var results = await service.DiscoverMentorsAsync(
            filterAreas: [MentorExpertiseArea.SoftwareEngineering],
            limit: 10);

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].ExpertiseAreas).Contains(MentorExpertiseArea.SoftwareEngineering);
    }

    [Test]
    public async Task DiscoverMentors_ExcludesPausedAndOptedOut()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var active = SeedSubject("disc-active@test.com");
        var paused = SeedSubject("disc-paused@test.com");
        var optedOut = SeedSubject("disc-optedout@test.com");

        await service.OptInAsync(active, new MentorOptInRequest([MentorExpertiseArea.OpenSource]));
        await service.OptInAsync(paused, new MentorOptInRequest([MentorExpertiseArea.OpenSource]));
        await service.OptInAsync(optedOut, new MentorOptInRequest([MentorExpertiseArea.OpenSource]));

        await service.UpdateStatusAsync(paused, new MentorStatusUpdateRequest(MentorshipStatus.Paused));
        await service.UpdateStatusAsync(optedOut, new MentorStatusUpdateRequest(MentorshipStatus.OptedOut));

        var results = await service.DiscoverMentorsAsync(null, limit: 10);

        await Assert.That(results.Count).IsEqualTo(1);
    }

    // ─── Mentor-pairing recommendation draft ──────────────────────────────────

    [Test]
    public async Task DraftMentorPairingSuggestionAsync_PersistsAuditablePendingDraft()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var draft = await service.DraftMentorPairingSuggestionAsync(
            new DraftMentorPairingSuggestionRequest(LookbackDays: 90),
            requestedBy: "organizer@hackmum.com");

        var persistedDecision = await db.Decisions.SingleAsync(d => d.Id == draft.DraftId);

        await Assert.That(persistedDecision.EntityType).IsEqualTo("mentor-pairing-suggestion");
        await Assert.That(persistedDecision.Status).IsEqualTo(Core.Enums.DecisionStatus.Pending);
        await Assert.That(draft.RequiresHumanApproval).IsTrue();
        await Assert.That(draft.HumanReviewPolicy).IsEqualTo("explicit-human-approval-required");
        await Assert.That(draft.DraftKind).IsEqualTo("mentor-pairing-suggestion");
        await Assert.That(draft.Audit.AgentName).IsEqualTo("mentorship-recommendation-engine");
    }

    [Test]
    public async Task DraftMentorPairingSuggestion_ReflectsActiveMentorCount()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var mentor1 = SeedSubject("pairing-m1@test.com");
        await service.OptInAsync(mentor1, new MentorOptInRequest([MentorExpertiseArea.SoftwareEngineering]));

        var draft = await service.DraftMentorPairingSuggestionAsync(
            new DraftMentorPairingSuggestionRequest(LookbackDays: 90),
            requestedBy: "organizer@test");

        await Assert.That(draft.Recommendation.Summary).Contains("1 discoverable mentor");
    }

    // ─── Availability clamping ────────────────────────────────────────────────

    [Test]
    public async Task OptIn_ClampsAvailabilityHours_WithinBounds()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var subject = SeedSubject();

        var profile = await service.OptInAsync(subject, new MentorOptInRequest(
            ExpertiseAreas: [MentorExpertiseArea.Other],
            AvailabilityHoursPerMonth: 999));

        await Assert.That(profile.AvailabilityHoursPerMonth).IsEqualTo(40);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static MentorshipService CreateService(BethuyaDbContext db)
    {
        var passportService = new CommunityPassportService(db);
        var mentorRepo = new MentorProfileRepository(db);
        var decisionRepo = new DecisionRepository(db);
        var journeyService = new CommunityJourneyReadModelService(db, passportService);
        return new MentorshipService(passportService, mentorRepo, decisionRepo, journeyService);
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"mentorship-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }

    private static CommunitySubjectContext SeedSubject(string email = "member@hackmum.com")
        => new(
            UserId: $"user-{email.Replace("@", "-at-")}",
            DisplayName: "Test Member",
            Email: email);
}

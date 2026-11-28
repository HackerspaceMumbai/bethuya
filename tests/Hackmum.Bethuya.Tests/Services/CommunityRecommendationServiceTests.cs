using System.Diagnostics;
using System.Text.Json;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class CommunityRecommendationServiceTests
{
    [Test]
    public async Task DraftMemberGrowthOpportunityAsync_PersistsAuditablePendingDraft()
    {
        await using var db = CreateDbContext();
        var recommendationService = CreateService(db);

        using var activity = new Activity("recommendation-test");
        activity.SetIdFormat(ActivityIdFormat.Hierarchical);
        activity.SetParentId(new string('p', 260));
        activity.Start();

        var draft = await recommendationService.DraftMemberGrowthOpportunityAsync(
            new DraftMemberGrowthRecommendationRequest(LookbackDays: 90, RequestedBy: "organizer@test"),
            requestedBy: "organizer@test");

        var persistedDecision = await db.Decisions.SingleAsync(decision => decision.Id == draft.DraftId);
        using var payload = JsonDocument.Parse(persistedDecision.Diff!);
        var audit = payload.RootElement.GetProperty("Audit");

        await Assert.That(persistedDecision.Status).IsEqualTo(DecisionStatus.Pending);
        await Assert.That(draft.RequiresHumanApproval).IsTrue();
        await Assert.That(payload.RootElement.GetProperty("HumanReviewPolicy").GetString()).IsEqualTo("explicit-human-approval-required");
        await Assert.That(audit.GetProperty("InputHash").GetString()).IsNotNull();
        await Assert.That(audit.GetProperty("TraceParent").GetString()!.Length).IsLessThanOrEqualTo(200);
        await Assert.That(audit.GetProperty("AgentName").GetString()).IsEqualTo("community-recommendation-engine");
    }

    [Test]
    public async Task ApproveDraftAsync_TransitionsDraftToApplied()
    {
        await using var db = CreateDbContext();
        var recommendationService = CreateService(db);

        var draft = await recommendationService.DraftWeeklyBriefingAsync(
            new DraftWeeklyCommunityBriefingRequest(LookbackDays: 90, RequestedBy: "organizer@test"),
            requestedBy: "organizer@test");

        var approved = await recommendationService.ApproveDraftAsync(
            draft.DraftId,
            new ApproveRecommendationDraftRequest(ApprovedBy: "lead@hackmum.com", ApprovalNotes: "Publish this briefing"));

        var persistedDecision = await db.Decisions.SingleAsync(decision => decision.Id == draft.DraftId);

        await Assert.That(approved.IsApproved).IsTrue();
        await Assert.That(approved.ApprovedAt).IsNotNull();
        await Assert.That(persistedDecision.Status).IsEqualTo(DecisionStatus.Applied);
        await Assert.That(persistedDecision.Reason).Contains("Approved by lead@hackmum.com");
    }

    [Test]
    public async Task DraftMemberGrowthOpportunityAsync_ChangesInputHashWhenCommunityDataChanges()
    {
        await using var db = CreateDbContext();
        var recommendationService = CreateService(db);
        var request = new DraftMemberGrowthRecommendationRequest(LookbackDays: 90, RequestedBy: "organizer@test");

        var initialDraft = await recommendationService.DraftMemberGrowthOpportunityAsync(
            request,
            requestedBy: "organizer@test");

        db.CommunityMembers.Add(new CommunityMember
        {
            UserId = "member-growth-snapshot-user",
            DisplayName = "Snapshot Member",
            Email = "snapshot@test.com",
            IsDiscoverableToCommunity = true
        });
        db.Registrations.Add(new Registration
        {
            EventId = Guid.CreateVersion7(),
            FullName = "Snapshot Member",
            Email = "snapshot@test.com",
            Status = RegistrationStatus.CheckedIn,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var updatedDraft = await recommendationService.DraftMemberGrowthOpportunityAsync(
            request,
            requestedBy: "organizer@test");

        await Assert.That(updatedDraft.Audit.InputHash).IsNotEqualTo(initialDraft.Audit.InputHash);

        var approvedDraft = await recommendationService.ApproveDraftAsync(
            updatedDraft.DraftId,
            new ApproveRecommendationDraftRequest(ApprovedBy: "lead@hackmum.com", ApprovalNotes: "Verified snapshot"));

        await Assert.That(approvedDraft.Audit.InputHash).IsEqualTo(updatedDraft.Audit.InputHash);

        var persistedDecision = await db.Decisions.SingleAsync(decision => decision.Id == updatedDraft.DraftId);
        using var payload = JsonDocument.Parse(persistedDecision.Diff!);
        var snapshot = payload.RootElement.GetProperty("InputSnapshot");

        await Assert.That(snapshot.GetProperty("Request").GetProperty("LookbackDays").GetInt32()).IsEqualTo(90);
        await Assert.That(snapshot.GetProperty("Dashboard").GetProperty("Attendance").GetProperty("AttendedCount").GetInt32()).IsEqualTo(1);
        await Assert.That(snapshot.GetProperty("Dashboard").GetProperty("LeadershipFunnel").GetProperty("DiscoverableMembers").GetInt32()).IsEqualTo(1);
        await Assert.That(snapshot.GetProperty("SnapshotCapturedAtUtc").GetString()).IsNotNull();
    }

    [Test]
    public async Task DraftWeeklyBriefingAsync_ProducesSameInputHashForIdenticalCommunityData()
    {
        await using var db = CreateDbContext();
        var recommendationService = CreateService(db);
        var request = new DraftWeeklyCommunityBriefingRequest(LookbackDays: 90, RequestedBy: "organizer@test");

        var firstDraft = await recommendationService.DraftWeeklyBriefingAsync(request, requestedBy: "organizer@test");
        var secondDraft = await recommendationService.DraftWeeklyBriefingAsync(request, requestedBy: "organizer@test");

        await Assert.That(secondDraft.Audit.InputHash).IsEqualTo(firstDraft.Audit.InputHash);
    }

    private static CommunityRecommendationService CreateService(BethuyaDbContext db)
    {
        var passportService = new CommunityPassportService(db);
        var journeyReadModelService = new CommunityJourneyReadModelService(db, passportService);
        var decisionRepository = new DecisionRepository(db);
        return new CommunityRecommendationService(journeyReadModelService, decisionRepository);
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"community-recommendation-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }
}

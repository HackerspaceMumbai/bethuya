using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class EventArchivePublicationOrchestratorTests
{
    [Test]
    public async Task PreviewArchivePublicationAsync_ReadOnlyPreview_DoesNotQueueOrPublish()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var repository = CreateRepository();
        var orchestrator = CreateOrchestrator(db, repository);

        var result = await orchestrator.PreviewArchivePublicationAsync(evt.Id);

        await Assert.That(result.Preview.HasChanges).IsTrue();
        await Assert.That(await db.EventArchiveOutboxMessages.CountAsync()).IsEqualTo(0);
        await repository.DidNotReceiveWithAnyArgs().PublishEventAsync(default!, default);
    }

    [Test]
    public async Task ConfirmArchivePublicationAsync_ConfirmedPreview_QueuesOneDurableRequest()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var repository = CreateRepository();
        var orchestrator = CreateOrchestrator(db, repository);

        var preview = await orchestrator.PreviewArchivePublicationAsync(evt.Id);
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer-123", preview.Preview.PreviewToken);
        evt.ArchivePublicationStatus = EventArchivePublicationStatus.Published;
        evt.ArchiveBranchName = "bethuya/event-archive-published";
        evt.ArchivePullRequestNumber = 42;
        evt.ArchivePullRequestUrl = "https://github.com/HackerspaceMumbai/events/pull/42";
        evt.GitHubFolderUrl = "https://github.com/HackerspaceMumbai/events/tree/main/events/2026/2026-11-28-community-summit";
        await db.SaveChangesAsync();
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer-123", preview.Preview.PreviewToken);

        var outbox = await db.EventArchiveOutboxMessages.ToListAsync();
        await Assert.That(outbox.Count).IsEqualTo(1);
        await Assert.That(outbox[0].ConfirmedBy).IsEqualTo("organizer-123");
        await Assert.That(outbox[0].ConfirmedPreviewToken).IsEqualTo(preview.Preview.PreviewToken);
        await Assert.That(evt.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.Published);
        await Assert.That(evt.ArchiveBranchName).IsEqualTo("bethuya/event-archive-published");
        await Assert.That(evt.ArchivePullRequestNumber).IsEqualTo(42);
        await Assert.That(evt.ArchivePullRequestUrl).IsEqualTo(
            "https://github.com/HackerspaceMumbai/events/pull/42");
        await Assert.That(evt.ArchivePublicationRequestedBy).IsEqualTo("organizer-123");
        await Assert.That(evt.ArchiveFolderPath).IsEqualTo(
            $"events/2026/2026-11-28-community-summit-{evt.Id:N}");
    }

    [Test]
    public async Task ConfirmArchivePublicationAsync_NewSnapshotWhileStillPending_RejectsInsteadOfDiscardingConfirmation()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var currentToken = "confirmed-snapshot-1";
        var repository = Substitute.For<IGitHubEventRepository>();
        repository.PreviewEventAsync(Arg.Any<EventPublicationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(CreatePreview(
                call.Arg<EventPublicationRequest>(),
                null,
                currentToken)));
        var orchestrator = CreateOrchestrator(db, repository);
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer-123", currentToken);
        var outbox = await db.EventArchiveOutboxMessages.SingleAsync();
        outbox.ClaimToken = "active-worker-claim";
        outbox.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(2);
        await db.SaveChangesAsync();

        currentToken = "confirmed-snapshot-2";
        var action = async () =>
        {
            await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer-123", currentToken);
        };

        var exception = await Assert.That(action).Throws<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains("Wait for it to finish");
        await Assert.That(outbox.ConfirmedPreviewToken).IsEqualTo("confirmed-snapshot-1");
        await Assert.That(outbox.ClaimToken).IsEqualTo("active-worker-claim");
        await Assert.That(outbox.ProcessedAt).IsNull();
        await Assert.That(await db.EventArchiveOutboxMessages.CountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task ConfirmArchivePublicationAsync_ReconfirmationAfterSnapshotFailure_RefreshesQueuedToken()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var currentToken = "confirmed-snapshot-1";
        var repository = Substitute.For<IGitHubEventRepository>();
        repository.PreviewEventAsync(Arg.Any<EventPublicationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(CreatePreview(
                call.Arg<EventPublicationRequest>(),
                null,
                currentToken)));
        var orchestrator = CreateOrchestrator(db, repository);

        var firstPreview = await orchestrator.PreviewArchivePublicationAsync(evt.Id);
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer-123", firstPreview.Preview.PreviewToken);
        var outbox = await db.EventArchiveOutboxMessages.SingleAsync();
        outbox.ProcessedAt = DateTimeOffset.UtcNow;
        outbox.LastError = "The archive changed after organizer confirmation.";
        evt.ArchivePublicationStatus = EventArchivePublicationStatus.Failed;
        await db.SaveChangesAsync();

        currentToken = "confirmed-snapshot-2";
        var nextPreview = await orchestrator.PreviewArchivePublicationAsync(evt.Id);
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer-123", nextPreview.Preview.PreviewToken);

        await Assert.That(outbox.ProcessedAt).IsNull();
        await Assert.That(outbox.LastError).IsNull();
        await Assert.That(outbox.ConfirmedPreviewToken).IsEqualTo("confirmed-snapshot-2");
        await Assert.That(evt.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.Pending);
        await Assert.That(await db.EventArchiveOutboxMessages.CountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task PreviewArchiveAdoptionAsync_ExactPath_ValidatesAndPreservesSourceProvenance()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        const string path = "events/2026/2026-11-28-community-summit";
        var entry = new EventArchiveEntry(
            "# Community Summit\nCommunity notes stay here.\n",
            """
            title: "Community Summit"
            slug: "2026-11-28-community-summit"
            date: 2026-11-28
            venue: "Community Hall"
            status: upcoming
            description: "Original description"
            """,
            "readme-sha",
            "metadata-sha");
        var repository = CreateRepository(entry);
        var orchestrator = CreateOrchestrator(db, repository);

        var preview = await orchestrator.PreviewArchiveAdoptionAsync(evt.Id, path);
        var adopted = await orchestrator.ConfirmArchiveAdoptionAsync(
            evt.Id,
            path,
            preview.Preview.PreviewToken,
            "organizer-123");

        await Assert.That(adopted.FolderPath).IsEqualTo(path);
        await Assert.That(adopted.Actor).IsEqualTo("organizer-123");
        await Assert.That(evt.ArchiveAdoptedAt).IsNotNull();
        await Assert.That(evt.ArchiveSourceReadmeSha).IsEqualTo("readme-sha");
        await Assert.That(evt.ArchiveSourceMetadataSha).IsEqualTo("metadata-sha");
        await Assert.That(await db.EventArchiveOutboxMessages.CountAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task PreviewArchiveAdoptionAsync_MetadataOnlyEntry_AllowsGeneratedReadmePreview()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        const string path = "events/2026/2026-11-28-community-summit";
        var entry = new EventArchiveEntry(
            null,
            """
            title: "Community Summit"
            date: 2026-11-28
            status: upcoming
            """,
            null,
            "metadata-sha");
        var orchestrator = CreateOrchestrator(db, CreateRepository(entry));

        var preview = await orchestrator.PreviewArchiveAdoptionAsync(evt.Id, path);

        await Assert.That(preview.Preview.ExistingReadme).IsNull();
        await Assert.That(preview.Preview.CandidateReadme).Contains("Community Summit");
    }

    [Test]
    public async Task PreviewArchiveAdoptionAsync_PathClaimedByAnotherEvent_RejectsAdoption()
    {
        await using var db = CreateDbContext();
        const string path = "events/2026/2026-11-28-community-summit";
        var evt = CreatePublishedEvent();
        var otherEvent = CreatePublishedEvent(path);
        db.Events.AddRange(evt, otherEvent);
        await db.SaveChangesAsync();
        var repository = CreateRepository(CreateValidArchiveEntry());
        var orchestrator = CreateOrchestrator(db, repository);

        var action = async () => { await orchestrator.PreviewArchiveAdoptionAsync(evt.Id, path); };

        await Assert.That(action).Throws<InvalidOperationException>();
        await repository.DidNotReceiveWithAnyArgs().GetArchiveEntryAsync(default!, default);
    }

    [Test]
    public async Task PreviewArchiveAdoptionAsync_AmbiguousEventIdentity_RefusesAdoption()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var repository = CreateRepository(new EventArchiveEntry(
            "# Community Summit",
            """
            title: "Community Summit"
            title: "Different event"
            date: 2026-11-28
            """,
            "readme-sha",
            "metadata-sha"));
        var orchestrator = CreateOrchestrator(db, repository);

        var action = async () =>
        {
            await orchestrator.PreviewArchiveAdoptionAsync(
                evt.Id,
                "events/2026/2026-11-28-community-summit");
        };

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task PreviewArchiveAdoptionAsync_DuplicateNestedEventIdentity_RefusesAdoption()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var repository = CreateRepository(new EventArchiveEntry(
            "# Community Summit",
            $"""
            title: "Community Summit"
            date: 2026-11-28
            bethuya:
              eventId: {evt.Id:D}
              eventId: {evt.Id:D}
            """,
            "readme-sha",
            "metadata-sha"));
        var orchestrator = CreateOrchestrator(db, repository);

        var action = async () =>
        {
            await orchestrator.PreviewArchiveAdoptionAsync(
                evt.Id,
                "events/2026/2026-11-28-community-summit");
        };

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task PreviewArchivePublicationAsync_DatePathMismatch_RefusesWithoutRemotePreview()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent("events/2026/2026-11-27-community-summit");
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var repository = CreateRepository();
        var orchestrator = CreateOrchestrator(db, repository);

        var action = async () => { await orchestrator.PreviewArchivePublicationAsync(evt.Id); };

        await Assert.That(action).Throws<InvalidOperationException>();
        await repository.DidNotReceiveWithAnyArgs().PreviewEventAsync(default!, default);
    }

    [Test]
    public async Task RefreshArchivePublicationStatusAsync_MergedPullRequest_PersistsPublishedState()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent("events/2026/2026-11-28-community-summit");
        evt.ArchivePullRequestNumber = 42;
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var repository = CreateRepository();
        repository.GetPublicationStatusAsync(
                42,
                "events/2026/2026-11-28-community-summit",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new EventArchivePullRequestStatus(
                true,
                true,
                true,
                42,
                "https://github.com/HackerspaceMumbai/events/pull/42",
                "https://github.com/HackerspaceMumbai/events/tree/main/events/2026/2026-11-28-community-summit")));
        var orchestrator = CreateOrchestrator(db, repository);

        await orchestrator.RefreshArchivePublicationStatusAsync(evt.Id);

        await Assert.That(evt.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.Published);
        await Assert.That(evt.GitHubFolderUrl).IsEqualTo(
            "https://github.com/HackerspaceMumbai/events/tree/main/events/2026/2026-11-28-community-summit");
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"archive-publication-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BethuyaDbContext(options);
    }

    private static Event CreatePublishedEvent(string? archivePath = null)
    {
        var evt = new Event
        {
            Title = "Community Summit",
            CreatedBy = "event-creator",
            Capacity = 120,
            StartDate = new DateTimeOffset(2026, 11, 28, 10, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 11, 28, 16, 0, 0, TimeSpan.Zero),
            ArchiveFolderPath = archivePath
        };
        evt.TransitionLifecycleTo(MeetupLifecycleState.VenueLocked, DateTimeOffset.UtcNow);
        evt.TransitionLifecycleTo(MeetupLifecycleState.CfpOpen, DateTimeOffset.UtcNow);
        evt.TransitionLifecycleTo(MeetupLifecycleState.ReviewAndPlanning, DateTimeOffset.UtcNow);
        evt.TransitionLifecycleTo(MeetupLifecycleState.AgendaApproved, DateTimeOffset.UtcNow);
        evt.TransitionLifecycleTo(MeetupLifecycleState.Published, DateTimeOffset.UtcNow);
        return evt;
    }

    private static EventArchiveEntry CreateValidArchiveEntry()
        => new(
            "# Community Summit",
            """
            title: "Community Summit"
            date: 2026-11-28
            status: upcoming
            description: "Original description"
            """,
            "readme-sha",
            "metadata-sha");

    private static IGitHubEventRepository CreateRepository(EventArchiveEntry? entry = null)
    {
        var repository = Substitute.For<IGitHubEventRepository>();
        repository.GetArchiveEntryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(entry));
        repository.PreviewEventAsync(Arg.Any<EventPublicationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(CreatePreview(call.Arg<EventPublicationRequest>(), entry)));
        repository.PreviewEventWithEntryAsync(
                Arg.Any<EventPublicationRequest>(),
                Arg.Any<EventArchiveEntry>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(CreatePreview(
                call.Arg<EventPublicationRequest>(),
                call.Arg<EventArchiveEntry>())));
        return repository;
    }

    private static EventArchivePreview CreatePreview(
        EventPublicationRequest request,
        EventArchiveEntry? entry,
        string previewToken = "preview-token")
        => new(
            request.FolderPath,
            previewToken,
            entry?.ReadmeMarkdown,
            request.ReadmeMarkdown,
            entry?.MetadataYaml,
            request.MetadataJson,
            true);

    private static EventLifecycleOrchestrator CreateOrchestrator(
        BethuyaDbContext db,
        IGitHubEventRepository repository)
        => new(
            db,
            Substitute.For<ILumaRegistrationService>(),
            Substitute.For<ITeamsNotificationService>(),
            Substitute.For<IWebCacheInvalidationService>(),
            repository);
}

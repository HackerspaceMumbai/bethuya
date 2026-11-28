using System.Data.Common;
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
    public async Task CompleteAsync_AssetDueAtWithOffset_StoresUtc()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        evt.Agenda = new Agenda
        {
            EventId = evt.Id,
            Sessions = [new AgendaSession { Title = "Postgres in production", Order = 1 }]
        };
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var orchestrator = CreateOrchestrator(db, CreateRepository());

        await orchestrator.CompleteAsync(evt.Id, "organizer", new DateTimeOffset(2026, 12, 19, 18, 0, 0, TimeSpan.FromHours(5.5)));

        var session = await db.AgendaSessions.SingleAsync();
        await Assert.That(session.AssetStatus).IsEqualTo(SessionAssetStatus.PendingUpload);
        await Assert.That(session.AssetsDueAt!.Value.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(session.AssetsDueAt!.Value).IsEqualTo(new DateTimeOffset(2026, 12, 19, 12, 30, 0, TimeSpan.Zero));
    }

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
        await using var db = CreateSqliteDbContext();
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
        await using var db = CreateSqliteDbContext();
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
        await using var db = CreateSqliteDbContext();
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

        await db.Entry(outbox).ReloadAsync();
        await Assert.That(outbox.ProcessedAt).IsNull();
        await Assert.That(outbox.LastError).IsNull();
        await Assert.That(outbox.ConfirmedPreviewToken).IsEqualTo("confirmed-snapshot-2");
        await Assert.That(evt.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.Pending);
        await Assert.That(await db.EventArchiveOutboxMessages.CountAsync()).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConfirmArchivePublicationAsync_HistoricalProjection_RequeuesWhenNotCurrent(bool changedSnapshot)
    {
        await using var db = CreateSqliteDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var token = "snapshot-a";
        var repository = CreateRepository();
        repository.PreviewEventAsync(Arg.Any<EventPublicationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(CreatePreview(call.Arg<EventPublicationRequest>(), null, token)));
        var orchestrator = CreateOrchestrator(db, repository);
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer", token);
        var original = await db.EventArchiveOutboxMessages.SingleAsync();
        original.ProcessedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        if (changedSnapshot)
        {
            token = "snapshot-new";
        }
        else
        {
            evt.Description = "Projection B";
            await db.SaveChangesAsync();
            await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer", token);
            var newer = await db.EventArchiveOutboxMessages.SingleAsync(item => item.Id != original.Id);
            newer.ProcessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            evt.Description = null;
            await db.SaveChangesAsync();
        }

        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer", token);

        await db.Entry(original).ReloadAsync();
        await Assert.That(original.ProcessedAt).IsNull();
        await Assert.That(original.ConfirmedPreviewToken).IsEqualTo(token);
        await Assert.That(evt.ArchivePublicationIdempotencyKey).IsEqualTo(original.IdempotencyKey);
        await Assert.That(evt.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.Pending);
        await Assert.That(await db.EventArchiveOutboxMessages.CountAsync()).IsEqualTo(changedSnapshot ? 1 : 2);
    }

    [Test]
    public async Task ConfirmArchivePublicationAsync_ConcurrentReactivation_DoesNotOverwriteWinningClaim()
    {
        var interceptor = new ReactivationRaceInterceptor();
        await using var db = CreateSqliteDbContext(interceptor);
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var orchestrator = CreateOrchestrator(db, CreateRepository());
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer", "preview-token");
        var outbox = await db.EventArchiveOutboxMessages.SingleAsync();
        outbox.ProcessedAt = DateTimeOffset.UtcNow;
        outbox.LastError = "old snapshot failed";
        await db.SaveChangesAsync();
        interceptor.Enabled = true;

        await Assert.That(async () => await orchestrator.ConfirmArchivePublicationAsync(
            evt.Id, "organizer", "preview-token")).Throws<InvalidOperationException>();

        // The rejected transaction rolls back the simulated winner too; its write must never be accepted.
        await db.Entry(outbox).ReloadAsync();
        await Assert.That(outbox.ProcessedAt).IsNotNull();
        await Assert.That(outbox.LastError).IsEqualTo("old snapshot failed");
    }

    [Test]
    [Arguments("events/2026/2026-11-28-other-path", true)]
    [Arguments("events/2026/2026-11-28-community-summit\n", false)]
    public async Task PreviewArchiveAdoptionAsync_InvalidEstablishedOrTrailingPath_RefusesBeforeRemoteRead(string path, bool established)
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent(established ? "events/2026/2026-11-28-community-summit" : null);
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var repository = CreateRepository(CreateValidArchiveEntry());

        await Assert.That(async () => await CreateOrchestrator(db, repository)
            .PreviewArchiveAdoptionAsync(evt.Id, path)).Throws<InvalidOperationException>();
        await repository.DidNotReceiveWithAnyArgs().GetArchiveEntryAsync(default!, default);
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
    public async Task PreviewArchiveAdoptionAsync_FourSpaceForeignIdentity_RefusesWithoutAdoption()
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var entry = CreateValidArchiveEntry() with
        {
            MetadataYaml = $"title: \"Community Summit\"\ndate: 2026-11-28\nbethuya:\n    eventId: {Guid.NewGuid():D}\n"
        };
        var repository = CreateRepository(entry);
        var orchestrator = CreateOrchestrator(db, repository);

        await Assert.That(async () =>
            await orchestrator.PreviewArchiveAdoptionAsync(evt.Id, "events/2026/2026-11-28-community-summit"))
            .Throws<InvalidOperationException>();
        await Assert.That(evt.ArchiveFolderPath).IsNull();
        await repository.DidNotReceiveWithAnyArgs().PreviewEventWithEntryAsync(default!, default!, default);
    }

    [Test]
    [Arguments("bethuya: {eventId: other-event}")]
    [Arguments("\"bethuya\":\n  eventId: other-event")]
    public async Task PreviewArchiveAdoptionAsync_UnsupportedIdentityMapping_Refuses(string mapping)
    {
        await using var db = CreateDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var entry = CreateValidArchiveEntry() with
        {
            MetadataYaml = $"title: \"Community Summit\"\ndate: 2026-11-28\n{mapping}\n"
        };
        var orchestrator = CreateOrchestrator(db, CreateRepository(entry));

        await Assert.That(async () =>
            await orchestrator.PreviewArchiveAdoptionAsync(evt.Id, "events/2026/2026-11-28-community-summit"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RefreshArchivePublicationStatusAsync_MergedPullRequest_PersistsPublishedState()
    {
        await using var db = CreateSqliteDbContext();
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

    [Test]
    public async Task RefreshArchivePublicationStatusAsync_ConcurrentConfirmation_DoesNotOverwritePendingProjection()
    {
        await using var db = CreateSqliteDbContext();
        var evt = CreatePublishedEvent("events/2026/2026-11-28-community-summit");
        evt.ArchivePullRequestNumber = 42;
        evt.ArchivePublicationIdempotencyKey = "old-projection";
        evt.ArchivePublicationStatus = EventArchivePublicationStatus.DraftPrPending;
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var repository = CreateRepository();
        repository.GetPublicationStatusAsync(42, evt.ArchiveFolderPath!, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await using var concurrentDb = new BethuyaDbContext(
                    new DbContextOptionsBuilder<BethuyaDbContext>()
                        .UseSqlite(db.Database.GetDbConnection()).Options);
                var current = await concurrentDb.Events.SingleAsync();
                current.Title = "Changed Community Summit";
                await concurrentDb.SaveChangesAsync();
                await CreateOrchestrator(concurrentDb, repository)
                    .ConfirmArchivePublicationAsync(evt.Id, "organizer-123", "preview-token");
                return new EventArchivePullRequestStatus(
                    false, true, true, 42, "https://github.com/HackerspaceMumbai/events/pull/42", "https://github.com/archive");
            });

        await Assert.That(async () =>
            await CreateOrchestrator(db, repository).RefreshArchivePublicationStatusAsync(evt.Id))
            .Throws<InvalidOperationException>();
        await db.Entry(evt).ReloadAsync();
        await Assert.That(evt.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.Pending);
        await Assert.That(evt.ArchivePullRequestNumber).IsEqualTo(42);
        await Assert.That(evt.ArchivePullRequestUrl).IsNull();
        await Assert.That(evt.GitHubFolderUrl).IsNull();
        await Assert.That(evt.ArchivePublicationIdempotencyKey).IsNotEqualTo("old-projection");
        await Assert.That(await db.EventArchiveOutboxMessages.CountAsync()).IsEqualTo(1);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RefreshArchivePublicationStatusAsync_ChangedPublicationIdentity_RefusesStaleResult(bool changeKey)
    {
        await using var db = CreateSqliteDbContext();
        var evt = CreatePublishedEvent("events/2026/2026-11-28-community-summit");
        evt.ArchivePullRequestNumber = 42;
        evt.ArchivePublicationIdempotencyKey = "old-projection";
        evt.ArchivePublicationStatus = EventArchivePublicationStatus.DraftPrPending;
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var repository = CreateRepository();
        repository.GetPublicationStatusAsync(42, evt.ArchiveFolderPath!, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await using var concurrentDb = new BethuyaDbContext(
                    new DbContextOptionsBuilder<BethuyaDbContext>()
                        .UseSqlite(db.Database.GetDbConnection()).Options);
                var current = await concurrentDb.Events.SingleAsync();
                current.ArchivePublicationIdempotencyKey = changeKey ? "new-projection" : "old-projection";
                current.ArchivePullRequestNumber = changeKey ? 42 : 43;
                await concurrentDb.SaveChangesAsync();
                return new EventArchivePullRequestStatus(
                    false, true, true, 42, "https://github.com/HackerspaceMumbai/events/pull/42", "https://github.com/archive");
            });

        await Assert.That(async () =>
            await CreateOrchestrator(db, repository).RefreshArchivePublicationStatusAsync(evt.Id))
            .Throws<InvalidOperationException>();
        await db.Entry(evt).ReloadAsync();
        await Assert.That(evt.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.DraftPrPending);
        await Assert.That(evt.ArchivePullRequestUrl).IsNull();
        await Assert.That(evt.GitHubFolderUrl).IsNull();
    }

    [Test]
    public async Task ConfirmArchivePublicationAsync_OlderPendingProjection_RejectsInsteadOfAcknowledgingItAsLatest()
    {
        await using var db = CreateSqliteDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var orchestrator = CreateOrchestrator(db, CreateRepository());
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer-a", "preview-token");
        evt.Description = "Projection B";
        await db.SaveChangesAsync();
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer-b", "preview-token");
        var latestKey = evt.ArchivePublicationIdempotencyKey;
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer-b", "preview-token");
        evt.Description = null;
        await db.SaveChangesAsync();

        await Assert.That(async () => await orchestrator.ConfirmArchivePublicationAsync(
            evt.Id, "organizer-a", "preview-token")).Throws<InvalidOperationException>();
        await db.Entry(evt).ReloadAsync();
        await Assert.That(evt.ArchivePublicationIdempotencyKey).IsEqualTo(latestKey);
        await Assert.That(evt.ArchivePublicationRequestedBy).IsEqualTo("organizer-b");
        await Assert.That(await db.EventArchiveOutboxMessages.CountAsync()).IsEqualTo(2);
    }

    [Test]
    public async Task ConfirmArchivePublicationAsync_ConfirmationDuringPreview_RequeuesCompletedProjectionAfterNewerWork()
    {
        await using var db = CreateSqliteDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var repository = CreateRepository();
        var orchestrator = CreateOrchestrator(db, repository);
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer-a", "preview-token");
        var original = await db.EventArchiveOutboxMessages.SingleAsync();
        original.ProcessedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        repository.PreviewEventAsync(Arg.Any<EventPublicationRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                repository.PreviewEventAsync(Arg.Any<EventPublicationRequest>(), Arg.Any<CancellationToken>())
                    .Returns(inner => Task.FromResult(CreatePreview(inner.Arg<EventPublicationRequest>(), null, "preview-token")));
                await using var concurrentDb = new BethuyaDbContext(new DbContextOptionsBuilder<BethuyaDbContext>()
                    .UseSqlite(db.Database.GetDbConnection()).Options);
                var current = await concurrentDb.Events.SingleAsync();
                current.Description = "Projection B";
                await concurrentDb.SaveChangesAsync();
                await CreateOrchestrator(concurrentDb, repository)
                    .ConfirmArchivePublicationAsync(evt.Id, "organizer-b", "preview-token");
                return CreatePreview(call.Arg<EventPublicationRequest>(), null, "preview-token");
            });

        await Assert.That(async () => await orchestrator.ConfirmArchivePublicationAsync(
            evt.Id, "organizer-a", "preview-token")).Throws<InvalidOperationException>();

        await db.Entry(original).ReloadAsync();
        await db.Entry(evt).ReloadAsync();
        await Assert.That(original.ProcessedAt).IsNotNull();
        await Assert.That(evt.ArchivePublicationIdempotencyKey).IsNotEqualTo(original.IdempotencyKey);
        await Assert.That(evt.ArchivePublicationRequestedBy).IsEqualTo("organizer-b");
        await Assert.That(evt.Description).IsEqualTo("Projection B");
        await Assert.That(await db.EventArchiveOutboxMessages.CountAsync()).IsEqualTo(2);
    }

    private static BethuyaDbContext CreateSqliteDbContext(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>().UseSqlite("Data Source=:memory:");
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }

        var db = new BethuyaDbContext(options.Options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    [Test]
    [Arguments(EventArchivePublicationStatus.Pending)]
    [Arguments(EventArchivePublicationStatus.Failed)]
    public async Task RefreshArchivePublicationStatusAsync_NewRequest_KeepsPriorPrDiscoverableWithoutPublishingNewWork(
        EventArchivePublicationStatus requestStatus)
    {
        await using var db = CreateSqliteDbContext();
        var evt = CreatePublishedEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var repository = CreateRepository();
        var orchestrator = CreateOrchestrator(db, repository);
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer", "preview-token");
        (await db.EventArchiveOutboxMessages.SingleAsync()).ProcessedAt = DateTimeOffset.UtcNow;
        evt.ArchivePublicationStatus = EventArchivePublicationStatus.DraftPrPending;
        evt.ArchivePullRequestNumber = 42;
        evt.ArchivePullRequestUrl = "https://github.com/org/repo/pull/42";
        evt.Description = "New projection";
        await db.SaveChangesAsync();
        await orchestrator.ConfirmArchivePublicationAsync(evt.Id, "organizer", "preview-token");
        var newKey = evt.ArchivePublicationIdempotencyKey;
        evt.ArchivePublicationStatus = requestStatus;
        await db.SaveChangesAsync();
        repository.GetPublicationStatusAsync(42, evt.ArchiveFolderPath!, Arg.Any<CancellationToken>())
            .Returns(new EventArchivePullRequestStatus(true, true, true, 42,
                "https://github.com/org/repo/pull/42", "https://github.com/org/repo/tree/main/events/example"));

        var status = await orchestrator.RefreshArchivePublicationStatusAsync(evt.Id);

        await Assert.That(status.IsMerged).IsTrue();
        await db.Entry(evt).ReloadAsync();
        await Assert.That(evt.ArchivePullRequestNumber).IsEqualTo(42);
        await Assert.That(evt.ArchivePublicationStatus).IsEqualTo(requestStatus);
        await Assert.That(evt.ArchivePublicationIdempotencyKey).IsEqualTo(newKey);
        await Assert.That(evt.GitHubFolderUrl).IsNull();
    }

    private sealed class ReactivationRaceInterceptor : DbCommandInterceptor
    {
        public bool Enabled { get; set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await ReactivateAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await ReactivateAsync(command, cancellationToken);
            return result;
        }

        private async Task ReactivateAsync(DbCommand command, CancellationToken ct)
        {
            if (!Enabled || !command.CommandText.StartsWith("UPDATE \"EventArchiveOutboxMessages\"", StringComparison.Ordinal))
            {
                return;
            }

            Enabled = false;
            await using var winner = command.Connection!.CreateCommand();
            winner.Transaction = command.Transaction;
            winner.CommandText = """
                UPDATE "EventArchiveOutboxMessages"
                SET "ProcessedAt" = NULL, "ClaimToken" = 'winning-worker',
                    "ConfirmedPreviewToken" = 'winning-snapshot', "ConfirmedBy" = 'winning-organizer'
                """;
            await winner.ExecuteNonQueryAsync(ct);
        }
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

using System.Data.Common;
using System.Reflection;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hackmum.Bethuya.Tests.Services;

/// <summary>
/// Verifies that the archive outbox continues to process the newest event projection even when older items fail repeatedly.
/// </summary>
public sealed class EventArchiveOutboxProcessorTests
{
    /// <summary>
    /// Ensures an exhausted older projection does not block the next valid snapshot for the same event.
    /// ExecuteUpdateAsync is not supported by the EF Core InMemory provider, so this test uses a
    /// SQLite in-memory database, which is relational and exercises the real translated SQL update.
    /// </summary>
    [Test]
    public async Task RecordFailureAsync_ExhaustedOlderMessage_DoesNotBlockNewerMessage()
    {
        using var connection = CreateOpenSqliteConnection();
        await using var db = CreateSqliteDbContext(connection);

        var eventId = EventId.From(Guid.NewGuid());
        var older = new EventArchiveOutboxMessage
        {
            Id = EventArchiveOutboxMessageId.From(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-06-30-old-event-abc123",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "old readme",
            MetadataJson = "{ \"version\": 1 }",
            IdempotencyKey = "old-key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AttemptCount = 8,
            CreatedAt = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero),
            ClaimToken = "old-claim"
        };

        var newer = new EventArchiveOutboxMessage
        {
            Id = EventArchiveOutboxMessageId.From(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-new-event-def456",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "new readme",
            MetadataJson = "{ \"version\": 2 }",
            IdempotencyKey = "new-key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AttemptCount = 0,
            CreatedAt = older.CreatedAt,
            ClaimToken = "new-claim"
        };

        db.EventArchiveOutboxMessages.AddRange(older, newer);
        await db.SaveChangesAsync();

        var processor = new EventArchiveOutboxProcessor(new TestScopeFactory(connection), NullLogger<EventArchiveOutboxProcessor>.Instance);
        var failureMethod = typeof(EventArchiveOutboxProcessor).GetMethod("RecordFailureAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var workItemType = typeof(EventArchiveOutboxProcessor).GetNestedType("EventArchiveOutboxWorkItem", BindingFlags.NonPublic)!;
        var workItem = Activator.CreateInstance(
            workItemType,
            older.Id,
            older.EventId,
            older.FolderPath,
            older.Title,
            older.ReadmeMarkdown,
            older.MetadataJson,
            older.IdempotencyKey,
            older.ConfirmedPreviewToken ?? "test-preview-token",
            older.AttemptCount,
            older.ClaimToken);

        var task = (Task)failureMethod.Invoke(
            processor,
            [workItem, new InvalidOperationException("permanent failure"), CancellationToken.None, false])!;
        await task;

        // Clear EF cache to force fresh load from database
        db.ChangeTracker.Clear();

        // Reload message from database to check updates (ExecuteUpdateAsync bypasses EF tracking)
        var olderReloaded = await db.EventArchiveOutboxMessages.FirstAsync(m => m.Id == older.Id);

        var claimMethod = typeof(EventArchiveOutboxProcessor).GetMethod("ClaimNextAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var claimTask = (Task)claimMethod.Invoke(processor, [CancellationToken.None])!;
        await claimTask;

        var result = claimTask.GetType().GetProperty("Result")!.GetValue(claimTask);
        var claimedId = (object?)result?.GetType().GetProperty("Id")?.GetValue(result);

        await Assert.That(olderReloaded.ProcessedAt).IsNotNull();
        await Assert.That(claimedId).IsNotNull();
        await Assert.That(((EventArchiveOutboxMessageId)claimedId!).Value).IsEqualTo(newer.Id.Value);
    }

    /// <summary>
    /// Ensures the last remaining projection remains retryable instead of being permanently discarded.
    /// ExecuteUpdateAsync is not supported by the EF Core InMemory provider, so this test uses a
    /// SQLite in-memory database, which is relational and exercises the real translated SQL update.
    /// </summary>
    [Test]
    public async Task RecordFailureAsync_ExhaustedLatestMessage_KeepsMessageAvailableForRetry()
    {
        using var connection = CreateOpenSqliteConnection();
        await using var db = CreateSqliteDbContext(connection);

        var eventId = EventId.From(Guid.NewGuid());
        var message = new EventArchiveOutboxMessage
        {
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-event-def456",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "latest readme",
            MetadataJson = "{ \"version\": 99 }",
            IdempotencyKey = "latest-key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AttemptCount = 8,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ClaimToken = "latest-claim"
        };

        db.EventArchiveOutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var processor = new EventArchiveOutboxProcessor(new TestScopeFactory(connection), NullLogger<EventArchiveOutboxProcessor>.Instance);
        var failureMethod = typeof(EventArchiveOutboxProcessor).GetMethod("RecordFailureAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var workItemType = typeof(EventArchiveOutboxProcessor).GetNestedType("EventArchiveOutboxWorkItem", BindingFlags.NonPublic)!;
        var workItem = Activator.CreateInstance(
            workItemType,
            message.Id,
            message.EventId,
            message.FolderPath,
            message.Title,
            message.ReadmeMarkdown,
            message.MetadataJson,
            message.IdempotencyKey,
            message.ConfirmedPreviewToken ?? "test-preview-token",
            message.AttemptCount,
            message.ClaimToken);

        var task = (Task)failureMethod.Invoke(
            processor,
            [workItem, new InvalidOperationException("retryable failure"), CancellationToken.None, false])!;
        await task;

        // Clear EF cache to force fresh load from database
        db.ChangeTracker.Clear();

        // Reload message from database to check updates (ExecuteUpdateAsync bypasses EF tracking)
        var messageReloaded = await db.EventArchiveOutboxMessages.FirstAsync(m => m.Id == message.Id);

        await Assert.That(messageReloaded.ProcessedAt).IsNull();
        await Assert.That(messageReloaded.LastError).IsNotNull();
        await Assert.That(messageReloaded.AvailableAt > DateTimeOffset.UtcNow).IsTrue();

        var claimMethod = typeof(EventArchiveOutboxProcessor).GetMethod("ClaimNextAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var claimTask = (Task)claimMethod.Invoke(processor, [CancellationToken.None])!;
        await claimTask;

        var result = claimTask.GetType().GetProperty("Result")!.GetValue(claimTask);
        var claimedId = (object?)result?.GetType().GetProperty("Id")?.GetValue(result);

        await Assert.That(claimedId).IsNull();
    }

    /// <summary>
    /// Ensures equal-timestamp messages for one event are claimed in stable identifier order, including
    /// when the earlier message is leased and after it is processed.
    /// </summary>
    [Test]
    public async Task ClaimNextAsync_EqualCreatedAtMessages_ClaimsInStableIdentifierOrder()
    {
        using var connection = CreateOpenSqliteConnection();
        await using var db = CreateSqliteDbContext(connection);
        var eventId = EventId.From(Guid.NewGuid());
        var createdAt = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        var firstId = EventArchiveOutboxMessageId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondId = EventArchiveOutboxMessageId.From(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var laterMessage = new EventArchiveOutboxMessage
        {
            Id = secondId,
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-11-28-later",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "later readme",
            MetadataJson = "{ \"version\": 2 }",
            IdempotencyKey = "later-key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = createdAt
        };
        var earlierMessage = new EventArchiveOutboxMessage
        {
            Id = firstId,
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-11-28-earlier",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "earlier readme",
            MetadataJson = "{ \"version\": 1 }",
            IdempotencyKey = "earlier-key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = createdAt
        };

        db.EventArchiveOutboxMessages.AddRange(laterMessage, earlierMessage);
        await db.SaveChangesAsync();

        var processor = new EventArchiveOutboxProcessor(
            new TestScopeFactory(connection),
            NullLogger<EventArchiveOutboxProcessor>.Instance);
        var claimMethod = typeof(EventArchiveOutboxProcessor).GetMethod(
            "ClaimNextAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        async Task<EventArchiveOutboxMessageId?> ClaimNextIdAsync()
        {
            var task = (Task)claimMethod.Invoke(processor, [CancellationToken.None])!;
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task);
            var claimedId = (object?)result?.GetType().GetProperty("Id")?.GetValue(result);
            return claimedId is EventArchiveOutboxMessageId id ? id : null;
        }

        var firstClaim = await ClaimNextIdAsync();
        await Assert.That(firstClaim).IsEqualTo(firstId);
        await Assert.That(await ClaimNextIdAsync()).IsNull();

        db.ChangeTracker.Clear();
        var claimedMessage = await db.EventArchiveOutboxMessages.FirstAsync(message => message.Id == firstId);
        claimedMessage.ProcessedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var secondClaim = await ClaimNextIdAsync();
        await Assert.That(secondClaim).IsEqualTo(secondId);
    }

    [Test]
    public async Task ClaimNextAsync_BackoffSuccessors_DoNotHideEligibleOtherEvents()
    {
        using var connection = CreateOpenSqliteConnection();
        await using var db = CreateSqliteDbContext(connection);
        var blockedEventId = EventId.From(Guid.NewGuid());
        var readyEventId = EventId.From(Guid.NewGuid());
        var createdAt = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        var blockedMessageId = EventArchiveOutboxMessageId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var messages = new List<EventArchiveOutboxMessage>
        {
            new()
            {
                Id = blockedMessageId,
                EventId = blockedEventId,
                Destination = "archive/test",
                FolderPath = "events/2026/blocked",
                ConfirmedBy = "test-organizer",
                ReadmeMarkdown = "blocked readme",
                MetadataJson = "{ }",
                IdempotencyKey = "blocked-key",
                AvailableAt = DateTimeOffset.UtcNow.AddHours(1),
                CreatedAt = createdAt
            }
        };

        for (var index = 2; index <= 101; index++)
        {
            messages.Add(new EventArchiveOutboxMessage
            {
                Id = EventArchiveOutboxMessageId.From(Guid.Parse($"00000000-0000-0000-0000-{index:D12}")),
                EventId = blockedEventId,
                Destination = "archive/test",
                FolderPath = $"events/2026/blocked-successor-{index}",
                ConfirmedBy = "test-organizer",
                ReadmeMarkdown = "blocked successor readme",
                MetadataJson = "{ }",
                IdempotencyKey = $"blocked-successor-{index}",
                AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                CreatedAt = createdAt
            });
        }

        var readyMessageId = EventArchiveOutboxMessageId.From(Guid.Parse("00000000-0000-0000-0000-000000000200"));
        messages.Add(new EventArchiveOutboxMessage
        {
            Id = readyMessageId,
            EventId = readyEventId,
            Destination = "archive/test",
            FolderPath = "events/2026/ready",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "ready readme",
            MetadataJson = "{ }",
            IdempotencyKey = "ready-key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = createdAt
        });

        db.EventArchiveOutboxMessages.AddRange(messages);
        await db.SaveChangesAsync();

        var processor = new EventArchiveOutboxProcessor(
            new TestScopeFactory(connection),
            NullLogger<EventArchiveOutboxProcessor>.Instance);
        var claimMethod = typeof(EventArchiveOutboxProcessor).GetMethod(
            "ClaimNextAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)claimMethod.Invoke(processor, [CancellationToken.None])!;
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        var claimedId = (object?)result?.GetType().GetProperty("Id")?.GetValue(result);

        await Assert.That(claimedId).IsEqualTo(readyMessageId);
    }

    /// <summary>
    /// Ensures the claim-lease heartbeat renews the lease while the claim token still matches, so a live worker's
    /// lease never lapses out from under it while it is still publishing.
    /// </summary>
    [Test]
    public async Task TryRenewClaimAsync_ClaimStillHeld_RenewsLeaseAndReturnsTrue()
    {
        // ExecuteUpdateAsync is not supported by the EF Core InMemory provider, so this test uses a
        // SQLite in-memory database, which is relational and exercises the real translated SQL update.
        using var connection = CreateOpenSqliteConnection();
        await using var db = CreateSqliteDbContext(connection);

        var message = new EventArchiveOutboxMessage
        {
            EventId = EventId.From(Guid.NewGuid()),
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-event-def456",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "readme",
            MetadataJson = "{ }",
            IdempotencyKey = "key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ClaimToken = "claim-token-1",
            LockedUntil = DateTimeOffset.UtcNow.AddSeconds(5)
        };

        db.EventArchiveOutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var processor = new EventArchiveOutboxProcessor(new TestScopeFactory(connection), NullLogger<EventArchiveOutboxProcessor>.Instance);
        var workItem = CreateWorkItem(message);

        var renewMethod = typeof(EventArchiveOutboxProcessor).GetMethod("TryRenewClaimAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var renewTask = (Task<bool>)renewMethod.Invoke(processor, [workItem, CancellationToken.None])!;
        var renewed = await renewTask;

        await Assert.That(renewed).IsTrue();

        var refreshed = await db.EventArchiveOutboxMessages.AsNoTracking().FirstAsync(item => item.Id == message.Id);
        await Assert.That(refreshed.LockedUntil > DateTimeOffset.UtcNow.AddMinutes(1)).IsTrue();
    }

    /// <summary>
    /// Ensures the claim-lease heartbeat reports the claim as lost once another worker has reclaimed the message
    /// (its claim token no longer matches), so the in-flight publish for the superseded worker can be cancelled
    /// before it can overwrite content a newer claimant has already published.
    /// </summary>
    [Test]
    public async Task TryRenewClaimAsync_ClaimReclaimedByAnotherWorker_ReturnsFalse()
    {
        using var connection = CreateOpenSqliteConnection();
        await using var db = CreateSqliteDbContext(connection);

        var message = new EventArchiveOutboxMessage
        {
            EventId = EventId.From(Guid.NewGuid()),
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-event-def456",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "readme",
            MetadataJson = "{ }",
            IdempotencyKey = "key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ClaimToken = "claim-token-1",
            LockedUntil = DateTimeOffset.UtcNow.AddSeconds(-1)
        };

        db.EventArchiveOutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var processor = new EventArchiveOutboxProcessor(new TestScopeFactory(connection), NullLogger<EventArchiveOutboxProcessor>.Instance);
        var workItem = CreateWorkItem(message);

        // Simulate another worker reclaiming the (lease-expired) message with a new claim token.
        message.ClaimToken = "claim-token-2";
        await db.SaveChangesAsync();

        var renewMethod = typeof(EventArchiveOutboxProcessor).GetMethod("TryRenewClaimAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var renewTask = (Task<bool>)renewMethod.Invoke(processor, [workItem, CancellationToken.None])!;
        var renewed = await renewTask;

        await Assert.That(renewed).IsFalse();
    }

    /// <summary>
    /// Verifies the complete hosted service flow: the processor starts, claims a message,
    /// publishes to GitHub, and persists completion without crashing or deadlocking.
    /// </summary>
    [Test]
    public async Task HostedService_ExecuteAsync_ProcessesAndCompletesMessage()
    {
        var connectionString = $"Data Source=event-archive-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();
        await using var db = CreateSqliteDbContext(keeperConnection);

        // Seed an outbox message ready to claim.
        var eventId = EventId.From(Guid.NewGuid());
        var message = new EventArchiveOutboxMessage
        {
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-event-def456",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "# Test Event",
            MetadataJson = "{ \"version\": 1 }",
            ConfirmedPreviewToken = "confirmed-preview-token",
            IdempotencyKey = Guid.NewGuid().ToString(),
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ClaimToken = "initial-token"  // Will be replaced by processor when claimed
        };

        db.EventArchiveOutboxMessages.Add(message);
        await db.SaveChangesAsync();

        // Seed the Event with folder URL placeholder (GitHubEventRepository updates it after publish).
        var @event = new Event
        {
            Id = eventId.Value,
            Title = "Test Event 2026",
            CreatedBy = "test-user",
            ArchivePublicationIdempotencyKey = message.IdempotencyKey
        };
        db.Events.Add(@event);
        await db.SaveChangesAsync();

        var publishResult = new EventPublicationResult(
            FolderUrl: "https://github.com/org/repo/tree/main/events/2026/2026-07-01-event-def456",
            MetadataUrl: "https://github.com/org/repo/blob/main/events/2026/2026-07-01-event-def456/event.yml",
            BranchName: "bethuya/event-archive-test",
            PullRequestNumber: 42,
            PullRequestUrl: "https://github.com/org/repo/pull/42");

        var mockRepository = new MockGitHubEventRepository(publishResult);
        var scopeFactory = new TestScopeFactory(connectionString, mockRepository);
        using var processor = new EventArchiveOutboxProcessor(scopeFactory, NullLogger<EventArchiveOutboxProcessor>.Instance);

        // Execute the processor and wait for the actual publication instead of relying on
        // a fixed delay that can expire before a busy CI runner schedules the polling tick.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var started = false;
        try
        {
            await processor.StartAsync(cts.Token);
            started = true;
            await mockRepository.FirstPublication.WaitAsync(cts.Token);
            await WaitForCompletedProjectionAsync(
                connectionString,
                message.Id,
                eventId,
                publishResult.PullRequestNumber,
                cts.Token);
        }
        finally
        {
            if (started)
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await processor.StopAsync(stopCts.Token);
            }
        }

        // Verify the message was published and persisted.
        await Assert.That(mockRepository.PublishedRequests.Count).IsGreaterThanOrEqualTo(1);

        var publishedRequest = mockRepository.PublishedRequests[0];
        var publishedEventIdGuid = publishedRequest.EventId.Value;
        System.Console.WriteLine($"Published EventId from mock: {publishedEventIdGuid}, Expected: {eventId.Value}, Match: {publishedEventIdGuid == eventId.Value}");
        await Assert.That(publishedRequest.EventId).IsEqualTo(eventId);
        await Assert.That(publishedRequest.ConfirmedPreviewToken).IsEqualTo("confirmed-preview-token");

        // Reload the message using a fresh DbContext to verify ProcessedAt was set.
        await using var verifyDb = CreateSqliteQueryDbContext(connectionString);
        var completedMessage = await verifyDb.EventArchiveOutboxMessages.FirstAsync(m => m.Id == message.Id);

        // Debug: log the state of the reloaded message
        var diagnosticInfo = $"Message after processor: Id={completedMessage.Id}, ProcessedAt={completedMessage.ProcessedAt}, " +
            $"ClaimToken={completedMessage.ClaimToken}, AttemptCount={completedMessage.AttemptCount}, " +
            $"LockedUntil={completedMessage.LockedUntil}, LastError={completedMessage.LastError}";
        System.Console.WriteLine(diagnosticInfo);

        await Assert.That(completedMessage.ProcessedAt).IsNotNull();

        var completedEvent = await verifyDb.Events.FirstAsync(e => e.Id == eventId.Value);
        await Assert.That(completedEvent.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.DraftPrPending);
        await Assert.That(completedEvent.ArchivePullRequestNumber).IsEqualTo(publishResult.PullRequestNumber);
        await Assert.That(completedEvent.ArchiveBranchName).IsEqualTo(publishResult.BranchName);
        await Assert.That(completedEvent.GitHubFolderUrl).IsNull();
    }

    [Test]
    public async Task ProcessOneAsync_EventStateSaveFails_RollsBackOutboxCompletion()
    {
        var connectionString = $"Data Source=event-archive-rollback-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();
        await using var db = CreateSqliteDbContext(keeperConnection);
        var eventId = EventId.From(Guid.NewGuid());
        var message = new EventArchiveOutboxMessage
        {
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-event-def456",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "# Test Event",
            MetadataJson = "title: Test Event",
            ConfirmedPreviewToken = "confirmed-preview-token",
            IdempotencyKey = Guid.NewGuid().ToString(),
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        db.EventArchiveOutboxMessages.Add(message);
        db.Events.Add(new Event
        {
            Id = eventId.Value,
            Title = "Test Event",
            CreatedBy = "test-organizer",
            ArchivePublicationIdempotencyKey = message.IdempotencyKey
        });
        await db.SaveChangesAsync();

        var result = new EventPublicationResult(
            "https://github.com/org/repo/tree/main/events/2026/2026-07-01-event-def456",
            "https://github.com/org/repo/blob/main/events/2026/2026-07-01-event-def456/event.yml",
            "bethuya/event-archive-test",
            42,
            "https://github.com/org/repo/pull/42");
        var repository = new MockGitHubEventRepository(result);
        var scopeFactory = new TestScopeFactory(
            connectionString,
            repository,
            new FailEventSaveChangesInterceptor());
        using var processor = new EventArchiveOutboxProcessor(
            scopeFactory,
            NullLogger<EventArchiveOutboxProcessor>.Instance);
        var processOneAsync = typeof(EventArchiveOutboxProcessor)
            .GetMethod("ProcessOneAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)processOneAsync.Invoke(processor, [CancellationToken.None])!;

        await using var verifyDb = CreateSqliteQueryDbContext(connectionString);
        var savedMessage = await verifyDb.EventArchiveOutboxMessages.FirstAsync(item => item.Id == message.Id);
        var savedEvent = await verifyDb.Events.FirstAsync(item => item.Id == eventId.Value);
        await Assert.That(savedMessage.ProcessedAt).IsNull();
        await Assert.That(savedMessage.LastError).IsNotNull();
        await Assert.That(savedEvent.ArchivePullRequestNumber).IsNull();
    }

    [Test]
    public async Task ProcessOneAsync_ConfirmedSnapshotMismatch_MarksPublicationFailedWithoutRetry()
    {
        var connectionString = $"Data Source=event-archive-snapshot-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();
        await using var db = CreateSqliteDbContext(keeperConnection);
        var eventId = EventId.From(Guid.NewGuid());
        var message = new EventArchiveOutboxMessage
        {
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-event-def456",
            ConfirmedBy = "test-organizer",
            ReadmeMarkdown = "# Test Event",
            MetadataJson = "title: Test Event",
            ConfirmedPreviewToken = "confirmed-preview-token",
            IdempotencyKey = Guid.NewGuid().ToString(),
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        db.EventArchiveOutboxMessages.Add(message);
        db.Events.Add(new Event
        {
            Id = eventId.Value,
            Title = "Test Event",
            CreatedBy = "test-organizer",
            ArchivePublicationStatus = EventArchivePublicationStatus.Pending,
            ArchivePublicationIdempotencyKey = message.IdempotencyKey
        });
        await db.SaveChangesAsync();

        var result = new EventPublicationResult(
            "https://github.com/org/repo/tree/main/events/2026/2026-07-01-event-def456",
            "https://github.com/org/repo/blob/main/events/2026/2026-07-01-event-def456/event.yml",
            "bethuya/event-archive-test",
            42,
            "https://github.com/org/repo/pull/42");
        var repository = new MockGitHubEventRepository(result)
        {
            PublishException = new ArchivePreviewMismatchException(
                "The archive changed after organizer confirmation. Re-preview the archive and confirm again before publication.")
        };
        var processor = new EventArchiveOutboxProcessor(
            new TestScopeFactory(connectionString, repository),
            NullLogger<EventArchiveOutboxProcessor>.Instance);
        var processOneAsync = typeof(EventArchiveOutboxProcessor)
            .GetMethod("ProcessOneAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)processOneAsync.Invoke(processor, [CancellationToken.None])!;

        await using var verifyDb = CreateSqliteQueryDbContext(connectionString);
        var failedMessage = await verifyDb.EventArchiveOutboxMessages.FirstAsync(item => item.Id == message.Id);
        var failedEvent = await verifyDb.Events.FirstAsync(item => item.Id == eventId.Value);
        await Assert.That(failedMessage.ProcessedAt).IsNotNull();
        await Assert.That(failedMessage.LastError).Contains("Re-preview the archive");
        await Assert.That(failedEvent.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.Failed);
    }

    private static async Task WaitForCompletedProjectionAsync(
        string connectionString,
        EventArchiveOutboxMessageId messageId,
        EventId eventId,
        int pullRequestNumber,
        CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var db = CreateSqliteQueryDbContext(connectionString);
                var messageProcessed = await db.EventArchiveOutboxMessages
                    .AsNoTracking()
                    .AnyAsync(message => message.Id == messageId && message.ProcessedAt != null, ct);
                var eventUpdated = await db.Events
                    .AsNoTracking()
                    .AnyAsync(
                        evt => evt.Id == eventId.Value && evt.ArchivePullRequestNumber == pullRequestNumber,
                        ct);
                if (messageProcessed && eventUpdated)
                {
                    return;
                }
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
            {
                // The processor briefly owns the shared in-memory database while committing.
            }

            await Task.Delay(100, ct);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ProcessOneAsync_ConcurrentConfirmation_PreservesNewerEventState(bool terminalFailure)
    {
        using var connection = CreateOpenSqliteConnection();
        await using var db = CreateSqliteDbContext(connection);
        var message = new EventArchiveOutboxMessage
        {
            EventId = EventId.From(Guid.NewGuid()),
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-event",
            ReadmeMarkdown = "# Event",
            MetadataJson = "title: Event",
            ConfirmedBy = "organizer",
            ConfirmedPreviewToken = "confirmed",
            IdempotencyKey = "old-confirmation",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        db.EventArchiveOutboxMessages.Add(message);
        db.Events.Add(new Event
        {
            Id = message.EventId.Value,
            Title = "Event",
            CreatedBy = "organizer",
            ArchivePublicationIdempotencyKey = message.IdempotencyKey,
            ArchivePublicationStatus = EventArchivePublicationStatus.Pending
        });
        await db.SaveChangesAsync();
        var repository = new MockGitHubEventRepository(new(
            "https://github.com/org/repo/tree/main/events/2026/2026-07-01-event",
            "https://github.com/org/repo/blob/main/events/2026/2026-07-01-event/event.yml",
            "bethuya/event-archive-test", 42, "https://github.com/org/repo/pull/42"))
        {
            PublishException = terminalFailure ? new ArchivePreviewMismatchException("changed snapshot") : null
        };
        var interceptor = new ConfirmBeforeEventUpdateInterceptor();
        using var processor = new EventArchiveOutboxProcessor(
            new TestScopeFactory(connection, repository, interceptor),
            NullLogger<EventArchiveOutboxProcessor>.Instance);
        var processOne = typeof(EventArchiveOutboxProcessor)
            .GetMethod("ProcessOneAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)processOne.Invoke(processor, [CancellationToken.None])!;

        db.ChangeTracker.Clear();
        var saved = await db.Events.SingleAsync();
        await Assert.That(interceptor.Triggered).IsTrue();
        await Assert.That(saved.ArchivePublicationIdempotencyKey).IsEqualTo("new-confirmation");
        await Assert.That(saved.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.Pending);
        await Assert.That(saved.ArchivePullRequestNumber).IsEqualTo(terminalFailure ? null : (int?)42);
        await Assert.That((await db.EventArchiveOutboxMessages.SingleAsync()).ProcessedAt).IsNotNull();
    }

    [Test]
    public async Task ProcessOneAsync_OlderSuccess_DoesNotReplaceNewerSuccessfulPrReference()
    {
        using var connection = CreateOpenSqliteConnection();
        await using var db = CreateSqliteDbContext(connection);
        var eventId = EventId.From(Guid.NewGuid());
        var older = new EventArchiveOutboxMessage
        {
            EventId = eventId, Destination = "archive/test", FolderPath = "events/2026/2026-07-01-event",
            ReadmeMarkdown = "# Event", MetadataJson = "title: Event", ConfirmedBy = "organizer",
            ConfirmedPreviewToken = "confirmed", IdempotencyKey = "older-confirmation",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2), AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        db.EventArchiveOutboxMessages.AddRange(older, new EventArchiveOutboxMessage
        {
            EventId = eventId, Destination = "archive/test", FolderPath = older.FolderPath,
            ReadmeMarkdown = "# New event", MetadataJson = "title: New event", ConfirmedBy = "organizer",
            ConfirmedPreviewToken = "new-confirmed", IdempotencyKey = "newer-confirmation",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1), ProcessedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        db.Events.Add(new Event
        {
            Id = eventId.Value, Title = "Event", CreatedBy = "organizer",
            ArchivePublicationIdempotencyKey = "newer-confirmation",
            ArchivePublicationStatus = EventArchivePublicationStatus.DraftPrPending,
            ArchiveBranchName = "new-branch", ArchivePullRequestNumber = 43,
            ArchivePullRequestUrl = "https://github.com/org/repo/pull/43"
        });
        await db.SaveChangesAsync();
        var repository = new MockGitHubEventRepository(new(
            "https://github.com/org/repo/tree/main/events/example",
            "https://github.com/org/repo/blob/main/events/example/event.yml",
            "old-branch", 42, "https://github.com/org/repo/pull/42"));
        using var processor = new EventArchiveOutboxProcessor(new TestScopeFactory(connection, repository),
            NullLogger<EventArchiveOutboxProcessor>.Instance);
        var processOne = typeof(EventArchiveOutboxProcessor)
            .GetMethod("ProcessOneAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)processOne.Invoke(processor, [CancellationToken.None])!;

        db.ChangeTracker.Clear();
        var saved = await db.Events.SingleAsync();
        await Assert.That(saved.ArchivePullRequestNumber).IsEqualTo(43);
        await Assert.That(saved.ArchiveBranchName).IsEqualTo("new-branch");
        await Assert.That(saved.ArchivePublicationStatus).IsEqualTo(EventArchivePublicationStatus.DraftPrPending);
        await Assert.That((await db.EventArchiveOutboxMessages.SingleAsync(item => item.Id == older.Id)).ProcessedAt).IsNotNull();
    }

    private sealed class ConfirmBeforeEventUpdateInterceptor : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await ConfirmAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await ConfirmAsync(command, cancellationToken);
            return result;
        }

        private async Task ConfirmAsync(DbCommand command, CancellationToken ct)
        {
            if (Triggered || !command.CommandText.StartsWith("UPDATE \"Events\"", StringComparison.Ordinal))
            {
                return;
            }

            Triggered = true;
            await using var confirmation = command.Connection!.CreateCommand();
            confirmation.Transaction = command.Transaction;
            confirmation.CommandText = """
                            UPDATE "Events" SET "ArchivePublicationIdempotencyKey" = 'new-confirmation',
                                "ArchivePublicationStatus" = 'Pending', "ArchivePullRequestNumber" = NULL,
                                "ArchivePullRequestUrl" = NULL
                            """;
            await confirmation.ExecuteNonQueryAsync(ct);
        }
    }
    private static object CreateWorkItem(EventArchiveOutboxMessage message)
    {
        var workItemType = typeof(EventArchiveOutboxProcessor).GetNestedType("EventArchiveOutboxWorkItem", BindingFlags.NonPublic)!;
        return Activator.CreateInstance(
            workItemType,
            message.Id,
            message.EventId,
            message.FolderPath,
            message.Title,
            message.ReadmeMarkdown,
            message.MetadataJson,
            message.IdempotencyKey,
            message.ConfirmedPreviewToken ?? "test-preview-token",
            message.AttemptCount,
            message.ClaimToken)!;
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"event-archive-outbox-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }

    private static SqliteConnection CreateOpenSqliteConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    private static BethuyaDbContext CreateSqliteDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new BethuyaDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static BethuyaDbContext CreateSqliteQueryDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseSqlite(connection)
            .Options;

        return new BethuyaDbContext(options);
    }

    private static BethuyaDbContext CreateSqliteQueryDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new BethuyaDbContext(options);
    }

    private sealed class FailEventSaveChangesInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("UPDATE \"Events\"", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Simulated event persistence failure.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestScopeFactory : IServiceScopeFactory
    {
        private readonly SqliteConnection? _connection;
        private readonly string? _connectionString;
        private readonly IGitHubEventRepository? _repository;
        private readonly IInterceptor? _saveChangesInterceptor;

        public TestScopeFactory(
            SqliteConnection connection,
            IGitHubEventRepository? repository = null,
            IInterceptor? saveChangesInterceptor = null)
        {
            _connection = connection;
            _repository = repository;
            _saveChangesInterceptor = saveChangesInterceptor;
        }

        public TestScopeFactory(
            string connectionString,
            IGitHubEventRepository? repository = null,
            IInterceptor? saveChangesInterceptor = null)
        {
            _connectionString = connectionString;
            _repository = repository;
            _saveChangesInterceptor = saveChangesInterceptor;
        }

        public IServiceScope CreateScope()
        {
            // Create a fresh DbContext for each scope (mimics real DI behavior)
            var optionsBuilder = new DbContextOptionsBuilder<BethuyaDbContext>();
            if (_connectionString is not null)
            {
                optionsBuilder.UseSqlite(_connectionString);
            }
            else
            {
                optionsBuilder.UseSqlite(_connection!);
            }

            if (_saveChangesInterceptor is not null)
            {
                optionsBuilder.AddInterceptors(_saveChangesInterceptor);
            }

            var db = new BethuyaDbContext(optionsBuilder.Options);
            return new TestScope(db, _repository);
        }
    }

    private sealed class TestScope(BethuyaDbContext db, IGitHubEventRepository? repository = null) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new TestServiceProvider(db, repository);

        public void Dispose()
        {
            // Dispose the DbContext when scope is disposed (mimics real DI behavior)
            db?.Dispose();
        }
    }

    private sealed class TestServiceProvider(BethuyaDbContext db, IGitHubEventRepository? repository = null) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(BethuyaDbContext))
            {
                return db;
            }

            if (serviceType == typeof(IGitHubEventRepository) && repository is not null)
            {
                return repository;
            }

            return null;
        }
    }

    private sealed class MockGitHubEventRepository(EventPublicationResult result) : IGitHubEventRepository
    {
        private readonly TaskCompletionSource _firstPublication =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstPublication => _firstPublication.Task;
        public List<EventPublicationRequest> PublishedRequests { get; } = [];
        public Exception? PublishException { get; init; }

        public Task<EventPublicationResult> PublishEventAsync(EventPublicationRequest request, CancellationToken ct = default)
        {
            if (PublishException is not null)
            {
                return Task.FromException<EventPublicationResult>(PublishException);
            }

            PublishedRequests.Add(request);
            _firstPublication.TrySetResult();
            return Task.FromResult(result);
        }

        public Task<EventArchiveEntry?> GetArchiveEntryAsync(string folderPath, CancellationToken ct = default)
            => Task.FromResult<EventArchiveEntry?>(null);

        public Task<EventArchivePreview> PreviewEventAsync(EventPublicationRequest request, CancellationToken ct = default)
            => Task.FromResult(new EventArchivePreview(
                request.FolderPath,
                "test-preview",
                null,
                request.ReadmeMarkdown,
                null,
                request.MetadataJson,
                true));

        public Task<EventArchivePreview> PreviewEventWithEntryAsync(
            EventPublicationRequest request,
            EventArchiveEntry entry,
            CancellationToken ct = default)
            => Task.FromResult(new EventArchivePreview(
                request.FolderPath,
                "test-preview",
                entry.ReadmeMarkdown,
                request.ReadmeMarkdown,
                entry.MetadataYaml,
                request.MetadataJson,
                true));

        public Task<EventArchivePullRequestStatus> GetPublicationStatusAsync(
            int pullRequestNumber,
            string folderPath,
            CancellationToken ct = default)
            => Task.FromResult(new EventArchivePullRequestStatus(
                true,
                false,
                false,
                pullRequestNumber,
                result.PullRequestUrl,
                result.FolderUrl));
    }
}

using System.Reflection;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-06-30-old-event-abc123",
            ReadmeMarkdown = "old readme",
            MetadataJson = "{ \"version\": 1 }",
            IdempotencyKey = "old-key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AttemptCount = 8,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            ClaimToken = "old-claim"
        };

        var newer = new EventArchiveOutboxMessage
        {
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-new-event-def456",
            ReadmeMarkdown = "new readme",
            MetadataJson = "{ \"version\": 2 }",
            IdempotencyKey = "new-key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AttemptCount = 0,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
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
            older.ReadmeMarkdown,
            older.MetadataJson,
            older.IdempotencyKey,
            older.AttemptCount,
            older.ClaimToken);

        var task = (Task)failureMethod.Invoke(processor, [workItem, new InvalidOperationException("permanent failure"), CancellationToken.None])!;
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
            message.ReadmeMarkdown,
            message.MetadataJson,
            message.IdempotencyKey,
            message.AttemptCount,
            message.ClaimToken);

        var task = (Task)failureMethod.Invoke(processor, [workItem, new InvalidOperationException("retryable failure"), CancellationToken.None])!;
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
        using var connection = CreateOpenSqliteConnection();
        await using var db = CreateSqliteDbContext(connection);

        // Seed an outbox message ready to claim.
        var eventGuid = Guid.NewGuid();
        var eventId = EventId.From(eventGuid);
        var message = new EventArchiveOutboxMessage
        {
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-event-def456",
            ReadmeMarkdown = "# Test Event",
            MetadataJson = "{ \"version\": 1 }",
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
            Id = eventGuid,
            Title = "Test Event 2026",
            CreatedBy = "test-user"
        };
        db.Events.Add(@event);
        await db.SaveChangesAsync();

        var publishResult = new EventPublicationResult(
            FolderUrl: "https://github.com/org/repo/tree/main/events/2026/2026-07-01-event-def456",
            MetadataUrl: "https://github.com/org/repo/blob/main/events/2026/2026-07-01-event-def456/metadata.json");

        var mockRepository = new MockGitHubEventRepository(publishResult);
        var scopeFactory = new TestScopeFactory(connection, mockRepository);
        var processor = new EventArchiveOutboxProcessor(scopeFactory, NullLogger<EventArchiveOutboxProcessor>.Instance);

        // Execute the processor for a short time, allowing it to claim and process the message.
        // The processor polls every 5 seconds, so we need to wait for at least one tick.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await processor.StartAsync(cts.Token);

        // Give the processor time to run at least one polling cycle (5 seconds poll + buffer)
        try
        {
            await Task.Delay(7000, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Timeout is expected; we're stopping anyway
        }

        // Stop the processor gracefully
        await processor.StopAsync(CancellationToken.None);

        // Verify the message was published and persisted.
        await Assert.That(mockRepository.PublishedRequests.Count).IsGreaterThanOrEqualTo(1);
        
        var publishedRequest = mockRepository.PublishedRequests[0];
        var publishedEventIdGuid = publishedRequest.EventId.Value;
        System.Console.WriteLine($"Published EventId from mock: {publishedEventIdGuid}, Expected: {eventGuid}, Match: {publishedEventIdGuid == eventGuid}");
        await Assert.That(publishedRequest.EventId).IsEqualTo(eventId);

        // Reload the message using a fresh DbContext to verify ProcessedAt was set.
        await using var verifyDb = CreateSqliteDbContext(connection);
        var completedMessage = await verifyDb.EventArchiveOutboxMessages.FirstAsync(m => m.Id == message.Id);
        
        // Debug: log the state of the reloaded message
        var diagnosticInfo = $"Message after processor: Id={completedMessage.Id}, ProcessedAt={completedMessage.ProcessedAt}, " +
            $"ClaimToken={completedMessage.ClaimToken}, AttemptCount={completedMessage.AttemptCount}, " +
            $"LockedUntil={completedMessage.LockedUntil}, LastError={completedMessage.LastError}";
        System.Console.WriteLine(diagnosticInfo);
        
        await Assert.That(completedMessage.ProcessedAt).IsNotNull();

        var completedEvent = await verifyDb.Events.FirstAsync(e => e.Id == eventGuid);
        await Assert.That(completedEvent.GitHubFolderUrl).IsEqualTo(publishResult.FolderUrl);
    }

    private static object CreateWorkItem(EventArchiveOutboxMessage message)
    {
        var workItemType = typeof(EventArchiveOutboxProcessor).GetNestedType("EventArchiveOutboxWorkItem", BindingFlags.NonPublic)!;
        return Activator.CreateInstance(
            workItemType,
            message.Id,
            message.EventId,
            message.FolderPath,
            message.ReadmeMarkdown,
            message.MetadataJson,
            message.IdempotencyKey,
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

    private sealed class TestScopeFactory : IServiceScopeFactory
    {
        private readonly SqliteConnection _connection;
        private readonly IGitHubEventRepository? _repository;

        public TestScopeFactory(SqliteConnection connection, IGitHubEventRepository? repository = null)
        {
            _connection = connection;
            _repository = repository;
        }

        public IServiceScope CreateScope()
        {
            // Create a fresh DbContext for each scope (mimics real DI behavior)
            var options = new DbContextOptionsBuilder<BethuyaDbContext>()
                .UseSqlite(_connection)
                .Options;
            var db = new BethuyaDbContext(options);
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
        public List<EventPublicationRequest> PublishedRequests { get; } = [];

        public Task<EventPublicationResult> PublishEventAsync(EventPublicationRequest request, CancellationToken ct = default)
        {
            PublishedRequests.Add(request);
            return Task.FromResult(result);
        }
    }
}

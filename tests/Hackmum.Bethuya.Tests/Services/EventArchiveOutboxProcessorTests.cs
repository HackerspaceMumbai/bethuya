using System.Reflection;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        var processor = new EventArchiveOutboxProcessor(new TestScopeFactory(db), NullLogger<EventArchiveOutboxProcessor>.Instance);
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

        var processor = new EventArchiveOutboxProcessor(new TestScopeFactory(db), NullLogger<EventArchiveOutboxProcessor>.Instance);
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

        var processor = new EventArchiveOutboxProcessor(new TestScopeFactory(db), NullLogger<EventArchiveOutboxProcessor>.Instance);
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

        var processor = new EventArchiveOutboxProcessor(new TestScopeFactory(db), NullLogger<EventArchiveOutboxProcessor>.Instance);
        var workItem = CreateWorkItem(message);

        // Simulate another worker reclaiming the (lease-expired) message with a new claim token.
        message.ClaimToken = "claim-token-2";
        await db.SaveChangesAsync();

        var renewMethod = typeof(EventArchiveOutboxProcessor).GetMethod("TryRenewClaimAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var renewTask = (Task<bool>)renewMethod.Invoke(processor, [workItem, CancellationToken.None])!;
        var renewed = await renewTask;

        await Assert.That(renewed).IsFalse();
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

    private sealed class TestScopeFactory(BethuyaDbContext db) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            return new TestScope(db);
        }
    }

    private sealed class TestScope(BethuyaDbContext db) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new TestServiceProvider(db);

        public void Dispose()
        {
        }
    }

    private sealed class TestServiceProvider(BethuyaDbContext db) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(BethuyaDbContext))
            {
                return db;
            }

            return null;
        }
    }
}

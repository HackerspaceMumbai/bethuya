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
            EventId = eventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-06-30-old-event-abc123",
            ConfirmedBy = "test-organizer",
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
            ConfirmedBy = "test-organizer",
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
            older.Title,
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

    private sealed class FailEventSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<Event>()
                .Any(entry => entry.State == EntityState.Modified) == true)
            {
                throw new InvalidOperationException("Simulated event persistence failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class TestScopeFactory : IServiceScopeFactory
    {
        private readonly SqliteConnection? _connection;
        private readonly string? _connectionString;
        private readonly IGitHubEventRepository? _repository;
        private readonly SaveChangesInterceptor? _saveChangesInterceptor;

        public TestScopeFactory(
            SqliteConnection connection,
            IGitHubEventRepository? repository = null,
            SaveChangesInterceptor? saveChangesInterceptor = null)
        {
            _connection = connection;
            _repository = repository;
            _saveChangesInterceptor = saveChangesInterceptor;
        }

        public TestScopeFactory(
            string connectionString,
            IGitHubEventRepository? repository = null,
            SaveChangesInterceptor? saveChangesInterceptor = null)
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

        public Task<EventPublicationResult> PublishEventAsync(EventPublicationRequest request, CancellationToken ct = default)
        {
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

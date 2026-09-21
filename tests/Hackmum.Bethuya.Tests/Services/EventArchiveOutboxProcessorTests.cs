using System.Reflection;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class EventArchiveOutboxProcessorTests
{
    [Test]
    public async Task RecordFailureAsync_ExhaustedOlderMessage_DoesNotBlockNewerMessage()
    {
        await using var db = CreateDbContext();

        var olderEventId = EventId.From(Guid.NewGuid());
        var newerEventId = EventId.From(Guid.NewGuid());
        var older = new EventArchiveOutboxMessage
        {
            EventId = olderEventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-06-30-old-event-abc123",
            ReadmeMarkdown = "old readme",
            MetadataJson = "{ \"version\": 1 }",
            IdempotencyKey = "old-key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AttemptCount = 8,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        var newer = new EventArchiveOutboxMessage
        {
            EventId = newerEventId,
            Destination = "archive/test",
            FolderPath = "events/2026/2026-07-01-new-event-def456",
            ReadmeMarkdown = "new readme",
            MetadataJson = "{ \"version\": 2 }",
            IdempotencyKey = "new-key",
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AttemptCount = 0,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
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
            older.AttemptCount);

        var task = (Task)failureMethod.Invoke(processor, [workItem, new InvalidOperationException("permanent failure"), CancellationToken.None])!;
        await task;

        var claimMethod = typeof(EventArchiveOutboxProcessor).GetMethod("ClaimNextAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var claimTask = (Task)claimMethod.Invoke(processor, [CancellationToken.None])!;
        await claimTask;

        var result = claimTask.GetType().GetProperty("Result")!.GetValue(claimTask);
        var claimedId = (object?)result?.GetType().GetProperty("Id")?.GetValue(result);

        await Assert.That(older.ProcessedAt).IsNotNull();
        await Assert.That(claimedId).IsNotNull();
        await Assert.That(((EventArchiveOutboxMessageId)claimedId!).Value).IsEqualTo(newer.Id.Value);
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"event-archive-outbox-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
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

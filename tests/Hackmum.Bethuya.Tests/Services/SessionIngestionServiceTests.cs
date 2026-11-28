using System.Data.Common;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class SessionIngestionServiceTests
{
    [Test]
    public async Task ImportSessionizeAsync_EventWithoutAgenda_InsertsAgendaAndSessionsInUtc()
    {
        await using var db = CreateDbContext();
        var evt = new Event
        {
            Title = "Postgres Night",
            Type = EventType.Meetup,
            Capacity = 50,
            StartDate = new DateTimeOffset(2026, 12, 12, 5, 30, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 12, 12, 9, 30, 0, TimeSpan.Zero),
            CreatedBy = "organizer",
            SessionizeEventId = "demo"
        };
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var ist = TimeSpan.FromHours(5.5);
        var sessionize = Substitute.For<ISessionizeService>();
        sessionize.GetSessionsAsync("demo", Arg.Any<CancellationToken>()).Returns(
        [
            new NormalizedSession(
                "Postgres in production",
                Description: null,
                Speakers: [new NormalizedSpeaker("Asha Rao")],
                SessionSource.Sessionize,
                SourceSessionId: "101",
                PreferredStartTime: new DateTimeOffset(2026, 12, 12, 11, 0, 0, ist),
                Duration: TimeSpan.FromMinutes(45))
        ]);
        var service = new SessionIngestionService(
            new EventRepository(db),
            sessionize,
            db,
            NullLogger<SessionIngestionService>.Instance);

        var importedCount = await service.ImportSessionizeAsync(evt.Id);

        await Assert.That(importedCount).IsEqualTo(1);
        db.ChangeTracker.Clear();
        var session = await db.Set<AgendaSession>().SingleAsync();
        await Assert.That(session.ScheduledStartAt!.Value.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(session.ScheduledStartAt!.Value).IsEqualTo(new DateTimeOffset(2026, 12, 12, 5, 30, 0, TimeSpan.Zero));
        await Assert.That(session.ScheduledEndAt!.Value.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(session.StartTime).IsEqualTo(new TimeOnly(11, 0));
        await Assert.That(session.EndTime).IsEqualTo(new TimeOnly(11, 45));
    }

    [Test]
    public async Task ImportSessionizeAsync_CommitFailsOnce_RetryStoresAndCountsSessions()
    {
        var commitFailure = new FailFirstCommit();
        await using var db = new BethuyaDbContext(new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseSqlite("Data Source=:memory:")
            .ReplaceService<IExecutionStrategyFactory, RetryFactory>()
            .AddInterceptors(commitFailure)
            .Options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var evt = CreateEvent();
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var service = new SessionIngestionService(
            new EventRepository(db),
            CreateSessionize(new DateTimeOffset(2026, 12, 12, 5, 30, 0, TimeSpan.Zero)),
            db,
            NullLogger<SessionIngestionService>.Instance);

        var importedCount = await service.ImportSessionizeAsync(evt.Id);

        await Assert.That(commitFailure.Failed).IsTrue();
        await Assert.That(importedCount).IsEqualTo(1);
        db.ChangeTracker.Clear();
        await Assert.That(await db.AgendaSessions.CountAsync()).IsEqualTo(1);
        await Assert.That(await db.Agendas.CountAsync()).IsEqualTo(1);
    }

    private static Event CreateEvent()
        => new()
        {
            Title = "Postgres Night",
            Type = EventType.Meetup,
            Capacity = 50,
            StartDate = new DateTimeOffset(2026, 12, 12, 5, 30, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 12, 12, 9, 30, 0, TimeSpan.Zero),
            CreatedBy = "organizer",
            SessionizeEventId = "demo"
        };

    private static ISessionizeService CreateSessionize(DateTimeOffset startsAt)
    {
        var sessionize = Substitute.For<ISessionizeService>();
        sessionize.GetSessionsAsync("demo", Arg.Any<CancellationToken>()).Returns(
        [
            new NormalizedSession(
                "Postgres in production",
                Description: null,
                Speakers: [new NormalizedSpeaker("Asha Rao")],
                SessionSource.Sessionize,
                SourceSessionId: "101",
                PreferredStartTime: startsAt,
                Duration: TimeSpan.FromMinutes(45))
        ]);
        return sessionize;
    }

    private sealed class FailFirstCommit : DbTransactionInterceptor
    {
        public bool Failed { get; private set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (!Failed)
            {
                Failed = true;
                throw new TimeoutException("Simulated transient commit failure");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class RetryFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new RetryStrategy(dependencies);
    }

    private sealed class RetryStrategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TimeoutException;
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"session-ingestion-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }
}

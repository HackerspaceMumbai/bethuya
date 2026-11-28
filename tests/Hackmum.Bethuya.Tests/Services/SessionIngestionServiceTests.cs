using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"session-ingestion-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }
}

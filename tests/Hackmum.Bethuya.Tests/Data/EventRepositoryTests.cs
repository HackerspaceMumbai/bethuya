using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hackmum.Bethuya.Tests.Data;

public sealed class EventRepositoryTests
{
    [Test]
    public async Task GetByIdAsync_UsesSplitQueriesForRegistrationsAndSessions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var executedSql = new List<string>();
        var options = CreateSqliteOptions(connection, executedSql);

        var seededEventId = await SeedEventWithCollectionsAsync(options, "split-by-id");
        executedSql.Clear();

        await using var readDb = new BethuyaDbContext(options);
        var repository = new EventRepository(readDb);

        var loaded = await repository.GetByIdAsync(seededEventId);

        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Registrations.Count).IsEqualTo(2);
        await Assert.That(loaded.Agenda).IsNotNull();
        await Assert.That(loaded.Agenda!.Sessions.Count).IsEqualTo(2);
        await Assert.That(executedSql.Count).IsEqualTo(3);
        await Assert.That(executedSql.Any(sql =>
            sql.Contains("\"Registrations\"", StringComparison.Ordinal) &&
            sql.Contains("\"AgendaSessions\"", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task GetByHashtagAsync_UsesSplitQueriesForRegistrationsAndSessions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var executedSql = new List<string>();
        var options = CreateSqliteOptions(connection, executedSql);

        await SeedEventWithCollectionsAsync(options, "split-by-hashtag");
        executedSql.Clear();

        await using var readDb = new BethuyaDbContext(options);
        var repository = new EventRepository(readDb);

        var loaded = await repository.GetByHashtagAsync("split-by-hashtag");

        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Registrations.Count).IsEqualTo(2);
        await Assert.That(loaded.Agenda).IsNotNull();
        await Assert.That(loaded.Agenda!.Sessions.Count).IsEqualTo(2);
        await Assert.That(executedSql.Count).IsEqualTo(3);
        await Assert.That(executedSql.Any(sql =>
            sql.Contains("\"Registrations\"", StringComparison.Ordinal) &&
            sql.Contains("\"AgendaSessions\"", StringComparison.Ordinal))).IsFalse();
    }

    private static DbContextOptions<BethuyaDbContext> CreateSqliteOptions(
        SqliteConnection connection,
        List<string> executedSql)
        => new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseSqlite(connection)
            .LogTo(message => executedSql.Add(message), [RelationalEventId.CommandExecuted])
            .Options;

    private static async Task<Guid> SeedEventWithCollectionsAsync(
        DbContextOptions<BethuyaDbContext> options,
        string hashtag)
    {
        await using var db = new BethuyaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var evt = new Event
        {
            Title = "Split Query Test Event",
            Hashtag = hashtag,
            StartDate = DateTimeOffset.UtcNow.AddDays(7),
            CreatedBy = "organizer@example.com",
            Agenda = new Agenda
            {
                Sessions =
                [
                    new AgendaSession { Title = "Opening Keynote", Order = 1 },
                    new AgendaSession { Title = "Closing Panel", Order = 2 }
                ]
            },
            Registrations =
            [
                new Registration { FullName = "Ada Lovelace", Email = "ada@example.com" },
                new Registration { FullName = "Grace Hopper", Email = "grace@example.com" }
            ]
        };

        db.Events.Add(evt);
        await db.SaveChangesAsync();
        return evt.Id;
    }
}

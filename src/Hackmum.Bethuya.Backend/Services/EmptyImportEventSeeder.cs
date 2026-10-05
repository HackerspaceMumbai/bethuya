using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>Creates a development event with no registrations for import testing.</summary>
public sealed partial class EmptyImportEventSeeder(
    BethuyaDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<EmptyImportEventSeeder> logger)
{
    /// <summary>Creates a fresh event without registrations or attendee profiles.</summary>
    public async Task<EmptyImportEventSeedResult> SeedAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var seedKey = Guid.CreateVersion7().ToString("N");
        var newEvent = new Event
        {
            Title = $"Import Test Event {now:dd MMM HH:mm} ({seedKey[..6]})",
            Description = "Development-only empty event for testing registration and attendance imports.",
            Type = EventType.Meetup,
            Status = EventStatus.RegistrationOpen,
            Capacity = 100,
            StartDate = now.AddDays(30),
            EndDate = now.AddDays(30).AddHours(3),
            Location = "Hackerspace Mumbai",
            Hashtag = $"import-test-{seedKey}",
            CreatedBy = "seed-import@hackerspacemumbai.dev"
        };

        dbContext.Events.Add(newEvent);
        await dbContext.SaveChangesAsync(ct);

        LogSeedCompleted(logger, newEvent.Id, newEvent.Title);
        return new EmptyImportEventSeedResult(newEvent.Id, newEvent.Title);
    }

    [LoggerMessage(
        EventId = 2402,
        Level = LogLevel.Information,
        Message = "Seeded empty import test event {EventId}: {EventTitle}")]
    private static partial void LogSeedCompleted(ILogger logger, Guid eventId, string eventTitle);
}

/// <summary>Identifies an event created for import testing.</summary>
/// <param name="EventId">The new event's identifier.</param>
/// <param name="EventTitle">The new event's title.</param>
public sealed record EmptyImportEventSeedResult(Guid EventId, string EventTitle);

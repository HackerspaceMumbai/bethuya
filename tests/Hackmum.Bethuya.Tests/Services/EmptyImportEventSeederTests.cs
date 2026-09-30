using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hackmum.Bethuya.Tests.Services;

public class EmptyImportEventSeederTests
{
    [Test]
    public async Task SeedAsync_CreatesEventWithoutRegistrationsOrAttendeeProfiles()
    {
        await using var dbContext = CreateDbContext();
        var seeder = new EmptyImportEventSeeder(
            dbContext,
            TimeProvider.System,
            NullLogger<EmptyImportEventSeeder>.Instance);

        var result = await seeder.SeedAsync();

        var seededEvent = await dbContext.Events
            .Include(evt => evt.Registrations)
            .SingleAsync(evt => evt.Id == result.EventId);

        await Assert.That(seededEvent.Title).IsEqualTo(result.EventTitle);
        await Assert.That(seededEvent.Title).Contains("Import Test Event");
        await Assert.That(seededEvent.Registrations).IsEmpty();
        await Assert.That(await dbContext.Registrations.CountAsync()).IsEqualTo(0);
        await Assert.That(await dbContext.AttendeeProfiles.CountAsync()).IsEqualTo(0);
        await Assert.That(seededEvent.Status).IsEqualTo(Hackmum.Bethuya.Core.Enums.EventStatus.RegistrationOpen);
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"empty-import-event-seeder-tests-{Guid.NewGuid():N}")
            .Options;

        return new BethuyaDbContext(options);
    }
}

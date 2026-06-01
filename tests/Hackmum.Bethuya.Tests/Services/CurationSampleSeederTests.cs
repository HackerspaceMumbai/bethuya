using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hackmum.Bethuya.Tests.Services;

public class CurationSampleSeederTests
{
    [Test]
    public async Task SeedAsync_CreatesCurationSandbox_WithReviewableAndHistoricalData()
    {
        await using var dbContext = CreateDbContext();
        var seeder = new CurationSampleSeeder(
            dbContext,
            new InclusionSignalsNormalizer(),
            TimeProvider.System,
            NullLogger<CurationSampleSeeder>.Instance);

        var result = await seeder.SeedAsync(50);
        var currentEvent = await dbContext.Events
            .Include(evt => evt.Registrations)
            .SingleAsync(evt => evt.Id == result.EventId);

        var currentRegistrations = currentEvent.Registrations;
        var reviewableCount = currentRegistrations.Count(r => r.Status is Hackmum.Bethuya.Core.Enums.RegistrationStatus.Pending or Hackmum.Bethuya.Core.Enums.RegistrationStatus.Waitlisted);
        var selectedCount = currentRegistrations.Count(r => r.Status is Hackmum.Bethuya.Core.Enums.RegistrationStatus.Accepted or Hackmum.Bethuya.Core.Enums.RegistrationStatus.CheckedIn);
        var registrations = await dbContext.Registrations.ToListAsync();
        var emailsWithHistory = registrations
            .GroupBy(registration => registration.Email, StringComparer.OrdinalIgnoreCase)
            .Count(group => group.Count() > 1);

        await Assert.That(result.ReviewableRegistrants).IsEqualTo(50);
        await Assert.That(reviewableCount).IsEqualTo(50);
        await Assert.That(selectedCount).IsGreaterThanOrEqualTo(8);
        await Assert.That(dbContext.AttendeeProfiles.Count()).IsGreaterThanOrEqualTo(58);
        await Assert.That(await dbContext.Events.CountAsync()).IsGreaterThanOrEqualTo(3);
        await Assert.That(emailsWithHistory).IsGreaterThan(0);
        await Assert.That(result.CurationPath).IsEqualTo($"/curation/{result.EventId}");
    }

    [Test]
    public async Task SeedAsync_ClampsLowRequestedCount_AboveVenueCapacity()
    {
        await using var dbContext = CreateDbContext();
        var seeder = new CurationSampleSeeder(
            dbContext,
            new InclusionSignalsNormalizer(),
            TimeProvider.System,
            NullLogger<CurationSampleSeeder>.Instance);

        var result = await seeder.SeedAsync(10);
        var currentEvent = await dbContext.Events
            .Include(evt => evt.Registrations)
            .SingleAsync(evt => evt.Id == result.EventId);
        var reviewableCount = currentEvent.Registrations.Count(r => r.Status is Hackmum.Bethuya.Core.Enums.RegistrationStatus.Pending or Hackmum.Bethuya.Core.Enums.RegistrationStatus.Waitlisted);

        await Assert.That(currentEvent.Capacity).IsEqualTo(25);
        await Assert.That(result.ReviewableRegistrants).IsGreaterThan(currentEvent.Capacity);
        await Assert.That(reviewableCount).IsGreaterThan(currentEvent.Capacity);
        await Assert.That(currentEvent.Registrations.Count).IsGreaterThan(currentEvent.Capacity);
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"curation-sample-seeder-tests-{Guid.NewGuid():N}")
            .Options;

        return new BethuyaDbContext(options);
    }
}

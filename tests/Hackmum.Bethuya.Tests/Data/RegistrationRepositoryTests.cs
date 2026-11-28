using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Tests.Data;

public sealed class RegistrationRepositoryTests
{
    [Test]
    public async Task GetHistoricalByEmailsAsync_MatchesEmailsCaseInsensitively()
    {
        await using var db = CreateDbContext();
        var previousEventId = Guid.CreateVersion7();
        var currentEventId = Guid.CreateVersion7();
        db.Registrations.AddRange(
            new Registration
            {
                EventId = previousEventId,
                FullName = "Alice Fernandes",
                Email = "alice@example.com",
                Status = RegistrationStatus.CheckedIn
            },
            new Registration
            {
                EventId = currentEventId,
                FullName = "Alice Fernandes",
                Email = "Alice@Example.com",
                Status = RegistrationStatus.Pending
            });
        await db.SaveChangesAsync();
        var repository = new RegistrationRepository(db);

        var history = await repository.GetHistoricalByEmailsAsync(["Alice@Example.com"], currentEventId);

        await Assert.That(history.TryGetValue("Alice@Example.com", out var registrations)).IsTrue();
        await Assert.That(registrations!.Count).IsEqualTo(1);
        await Assert.That(registrations[0].EventId).IsEqualTo(previousEventId);
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"registration-repository-tests-{Guid.NewGuid():N}")
            .Options;

        return new BethuyaDbContext(options);
    }
}

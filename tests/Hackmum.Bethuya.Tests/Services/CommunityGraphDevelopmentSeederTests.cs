using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class CommunityGraphDevelopmentSeederTests
{
    [Test]
    public async Task Seed_RepeatedDashboardInvocationProducesSameVerifiedGraph()
    {
        await using var db = new BethuyaDbContext(new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"graph-seed-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        var seeder = new CommunityGraphDevelopmentSeeder(db, environment);
        await seeder.SeedAsync();
        var first = await new CommunityGraphService(db).ReadAsync("graph-demo-v1:priya");
        await seeder.SeedAsync();
        var second = await new CommunityGraphService(db).ReadAsync("graph-demo-v1:priya");
        await Assert.That(second.Nodes.Count).IsEqualTo(first.Nodes.Count);
        await Assert.That(second.Relationships.Count).IsEqualTo(first.Relationships.Count);
        await Assert.That(await db.ParticipationLedgerEntries.CountAsync()).IsEqualTo(15);
        await Assert.That(second.Opportunities.Count).IsEqualTo(4);
    }
}

using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Services;

public class CommunitySimulationSeederTests
{
    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"community-simulation-seeder-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BethuyaDbContext(options);
    }

    private static CommunitySimulationSeeder CreateSeeder(BethuyaDbContext dbContext)
        => new(dbContext, TimeProvider.System, NullLogger<CommunitySimulationSeeder>.Instance);

    [Test]
    public async Task SeedAsync_Creates_SixCommunityMembers_MatchingPersonaCatalog()
    {
        await using var dbContext = CreateDbContext();
        var seeder = CreateSeeder(dbContext);

        var result = await seeder.SeedAsync();

        var members = await dbContext.CommunityMembers.ToListAsync();
        await Assert.That(members.Count).IsEqualTo(6);
        await Assert.That(result.PersonasProvisioned).IsEqualTo(6);
        await Assert.That(result.MembersCreated).IsEqualTo(6);

        var catalog = DevelopmentPersonaCatalog.All;
        foreach (var persona in catalog)
        {
            var member = members.SingleOrDefault(m => m.UserId == persona.Subject);
            await Assert.That(member).IsNotNull();
            await Assert.That(member!.DisplayName).IsEqualTo(persona.DisplayName);
            await Assert.That(member.Email).IsEqualTo(persona.Email);
        }
    }

    [Test]
    public async Task SeedAsync_CreatesExactlyOnePlatformIdentityPerMember()
    {
        await using var dbContext = CreateDbContext();
        var seeder = CreateSeeder(dbContext);

        await seeder.SeedAsync();

        var members = await dbContext.CommunityMembers.ToListAsync();
        foreach (var member in members)
        {
            var identities = await dbContext.ExternalIdentities
                .Where(i => i.CommunityMemberId == member.Id && i.Provider == IdentityProviderKind.Platform)
                .ToListAsync();
            await Assert.That(identities.Count).IsEqualTo(1);
            await Assert.That(identities[0].Subject).IsEqualTo(member.UserId);
        }
    }

    [Test]
    public async Task SeedAsync_EachMemberHasAtLeastTwoLedgerEntries_WithUniqueTuples()
    {
        await using var dbContext = CreateDbContext();
        var seeder = CreateSeeder(dbContext);

        await seeder.SeedAsync();

        var members = await dbContext.CommunityMembers.ToListAsync();
        foreach (var member in members)
        {
            var entries = await dbContext.ParticipationLedgerEntries
                .Where(e => e.CommunityMemberId == member.Id)
                .ToListAsync();
            await Assert.That(entries.Count).IsGreaterThanOrEqualTo(2);

            var uniqueTuples = entries
                .Select(e => (e.CommunityMemberId, e.Connector, e.ProvenanceKey))
                .ToHashSet();
            await Assert.That(uniqueTuples.Count).IsEqualTo(entries.Count);
        }
    }

    [Test]
    public async Task SeedAsync_CreatesExactlyOneSharedFixtureEvent_AndSixRegistrations()
    {
        await using var dbContext = CreateDbContext();
        var seeder = CreateSeeder(dbContext);

        var result = await seeder.SeedAsync();

        var events = await dbContext.Events
            .Where(e => e.Hashtag == "community-simulation-fixture")
            .ToListAsync();
        await Assert.That(events.Count).IsEqualTo(1);
        await Assert.That(events[0].Id).IsEqualTo(result.EventId.Value);

        var registrations = await dbContext.Registrations
            .Where(r => r.EventId == result.EventId.Value)
            .ToListAsync();
        await Assert.That(registrations.Count).IsEqualTo(6);

        var personaEmails = DevelopmentPersonaCatalog.All.Select(p => p.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var reg in registrations)
        {
            await Assert.That(personaEmails.Contains(reg.Email)).IsTrue();
        }
    }

    [Test]
    public async Task SeedAsync_IsIdempotent_SecondCallProducesZeroNewRows()
    {
        await using var dbContext = CreateDbContext();
        var seeder = CreateSeeder(dbContext);

        var result1 = await seeder.SeedAsync();
        var result2 = await seeder.SeedAsync();

        // Second call creates nothing new
        await Assert.That(result2.MembersCreated).IsEqualTo(0);
        await Assert.That(result2.MembersAlreadyExisted).IsEqualTo(6);
        await Assert.That(result2.LedgerEntriesCreated).IsEqualTo(0);
        await Assert.That(result2.LedgerEntriesAlreadyExisted).IsEqualTo(result1.LedgerEntriesCreated);
        await Assert.That(result2.RegistrationsCreated).IsEqualTo(0);
        await Assert.That(result2.RegistrationsAlreadyExisted).IsEqualTo(6);

        // Row counts unchanged
        var memberCount = await dbContext.CommunityMembers.CountAsync();
        var ledgerCount = await dbContext.ParticipationLedgerEntries.CountAsync();
        var regCount = await dbContext.Registrations.Where(r => r.EventId == result1.EventId.Value).CountAsync();
        await Assert.That(memberCount).IsEqualTo(6);
        await Assert.That(ledgerCount).IsEqualTo(result1.LedgerEntriesCreated);
        await Assert.That(regCount).IsEqualTo(6);
    }

    [Test]
    public async Task SeedAsync_PersonaKeysList_MatchesDevelopmentPersonaCatalogExactly()
    {
        await using var dbContext = CreateDbContext();
        var seeder = CreateSeeder(dbContext);

        var result = await seeder.SeedAsync();

        var catalogKeys = DevelopmentPersonaCatalog.All
            .Select(p => p.Key)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
        var resultKeys = result.PersonaKeys
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        await Assert.That(resultKeys.Count).IsEqualTo(catalogKeys.Count);
        for (var i = 0; i < catalogKeys.Count; i++)
        {
            await Assert.That(resultKeys[i]).IsEqualTo(catalogKeys[i]);
        }
    }
}

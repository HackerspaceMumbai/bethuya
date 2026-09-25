using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class ImportTemplateSeederTests
{
    [Test]
    public async Task EnsureSeededAsync_SeedsFourSystemTemplates()
    {
        await using var db = CreateDbContext();

        await ImportTemplateSeeder.EnsureSeededAsync(db);

        var templates = await db.ImportTemplates.Include(t => t.ColumnMappings).ToListAsync();
        await Assert.That(templates.Count).IsEqualTo(4);
        await Assert.That(templates.All(t => t.Scope == ImportTemplateScope.System)).IsTrue();
        await Assert.That(templates.All(t => t.ColumnMappings.Count > 0)).IsTrue();
        await Assert.That(templates.Any(t => t.SourceKind == ImportSourceKind.Luma && t.ImportKind == ImportKind.Registration)).IsTrue();
        await Assert.That(templates.Any(t => t.SourceKind == ImportSourceKind.Luma && t.ImportKind == ImportKind.Attendance)).IsTrue();
        await Assert.That(templates.Any(t => t.SourceKind == ImportSourceKind.MLH && t.ImportKind == ImportKind.Registration)).IsTrue();
        await Assert.That(templates.Any(t => t.SourceKind == ImportSourceKind.MLH && t.ImportKind == ImportKind.Attendance)).IsTrue();
    }

    [Test]
    public async Task EnsureSeededAsync_RunningTwice_DoesNotDuplicateTemplates()
    {
        await using var db = CreateDbContext();

        await ImportTemplateSeeder.EnsureSeededAsync(db);
        await ImportTemplateSeeder.EnsureSeededAsync(db);

        var count = await db.ImportTemplates.CountAsync();
        await Assert.That(count).IsEqualTo(4);
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"import-template-seeder-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }
}

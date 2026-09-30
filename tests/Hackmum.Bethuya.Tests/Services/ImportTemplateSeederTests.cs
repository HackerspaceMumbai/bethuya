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

        var lumaRegistration = templates.Single(t => t.SourceKind == ImportSourceKind.Luma && t.ImportKind == ImportKind.Registration);
        await Assert.That(lumaRegistration.ColumnMappings.Any(mapping =>
            mapping.SourceColumnName == "approval_status" && mapping.TargetField == ImportTargetField.ApprovalStatus)).IsTrue();
        var lumaAttendance = templates.Single(t => t.SourceKind == ImportSourceKind.Luma && t.ImportKind == ImportKind.Attendance);
        await Assert.That(lumaAttendance.ColumnMappings.Any(mapping =>
            mapping.SourceColumnName == "checked_in_at" && mapping.TargetField == ImportTargetField.CheckedInAt)).IsTrue();
        var mlhAttendance = templates.Single(t => t.SourceKind == ImportSourceKind.MLH && t.ImportKind == ImportKind.Attendance);
        await Assert.That(mlhAttendance.ColumnMappings.Any(mapping =>
            mapping.SourceColumnName == "Checked In At" && mapping.TargetField == ImportTargetField.CheckedInAt)).IsTrue();
    }

    [Test]
    public async Task EnsureSeededAsync_UpgradesExistingLumaSystemTemplatesWithLifecycleFields()
    {
        await using var db = CreateDbContext();
        var template = new Hackmum.Bethuya.Core.Models.ImportTemplate
        {
            Name = "Luma Standard Registration Export",
            Scope = ImportTemplateScope.System,
            SourceKind = ImportSourceKind.Luma,
            ImportKind = ImportKind.Registration
        };
        template.ColumnMappings.Add(new Hackmum.Bethuya.Core.Models.ImportColumnMapping
        {
            ImportTemplateId = template.Id,
            SourceColumnName = "Name",
            TargetField = ImportTargetField.FullName
        });
        template.ColumnMappings.Add(new Hackmum.Bethuya.Core.Models.ImportColumnMapping
        {
            ImportTemplateId = template.Id,
            SourceColumnName = "Email",
            TargetField = ImportTargetField.Email
        });
        db.ImportTemplates.Add(template);
        await db.SaveChangesAsync();

        await ImportTemplateSeeder.EnsureSeededAsync(db);

        var upgraded = await db.ImportTemplates.Include(item => item.ColumnMappings)
            .SingleAsync(item => item.Name == "Luma Standard Registration Export");
        await Assert.That(upgraded.ColumnMappings.Any(mapping =>
            mapping.SourceColumnName == "approval_status" && mapping.TargetField == ImportTargetField.ApprovalStatus)).IsTrue();
        await Assert.That(await db.ImportTemplates.CountAsync(item => item.Scope == ImportTemplateScope.System)).IsEqualTo(4);
    }

    [Test]
    public async Task EnsureSeededAsync_UpgradesExistingMlhAttendanceCheckInMapping()
    {
        await using var db = CreateDbContext();
        await ImportTemplateSeeder.EnsureSeededAsync(db);
        var template = await db.ImportTemplates
            .Include(item => item.ColumnMappings)
            .SingleAsync(item => item.Name == "MLH Attendance Export");
        template.ColumnMappings.Single(mapping => mapping.SourceColumnName == "Checked In At").TargetField =
            ImportTargetField.OccurredAt;
        await db.SaveChangesAsync();

        await ImportTemplateSeeder.EnsureSeededAsync(db);

        var upgraded = await db.ImportTemplates
            .Include(item => item.ColumnMappings)
            .SingleAsync(item => item.Name == "MLH Attendance Export");
        await Assert.That(upgraded.ColumnMappings.Any(mapping =>
            mapping.SourceColumnName == "Checked In At" && mapping.TargetField == ImportTargetField.CheckedInAt)).IsTrue();
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

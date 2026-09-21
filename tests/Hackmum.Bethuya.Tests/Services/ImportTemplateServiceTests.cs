using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class ImportTemplateServiceTests
{
    [Test]
    public async Task CreateAsync_CreatesUserScopedTemplate()
    {
        await using var db = CreateDbContext();
        var service = new ImportTemplateService(db);

        var template = await service.CreateAsync(
            "My Custom Mapping",
            ImportSourceKind.Custom,
            ImportKind.Registration,
            "organizer-1",
            [new ImportColumnMappingInput("Email", ImportTargetField.Email)]);

        await Assert.That(template.Scope).IsEqualTo(ImportTemplateScope.User);
        await Assert.That(template.OwnerUserId).IsEqualTo("organizer-1");
        await Assert.That(template.ColumnMappings.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CloneAsync_ClonesSystemTemplateIntoEditableUserTemplate()
    {
        await using var db = CreateDbContext();
        var service = new ImportTemplateService(db);

        var systemTemplate = await SeedSystemTemplateAsync(db);

        var clone = await service.CloneAsync(systemTemplate.Id, "organizer-2", requestingUserIsAdmin: false);

        await Assert.That(clone.Id).IsNotEqualTo(systemTemplate.Id);
        await Assert.That(clone.Scope).IsEqualTo(ImportTemplateScope.User);
        await Assert.That(clone.OwnerUserId).IsEqualTo("organizer-2");
        await Assert.That(clone.ClonedFromTemplateId).IsEqualTo(systemTemplate.Id);
        await Assert.That(clone.ColumnMappings.Count).IsEqualTo(systemTemplate.ColumnMappings.Count);
    }

    [Test]
    public async Task UpdateAsync_SystemTemplate_Throws()
    {
        await using var db = CreateDbContext();
        var service = new ImportTemplateService(db);
        var systemTemplate = await SeedSystemTemplateAsync(db);

        var action = async () => (Hackmum.Bethuya.Core.Models.ImportTemplate?)await service.UpdateAsync(
            systemTemplate.Id, "organizer-1", requestingUserIsAdmin: false, "New Name",
            [new ImportColumnMappingInput("Email", ImportTargetField.Email)]);

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UpdateAsync_NonOwnerNonAdmin_Throws()
    {
        await using var db = CreateDbContext();
        var service = new ImportTemplateService(db);
        var template = await service.CreateAsync(
            "Owned Template", ImportSourceKind.Custom, ImportKind.Registration, "owner-1",
            [new ImportColumnMappingInput("Email", ImportTargetField.Email)]);

        var action = async () => (Hackmum.Bethuya.Core.Models.ImportTemplate?)await service.UpdateAsync(
            template.Id, "someone-else", requestingUserIsAdmin: false, "New Name",
            [new ImportColumnMappingInput("Email", ImportTargetField.Email)]);

        await Assert.That(action).Throws<UnauthorizedAccessException>();
    }

    [Test]
    public async Task UpdateAsync_Admin_CanEditAnyUserTemplate()
    {
        await using var db = CreateDbContext();
        var service = new ImportTemplateService(db);
        var template = await service.CreateAsync(
            "Owned Template", ImportSourceKind.Custom, ImportKind.Registration, "owner-1",
            [new ImportColumnMappingInput("Email", ImportTargetField.Email)]);

        var updated = await service.UpdateAsync(
            template.Id, "admin-user", requestingUserIsAdmin: true, "Renamed by Admin",
            [new ImportColumnMappingInput("Email Address", ImportTargetField.Email)]);

        await Assert.That(updated.Name).IsEqualTo("Renamed by Admin");
        await Assert.That(updated.ColumnMappings.Single().SourceColumnName).IsEqualTo("Email Address");
    }

    [Test]
    public async Task ListAsync_ReturnsSystemTemplatesAndOnlyCallersOwnUserTemplates()
    {
        await using var db = CreateDbContext();
        var service = new ImportTemplateService(db);

        await SeedSystemTemplateAsync(db);
        await service.CreateAsync("Mine", ImportSourceKind.Custom, ImportKind.Registration, "organizer-1",
            [new ImportColumnMappingInput("Email", ImportTargetField.Email)]);
        await service.CreateAsync("Someone Else's", ImportSourceKind.Custom, ImportKind.Registration, "organizer-2",
            [new ImportColumnMappingInput("Email", ImportTargetField.Email)]);

        var visible = await service.ListAsync("organizer-1");

        await Assert.That(visible.Any(t => t.Scope == ImportTemplateScope.System)).IsTrue();
        await Assert.That(visible.Any(t => t.Name == "Mine")).IsTrue();
        await Assert.That(visible.Any(t => t.Name == "Someone Else's")).IsFalse();
    }

    [Test]
    public async Task GetForUserAsync_NonOwner_Throws()
    {
        await using var db = CreateDbContext();
        var service = new ImportTemplateService(db);
        var template = await service.CreateAsync(
            "Private", ImportSourceKind.Custom, ImportKind.Registration, "owner-1",
            [new ImportColumnMappingInput("Email", ImportTargetField.Email)]);

        var action = async () => await service.GetForUserAsync(
            template.Id, "someone-else", requestingUserIsAdmin: false);

        await Assert.That(action).Throws<UnauthorizedAccessException>();
    }

    [Test]
    public async Task CloneAsync_NonOwner_Throws()
    {
        await using var db = CreateDbContext();
        var service = new ImportTemplateService(db);
        var template = await service.CreateAsync(
            "Private", ImportSourceKind.Custom, ImportKind.Registration, "owner-1",
            [new ImportColumnMappingInput("Email", ImportTargetField.Email)]);

        var action = async () => await service.CloneAsync(
            template.Id, "someone-else", requestingUserIsAdmin: false);

        await Assert.That(action).Throws<UnauthorizedAccessException>();
    }

    private static async Task<Hackmum.Bethuya.Core.Models.ImportTemplate> SeedSystemTemplateAsync(BethuyaDbContext db)
    {
        var template = new Hackmum.Bethuya.Core.Models.ImportTemplate
        {
            Name = "System Registration Template",
            Scope = ImportTemplateScope.System,
            SourceKind = ImportSourceKind.Luma,
            ImportKind = ImportKind.Registration
        };
        template.ColumnMappings.Add(new Hackmum.Bethuya.Core.Models.ImportColumnMapping
        {
            ImportTemplateId = template.Id,
            SourceColumnName = "Email",
            TargetField = ImportTargetField.Email
        });

        db.ImportTemplates.Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"import-template-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }
}

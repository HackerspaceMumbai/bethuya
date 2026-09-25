using System.Text;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class ImportDryRunServiceTests
{
    [Test]
    public async Task StartAsync_NewRegistrationImport_CreatesBatchWithArtifactAndRawRows()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedRegistrationTemplateAsync(db);

        var service = CreateService(db);
        var csvBytes = Encoding.UTF8.GetBytes("Name,Email\nAda Lovelace,ada@example.com\nAlan Turing,alan@example.com\n");

        var batch = await service.StartAsync(
            eventId,
            template.Id,
            ImportKind.Registration,
            "luma-export.csv",
            "text/csv",
            csvBytes,
            "organizer-1");

        await Assert.That(batch.Status).IsEqualTo(ImportBatchStatus.DryRunCompleted);
        await Assert.That(batch.TotalRows).IsEqualTo(2);
        await Assert.That(batch.ValidRows).IsEqualTo(2);
        await Assert.That(batch.ErrorRows).IsEqualTo(0);
        await Assert.That(batch.RowsToCreate).IsEqualTo(2);
        await Assert.That(batch.RowsToUpdate).IsEqualTo(0);

        var persistedRawRows = await db.ImportRawRows.Where(r => r.ImportBatchId == batch.Id).CountAsync();
        await Assert.That(persistedRawRows).IsEqualTo(2);

        var artifact = await db.ImportArtifacts.SingleAsync(a => a.ImportBatchId == batch.Id);
        await Assert.That(artifact.FileName).IsEqualTo("luma-export.csv");
    }

    [Test]
    public async Task StartAsync_RowMatchingExistingRegistration_IsClassifiedAsUpdate()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedRegistrationTemplateAsync(db);

        db.Registrations.Add(new Registration
        {
            EventId = eventId,
            FullName = "Ada Lovelace",
            Email = "ada@example.com",
            Status = RegistrationStatus.Accepted
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var csvBytes = Encoding.UTF8.GetBytes("Name,Email\nAda Lovelace,ada@example.com\nAlan Turing,alan@example.com\n");

        var batch = await service.StartAsync(
            eventId, template.Id, ImportKind.Registration, "luma-export.csv", "text/csv", csvBytes, "organizer-1");

        await Assert.That(batch.RowsToCreate).IsEqualTo(1);
        await Assert.That(batch.RowsToUpdate).IsEqualTo(1);
    }

    [Test]
    public async Task StartAsync_InvalidRow_IsCountedAsError()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedRegistrationTemplateAsync(db);

        var service = CreateService(db);
        var csvBytes = Encoding.UTF8.GetBytes("Name,Email\nAda Lovelace,not-an-email\n");

        var batch = await service.StartAsync(
            eventId, template.Id, ImportKind.Registration, "luma-export.csv", "text/csv", csvBytes, "organizer-1");

        await Assert.That(batch.ErrorRows).IsEqualTo(1);
        await Assert.That(batch.ValidRows).IsEqualTo(0);
    }

    [Test]
    public async Task StartAsync_TemplateKindMismatch_Throws()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedRegistrationTemplateAsync(db);
        var service = CreateService(db);

        var action = async () => (ImportBatch?)await service.StartAsync(
            eventId, template.Id, ImportKind.Attendance, "x.csv", "text/csv", "Name,Email\n"u8.ToArray(), "organizer-1");

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RerunAsync_ReplaysPersistedRawRowsWithoutReparsingFile()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedRegistrationTemplateAsync(db);
        var service = CreateService(db);

        var csvBytes = Encoding.UTF8.GetBytes("Name,Email\nAda Lovelace,ada@example.com\n");
        var batch = await service.StartAsync(
            eventId, template.Id, ImportKind.Registration, "luma-export.csv", "text/csv", csvBytes, "organizer-1");

        var rerunBatch = await service.RerunAsync(batch.Id);

        await Assert.That(rerunBatch.Id).IsEqualTo(batch.Id);
        await Assert.That(rerunBatch.Status).IsEqualTo(ImportBatchStatus.DryRunCompleted);
        await Assert.That(rerunBatch.TotalRows).IsEqualTo(1);
    }

    [Test]
    public async Task RerunAsync_CommittedBatch_Throws()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedRegistrationTemplateAsync(db);
        var service = CreateService(db);

        var csvBytes = Encoding.UTF8.GetBytes("Name,Email\nAda Lovelace,ada@example.com\n");
        var batch = await service.StartAsync(
            eventId, template.Id, ImportKind.Registration, "luma-export.csv", "text/csv", csvBytes, "organizer-1");

        batch.Status = ImportBatchStatus.Committed;
        await db.SaveChangesAsync();

        var action = async () => (ImportBatch?)await service.RerunAsync(batch.Id);

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetPreviewAsync_ReturnsPerRowDispositions()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedRegistrationTemplateAsync(db);
        var service = CreateService(db);

        var csvBytes = Encoding.UTF8.GetBytes("Name,Email\nAda Lovelace,ada@example.com\nBad Row,not-an-email\n");
        var batch = await service.StartAsync(
            eventId, template.Id, ImportKind.Registration, "luma-export.csv", "text/csv", csvBytes, "organizer-1");

        var preview = await service.GetPreviewAsync(batch.Id);

        await Assert.That(preview.Rows.Count).IsEqualTo(2);
        await Assert.That(preview.Rows[0].Disposition).IsEqualTo(ImportRowDisposition.WillCreate);
        await Assert.That(preview.Rows[1].Disposition).IsEqualTo(ImportRowDisposition.Error);
        await Assert.That(preview.CanCommit).IsFalse();
    }

    private static ImportDryRunService CreateService(BethuyaDbContext db)
        => new(db, new ImportFileParserResolver([new CsvImportFileParser(), new XlsxImportFileParser()]), new LocalDiskImportArtifactStore());

    private static async Task<Guid> SeedEventAsync(BethuyaDbContext db)
    {
        var @event = new Event
        {
            Title = "Import Test Event",
            Type = EventType.Meetup,
            Capacity = 100,
            StartDate = new DateTimeOffset(2026, 8, 1, 18, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 8, 1, 21, 0, 0, TimeSpan.Zero),
            CreatedBy = "organizer"
        };
        db.Events.Add(@event);
        await db.SaveChangesAsync();
        return @event.Id;
    }

    private static async Task<ImportTemplate> SeedRegistrationTemplateAsync(BethuyaDbContext db)
    {
        var template = new ImportTemplate
        {
            Name = "Test Registration Template",
            Scope = ImportTemplateScope.System,
            SourceKind = ImportSourceKind.Luma,
            ImportKind = ImportKind.Registration
        };
        template.ColumnMappings.Add(new ImportColumnMapping { ImportTemplateId = template.Id, SourceColumnName = "Name", TargetField = ImportTargetField.FullName });
        template.ColumnMappings.Add(new ImportColumnMapping { ImportTemplateId = template.Id, SourceColumnName = "Email", TargetField = ImportTargetField.Email });

        db.ImportTemplates.Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"import-dryrun-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }
}

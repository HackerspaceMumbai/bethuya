using System.Text;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class ImportCommitServiceTests
{
    [Test]
    public async Task CommitAsync_RegistrationImport_CreatesMembersAndRegistrations()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedTemplateAsync(db, ImportKind.Registration);

        var dryRunService = CreateDryRunService(db);
        var csvBytes = Encoding.UTF8.GetBytes("Name,Email\nAda Lovelace,ada@example.com\nAlan Turing,alan@example.com\n");
        var batch = await dryRunService.StartAsync(
            eventId, template.Id, ImportKind.Registration, "luma-export.csv", "text/csv", csvBytes, "organizer-1");

        var commitService = new ImportCommitService(db);
        var committed = await commitService.CommitAsync(batch.Id);

        await Assert.That(committed.Status).IsEqualTo(ImportBatchStatus.Committed);
        await Assert.That(committed.RowsToCreate).IsEqualTo(2);
        await Assert.That(committed.RowsToUpdate).IsEqualTo(0);

        var registrations = await db.Registrations.Where(r => r.EventId == eventId).ToListAsync();
        await Assert.That(registrations.Count).IsEqualTo(2);
        await Assert.That(registrations.All(r => r.Status == RegistrationStatus.Accepted)).IsTrue();

        var members = await db.CommunityMembers.Where(m => m.Email == "ada@example.com" || m.Email == "alan@example.com").ToListAsync();
        await Assert.That(members.Count).IsEqualTo(2);
    }

    [Test]
    public async Task CommitAsync_RegistrationImport_UpdatesExistingRegistrationInsteadOfDuplicating()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedTemplateAsync(db, ImportKind.Registration);

        db.Registrations.Add(new Registration
        {
            EventId = eventId,
            FullName = "Old Name",
            Email = "ada@example.com",
            Status = RegistrationStatus.Accepted
        });
        await db.SaveChangesAsync();

        var dryRunService = CreateDryRunService(db);
        var csvBytes = Encoding.UTF8.GetBytes("Name,Email\nAda Lovelace,ada@example.com\n");
        var batch = await dryRunService.StartAsync(
            eventId, template.Id, ImportKind.Registration, "luma-export.csv", "text/csv", csvBytes, "organizer-1");

        var commitService = new ImportCommitService(db);
        var committed = await commitService.CommitAsync(batch.Id);

        await Assert.That(committed.RowsToCreate).IsEqualTo(0);
        await Assert.That(committed.RowsToUpdate).IsEqualTo(1);

        var registrations = await db.Registrations.Where(r => r.EventId == eventId).ToListAsync();
        await Assert.That(registrations.Count).IsEqualTo(1);
        await Assert.That(registrations[0].FullName).IsEqualTo("Ada Lovelace");
    }

    [Test]
    public async Task CommitAsync_AttendanceImport_WritesLedgerEntriesAndSkipsDuplicates()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedTemplateAsync(db, ImportKind.Attendance);

        var dryRunService = CreateDryRunService(db);
        var csvBytes = Encoding.UTF8.GetBytes("Name,Email\nAda Lovelace,ada@example.com\n");
        var firstBatch = await dryRunService.StartAsync(
            eventId, template.Id, ImportKind.Attendance, "luma-attendance.csv", "text/csv", csvBytes, "organizer-1");

        var commitService = new ImportCommitService(db);
        var firstCommit = await commitService.CommitAsync(firstBatch.Id);
        await Assert.That(firstCommit.RowsToCreate).IsEqualTo(1);

        // Re-import the same attendee for the same event in a second batch: must be recognized
        // as a duplicate and skipped rather than written as a second ledger entry.
        var secondBatch = await dryRunService.StartAsync(
            eventId, template.Id, ImportKind.Attendance, "luma-attendance-2.csv", "text/csv", csvBytes, "organizer-1");
        var secondCommit = await commitService.CommitAsync(secondBatch.Id);

        await Assert.That(secondCommit.RowsToCreate).IsEqualTo(0);
        await Assert.That(secondCommit.RowsToUpdate).IsEqualTo(1);

        var ledgerEntries = await db.ParticipationLedgerEntries.Where(e => e.EventId == eventId).ToListAsync();
        await Assert.That(ledgerEntries.Count).IsEqualTo(1);
        await Assert.That(ledgerEntries[0].IngestionMethod).IsEqualTo(ParticipationIngestionMethod.FileImport);
        await Assert.That(ledgerEntries[0].ImportBatchId).IsEqualTo(firstBatch.Id);
    }

    [Test]
    public async Task CommitAsync_BatchNotDryRunCompleted_Throws()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedTemplateAsync(db, ImportKind.Registration);

        var batch = new ImportBatch
        {
            EventId = eventId,
            ImportKind = ImportKind.Registration,
            ImportTemplateId = template.Id,
            CreatedByUserId = "organizer-1",
            Status = ImportBatchStatus.Draft
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        var commitService = new ImportCommitService(db);
        var action = async () => (ImportBatch?)await commitService.CommitAsync(batch.Id);

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task CommitAsync_AlreadyCommittedBatch_IsIdempotentNoOp()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedTemplateAsync(db, ImportKind.Registration);

        var dryRunService = CreateDryRunService(db);
        var csvBytes = Encoding.UTF8.GetBytes("Name,Email\nAda Lovelace,ada@example.com\n");
        var batch = await dryRunService.StartAsync(
            eventId, template.Id, ImportKind.Registration, "luma-export.csv", "text/csv", csvBytes, "organizer-1");

        var commitService = new ImportCommitService(db);
        var firstCommit = await commitService.CommitAsync(batch.Id);
        var secondCommit = await commitService.CommitAsync(batch.Id);

        await Assert.That(secondCommit.Status).IsEqualTo(ImportBatchStatus.Committed);
        await Assert.That(secondCommit.CommittedAt).IsEqualTo(firstCommit.CommittedAt);

        var registrations = await db.Registrations.Where(r => r.EventId == eventId).ToListAsync();
        await Assert.That(registrations.Count).IsEqualTo(1);
    }

    private static ImportDryRunService CreateDryRunService(BethuyaDbContext db)
        => new(db, new ImportFileParserResolver([new CsvImportFileParser(), new XlsxImportFileParser()]), new LocalDiskImportArtifactStore());

    private static async Task<Guid> SeedEventAsync(BethuyaDbContext db)
    {
        var @event = new Event
        {
            Title = "Import Commit Test Event",
            Type = EventType.Meetup,
            Capacity = 100,
            StartDate = new DateTimeOffset(2026, 8, 5, 18, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 8, 5, 21, 0, 0, TimeSpan.Zero),
            CreatedBy = "organizer"
        };
        db.Events.Add(@event);
        await db.SaveChangesAsync();
        return @event.Id;
    }

    private static async Task<ImportTemplate> SeedTemplateAsync(BethuyaDbContext db, ImportKind importKind)
    {
        var template = new ImportTemplate
        {
            Name = $"Test {importKind} Template",
            Scope = ImportTemplateScope.System,
            SourceKind = ImportSourceKind.Luma,
            ImportKind = importKind
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
            .UseInMemoryDatabase($"import-commit-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BethuyaDbContext(options);
    }
}

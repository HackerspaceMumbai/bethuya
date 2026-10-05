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
    public async Task CommunityMemberLookup_TranslatesToIndexedNormalizedEmailExpression()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseNpgsql("Host=localhost;Database=bethuya;Username=test;Password=test")
            .Options;
        await using var db = new BethuyaDbContext(options);
        var emails = new[] { "ada@example.com" };
#pragma warning disable CA1304, CA1311 // Mirrors the indexed Npgsql query in ImportCommitService.
        var sql = db.CommunityMembers
            .Where(member => emails.Contains(member.Email.ToLower()))
            .ToQueryString();
#pragma warning restore CA1304, CA1311

        await Assert.That(sql).Contains("lower(c.\"Email\")");
    }

    [Test]
    public async Task CommitAsync_LumaExports_PreservesApprovalAndCheckInWithoutDuplicateHistory()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var registrationTemplate = await SeedTemplateAsync(db, ImportKind.Registration);
        db.ImportColumnMappings.Add(new ImportColumnMapping
        {
            ImportTemplateId = registrationTemplate.Id,
            SourceColumnName = "approval_status",
            TargetField = ImportTargetField.ApprovalStatus
        });
        var attendanceTemplate = await SeedTemplateAsync(db, ImportKind.Attendance);
        db.ImportColumnMappings.Add(new ImportColumnMapping
        {
            ImportTemplateId = attendanceTemplate.Id,
            SourceColumnName = "checked_in_at",
            TargetField = ImportTargetField.CheckedInAt
        });
        await db.SaveChangesAsync();

        var dryRun = CreateDryRunService(db);
        var commit = new ImportCommitService(db);
        async Task<ImportBatch> ImportAsync(ImportTemplate template, string csv)
        {
            var batch = await dryRun.StartAsync(eventId, template.Id, template.ImportKind,
                "guests.csv", "text/csv", Encoding.UTF8.GetBytes(csv), "organizer-1");
            return await commit.CommitAsync(batch.Id);
        }

        const string pending = "Name,Email,approval_status\nAda,ada@example.com,pending_approval\nGrace,grace@example.com,declined\n";
        await ImportAsync(registrationTemplate, pending);
        var pendingRegistration = await db.Registrations.SingleAsync(r => r.Email == "ada@example.com");
        await Assert.That(pendingRegistration.Status).IsEqualTo(RegistrationStatus.Pending);
        await Assert.That(pendingRegistration.ApprovalObservedAt).IsNull();
        await Assert.That((await db.Registrations.SingleAsync(r => r.Email == "grace@example.com")).Status)
            .IsEqualTo(RegistrationStatus.Rejected);

        var approved = await ImportAsync(registrationTemplate,
            "Name,Email,approval_status\nAda,ada@example.com,approved\nGrace,grace@example.com,declined\n");
        await Assert.That(approved.RowsToCreate).IsEqualTo(0);
        pendingRegistration = await db.Registrations.SingleAsync(r => r.Email == "ada@example.com");
        await Assert.That(pendingRegistration.Status).IsEqualTo(RegistrationStatus.Accepted);
        var approvedAt = pendingRegistration.ApprovalObservedAt;
        await Assert.That(approvedAt).IsNotNull();

        var preEvent = await dryRun.StartAsync(eventId, attendanceTemplate.Id, ImportKind.Attendance,
            "guests.csv", "text/csv",
            Encoding.UTF8.GetBytes("Name,Email,checked_in_at\nAda,ada@example.com,\nGrace,grace@example.com,\n"),
            "organizer-1");
        var preview = await dryRun.GetPreviewAsync(preEvent.Id);
        await Assert.That(preview.RowsToCreate).IsEqualTo(0);
        await Assert.That(preview.Rows.All(row => row.Disposition == ImportRowDisposition.Skipped)).IsTrue();
        await commit.CommitAsync(preEvent.Id);
        await Assert.That(await db.ParticipationLedgerEntries.CountAsync(e => e.EventId == eventId)).IsEqualTo(0);

        var checkedIn = "Name,Email,checked_in_at\nAda,ada@example.com,2026-09-28T08:00:00Z\nGrace,grace@example.com,\n";
        var first = await ImportAsync(attendanceTemplate, checkedIn);
        var again = await ImportAsync(attendanceTemplate, checkedIn);
        await Assert.That(first.RowsToCreate).IsEqualTo(1);
        await Assert.That(again.RowsToCreate).IsEqualTo(0);
        await Assert.That(again.RowsToUpdate).IsEqualTo(1);
        pendingRegistration = await db.Registrations.SingleAsync(r => r.Email == "ada@example.com");
        await Assert.That(pendingRegistration.Status).IsEqualTo(RegistrationStatus.CheckedIn);
        await Assert.That(await db.ParticipationLedgerEntries.CountAsync(e => e.EventId == eventId)).IsEqualTo(1);

        await ImportAsync(registrationTemplate, pending);
        pendingRegistration = await db.Registrations.SingleAsync(r => r.Email == "ada@example.com");
        await Assert.That(pendingRegistration.Status).IsEqualTo(RegistrationStatus.CheckedIn);
        await Assert.That(pendingRegistration.ApprovalObservedAt).IsEqualTo(approvedAt);
        await Assert.That(await db.Registrations.CountAsync(r => r.EventId == eventId)).IsEqualTo(2);
        await Assert.That(await db.CommunityMembers.CountAsync()).IsEqualTo(2);
    }

    [Test]
    public async Task CommitAsync_SeparateApprovalExport_UpdatesExistingRegistrationWithoutNameOrDuplicates()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedTemplateAsync(db, ImportKind.Registration);
        db.ImportColumnMappings.Add(new ImportColumnMapping
        {
            ImportTemplateId = template.Id,
            SourceColumnName = "approval_status",
            TargetField = ImportTargetField.ApprovalStatus
        });
        await db.SaveChangesAsync();

        var dryRun = CreateDryRunService(db);
        var commit = new ImportCommitService(db);
        var request = await dryRun.StartAsync(eventId, template.Id, ImportKind.Registration,
            "requests.csv", "text/csv", Encoding.UTF8.GetBytes("Name,Email,approval_status\nAda,ada@example.com,pending_approval\n"), "organizer-1");
        await commit.CommitAsync(request.Id);
        var storedRegistration = await db.Registrations.SingleAsync(r => r.EventId == eventId);
        storedRegistration.Email = "ADA@Example.COM";
        await db.SaveChangesAsync();

        var approval = await dryRun.StartAsync(eventId, template.Id, ImportKind.Registration,
            "approvals.csv", "text/csv", Encoding.UTF8.GetBytes("Email,approval_status\nada@example.com,approved\n"), "organizer-1");
        var preview = await dryRun.GetPreviewAsync(approval.Id);
        await Assert.That(preview.RowsToUpdate).IsEqualTo(1);
        await Assert.That(preview.ErrorRows).IsEqualTo(0);
        await commit.CommitAsync(approval.Id);

        var registration = await db.Registrations.SingleAsync(r => r.EventId == eventId);
        await Assert.That(registration.Status).IsEqualTo(RegistrationStatus.Accepted);
        await Assert.That(registration.FullName).IsEqualTo("Ada");
        await Assert.That(await db.CommunityMembers.CountAsync()).IsEqualTo(1);

        var unknown = await dryRun.StartAsync(eventId, template.Id, ImportKind.Registration,
            "orphan-approval.csv", "text/csv", Encoding.UTF8.GetBytes("Email,approval_status\nnew@example.com,approved\n"), "organizer-1");
        var unknownPreview = await dryRun.GetPreviewAsync(unknown.Id);
        await Assert.That(unknownPreview.ErrorRows).IsEqualTo(1);
        await Assert.That(unknownPreview.Rows[0].ValidationErrors).Contains("Full name is required for a new registration.");
    }

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
        await Assert.That(registrations.All(r => r.Status == RegistrationStatus.Pending)).IsTrue();

        var members = await db.CommunityMembers.Where(m => m.Email == "ada@example.com" || m.Email == "alan@example.com").ToListAsync();
        await Assert.That(members.Count).IsEqualTo(2);
    }

    [Test]
    public async Task CommitAsync_TemplateMappingsChangedAfterDryRun_RejectsWithoutWritingRegistrations()
    {
        await using var db = CreateDbContext();
        var eventId = await SeedEventAsync(db);
        var template = await SeedTemplateAsync(db, ImportKind.Registration);
        var dryRun = CreateDryRunService(db);
        var batch = await dryRun.StartAsync(
            eventId,
            template.Id,
            ImportKind.Registration,
            "luma-export.csv",
            "text/csv",
            Encoding.UTF8.GetBytes("Name,Email,Alternate Email\nAda Lovelace,ada@example.com,other@example.com\n"),
            "organizer-1");

        template.ColumnMappings.Single(mapping => mapping.TargetField == ImportTargetField.Email).SourceColumnName = "Alternate Email";
        await db.SaveChangesAsync();

        var committed = await new ImportCommitService(db).CommitAsync(batch.Id);

        await Assert.That(committed.Status).IsEqualTo(ImportBatchStatus.Failed);
        await Assert.That(committed.FailureReason).Contains("template mappings changed after the last Dry Run");
        await Assert.That(await db.Registrations.CountAsync(r => r.EventId == eventId)).IsEqualTo(0);
        await Assert.That(await db.CommunityMembers.CountAsync()).IsEqualTo(0);
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

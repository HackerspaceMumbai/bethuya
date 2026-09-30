using System.Text.Json;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Commits a Dry-Run-validated <see cref="ImportBatch"/>: re-validates defensively, then
/// atomically resolves/creates <see cref="CommunityMember"/>s and writes
/// <see cref="Registration"/> or <see cref="ParticipationLedgerEntry"/> rows. Commit only ever
/// runs from <see cref="ImportBatchStatus.DryRunCompleted"/> with zero validation errors, and is
/// all-rows-or-none.
/// </summary>
public sealed class ImportCommitService(BethuyaDbContext db)
{
    // Member creation is keyed by a deterministic import user id. Serialize the lookup/create
    // window so simultaneous import commits in this service host cannot race the unique index.
    private static readonly SemaphoreSlim MemberCreationGate = new(1, 1);

    public async Task<ImportBatch> CommitAsync(Guid importBatchId, CancellationToken ct = default)
    {
        await ImportMutationGate.Instance.WaitAsync(ct);
        try
        {
            var batch = await db.ImportBatches
                .Include(b => b.RawRows)
                .SingleOrDefaultAsync(b => b.Id == importBatchId, ct)
                ?? throw new InvalidOperationException($"Import batch '{importBatchId}' was not found.");

            if (batch.Status == ImportBatchStatus.Committed)
            {
                // Idempotent: a repeated commit request (e.g. a retried client call) is a no-op.
                return batch;
            }

            if (batch.Status != ImportBatchStatus.DryRunCompleted)
            {
                throw new InvalidOperationException(
                    "Run a Dry Run and resolve all validation errors before committing this import.");
            }

            var template = await db.ImportTemplates
                .Include(t => t.ColumnMappings)
                .SingleAsync(t => t.Id == batch.ImportTemplateId, ct);

            if (!string.Equals(
                    batch.DryRunMappingFingerprint,
                    ImportMappingFingerprint.Compute(template),
                    StringComparison.Ordinal))
            {
                batch.Status = ImportBatchStatus.Failed;
                batch.FailureReason =
                    "Commit was rejected because the template mappings changed after the last Dry Run. Run the Dry Run again before committing.";
                await db.SaveChangesAsync(ct);
                return batch;
            }

            var rawRows = batch.RawRows
                .OrderBy(r => r.RowIndex)
                .Select(r => (IReadOnlyDictionary<string, string?>)(
                    JsonSerializer.Deserialize<Dictionary<string, string?>>(r.RawDataJson) ?? []))
                .ToList();

            var normalizedRows = ImportRowNormalizer.NormalizeAll(new ParsedImportFile([], rawRows), template);

            if (normalizedRows.Any(row => !row.IsValid))
            {
            // Defensive re-check: the underlying template or data may have changed since the last
            // Dry Run. Reject rather than commit partially-invalid data; the organizer must re-run
            // the Dry Run to see current validation state.
            batch.Status = ImportBatchStatus.Failed;
            batch.FailureReason =
                "Commit was rejected: rows failed validation on re-check. Run the Dry Run again before committing.";
            await db.SaveChangesAsync(ct);
                return batch;
            }

        var connector = ImportConnectorMapper.ToConnector(template.SourceKind);
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            foreach (var trackedEntry in db.ChangeTracker.Entries().ToList())
            {
                trackedEntry.State = EntityState.Detached;
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var (created, updated) = batch.ImportKind == ImportKind.Registration
                ? await CommitRegistrationsAsync(batch, normalizedRows, ct)
                : await CommitAttendanceAsync(batch, normalizedRows, connector, ct);

            batch.Status = ImportBatchStatus.Committed;
            batch.CommittedAt = DateTimeOffset.UtcNow;
            batch.RowsToCreate = created;
            batch.RowsToUpdate = updated;
            batch.FailureReason = null;

            // Mark only the batch entity itself as modified. Calling db.ImportBatches.Update(batch)
            // would walk the whole reachable navigation graph (e.g. batch.Event.Registrations, wired
            // up via EF's automatic relationship fixup against other tracked entities in this
            // context) and re-attach already-tracked child entities, causing a duplicate-tracking
            // conflict.
            db.Entry(batch).State = EntityState.Modified;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

            return batch;
        }
        finally
        {
            ImportMutationGate.Instance.Release();
        }
    }

    private async Task<(int Created, int Updated)> CommitRegistrationsAsync(
        ImportBatch batch,
        IReadOnlyList<NormalizedImportRow> rows,
        CancellationToken ct)
    {
        var importedEmails = rows.Select(row => row.Email!).ToHashSet(StringComparer.Ordinal);
        var existingRegistrations = await LoadRegistrationsAsync(batch.EventId, importedEmails, ct);
        var existingByEmail = existingRegistrations
            .GroupBy(r => r.Email.Trim().ToLowerInvariant(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        if (rows.Any(row => row.FullName is null && !existingByEmail.ContainsKey(row.Email!)))
        {
            throw new InvalidOperationException("A full name is required when an approval import creates a new registration.");
        }

        var fullNameByEmail = rows.ToDictionary(
            row => row.Email!,
            row => row.FullName ?? existingByEmail.GetValueOrDefault(row.Email!)?.FullName,
            StringComparer.Ordinal);
        await ResolveOrCreateMembersAsync(fullNameByEmail, ct);

        var created = 0;
        var updated = 0;

        foreach (var row in rows)
        {
            var email = row.Email!;
            if (existingByEmail.TryGetValue(email, out var registration))
            {
                registration.FullName = row.FullName ?? registration.FullName;
                registration.Intent = row.Intent ?? registration.Intent;
                registration.Goals = row.Goals ?? registration.Goals;
                registration.ExperienceLevel = row.ExperienceLevel ?? registration.ExperienceLevel;
                registration.DietaryRequirements = row.DietaryRequirements ?? registration.DietaryRequirements;
                registration.AccessibilityNeeds = row.AccessibilityNeeds ?? registration.AccessibilityNeeds;
                if (row.ApprovalStatus is { } newStatus &&
                    (registration.Status == RegistrationStatus.Pending ||
                     registration.Status == RegistrationStatus.Rejected && newStatus == RegistrationStatus.Accepted))
                {
                    // An older export must not undo a later approval or check-in. Explicit
                    // declines can only replace pending/declined states; review other changes manually.
                    registration.Status = newStatus;
                }
                if (row.ApprovalStatus == RegistrationStatus.Accepted && registration.ApprovalObservedAt is null)
                {
                    registration.ApprovalObservedAt = DateTimeOffset.UtcNow;
                }
                registration.UpdatedAt = DateTimeOffset.UtcNow;
                updated++;
            }
            else
            {
                registration = new Registration
                {
                    EventId = batch.EventId,
                    FullName = row.FullName!,
                    Email = email,
                    Intent = row.Intent,
                    Goals = row.Goals,
                    ExperienceLevel = row.ExperienceLevel,
                    DietaryRequirements = row.DietaryRequirements,
                    AccessibilityNeeds = row.AccessibilityNeeds,
                    Bio = row.Notes,
                    // Without an explicit source approval decision, keep imported requests pending.
                    Status = row.ApprovalStatus ?? RegistrationStatus.Pending,
                    ApprovalObservedAt = row.ApprovalStatus == RegistrationStatus.Accepted ? DateTimeOffset.UtcNow : null,
                    RegisteredAt = row.OccurredAt ?? DateTimeOffset.UtcNow
                };
                db.Registrations.Add(registration);
                existingByEmail[email] = registration;
                created++;
            }
        }

        await db.SaveChangesAsync(ct);
        return (created, updated);
    }

    private async Task<(int Created, int Updated)> CommitAttendanceAsync(
        ImportBatch batch,
        IReadOnlyList<NormalizedImportRow> rows,
        ParticipationConnectorKind connector,
        CancellationToken ct)
    {
        var checkedInRows = rows.Where(row => !row.SkipAttendance).ToArray();
        var fullNameByEmail = checkedInRows.ToDictionary(row => row.Email!, row => row.FullName, StringComparer.Ordinal);
        var memberIdByEmail = await ResolveOrCreateMembersAsync(fullNameByEmail, ct);

        var provenanceKeys = checkedInRows
            .Select(row => ImportProvenanceKeyBuilder.BuildAttendanceProvenanceKey(batch.EventId, row.Email!))
            .ToArray();
        var existingKeys = (await db.ParticipationLedgerEntries
                .Where(e => e.EventId == batch.EventId && provenanceKeys.Contains(e.ProvenanceKey))
                .Select(e => e.ProvenanceKey)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var skipped = 0;
        var checkedInEmails = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (row.SkipAttendance)
            {
                continue;
            }

            var email = row.Email!;
            checkedInEmails.Add(email);
            var provenanceKey = ImportProvenanceKeyBuilder.BuildAttendanceProvenanceKey(batch.EventId, email);
            if (!existingKeys.Add(provenanceKey))
            {
                // Already recorded for this event (either an earlier import or this batch's own
                // in-file duplicate defensive check) — attendance is append-only, so skip rather
                // than write a second entry.
                skipped++;
                continue;
            }

            db.ParticipationLedgerEntries.Add(new ParticipationLedgerEntry
            {
                CommunityMemberId = memberIdByEmail[email],
                Connector = connector,
                IngestionMethod = ParticipationIngestionMethod.FileImport,
                ImportBatchId = batch.Id,
                ExternalMemberKey = email,
                EventId = batch.EventId,
                ExternalRecordId = row.ExternalRecordId,
                Activity = ParticipationActivityKind.Attended,
                Evidence = string.IsNullOrWhiteSpace(row.Notes)
                    ? $"Imported attendance record for {email}."
                    : row.Notes,
                ProvenanceKey = provenanceKey,
                OccurredAt = row.OccurredAt ?? DateTimeOffset.UtcNow
            });
            created++;
        }

        var registrations = await LoadRegistrationsAsync(batch.EventId, checkedInEmails, ct);
        foreach (var registration in registrations.Where(r => r.Status != RegistrationStatus.CheckedIn))
        {
            registration.Status = RegistrationStatus.CheckedIn;
            registration.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return (created, skipped);
    }

    private async Task<List<Registration>> LoadRegistrationsAsync(
        Guid eventId,
        HashSet<string> normalizedEmails,
        CancellationToken ct)
    {
        var registrationKeys = await db.Registrations
            .Where(registration => registration.EventId == eventId)
            .Select(registration => new { registration.Id, registration.Email })
            .ToListAsync(ct);
        var matchingIds = registrationKeys
            .Where(registration => normalizedEmails.Contains(registration.Email.Trim().ToLowerInvariant()))
            .Select(registration => registration.Id)
            .ToArray();

        return matchingIds.Length == 0
            ? []
            : await db.Registrations
                .Where(registration => matchingIds.Contains(registration.Id))
                .ToListAsync(ct);
    }

    /// <summary>
    /// Resolves each email to an existing <see cref="CommunityMember"/> (exact, case-insensitive
    /// email match — the only identity resolution strategy in this phase) or creates a new one.
    /// Newly created members get a synthetic <c>import:{email}</c> user id placeholder until the
    /// person signs in and their account can be linked/reconciled by an organizer.
    /// </summary>
    private async Task<Dictionary<string, CommunityMemberId>> ResolveOrCreateMembersAsync(
        IReadOnlyDictionary<string, string?> fullNameByEmail,
        CancellationToken ct)
    {
        await MemberCreationGate.WaitAsync(ct);
        try
        {
            var emails = fullNameByEmail.Keys.ToArray();
#pragma warning disable CA1304, CA1311 // Npgsql translates ToLower() to lower(), backed by IX_CommunityMembers_NormalizedEmail.
            var existingMembers = emails.Length == 0
                ? []
                : await db.CommunityMembers
                    .Where(member => emails.Contains(member.Email.ToLower()))
                    .ToListAsync(ct);
#pragma warning restore CA1304, CA1311

            var resolved = existingMembers
                .GroupBy(m => m.Email.Trim().ToLowerInvariant(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

            foreach (var email in emails)
            {
                if (resolved.ContainsKey(email))
                {
                    continue;
                }

                var fullName = fullNameByEmail[email];
                var member = new CommunityMember
                {
                    UserId = $"import:{email}",
                    DisplayName = string.IsNullOrWhiteSpace(fullName) ? email : fullName,
                    Email = email
                };

                db.CommunityMembers.Add(member);
                resolved[email] = member.Id;
            }

            await db.SaveChangesAsync(ct);
            return resolved;
        }
        finally
        {
            MemberCreationGate.Release();
        }
    }
}

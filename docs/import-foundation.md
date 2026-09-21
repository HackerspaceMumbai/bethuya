# Registration & Attendance Import Foundation

**Status:** ✅ Backend implemented (Phase 1 MVP, [issue #59](https://github.com/HackerspaceMumbai/bethuya/issues/59))
**Scope:** CSV/XLSX import of registrations and attendance from Luma, MLH/OrganizerHQ, and
custom spreadsheets, via a **Dry Run → Commit** workflow.
**Not in this phase:** API/webhook integrations, real-time sync, advanced identity matching,
community scoring, curator recommendations, passport achievements, Community Graph analytics,
and organizer-facing UI (backend/API only — see [Out of scope](#out-of-scope)).

---

## 1. Why this exists

Most Bethuya-supported events collect registrations on an external platform (Luma, MLH /
OrganizerHQ, Meetup, sponsor sheets, community spreadsheets). This feature lets an organizer take
that platform's CSV/XLSX export and bring it into Bethuya, so registration and attendance history
becomes part of a member's long-term community profile — without building bespoke integrations
for every platform.

Design principle: **AI/automation drafts nothing here — this is a deterministic, organizer-driven
ETL pipeline.** Every import is explicit, previewable, auditable, and reversible up until commit.

---

## 2. Architecture overview

```
┌─────────────┐   upload    ┌────────────────────┐   preview    ┌──────────────┐
│  CSV / XLSX │ ──────────▶ │ ImportDryRunService │ ───────────▶ │ Organizer UI │
│   (file)    │             │  (parse + validate  │              │  (review)    │
└─────────────┘             │   + classify rows)  │              └──────┬───────┘
                             └─────────┬──────────┘                     │
                                       │ persists                       │ commit
                                       ▼                                ▼
                             ┌──────────────────┐             ┌──────────────────────┐
                             │  ImportBatch +   │             │  ImportCommitService  │
                             │  ImportArtifact + │◀───────────│  (resolve members,    │
                             │  ImportRawRow[]   │  re-reads   │   write Registration/ │
                             └──────────────────┘   raw rows  │   ParticipationLedger) │
                                                               └──────────────────────┘
```

Three layers, each independently testable:

| Layer | Project | Responsibility |
| --- | --- | --- |
| **Parsing** | `Hackmum.Bethuya.Infrastructure.Services` | Turn file bytes into `IReadOnlyDictionary<string, string?>` rows (`CsvImportFileParser`, `XlsxImportFileParser`), resolved by file extension via `ImportFileParserResolver`. No validation, no DB. |
| **Normalization (pure)** | `Hackmum.Bethuya.Core.Services.ImportRowNormalizer` | Maps raw column headers to canonical `ImportTargetField`s using a template's `ImportColumnMapping`s, validates required fields and email format, flags in-file duplicate emails. No DB access — fully unit-testable with plain objects. |
| **Orchestration (DB-aware)** | `Hackmum.Bethuya.Backend.Services` | `ImportDryRunService` persists the artifact/raw rows and classifies each valid row as create/update against existing Bethuya data. `ImportCommitService` re-validates and atomically writes `Registration` or `ParticipationLedgerEntry` rows. `ImportTemplateService` manages reusable column mappings. |

### Why parsing and normalization are separated

Identity resolution (does this email already exist in Bethuya?) requires a database round-trip,
but header-mapping and field validation do not. Keeping `ImportRowNormalizer` pure means:

- It has zero setup cost in tests (no `DbContext`, no fixtures).
- The same normalization logic runs identically during Dry Run, Dry Run re-run ("replay"), and
  Commit — there is only one code path that decides "is this row valid," so Dry Run's preview can
  never drift from what Commit will actually do.

---

## 3. Domain model

```
Event                    ImportTemplate                    ImportBatch
─────                    ──────────────                    ───────────
Id                        Id                                 Id
Registrations[]  ◀──┐     Name                               EventId ────────┐
                     │     Scope (System | User)              ImportKind      │
                     │     SourceKind (Luma | MLH | Custom)   ImportTemplateId├─▶ ImportTemplate
                     │     ImportKind (Registration|Attendance) Status         │
                     │     OwnerUserId (null for System)      TotalRows        │
                     │     ClonedFromTemplateId               ValidRows       │
                     │     ColumnMappings[] ─▶ ImportColumnMapping             │
                     │                         (SourceColumnName, TargetField) ErrorRows
                     │                                        RowsToCreate
                     │                                        RowsToUpdate
                     │                                        FailureReason
                     │                                        CreatedByUserId / CreatedAt
                     │                                        DryRunCompletedAt / CommittedAt
                     │                                        ImportArtifact (1:1) ──▶ ImportArtifact
                     │                                        RawRows[] (1:N) ────────▶ ImportRawRow
                     │
                     └───── Registration (existing model; Email is the join key for imports)

CommunityMember                          ParticipationLedgerEntry (existing model)
───────────────                          ─────────────────────────
Id                                        ImportBatchId (nullable FK — set when written by an import)
Email  ◀── identity-resolution key        IngestionMethod (FileImport | ... existing values)
DisplayName                               ProvenanceKey (dedupe key; see §5)
UserId ("import:{email}" placeholder      ...
        until the person signs in)
```

| Model | Purpose |
| --- | --- |
| **`ImportTemplate`** | A reusable, named set of column mappings for one `ImportSourceKind` × `ImportKind` combination. `Scope = System` templates (seeded for Luma/MLH, see §6) are read-only; organizers clone them into an editable `Scope = User` template to tweak headers for their event's actual export. |
| **`ImportColumnMapping`** | One `SourceColumnName` (the header text in the uploaded file, case-insensitive) → `ImportTargetField` (a canonical field Bethuya understands) pair. |
| **`ImportBatch`** | One import attempt: one uploaded file, scoped to one event, one `ImportKind`. Carries row counts and status. This is the unit of audit and the unit of replay (§7). |
| **`ImportArtifact`** | The original uploaded file's bytes (via `IImportArtifactStore`), plus `FileName`, `ContentType`, `SizeBytes`, and a `Sha256Checksum` for integrity/audit. 1:1 with `ImportBatch`. |
| **`ImportRawRow`** | One row of the original file, persisted verbatim as JSON (`RawDataJson`), indexed by `RowIndex`. Retaining raw rows (not just normalized ones) is what makes Dry Run replay possible without re-uploading the file. |
| **`Registration`** (existing) | Created/updated by `ImportCommitService` for Registration imports. Imported rows are stamped `RegistrationStatus.Accepted` — an import represents an already-completed sign-up on the source platform, so it does not re-enter Bethuya's own curation/waitlist flow. |
| **`ParticipationLedgerEntry`** (existing, extended) | Appended for Attendance imports. Two columns were added: `ImportBatchId` (nullable FK, traces an entry back to the batch that wrote it) and `IngestionMethod` (`ParticipationIngestionMethod.FileImport` for imports, vs. other values for e.g. live check-in). |

---

## 4. Organizer workflow

1. **Choose or clone a template.** `GET /api/import/templates?importKind=Registration` lists the
   System templates (Luma, MLH) plus any of the organizer's own. If the event's export headers
   don't match a System template exactly, the organizer clones it (`POST
   /api/import/templates/{id}/clone`) and edits the mapping (`PUT /api/import/templates/{id}`).
2. **Upload the file.** `POST /api/import/batches` (multipart form: `eventId`,
   `importTemplateId`, `importKind`, `file`). This immediately runs the first Dry Run and returns
   an `ImportBatch` in `DryRunCompleted` status with row counts.
3. **Review the Dry Run preview.** `GET /api/import/batches/{id}/preview` returns a
   per-row breakdown: `WillCreate`, `WillUpdate`, or `Error` (with the specific validation
   message(s) for that row) — nothing has been written to `Registration` or
   `ParticipationLedgerEntry` yet.
4. **Fix and replay, if needed.** If the template mapping was wrong, the organizer edits the
   template and calls `POST /api/import/batches/{id}/dry-run` to re-validate the *same* uploaded
   file against the corrected mapping — see §7 (Replay model) for exactly what this does and does
   not re-check.
5. **Commit.** Once the preview shows zero errors, `POST /api/import/batches/{id}/commit` atomically
   resolves-or-creates `CommunityMember`s, writes `Registration`/`ParticipationLedgerEntry` rows,
   and marks the batch `Committed`. The batch is now immutable.
6. **Verify.** Imported registrations/attendance appear via the existing event/member endpoints —
   no separate "import" surface is needed to see the result, satisfying the PRD's success
   criterion that imported data becomes visible in member history and event reporting.

### Batch status lifecycle

```
Draft ──▶ DryRunCompleted ──▶ Committed
              │  ▲
              └──┘ (re-run Dry Run any number of times)
              │
              ▼
            Failed  (defensive re-check failed at commit time; re-run Dry Run to see why)
```

Status only ever moves forward. `Committed` is a terminal, locked state: a second `commit` call
on an already-committed batch is a no-op (idempotent — safe for a retried client request), and
`dry-run` (replay) on a committed batch throws.

---

## 5. Identity resolution strategy

**Only one strategy exists in this phase: exact, case-insensitive email match.** This is a
deliberate scope decision from the PRD ("avoid accidental member merges" outranks "resolve every
possible duplicate").

- **Registration imports** match against `Registration.Email` *scoped to the same event* — an
  existing registration for that event + email is an update (their submitted answers are
  refreshed); no match is a new `Registration`.
- **Attendance imports** match against a computed `ProvenanceKey` (`BuildAttendanceProvenanceKey(eventId, email)` — see §7) against existing `ParticipationLedgerEntry` rows for that event.
  Attendance is an **append-only ledger**, so a "match" here doesn't rewrite anything — it means
  the row is skipped as already-recorded.
- **Member resolution** (`ImportCommitService.ResolveOrCreateMembersAsync`) looks up
  `CommunityMember` by trimmed, lower-cased email across *all* events. No match creates a new
  `CommunityMember` with a synthetic `UserId` of `import:{email}` — a placeholder until the real
  person signs in and an organizer/admin links the accounts (that reconciliation flow is out of
  scope for this phase; see §8).

### What this does *not* do (by design)

- No fuzzy/name-based matching, no phone-number matching, no cross-referencing multiple emails
  for one person.
- No automatic merging of two `CommunityMember` rows that turn out to be the same person under
  different emails.
- No confidence scoring beyond a binary match/no-match on email.

### Risk mitigation actually in place

| Risk (from PRD) | Mitigation |
| --- | --- |
| Accidental member merges | Only an exact, case-insensitive email match ever links a row to an existing `CommunityMember`/`Registration` — no partial or fuzzy matching that could conflate two different people. |
| Silent duplicate registrations | In-file duplicate emails are flagged as **validation errors on every row that shares the email** (`ImportRowNormalizer.FlagDuplicateEmails`) — the organizer must resolve them in the source file before the row can be committed, rather than Bethuya silently picking one. |
| Duplicate attendance across repeated imports | The `ProvenanceKey` dedupe check runs both within Dry Run preview (§4 step 3) and again at commit time against the live table, so re-importing overlapping data (e.g. an updated export that includes previously-imported rows) creates zero duplicate ledger entries. |
| Committing partially-invalid data | Commit defensively re-normalizes and re-validates every row immediately before writing; if anything fails at that point the whole batch is marked `Failed` and nothing is written — see §7. |

### Explicitly deferred to a future phase

"High confidence / potential duplicate / needs review" tiers, and any organizer-facing reconciliation
UI for merging or re-pointing `import:{email}` placeholder members once someone signs in, are **not**
implemented yet. The `UserId = import:{email}` convention exists specifically so that future phase
has an unambiguous marker for "this member was created by an import and has never authenticated."

---

## 6. Templates

`ImportTemplateSeeder.EnsureSeededAsync` idempotently seeds four `Scope = System` templates on
startup (see `Program.cs`):

| Template | Source | Kind | Mapped columns |
| --- | --- | --- | --- |
| Luma Standard Registration Export | Luma | Registration | Name, Email, Registered At |
| Luma Attendance Export | Luma | Attendance | Name, Email, Check-in Time |
| MLH Registration Export | MLH | Registration | Full Name, Email Address, Application Date, School (→ Notes) |
| MLH Attendance Export | MLH | Attendance | Full Name, Email Address, Checked In At |

System templates are **read-only** (`ImportTemplateService.UpdateAsync` throws
`InvalidOperationException` if `Scope == System`) — they are treated as configuration data that
tracks the real column headers of each platform's export format, not hardcoded parser logic, so a
platform changing its export headers is a data fix (clone + edit), not a code deployment. An
organizer clones a System template (`POST /api/import/templates/{id}/clone`) to get an editable
`Scope = User` copy, which only its owner or an Admin can subsequently edit or re-clone.

`ImportSourceKind.Custom` exists for sponsor sheets or ad-hoc spreadsheets that don't match either
platform — organizers build a `User`-scope template from scratch (`POST /api/import/templates`)
by mapping their own headers to the canonical `ImportTargetField`s (`Email`, `FullName`,
`OccurredAt`, `Notes`, `Intent`, `Goals`, `ExperienceLevel`, `DietaryRequirements`,
`AccessibilityNeeds`, `ExternalRecordId`).

---

## 7. Replay model

"Replay" means re-running Dry Run validation against an **already-uploaded** file, typically after
fixing the template's column mapping, without asking the organizer to re-upload anything.

This works because `ImportDryRunService.StartAsync` persists every parsed row as an
`ImportRawRow` (raw header→value JSON, `RowIndex`-ordered) at upload time, *before* any mapping is
applied. `POST /api/import/batches/{id}/dry-run` (`ImportDryRunService.RerunAsync`):

1. Loads the batch's persisted `RawRows` (not the original file bytes — the file itself is only
   re-read if you need the artifact directly via `IImportArtifactStore`).
2. Loads the **current** state of the batch's `ImportTemplate` (its mappings may have changed
   since the last run).
3. Re-runs the exact same `ImportRowNormalizer.NormalizeAll` pure function used at upload time and
   at commit time.
4. Recomputes the create/update/error classification against the **current** state of
   `Registration`/`ParticipationLedgerEntry` (another import may have landed data in between).
5. Overwrites the batch's row counts and sets `DryRunCompletedAt` to `UtcNow`, but does **not**
   touch `Status` if it's already `Committed` — replay is blocked entirely on a committed batch
   (`RerunAsync` throws `InvalidOperationException`, since committed data is locked).

**Guarantees:**
- Idempotent: running Dry Run N times on an unchanged template and unchanged existing data
  produces byte-identical row counts and per-row dispositions every time.
- Non-destructive: Dry Run (initial or replay) never writes to `Registration` or
  `ParticipationLedgerEntry` — only to the batch's own bookkeeping columns and (on first upload
  only) the `ImportArtifact`/`ImportRawRow` rows.
- Consistent with Commit: because both Dry Run and Commit call the same
  `ImportRowNormalizer.NormalizeAll`, what the organizer approved in the last Dry Run preview is
  exactly what Commit will (re-)validate — Commit's own re-check (§4 step 5) exists only to catch
  drift *since* that last replay (e.g. someone edited the template after the organizer's last
  look, or someone else committed a conflicting import concurrently), not because Dry Run and
  Commit disagree on what "valid" means.

**Provenance keys**, used for attendance dedupe both in Dry Run preview and at commit, are built by
`ImportProvenanceKeyBuilder.BuildAttendanceProvenanceKey(eventId, email)` — deterministic and
stable across replays and repeated imports, so the same person's attendance at the same event
always hashes to the same key regardless of which import batch (or how many replays) produced it.

---

## 8. Out of scope (explicit)

Per the PRD, this phase does **not** include: API/webhook integrations with Luma/MLH/Meetup,
real-time synchronization, advanced (fuzzy/multi-field) identity matching, community scoring,
curator recommendations, passport achievements, or Community Graph analytics. It also does not
include an organizer-facing Blazor UI — the backend/API described here is complete and
independently usable via HTTP (e.g. from Scalar, curl, or a future UI), but no upload/dry-run/
commit wizard page has been built yet.

---

## 9. Local & manual testing

### 9.1 Automated tests (fastest feedback loop)

All pure and integration-style logic has TUnit coverage using the EF Core InMemory provider — no
Aspire/Postgres/Docker required:

```powershell
# Full suite
dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj

# Just the Import feature (TUnit uses the Microsoft Testing Platform CLI —
# note the `--` separator and `--treenode-filter`, not `--filter`)
dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -- --treenode-filter "/*/*/*Import*/*"
```

Covered: `ImportRowNormalizer` (header mapping, required-field/email validation, duplicate-email
flagging), `CsvImportFileParser`/`XlsxImportFileParser`/`ImportFileParserResolver`,
`ImportDryRunService` (new-batch creation, update classification, invalid-row handling, template
mismatch, replay, replay-on-committed-batch rejection), `ImportCommitService` (create, update,
attendance dedupe across batches, commit-preconditions, idempotent double-commit),
`ImportTemplateService` (create, clone, system-template immutability, ownership/Admin
authorization, scoped listing), and `ImportTemplateSeeder` (seeds all four templates, idempotent
on re-run).

### 9.2 Manual end-to-end testing via Aspire

1. Start the app:
   ```powershell
   aspire start --isolated
   ```
2. Open the Aspire Dashboard (<http://localhost:18888>), find the `backend` resource, and note its
   HTTP port. Open `/scalar` on that port for interactive API docs, or use the port directly with
   `curl`/Postman.
3. **Sign in as an Organizer or Admin** first — every `/api/import/*` endpoint requires the
   `RequireOrganizer` policy.
4. **List templates** to get a `templateId` and confirm the four System templates are seeded:
   ```powershell
   $API = "http://localhost:$BACKEND_PORT"
   curl "$API/api/import/templates?importKind=Registration"
   ```
5. **Prepare a small test file** matching one System template's headers exactly, e.g.
   `luma-registrations.csv`:
   ```csv
   Name,Email,Registered At
   Jane Doe,jane@example.com,2026-09-01T10:00:00Z
   John Smith,not-an-email,2026-09-01T11:00:00Z
   ```
   (the second row is intentionally invalid, to confirm error reporting.)
6. **Upload and Dry Run** (multipart form):
   ```powershell
   curl -X POST "$API/api/import/batches" `
     -F "eventId=$EVENT_ID" `
     -F "importTemplateId=$TEMPLATE_ID" `
     -F "importKind=Registration" `
     -F "file=@luma-registrations.csv;type=text/csv"
   ```
   Expect `Status: DryRunCompleted`, `TotalRows: 2`, `ValidRows: 1`, `ErrorRows: 1`,
   `RowsToCreate: 1`.
7. **Inspect the per-row preview:**
   ```powershell
   curl "$API/api/import/batches/$BATCH_ID/preview"
   ```
   Confirm row 0 is `WillCreate` and row 1 is `Error` with a message about the invalid email.
8. **Replay after a template fix** (optional): edit the template's mapping via `PUT
   /api/import/templates/{templateId}`, then re-run `POST
   /api/import/batches/$BATCH_ID/dry-run` and re-check the preview — row counts should reflect
   the corrected mapping without re-uploading the file.
9. **Fix the bad row in the source file and re-upload as a *new* batch**, or accept the batch as-is
   if only the valid row should be committed — note commit is all-rows-or-none only in the sense
   that *if any row is currently invalid, commit refuses entirely* (`Failed` status); a batch with
   zero errors commits every valid row.
10. **Commit:**
    ```powershell
    curl -X POST "$API/api/import/batches/$BATCH_ID/commit"
    ```
    Expect `Status: Committed`. Re-running commit on the same batch returns the same result
    (idempotent no-op) rather than erroring.
11. **Verify visibility:** confirm the new `Registration` appears via the existing
    `GET /api/events/{eventId}` / registrations listing endpoints, and (for an Attendance import)
    that a `ParticipationLedgerEntry` with `IngestionMethod = FileImport` and the matching
    `ImportBatchId` shows up in the member's participation history.
12. **Confirm immutability:** attempt `POST /api/import/batches/$BATCH_ID/dry-run` again — expect
    a `400 Bad Request` ("already been committed and its data is locked").

### 9.3 Applying the EF Core migration to a local database

The `ImportFoundation` migration has been generated and reviewed but is applied like any other
migration in this repo — via the `Bethuya.MigrationService` worker on `aspire start`, or manually:

```powershell
dotnet ef database update `
  --project src\Hackmum.Bethuya.Infrastructure `
  --startup-project src\Hackmum.Bethuya.Backend
```

### 9.4 Local artifact storage

`LocalDiskImportArtifactStore` (the local-dev `IImportArtifactStore` implementation) writes
uploaded files to `%TEMP%\bethuya-import-artifacts` by default, keyed by a random storage key
(not the original filename, to avoid collisions/overwrites) — useful to inspect if you need to
confirm the exact bytes an `ImportArtifact.Sha256Checksum` corresponds to. Production deployments
should swap in a blob-storage-backed implementation of the same interface.

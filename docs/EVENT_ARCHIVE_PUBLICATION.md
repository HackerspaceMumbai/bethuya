# Event Archive Publication

This document describes the durable event archive publication flow used by Bethuya, the safety rules around GitHub publishing, and how to validate the feature locally.

## Purpose

When an event is published or transitions to a public lifecycle state, Bethuya creates a durable archive projection for the event and pushes the generated artifact to the configured archive repository. The archive is not written synchronously during a user action; instead, the system records a durable outbox message and processes it asynchronously.

This pattern avoids data loss when the GitHub write fails or a transient outage occurs. It also makes retries and duplicate replays safe and deterministic.

## Architecture

### Components

- `EventLifecycleOrchestrator`
  - creates the archive payload and writes the durable outbox message
  - decides when a lifecycle transition should queue a publication
- `EventArchiveOutboxMessage`
  - durable record of a publication request
  - stores event id, destination, folder path, markdown, metadata, and retry metadata
- `EventArchiveOutboxProcessor`
  - background worker that claims pending outbox items and publishes them
  - tracks lease state, retry delays, and failure state
- `GitHubEventRepository`
  - writes `README.md` and `event.yml` to the configured GitHub archive repository
- `BethuyaDbContext`
  - persists the durable work queue and event state together in a transaction

### High-level flow

```text
Event lifecycle transition
        │
        ▼
EventLifecycleOrchestrator
        │
        ├─ creates deterministic archive folder path
        ├─ renders README + metadata payload
        ├─ inserts EventArchiveOutboxMessage
        └─ saves in same DB transaction
        │
        ▼
Outbox table (durable queue)
        │
        ▼
EventArchiveOutboxProcessor
        │
        ├─ claims next eligible message
        ├─ prevents stale/older work from blocking newer work
        ├─ retries with exponential backoff
        └─ marks success/failure in the database
        │
        ▼
GitHubEventRepository
        │
        ├─ validates token is present
        ├─ upserts README.md and event.yml
        └─ records folder URL back on the event
```

## Archive path and payload rules

The archive projection uses a deterministic path derived from the event start time in Asia/Kolkata and the event identifier.

```csharp
var localStart = ToAsiaKolkata(evt.StartDate);
var folderSlug = $"{localStart:yyyy-MM-dd}-{slug}-{evt.Id:N}";
return $"events/{localStart:yyyy}/{folderSlug}";
```

This avoids stale or conflicting archive folders when titles change or multiple events share similar metadata. The folder path is persisted on the event so the same correct archive location is reused for retries and later replays.

The publication payload includes:

- a README generated from event state
- a YAML metadata file for the archive record
- an idempotency key based on the effective payload
- the archive folder path and configured destination

The design intentionally makes archive writes idempotent enough to tolerate retries without writing duplicate or conflicting content.

## Data model

### Event

The event aggregate tracks lifecycle and archive metadata, including:

- lifecycle state (`Draft`, `Published`, `Completed`, `Archived`, etc.)
- start date/time and timezone-normalized dates
- archive folder path
- repository URL used for the public archive record

### EventArchiveOutboxMessage

The durable outbox message stores the publication request. Key fields include:

- `Id` — unique Vogen-backed identifier
- `EventId` — event identifier
- `Destination` — intended archive destination identifier
- `FolderPath` — repository-relative path for the event artifacts
- `ReadmeMarkdown` — rendered README content
- `MetadataJson` — serialized YAML/metadata payload
- `IdempotencyKey` — stable key used to prevent duplicate publication writes
- `AvailableAt` — earliest time the processor may attempt the message
- `AttemptCount` — total processing attempts
- `LockedUntil` — current lease expiration
- `ClaimToken` — unique ownership token for the current lease
- `ProcessedAt` — success timestamp
- `LastError` — last error message for diagnostics

## Safety rules for GitHub publication

This feature is intentionally conservative.

### 1. No PAT is assumed to exist

The GitHub repository layer validates credentials at runtime and throws if neither configuration nor environment variables supply a token:

```csharp
var token = settings.Token ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException(
        "GitHubEvents:Token or GITHUB_TOKEN must be configured to publish event artifacts.");
}
```

This is a critical safety guardrail: the app does not silently publish to GitHub when the PAT is missing or invalid.

### 2. Destination must be explicit

The system does not assume a "test event" is safe to publish to the real archive repo. A test event is not treated as automatically safe just because it is marked as such or because the repository defaults to `events`.

Real publication still requires the app to be configured with the intended repository and branch and to validate that the target repository is the deliberate archive destination. The system should never infer safety from generic metadata like `IsTestEvent` or from a default fallback.

### 3. Retry and stale-message safety

The outbox processor uses a lease mechanism and rejects processing stale claims. It also prevents an older failed message from permanently blocking newer work for the same event. This matters because archived events can be re-projected or retried while newer state exists.

The processor also treats duplicate insert races as safe idempotent outcomes rather than hard failures when the database uniqueness check conflicts.

## Workflow details

### Lifecycle trigger

Publication is queued when an event enters a public lifecycle state or an archive-related projection is required. Typical triggers:

- `PublishAsync`
- `CompleteAsync`
- `ArchiveAsync`
- `TransitionAsync` when the target state is public
- schedule-alteration flows that require a fresh projection

### Processing semantics

The worker chooses the oldest eligible message that:

- is not already processed
- is available by `AvailableAt`
- is not locked by an active lease
- is not blocked by a newer unprocessed projection for the same event

It records a claim token, increments attempts, and performs the GitHub publish. On success, it marks the message processed and stores the resulting GitHub folder URL on the event. On failure, it backs off and retries according to a bounded exponential delay schedule.

After a threshold of repeated failures, the worker marks the message terminal if a newer projection exists; this prevents stale failures from stalling newer archive work.

## Local and manual testing

### Required configuration

For publish flows, make sure the app configuration includes a valid GitHub archive target, for example:

```json
{
  "GitHubEvents": {
    "Owner": "HackerspaceMumbai",
    "Repository": "events",
    "Branch": "main",
    "Token": "<fine-grained PAT>"
  }
}
```

For local development:

- prefer environment variables over hardcoded secrets
- verify the token is scoped only to the intended archive repository and branch
- do not assume the token exists unless a runtime validation step confirms it

### Validate the outbox behavior

Run the focused test project:

```bash
dotnet test tests/Hackmum.Bethuya.Tests --filter EventArchiveOutboxProcessor
```

This covers the retry, stale-claim, and newer-message safety scenarios.

### Validate build integrity

```bash
dotnet build
dotnet test
```

The full test run is the baseline for release readiness, but the outbox-specific suite is the fastest validation loop when changing archive sync logic.

### Manual smoke test

1. Start the local app or Aspire environment.
2. Create or load an event that can reach a public lifecycle state.
3. Trigger the publish or archive flow.
4. Verify that:
   - the outbox record is created in the database
   - the background processor picks it up
   - the generated README and metadata are written to the expected folder path
   - the event receives a GitHub folder URL only after successful publication
5. Retry the flow with a missing or invalid token to confirm the app fails safely instead of silently publishing.

## Operational notes

- The archive worker currently uses a polling loop; it is intentionally simple and durable rather than event-driven.
- The path and payload are deterministic, which allows safe retries and easier debugging.
- Any new archive destination or repo safety rule should be tested with the outbox processor and the GitHub repo integration tests before production rollout.

## Release checklist

Before shipping a change to the archive pipeline, confirm:

- [ ] token presence is validated at runtime
- [ ] repo/branch target is intentional and not inferred from event metadata alone
- [ ] archive folder path is deterministic and persisted
- [ ] stale failure does not block newer same-event projections
- [ ] retries remain idempotent and safe under duplicate writes
- [ ] tests cover the outbox failure/retry path

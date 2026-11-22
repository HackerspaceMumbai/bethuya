# Event Archive Publication

Bethuya prepares archive changes only after an organizer explicitly reviews a preview and confirms publication. Confirmed work uses the existing durable outbox and opens or updates a Draft pull request in `HackerspaceMumbai/events`; Bethuya never merges it.

## Flow

```text
Event lifecycle change
  -> operational event state saved; archive projection is not queued automatically
  -> organizer requests an archive preview
  -> organizer reviews the diff and confirms that preview
  -> confirmed projection is saved to the durable outbox
  -> outbox worker creates/updates a deterministic publication branch and Draft PR
  -> human reviews and merges the PR
  -> organizer explicitly refreshes status when needed
```

`EventArchiveOutboxProcessor` retains its durable leases, heartbeat, ordering, retries, and stale-claim checks. Only a confirmed outbox message is eligible for publication. The preview and adoption-preview operations are read-only; only confirmation enqueues publication, and no background process polls pull-request status.

## Organizer endpoints

Event lifecycle and archive operations require the existing `RequireOrganizer` policy. Audit actors are taken from the authenticated identity, not from caller-supplied actor fields.

- `GET /api/events/{id}/archive/preview` reads the current archive entry and returns the candidate diff and a preview token.
- `POST /api/events/{id}/archive/confirm` queues the reviewed candidate only if the remote entry still matches that preview.
- `POST /api/events/{id}/archive/adoption/preview` reads the exact repository-relative path supplied by the organizer and returns its identity validation and candidate diff.
- `POST /api/events/{id}/archive/adoption/confirm` adopts the reviewed path and records the actor, timestamp, and source file SHAs. Adoption itself does not enqueue a write.
- `POST /api/events/{id}/archive/status/refresh` queries GitHub on demand and records whether the PR remains open, was closed, or was merged.

Preview tokens are derived from the exact candidate content and source file SHAs. A changed archive entry invalidates the token and requires another preview. Repeated confirmation of the same projection is idempotent.

Confirmation persists the token with the outbox projection. Before any GitHub write, the worker reads both archive files at one target commit and verifies the confirmed token. A mismatch (or newly ambiguous content) ends that attempt as failed without automatic retry; resolve any ambiguous structure, request a new preview, and confirm again. Reconfirmation reactivates the failed item without changing its projection idempotency key. If the older confirmation is still queued or processing, a different token is rejected explicitly: wait for that attempt to finish before reconfirming. Unchanged-target retries, including a retry after branch creation but before successful PR creation, reuse matching branch files.

The `EventArchiveConfirmedPreviewSnapshot` migration adds a nullable token column for compatibility with existing rows. Older confirmed requests without a token fail safely and require a fresh preview/confirmation; no token is inferred or backfilled. Apply the migration before running the updated worker, and stop older worker binaries before enabling the new safeguards.

## Archive ownership and conflicts

Bethuya manages these top-level `event.yml` values: `title`, `date`, `status`, and `description`; under `bethuya`, it manages `eventId` and `projectionVersion`. Other metadata is preserved from the archive. Bethuya's README content is enclosed by `<!-- bethuya:managed:start -->` and `<!-- bethuya:managed:end -->`; content outside the markers is preserved. If the markers or managed YAML structure are ambiguous, publication stops for human resolution instead of overwriting the entry.

Adoption requires an exact repository-relative path under `events/` and an `event.yml` whose title/date match the event (and whose Bethuya `eventId`, when present, matches exactly). A README is optional; when absent, the candidate preview shows the README Bethuya would add. Duplicate/ambiguous identity, a path already claimed by another event, or a stale preview is rejected. The adopted path is persisted and reused; title-based discovery is not performed.

The established archive path must have the date prefix matching the event's Asia/Kolkata start date. If an event's date changes and that path no longer matches, preview/confirmation refuses publication. Archive folder moves are not supported; an organizer must resolve the conflict without silently moving or publishing to the invalid path.

## Draft PR publication

The worker writes both files as one Git tree and commit on a deterministic, stable-per-event branch named from the event ID. It creates a Draft PR if none is open for that branch, otherwise updates the existing PR's branch as projections change. Archive reads are pinned to the target branch commit used as the new commit's parent; if the target advances later, the PR may need human conflict resolution rather than silently overwriting newer content. This prevents a partially updated README/metadata pair from being treated as a successful publication. The PR is never merged automatically.

The existing idempotency key calculation remains based on the event ID and serialized projection payload (folder path, README, and metadata); it is not changed by PR publication. Publication state is persisted on the event, including queued/Draft-PR-pending, published (merged), closed, and failed states as applicable. `GitHubFolderUrl` is set only after an explicit refresh observes a merge.

## GitHub configuration and permissions

The existing token configuration is unchanged: `GitHubEvents:Token` or `GITHUB_TOKEN` is used with the configured repository and target branch. Local development and automated tests must use a fake/test GitHub adapter; do not configure tests to write to the live events repository.

The token needs **Contents: read and write** and **Pull requests: read and write** access on the archive repository. No GitHub App migration, OAuth flow, webhook, or background PR polling is required.

## Validation

Run the test suite with:

```powershell
dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj
```

Archive tests use fake GitHub HTTP responses. They cover read-only previews, confirmation/outbox behavior, deterministic branches, Draft PR creation and reuse, retries, preservation of archive-owned content, adoption validation, status refresh, date/path mismatch refusal, authorization, and outbox lease/retry behavior. No live GitHub write is part of test execution.

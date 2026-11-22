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

The same policy protects `POST /api/events`, `PUT /api/events/{id}`, `PUT /api/events/{id}/fairness-targets`, `DELETE /api/events/{id}`, and `POST /api/events/{id}/sessionize/import`. Organizer and Admin callers remain authorized; anonymous callers receive 401 and other roles receive 403. Existing public event GET endpoints are unchanged when the optional authenticated fallback policy is disabled.

Event creation retains the `CreatedBy` request field for client compatibility but ignores its value: the persisted creator comes from the authenticated `sub` or NameIdentifier claim. Missing or blank stable subjects receive 401; a display name alone is not a subject. Authorized event updates retain their existing semantics and do not add lifecycle or archive side effects.

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

The conservative YAML reader supports plain-key block mappings, including consistently indented provenance children with more than two spaces. Quoted/complex mapping keys, anchored/tagged managed values, and inline/alias `bethuya` mappings are refused rather than guessed or rewritten. Existing community block-scalar paragraph breaks are preserved when adding provenance. Adoption uses the same structural reader and reads only the direct `bethuya.eventId`, not unrelated nested identities. README markers must be standalone comments outside fenced or indented examples; ambiguous literal markers are refused.

Adoption requires an exact repository-relative path under `events/` and an `event.yml` whose title/date match the event (and whose Bethuya `eventId`, when present, matches exactly). A README is optional; when absent, the candidate preview shows the README Bethuya would add. Duplicate/ambiguous identity, a path already claimed by another event, or a stale preview is rejected. The adopted path is persisted and reused; title-based discovery is not performed.

The established archive path must have the date prefix matching the event's Asia/Kolkata start date. If an event's date changes and that path no longer matches, preview/confirmation refuses publication. Archive folder moves are not supported; an organizer must resolve the conflict without silently moving or publishing to the invalid path.

## Draft PR publication

The worker writes both files as one Git tree and commit on a deterministic, stable-per-event branch named from the event ID. It creates a Draft PR if none is open for that branch, otherwise updates the existing Draft PR's branch as projections change. Archive reads are pinned to the target branch commit; the initial publication commit is parented to that commit. Existing-branch updates instead parent to the observed publication branch ref SHA and use that commit's tree as the base, with `force: false`: GitHub refuses a concurrent non-fast-forward update rather than losing intervening commits. If the target advances later, the PR may need human conflict resolution. Both archive files change coherently, never as a partially updated pair. The PR is never merged automatically.

Generated commits record their whole-tree SHA in a `Bethuya-Archive-Tree` message trailer. Existing heads must match that recorded tree before publication: edits anywhere in the tree, including community README content or unrelated files, stop publication for human resolution instead of silently overwriting an unconfirmed diff. Copying an old generated message onto a changed tree does not bypass this check. This is change-detection provenance, not an authentication signature. Legacy branches without the trailer are also refused; preserve their contents through human review and complete the proposal or resolve publication manually before retiring the branch and reconfirming. Bethuya never deletes, resets, or force-updates the branch.

Publication reads the latest PR for its deterministic head, including closed proposals. A proposal closed without merge stops retries before GitHub writes, even if the branch was deleted; human review must explicitly reopen it as Draft or resolve publication manually. After a merge, the retained merged branch must be safely retired by an organizer before another confirmed projection starts from the current target snapshot. New commits also record a `Bethuya-Archive-Preview` trailer immediately before the tree trailer: if creating the next PR fails after creating its branch, retries may reuse that branch only when both its confirmed-preview token and actual whole-tree SHA match the recorded candidate. Old retained heads and human-edited trees remain refused. Older branches without this preview provenance cannot be inferred to belong to a new post-merge attempt and require human resolution. Bethuya never silently recreates a rejected proposal or resets a retained merged branch.

An existing open PR must still be Draft before preparation and is checked again immediately before ref mutation; ready or newly closed PRs are refused. GitHub's PR-state and Git-ref APIs are separate operations, so the Draft check cannot be atomic with the ref update if someone changes PR state during that final request window. Ref concurrency remains protected by server-enforced fast-forward semantics, not by a pre-write GET followed by force PATCH.

The existing idempotency key calculation remains based on the event ID and serialized projection payload (folder path, README, and metadata); it is not changed by PR publication. Publication state is persisted on the event, including queued/Draft-PR-pending, published (merged), closed, and failed states as applicable. `GitHubFolderUrl` is set only after an explicit refresh observes a merge.

Status refresh uses an atomic database compare-and-set on the observed PR number, projection key, status, and archive path. If a newer confirmation arrived while GitHub responded, the old status is not saved and the organizer is asked to refresh the current publication. Confirmation retains the last known PR reference for organizer discovery. While the new request is Pending or Failed, refresh returns that prior PR's status read-only: its merge does not mark the new projection Published. Successful outbox completion replaces the reference. No new schema or background polling is needed.

Reconfirmation reactivates completed historical projections when they are no longer current (including A -> B -> A), or when their confirmed snapshot changed. The projection key and unique row remain unchanged; reactivation atomically checks the observed claim/snapshot and resets queue creation time to preserve confirmation ordering. Concurrent reactivation or a different winning queued token is refused rather than replacing an active claim. Outbox completion and terminal failure condition event updates on the projection key in SQL, including execution-strategy retries.

Before applying the unique archive-path migration, check existing non-null `ArchiveFolderPath` values for duplicates and resolve ownership manually. The unique index deliberately refuses conflicting legacy claims; the migration does not erase or reassign them automatically.

## GitHub configuration and permissions

The existing token configuration is unchanged: `GitHubEvents:Token` or `GITHUB_TOKEN` is used with the configured repository and target branch. Local development and automated tests must use a fake/test GitHub adapter; do not configure tests to write to the live events repository.

The token needs **Contents: read and write** and **Pull requests: read and write** access on the archive repository. No GitHub App migration, OAuth flow, webhook, or background PR polling is required.

## Validation

Run the test suite with:

```powershell
dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj
```

Archive tests use fake GitHub HTTP responses. They cover read-only previews, confirmation/outbox behavior, deterministic branches, Draft PR creation and reuse, retries, preservation of archive-owned content, adoption validation, status refresh, date/path mismatch refusal, authorization, and outbox lease/retry behavior. No live GitHub write is part of test execution.

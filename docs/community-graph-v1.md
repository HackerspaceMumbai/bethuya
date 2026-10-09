# Community Graph V1

`/community-graph` (also `/graph`) is an authenticated InteractiveServer exploration surface. The graph navigates; the Community Passport Preview contains relationship explanations, entity details, member-owned ledger records and suggested pathways. Selecting a node or a relationship updates this preview. Search, discovery modes and entity/relation filters operate on the fetched snapshot. Refresh reloads privacy and participation state.

## Data contract

`GET /api/community/graph` is a read-only, authenticated Scalar-visible endpoint. The viewer must have a CommunityMember record; otherwise the response is empty. Only the viewer's community is queried. Other members must be discoverable and have Public or CommunityOnly visibility. Organizer-sharing consent is independent and does not gate community discovery. OrganizerOnly profiles are excluded, including for organizer callers. Responses use `Cache-Control: no-store` and omit emails, external identity keys, provenance keys and correlation tokens.

Graph relationships are never saved or manually created. They are projected from non-future ledger entries with `IsVerified=true`, readable evidence and an eligible activity. Legacy entries default to false. Verification is asserted by the existing connector-ingestion policy, not by a graph editor. A connector must verify source participation before setting this flag. Existing idempotency rules still apply: duplicate provenance is ignored, not rewritten. There is no automatic backfill of verification.

Trusted ingestion through `/api/community/passport/participation` now accepts `isVerified`, `targetKind`, `targetKey` and `targetLabel`. Target fields must be provided together. External target keys are scoped by connector; internal EventId and canonical member IDs use their existing identity scope. Do not put sensitive fields into evidence text or display labels; these fields are the member-visible explanation.

| Activity | Target | Relationship |
| --- | --- | --- |
| Attended | Internal EventId or Event target | Attended |
| Spoke | Internal EventId or Event target | Spoke at |
| Volunteered | Internal EventId or Event target | Volunteered |
| ContributedProject | Project | Contributes to |
| JoinedCommunity | Community, or member community slug | Community member |
| JoinedChapter | Chapter | Chapter member |
| Mentored | Member, using canonical CommunityMemberId | Mentors |
| UsedTechnology | Technology | Uses technology |

Registered, Waitlisted, SubmittedSession, MessageEngaged and Other do not establish graph participation. Shared attendance uses matching event identities and retains both members' records. It does not imply mentorship. Passport record counts filter evidence by its owning member.

## Suggestions and health

Two distinct speaking events suggest Workshop Speaker; two distinct contribution records suggest Project Reviewer; mentorship of two distinct members suggests Mentorship Circle Lead. Every suggestion includes the supporting ledger records and requires human review. These are pathways, not claims of an available vacancy. There are no match percentages, hidden weights or automatic selection decisions. Suggested links are dashed; verified participation links are solid.

Health overlays show the observed record/member counts and unique community joins over two adjacent 30-day periods. They describe the bounded snapshot and do not claim whole-community health. Mentor demand is not fabricated in the absence of recorded demand.

The server bounds a snapshot at 80 visible members, 400 recent verified records and 200 shared-attendance pairs. The browser draws at most 48 nodes, shows a limitation notice, and allows filtering within that snapshot. This is a V1 bounded exploration view, not an exhaustive export. Privacy changes require refresh; there is no live push subscription.

## Migration and validation

Apply the `CommunityGraphVerifiedTargets` EF migration through the existing MigrationService deployment path. It adds the verification flag and three nullable target columns. No relationships or scores are persisted.

For local visual validation, start Aspire, then choose **backend → Commands → Seed Community Graph** in the Aspire dashboard. This provisions the canonical development personas, then seeds the fictional graph records. The command is safe to repeat and preserves existing records. Refresh Community Graph afterward.

The underlying `/api/dev/community-graph/seed` route requires an organizer. The dashboard command, route and seeder are Development-only and never invoked automatically. All graph fixture members and evidence are fictional and use the `graph-demo-v1:` namespace.

Run focused TUnit checks:

```powershell
dotnet run --project tests/Hackmum.Bethuya.Tests -- --treenode-filter '/*/*/CommunityGraph*/*'
```

Run Playwright for .NET with the active Aspire URLs:

```powershell
$env:BETHUYA_BASE_URL = '<web HTTPS URL>'
$env:BETHUYA_BACKEND_URL = '<backend URL>'
dotnet test --project tests/Hackmum.Bethuya.E2E --filter 'FullyQualifiedName~CommunityGraphFlowTests'
```

The browser test verifies the real Web → Refit → Backend → PostgreSQL path, selection, typed search, keyboard relationship inspection, health toggle, mobile overflow and screenshot output under `artifacts/community-graph`.

## Accepted authorization decisions (2026-10-07)

Community Passport creation establishes community membership and grants access to that community's graph. No separate approval is required to view community relationships. Elevated privileges remain role-based, and graph visibility continues to respect member privacy settings.

Identity resolution is intentionally global across the Participation Ledger. This supports portable Community Passports, cross-community participation history, opportunity matching, mentorship discovery and relationship graph generation. Governance controls should enforce participation source authority, provenance, auditing and verification rather than restricting identity resolution boundaries. Global identity resolution does not itself remove the graph's existing read-visibility checks.

Both authorization assumptions raised in the security review are accepted as intended behavior. The duplicated relationship evidence finding is resolved by snapshot-level evidence records referenced through EvidenceIds on relationships and opportunities. No supporting proof is dropped. A dense 400-record regression enforces a 2 MB serialized payload budget and JSON round-trip integrity.

Verified attendance and volunteering require an internal EventId or complete Event target; unverified legacy inputs retain their previous behavior. The development seeder clears tracked state before each retry attempt.

After integrating the granular Passport privacy controls, graph membership also requires relationship insights. Private and organizer-only peer profiles are excluded. Shared-attendance discovery requires collaborator opt-in on both members; opportunity suggestions require opportunity opt-in plus their speaker, collaborator or mentorship category preference. Both project contribution activity names are recognized across the graph and Passport.

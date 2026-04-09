# Squad Decisions

## Evidence & Verification Policy (Hard Rules)

This file is the canonical memory for Bethuya’s Squad. Decisions recorded here must be durable, inspectable, and actionable. [1](https://www.youtube.com/watch?v=eyQx9wTUQgg)[2](https://burkeholland.github.io/anvil/)

### When Evidence is Required

If `.squad/routing.md` requires Burke’s Anvil, the related decision entry MUST include:

- commit hash
- Anvil evidence summary (build/tests/lint + reviewer verdicts)
- any regressions found and how they were resolved
- rollback command (if provided) [3](https://github.com/HackerspaceMumbai/bethuya/tree/main/.github/workflows)[4](https://github.com/HackerspaceMumbai/bethuya/blob/main/.claude/skills/aspire/SKILL.md)

### Validity Rule

Any Anvil-required decision entry without evidence is considered **INCOMPLETE** and must not be treated as approved/merge-ready.

### Recording Rule (Scribe)

Scribe (Session Logger) ensures evidence references are captured in this file or linked from it, so future sessions can reason from proof, not memory. [1](https://www.youtube.com/watch?v=eyQx9wTUQgg)[2](https://burkeholland.github.io/anvil/)

## Active Decisions

### 2026-04-09 — Rename aspire-secrets → aspire-aca-secrets skill, fix inverted guidance

**Author:** Tank (Backend Dev)

**Context:** Copilot skill guidance was inverted, directing agents to ADD `secret: true` to AddParameter calls. This parameter causes "Unsupported value type System.Boolean" failures in Azure Container Apps deployment. Root cause required skill rename and guidance correction.

**Decision:**
- Renamed both skills from `aspire-secrets` → `aspire-aca-secrets`
- Scoped skill descriptions to Azure Container Apps (not generic Aspire)
- Fixed copilot skill guidance: now directs to REMOVE `secret: true` and use AddSecret pattern
- Updated `.squad/` metadata and routing references

**Files:** `copilot/skills/aspire-aca-secrets/`, `.squad/skills/`

**Impact:** Agents now receive correct guidance; deployment failures eliminated; skills scoped to specific platform context.

---

### Add Keycloak Container to Aspire AppHost (2026-03-21)

**Author:** Tank (Backend Dev)

**Context:** Auth system supports Entra/Auth0/Keycloak via config, but no local OIDC testing on `main` without external IdP.

**Decision:** Added `Aspire.Hosting.Keycloak` (preview 13.1.2-preview.1.26125.13) to AppHost. `dotnet run --project AppHost/AppHost` now spins up Keycloak on port 8080.

**Trade-offs:**

- Preview package (may need version bumps as Aspire 13.x stabilizes)
- Port 8080 reserved (adjustable in AppHost.cs)
- Realm setup is manual (bethuya realm + client creation in admin)

**Impact:** Auth docs updated in README; SECURITY.md refreshed; all tests pass.

---

### 2026-04-08 — Integration Test Infrastructure (TUnit + Aspire.Hosting.Testing)

**Author:** Switch (via Augustine Correa)

**Decision:** Bethuya.IntegrationTests uses TUnit `IAsyncInitializer` + `ClassDataSource<BethuyaAppFixture>(Shared = SharedType.PerTestSession)` as the xUnit `ICollectionFixture` equivalent. Respawn uses `DbAdapter.SqlServer`. No Backend.csproj reference — contract types duplicated in `Contracts/` for BP6 anti-regression.

**Rationale:** Aspire testing BP1/BP2/BP3/BP5/BP6 compliance; TUnit-native pattern (not xUnit).

**Files:** `Directory.Packages.props` (Aspire.Hosting.Testing 13.2.1, Respawn 6.2.1)

---

### 2026-04-08 — Remove secret: true from AddParameter for Azure Container Apps

**Author:** Tank (Backend Dev)

**Context:** `azd`/aspire deploy fails with "Unsupported value type System.Boolean" when `AddParameter` uses `secret: true`. The boolean flag is not supported by Azure Container Apps deployment pipeline.

**Decision:** Remove `secret: true` from all `AddParameter` calls in AppHost.cs. For production secrets, use the Key Vault `AddSecret` pattern instead.

**Files:** `AppHost/AppHost/AppHost.cs`

---

### 2026-03-31 — Performance Budget Directive

**Author:** Augustine Correa (via Copilot)

**Directive:** Ensure Playwright tests pass within project performance budgets:
- Hot path p99 < 180ms @ 2,500 RPS
- 0 B hot-path allocations
- > 90% cache hit rate

---

### E2E Test Selector Standards (2026-03-21)

**Author:** Switch (Tester)

**Decision:** All E2E Playwright tests MUST use `data-test` attributes as selectors instead of role-based or text-based selectors.

**Rationale:**
- Role-based selectors break when button text changes
- `data-test` provides stability decoupled from UI text and styling
- Explicit test contract between frontend and tests

**Standard Selectors:**
- `[data-test='new-event-btn']` — Opens create event dialog
- `[data-test='create-event-submit']` — Submits create form
- `[data-test='event-card']` — Individual event cards in list
- `[data-test='notification']` — Success/error notifications

**Implementation:** Trinity adds `data-test` attributes to interactive elements; Switch uses `Page.Locator("[data-test='selector']")` exclusively.

---

### Add Cloudinary Image Upload for Event Cover Pics (2026-03-21)

**Author:** Tank (Backend Dev)

**Decision:** Integrated **CloudinaryDotNet 1.26.2** as the image upload provider behind an `IImageUploadService` abstraction in Core. Implementation lives in Infrastructure (`CloudinaryImageUploadService`), configured via `CloudinaryOptions`.

**Changes:**
- `CoverImageUrl` (nullable, max 2048 chars) added to Event model, API contracts, EF config, Refit DTOs
- New `POST /api/images/upload` endpoint with validation: 5 MB max, JPEG/PNG/WebP/GIF only
- Images stored in `bethuya/events` folder; secure URL returned
- DI: `IImageUploadService` → `CloudinaryImageUploadService` (singleton)

**Trade-offs:** Vendor coupling mitigated by `IImageUploadService` abstraction (swap to Azure Blob or S3 by implementing interface).

**Impact:** All projects build cleanly (0 warnings, 0 errors); existing tests updated for new `CoverImageUrl` parameter.

---

### Event Endpoint DTO Pattern (2026-03-21)

**Author:** Tank (Backend Dev)

**Context:** Backend event creation endpoint returned raw `Event` domain entities with navigation properties, causing serialization cycles and type mismatches with frontend expectations (enums vs strings).

**Decision:** Added `EventResponse` DTO that decouples domain from API contracts. All endpoints now return DTOs:
- GET `/api/events` → `List<EventResponse>`
- GET `/api/events/{id}` → `EventResponse`
- POST `/api/events` → `EventResponse`
- PUT `/api/events/{id}` → `EventResponse`

**Benefits:**
- ✅ Type safety: Frontend `EventDto` matches Backend `EventResponse`
- ✅ Enum consistency: Serialized as strings via `JsonStringEnumConverter`
- ✅ No serialization issues: DTOs have no navigation properties
- ✅ API stability: Domain changes don't break frontend

**Validation added:** Title (required, max 200 chars), Capacity (1-10,000), EndDate >= StartDate, CreatedBy (required).

---

### Notification Pattern for Dialog Components (2026-03-21)

**Author:** Trinity

**Decision:** Implement reusable notification pattern for dialog components:
1. Dialog emits notifications via `[Parameter] EventCallback<string> OnNotification`
2. Parent page renders `<Notification>` with `@bind-IsVisible`
3. Parent handler determines `AlertVariant` based on message content

**Implementation:** Applied to CreateEventDialog, Home.razor, Events.razor

**Impact:**
- ✅ Consistent UX across create flows
- ✅ Testable via `data-test="notification"`
- ✅ Pattern reusable for other dialogs (edit, delete, etc.)

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

## Standing policies

The following policy documents are canonical and must be followed by all agents
and contributors:

- Rendering policy (Web only): `.squad/policies/rendering-policy.md`
- Shared RCL boundaries: `.squad/policies/rcl-boundaries.md`
## Reusable “Decision Entry Template”

---

## YYYY-MM-DD — <Short Decision Title>

### Context

- **Why now:** <What triggered this decision? bug/feature/security/tech debt>
- **Scope:** <What areas/files/services are impacted?>
- **Constraints:** <Guardrails, privacy rules, performance targets, etc.>

### Decision

<What we decided to do, in 1–3 bullets.>

### Alternatives Considered

- <Option A> — <Why not?>
- <Option B> — <Why not?>

### Ownership & Roles

- **Lead gate:** Neo
- **Implementer(s):** <Trinity/Tank/@copilot?>
- **Tester verification:** Switch
- **Security review:** Morpheus (if applicable)
- **Recorder:** Scribe [2](https://burkeholland.github.io/anvil/)[1](https://www.youtube.com/watch?v=eyQx9wTUQgg)

### Verification (Required when Anvil is required by routing.md)

- **Anvil used:** <Yes/No>
- **Commit:** <hash>
- **Evidence summary:**
  - Build: <✅/❌> <command if known>
  - Tests: <✅/❌> <counts / key suites>
  - Lint/format: <✅/❌> <tool>
  - Reviewers (Anvil): <Model A verdict> / <Model B verdict> / <Model C verdict> [3](https://github.com/HackerspaceMumbai/bethuya/tree/main/.github/workflows)[4](https://github.com/HackerspaceMumbai/bethuya/blob/main/.claude/skills/aspire/SKILL.md)
- **Regressions:** <None / list>
- **Rollback:** <command> (if provided by Anvil) [3](https://github.com/HackerspaceMumbai/bethuya/tree/main/.github/workflows)[4](https://github.com/HackerspaceMumbai/bethuya/blob/main/.claude/skills/aspire/SKILL.md)

### Follow-ups

- [ ] <task 1>
- [ ] <task 2>

### Status

- **Approved by:** <Neo / Neo+Morpheus / etc>
- **Date approved:** YYYY-MM-DD

---

Examples of decisions recorded in this format

Example 1 — Standard “Anvil Required” entry

---
```markdown
## 2026-04-04 — Refactor Shared UI Component Placement

### Context

- **Why now:** Reduce duplication between Web and MAUI and enforce shared-component rule.
- **Scope:** Bethuya.Shared + UI components used across Web and Hybrid.
- **Constraints:** Cross-platform UI must live in `Bethuya.Shared`.

### Decision

- Move shared UI component(s) into `Bethuya.Shared`.
- Update imports/references in Web and Hybrid projects.
- Add/adjust tests for component usage.

### Alternatives Considered

- Keep components duplicated in Web and Hybrid — rejected (drift risk).
- Create platform-specific wrappers — rejected (extra maintenance).

### Ownership & Roles

- **Lead gate:** Neo
- **Implementer(s):** Trinity
- **Tester verification:** Switch
- **Security review:** N/A
- **Recorder:** Scribe [2](https://burkeholland.github.io/anvil/)[1](https://www.youtube.com/watch?v=eyQx9wTUQgg)

### Verification (Required when Anvil is required by routing.md)

- **Anvil used:** Yes
- **Commit:** <commit-hash>
- **Evidence summary:**
  - Build: ✅ Passed
  - Tests: ✅ <n> passed, 0 failed
  - Lint/format: ✅ Clean
  - Reviewers (Anvil): GPT ✅ / Claude ✅ / Gemini ✅ [3](https://github.com/HackerspaceMumbai/bethuya/tree/main/.github/workflows)[4](https://github.com/HackerspaceMumbai/bethuya/blob/main/.claude/skills/aspire/SKILL.md)
- **Regressions:** None
- **Rollback:** <rollback-command-if-provided> [3](https://github.com/HackerspaceMumbai/bethuya/tree/main/.github/workflows)[4](https://github.com/HackerspaceMumbai/bethuya/blob/main/.claude/skills/aspire/SKILL.md)

### Follow-ups

- [ ] Add Playwright screenshot proof for the updated UI states.

### Status

- **Approved by:** Neo
- **Date approved:** 2026-04-04
```

---

## Example 2 — Security/Auth decision entry (stronger gate wording)

---

```markdown

## 2026-04-04 — Harden Auth Boundary for Provider-Pluggable Identity

### Context

- **Why now:** Security hardening of authentication/authorization boundaries.
- **Scope:** Auth provider routing and related access-control paths.
- **Constraints:** No auth bypass regressions; protect sensitive data boundaries. [5](https://github.com/bradygaster/squad/blob/dev/.squad/agents/sims/charter.md)

### Decision

- Apply changes to auth boundary logic as scoped.
- Require Morpheus security review + Switch verification.
- Require Anvil evidence bundle before approval.

### Alternatives Considered

- Defer hardening until after feature work — rejected (risk accumulation).
- Hotfix without test coverage — rejected (unproven correctness).

### Ownership & Roles

- **Lead gate:** Neo
- **Implementer(s):** Tank (and/or Trinity if UI auth involved)
- **Tester verification:** Switch
- **Security review:** Morpheus
- **Recorder:** Scribe [2](https://burkeholland.github.io/anvil/)[1](https://www.youtube.com/watch?v=eyQx9wTUQgg)

### Verification (Required when Anvil is required by routing.md)

- **Anvil used:** Yes
- **Commit:** <commit-hash>
- **Evidence summary:**
  - Build: ✅ Passed
  - Tests: ✅ <n> passed, 0 failed (include auth-related suites)
  - Lint/format: ✅ Clean
  - Reviewers (Anvil): GPT ✅ / Claude ⚠️ <note> / Gemini ✅ [3](https://github.com/HackerspaceMumbai/bethuya/tree/main/.github/workflows)[4](https://github.com/HackerspaceMumbai/bethuya/blob/main/.claude/skills/aspire/SKILL.md)
- **Regressions:** <None / list>
- **Rollback:** <rollback-command-if-provided> [3](https://github.com/HackerspaceMumbai/bethuya/tree/main/.github/workflows)[4](https://github.com/HackerspaceMumbai/bethuya/blob/main/.claude/skills/aspire/SKILL.md)

### Follow-ups

- [ ] Add/strengthen negative tests for auth bypass attempts.
- [ ] Document any new security assumptions in SECURITY.md.

### Status

- **Approved by:** Neo + Morpheus
- **Date approved:** 2026-04-04
```

---

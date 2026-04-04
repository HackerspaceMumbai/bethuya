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

### Add Keycloak Container to Aspire AppHost (2026-03-21)

**Author:** Tank (Backend Dev)

**Context:** Auth system supports Entra/Auth0/Keycloak via config, but no local OIDC testing on `main` without external IdP.

**Decision:** Added `Aspire.Hosting.Keycloak` (preview 13.1.2-preview.1.26125.13) to AppHost. `dotnet run --project AppHost/AppHost` now spins up Keycloak on port 8080.

**Trade-offs:**

- Preview package (may need version bumps as Aspire 13.x stabilizes)
- Port 8080 reserved (adjustable in AppHost.cs)
- Realm setup is manual (bethuya realm + client creation in admin)

**Impact:** Auth docs updated in README; SECURITY.md refreshed; all tests pass.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

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

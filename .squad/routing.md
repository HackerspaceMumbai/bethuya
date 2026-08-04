# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture, decomposition, and code review | Neo | System design, scope trade-offs, reviewer gates, cross-cutting changes |
| Blazor UI, render modes, and UX flows | Trinity | Razor components, page flows, UI interactions, InteractiveServer-sensitive pages |
| APIs, data access, and service wiring | Tank | ASP.NET Core, DI, data flow, provider abstractions, backend integration |
| Testing and verification | Switch | TUnit, Playwright, edge cases, regression checks |
| Authentication, authorization, and privacy-sensitive work | Morpheus | Auth providers, claims/roles, policy design, security review |
| Code review | Neo | Review PRs, check quality, enforce reviewer gates |
| Testing | Switch | Write tests, find edge cases, verify fixes |
| Scope & priorities | Neo | What to build next, trade-offs, decisions |
| Async issue work (bugs, tests, small features) | @copilot 🤖 | Well-defined tasks matching capability profile |
| Session logging | Scribe | Automatic - never needs routing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, evaluate @copilot fit, assign `squad:{member}` label | Lead |
| `squad:{name}` | Pick up issue and complete the work | Named member |
| `squad:copilot` | Assign to @copilot for autonomous work (if enabled) | @copilot 🤖 |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it - analyzing content, evaluating @copilot's capability profile, assigning the right `squad:{member}` label, and commenting with triage notes.
2. **@copilot evaluation:** The Lead checks if the issue matches @copilot's capability profile (🟢 good fit / 🟡 needs review / 🔴 not suitable). If it's a good fit, the Lead may route to `squad:copilot` instead of a squad member.
3. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
4. When `squad:copilot` is applied and auto-assign is enabled, `@copilot` is assigned on the issue and picks it up autonomously.
5. Members can reassign by removing their label and adding another member's label.
6. The `squad` label is the "inbox" - untriaged issues waiting for Lead review.

### Lead Triage Guidance for @copilot

When triaging, the Lead should ask:

1. **Is this well-defined?** Clear title, reproduction steps or acceptance criteria, bounded scope → likely 🟢
2. **Does it follow existing patterns?** Adding a test, fixing a known bug, updating a dependency → likely 🟢
3. **Does it need design judgment?** Architecture, API design, UX decisions → likely 🔴
4. **Is it security-sensitive?** Auth, encryption, access control → always 🔴
5. **Is it medium complexity with specs?** Feature with clear requirements, refactoring with tests → likely 🟡

## Rules

1. **Eager by default** - spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** - when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
8. **@copilot routing** - when evaluating issues, check @copilot's capability profile in `team.md`. Route 🟢 good-fit tasks to `squad:copilot`. Flag 🟡 needs-review tasks for PR review. Keep 🔴 not-suitable tasks with squad members.
9. **AppHost modifications** - if a task involves AppHost modifications (e.g., editing `.AppHost` project files, changing service orchestration, updating resource declarations), Neo takes exclusive lock. All other agents must pause until Neo confirms resource availability via the Aspire MCP list_resources tool.

## Evidence Policy - Bethuya

A lightweight, evidence-first verification practice: risky changes must be backed by a build/test summary and a linked commit hash, not just a claim of "done."

### 1) When Evidence is REQUIRED (Hard Rule)

A build/test evidence summary is REQUIRED for any of the following:

- Authentication / authorization / roles / permissions changes
- Any security-sensitive boundary change (PII handling, access control, secrets, encryption)
- Infrastructure / Aspire AppHost wiring changes
- Dependency upgrades (Directory.Packages.props, toolchain, CI workflow changes)
- Refactors touching 3+ files
- Any change explicitly marked "high risk" by Neo or Morpheus
- Any change where agents disagree and Neo/Morpheus requests proof

### 2) Who produces evidence

- Trinity (Frontend Dev) and Tank (Backend Dev) - implementation owners, produce the evidence summary (commit hash + build/test results) for their own changes.
- Morpheus (Security Engineer) - may independently re-run tests to verify security-critical changes.
- Switch (Tester) - validates evidence and test outcomes; independently re-runs only under the exception rule below.
- Neo (Lead) - requires evidence before approving; does not routinely re-run tests themselves.
- Scribe (Session Logger) - records evidence links; does not execute builds/tests.
- Ralph (Work Monitor) - monitor only.
- @copilot - may propose scoped changes; evidence must be produced by Trinity/Tank.

### 3) Switch Exception Rule (Verification-Only Re-run)

Switch may independently re-run build/tests ONLY for verification, and ONLY when:

- The evidence is missing/incomplete, OR
- Failures are suspected flaky/nondeterministic and need independent reproduction, OR
- Neo or Morpheus explicitly requests an independent verification re-run.

If Switch re-runs under this exception:

- It must be verification-only (no feature edits, no behavior changes).
- Output must include the evidence summary and commit hash (or explain why no commit was produced).

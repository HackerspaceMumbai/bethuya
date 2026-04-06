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
| Session logging | Scribe | Automatic — never needs routing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, evaluate @copilot fit, assign `squad:{member}` label | Lead |
| `squad:{name}` | Pick up issue and complete the work | Named member |
| `squad:copilot` | Assign to @copilot for autonomous work (if enabled) | @copilot 🤖 |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, evaluating @copilot's capability profile, assigning the right `squad:{member}` label, and commenting with triage notes.
2. **@copilot evaluation:** The Lead checks if the issue matches @copilot's capability profile (🟢 good fit / 🟡 needs review / 🔴 not suitable). If it's a good fit, the Lead may route to `squad:copilot` instead of a squad member.
3. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
4. When `squad:copilot` is applied and auto-assign is enabled, `@copilot` is assigned on the issue and picks it up autonomously.
5. Members can reassign by removing their label and adding another member's label.
6. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

### Lead Triage Guidance for @copilot

When triaging, the Lead should ask:

1. **Is this well-defined?** Clear title, reproduction steps or acceptance criteria, bounded scope → likely 🟢
2. **Does it follow existing patterns?** Adding a test, fixing a known bug, updating a dependency → likely 🟢
3. **Does it need design judgment?** Architecture, API design, UX decisions → likely 🔴
4. **Is it security-sensitive?** Auth, encryption, access control → always 🔴
5. **Is it medium complexity with specs?** Feature with clear requirements, refactoring with tests → likely 🟡

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
8. **@copilot routing** — when evaluating issues, check @copilot's capability profile in `team.md`. Route 🟢 good-fit tasks to `squad:copilot`. Flag 🟡 needs-review tasks for PR review. Keep 🔴 not-suitable tasks with squad members.
9. **AppHost modifications** — if a task involves AppHost modifications (e.g., editing `.AppHost` project files, changing service orchestration, updating resource declarations), Neo takes exclusive lock. All other agents must pause until Neo confirms resource availability via the Aspire MCP list_resources tool.

## Anvil (Evidence-First) Policy — Bethuya

Burke’s Anvil is our evidence-first execution and verification loop.
When used, it must produce an evidence bundle (build/tests/lint and reviewer verdicts). [3](https://github.com/HackerspaceMumbai/bethuya/tree/main/.github/workflows)[4](https://github.com/HackerspaceMumbai/bethuya/blob/main/.claude/skills/aspire/SKILL.md)

### 1) When Anvil is REQUIRED (Hard Rule)

Anvil evidence is REQUIRED for any of the following:

- Authentication / authorization / roles / permissions changes
- Any security-sensitive boundary change (PII handling, access control, secrets, encryption)
- Infrastructure / Aspire AppHost wiring changes
- Dependency upgrades (Directory.Packages.props, toolchain, CI workflow changes)
- Refactors touching 3+ files
- Any change explicitly marked "high risk" by Neo or Morpheus
- Any change where agents disagree and Neo/Morpheus requests proof

### 2) Who MAY run Anvil (Authority)

Primary Anvil executors (implementation owners):
- Trinity (Frontend Dev)
- Tank (Backend Dev)

Conditional Anvil executor (verification runs):

- Morpheus (Security Engineer) — may re-run Anvil to independently verify security-critical changes

Validator-only (not primary Anvil runners):

- Switch (Tester) — validates evidence and test outcomes; re-runs Anvil only under the exception rule below
- Neo (Lead) — requires evidence; does not routinely run Anvil
- Scribe (Session Logger) — records evidence links; does not execute Anvil
- Ralph (Work Monitor) — monitor only; does not execute Anvil
- @copilot — may propose scoped changes; Anvil execution + evidence must be produced by Trinity/Tank

### 3) Switch Exception Rule (Verification-Only Re-run)

Switch may run Anvil ONLY for verification, and ONLY when:

- The evidence bundle is missing/incomplete, OR
- Failures are suspected flaky/nondeterministic and need independent reproduction, OR
- Neo or Morpheus explicitly requests an independent verification re-run.

If Switch runs Anvil under this exception:

- It must be verification-only (no feature edits, no behavior changes).
- Output must include the evidence bundle summary and commit hash (or explain why no commit was produced).

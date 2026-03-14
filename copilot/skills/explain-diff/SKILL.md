# Skill: explain-diff

## Description
Produce a PR summary and risk analysis for the current branch's changes. Highlights what changed, why it matters, and any risk callouts for reviewers.

## Trigger
Run before opening a PR or requesting a review. Invoke with `/explain-diff` in Copilot CLI.

## Steps
1. Run `git diff main...HEAD --stat` to get changed files.
2. Run `git diff main...HEAD` to get full diff.
3. Analyse changes and group by concern:
   - **Domain** (`src/Bethuya.Core/`) — model changes, new aggregates, business rule changes
   - **Agents** (`src/Bethuya.Agents/`) — new/changed agent logic, tool registration, memory changes
   - **AI Routing** (`src/Bethuya.AI/`) — provider router changes, prompt changes, model configuration
   - **UI (Blazor)** (`src/Bethuya.Hybrid/`) — component changes, new pages, layout changes
   - **Backend/Infrastructure** (`src/Bethuya.Backend/`, `src/Bethuya.Infrastructure/`) — API endpoint changes, storage changes
   - **AppHost** (`AppHost/`) — new/removed Aspire resources, service wiring changes
   - **Tests** (`tests/`) — new test coverage, removed tests, changed assertions
   - **Config/Build** (`Directory.Packages.props`, `Directory.Build.props`, `.editorconfig`, `global.json`) — package version bumps, build property changes
4. Identify new/removed public APIs.
5. Flag missing tests for new logic.
6. Produce structured summary.

## Output Format
```markdown
## Summary
[2-3 sentence overview of what this PR does]

## Changes by Area
- **Domain (Bethuya.Core):** ...
- **Agents (Bethuya.Agents):** ...
- **AI Routing (Bethuya.AI):** ...
- **UI (Blazor Hybrid / Web):** ...
- **Backend / Infrastructure:** ...
- **AppHost (Aspire wiring):** ...
- **Tests:** ...
- **Config/Build:** ...

## Risk Callouts
- ⚠️ [Risk description and suggested reviewer action]

## Missing Coverage
- [ ] [Untested path or scenario]
```

## Risk Flag Rules
Always flag the following as **high-risk** (require explicit human review):
- Any change to Curator Agent guardrail logic (`curator.md`, `CuratorAgent.cs`)
- Any change that routes PII data to a cloud provider instead of Foundry Local
- AppHost resource additions/removals (service topology changes)
- AI provider routing changes (`src/Bethuya.AI/`)
- Major version bumps in `Directory.Packages.props` (MAF, Aspire, MAUI packages)
- Removal of `data-test` attributes from Razor components
- New public API surface without XML doc comments


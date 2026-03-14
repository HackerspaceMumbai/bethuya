# Bethuya — Copilot Instructions

> Concise workspace instructions for GitHub Copilot Chat and Copilot CLI. For full context, see [AGENTS.md](../AGENTS.md).

## Project Identity
**Bethuya** — AI-augmented, agent-first community event platform. .NET 10, Aspire 13, MAUI Blazor Hybrid, Blazor Web App, Blazor Blueprint UI. Debuting at GitHub Copilot Dev Days.

## Tech Stack Quick-Reference
- **Runtime:** .NET 10 / C# 14
- **Orchestration:** .NET Aspire 13
- **UI:** Blazor Blueprint UI (`BlazorBlueprint.Components`)
- **Tests:** **TUnit** (unit/integration) · **Playwright** (E2E) · BenchmarkDotNet (perf)
- **AI Agents:** Microsoft Agent Framework
- **AI Providers:** Foundry Local (PII/sensitive) → Ollama → Azure OpenAI → OpenAI
- **Package Management:** Central (`Directory.Packages.props`) — never put `Version=""` in `.csproj`

## Naming & Conventions
- **Namespaces:** file-scoped (`namespace Bethuya.Core;`)
- **Private fields:** `_camelCase`; public members: `PascalCase`; locals: `camelCase`
- **Test selectors:** always `data-test` attributes — never CSS classes
- **New packages:** add version to `Directory.Packages.props` first, then reference without version in `.csproj`

## Domain Agents
| Agent | Domain | Key Constraint |
|---|---|---|
| Planner | Agenda drafting | Human approves all drafts |
| Curator | Attendee selection (3× oversubscription) | **NEVER** auto-accept/reject; Foundry Local for PII |
| Facilitator | Live session assistance | All capture is opt-in |
| Reporter | Post-event summaries | Human edit pass before publish |

## DEI & Privacy Guardrails
- Curator Agent: only uses consented DEI fields; never infers sensitive traits; always surfaces explainable reasoning.
- All attendee PII is processed locally via **Foundry Local** — never sent to cloud providers.

## Testing Mandate
- Every feature starts with a **TUnit** test (test-first).
- E2E uses **Playwright for .NET** with `data-test` selectors.
- Visual proof (screenshots) required for UI changes before marking tasks done.

## Development Protocol
1. Add task to `tasks/todo.md` before writing code.
2. Record mistakes/discoveries in `tasks/lessons.md`.
3. No temporary hacks — fix root causes.
4. Agents may autonomously fix failing tests/CI (record reasoning).

## Performance Targets
- Hot path p99 < 180ms @ 2,500 RPS · 0B hot-path allocations · >90% cache hit rate

## Available Skills
Use these via `/skill-name` in Copilot CLI:
- `seed-db` — seed dev data via Backend API (requires Backend project)
- `curate-attendees` — scaffold Curator MAF agent, models, and TUnit stubs
- `run-e2e` — run Playwright suite and summarize failures with traces
- `explain-diff` — PR summary & risk callouts (Curator guardrails, AppHost wiring, AI routing)
- `run-benchmarks` — BenchmarkDotNet micro-benchmarks + NBomber load tests
- `scaffold-agent` — scaffold a new MAF agent with tools, memory, worker, and TUnit stubs
- `setup-ai-providers` — configure Foundry Local / Ollama / Azure OpenAI user-secrets
- `run-tunit` — TUnit test runner with watch mode, filtering, and TDD loop support

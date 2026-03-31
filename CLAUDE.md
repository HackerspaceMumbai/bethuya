# Bethuya — Claude Context

> This file mirrors [AGENTS.md](AGENTS.md) for Claude models. When in doubt, AGENTS.md is the source of truth.

## Project Overview

**Bethuya** is an AI-augmented, agent-first, .NET 10 Aspire-orchestrated platform for community event management. Built for HackerspaceMumbai, debuting at GitHub Copilot Dev Days.

**Principle:** AI *drafts*, humans *approve*, community *owns*.

## Architecture

- **AppHost** — .NET Aspire orchestration
- **ServiceDefaults** — Shared resilience, service discovery, OpenTelemetry
- **Bethuya.Hybrid** — .NET MAUI Blazor Hybrid (Android/iOS/macOS/Windows)
- **Bethuya.Hybrid.Web** — Blazor Web App (SSR + WASM)
- **Bethuya.Hybrid.Web.Client** — Blazor WebAssembly client
- **Bethuya.Hybrid.Shared** — Shared Razor component library (Blazor Blueprint UI)

## Technology Stack

.NET 10 · C# 14 · Aspire 13 · MAUI Blazor Hybrid · Blazor Blueprint UI · Microsoft Agent Framework · TUnit · Playwright for .NET · BenchmarkDotNet · OpenTelemetry · Central Package Management

## Coding Standards

- Nullable enabled; TreatWarningsAsErrors; AnalysisLevel latest-Recommended
- File-scoped namespaces; primary constructors; collection expressions; pattern matching
- Private fields: `_camelCase`; public: `PascalCase`; locals: `camelCase`
- **Never** add `Version=""` to `<PackageReference>` — all versions in `Directory.Packages.props`
- **Never** use xUnit or NUnit — always **TUnit**
- **UI: Blazor Blueprint first** — always use BB components before writing custom CSS. Form-field wrappers: `BbFormFieldInput`, `BbFormFieldSelect`, `BbFormSection`. Standalone (wrap with `BbLabel` + `form-group`): `BbTextarea`, `BbNumericInput`, `BbFileUpload`, `BbDatePicker`, `BbTimePicker`. Custom CSS must have a comment explaining why BB couldn't handle it.
- Manual HTML/CSS wrappers only when BB lacks a `BbFormField*` wrapper for a component (e.g., `BbDatePicker`/`BbTimePicker` form-field wrappers, inline prefix adornments, remote URL image preview)

## Domain Agents & Guardrails

### Planner
Draft agendas → human approves/edits/rejects. Never publishes autonomously.

### Curator ⚠️
Attendee selection for 3× oversubscription. **Hard rules:**
- NEVER auto-accept or auto-reject
- NEVER infer sensitive traits
- Use Foundry Local for all PII (on-device, offline)
- Only use consented DEI fields; always surface reasoning

### Facilitator
Opt-in live assistance. Organizer controls all publishing.

### Reporter
Post-event drafts → human edit pass → publish with attribution.

## AI Provider Routing

1. **Foundry Local** — PII/sensitive (on-device)
2. **Ollama** — local dev/test
3. **Azure OpenAI** — non-sensitive, enterprise
4. **OpenAI** — optional fallback

## Development Protocol

1. Plan first — add to `tasks/todo.md` before coding
2. No hacks — fix root causes
3. Record all lessons in `tasks/lessons.md`
4. Test-first with TUnit; E2E with Playwright (`data-test` selectors)
5. Screenshot UI changes before marking done
6. **Pre-commit review (mandatory):** Run `code-review` agent on staged changes, `dotnet-diag:optimizing-dotnet-performance` on .NET files, `/explain-diff` before PRs. Never rely on humans to catch code issues.

## Performance Targets

| Metric | Target |
|---|---|
| Hot path latency (p99) | < 180ms @ 2,500 RPS |
| Hot path allocations | 0 B |
| Cache hit rate | > 90% |

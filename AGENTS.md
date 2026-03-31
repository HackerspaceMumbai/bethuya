# Bethuya — AI Agent Context

> **Read this file first.** It is the canonical project context for all AI agents, coding assistants, and automated tools operating in this repository.

## Project Overview

**Bethuya** is an AI-augmented, agent-first, .NET-built, Aspire-orchestrated platform for planning, curating, running, and reporting community events — built for and by [HackerspaceMumbai](https://hackerspacemumbai.com), debuting at GitHub Copilot Dev Days.

**Principle:** AI *drafts*, humans *approve*, community *owns*.

---

## Architecture

```
AppHost (Aspire)
  ├─ Bethuya.Hybrid.Web          # Blazor Web App (SSR + WASM)
  ├─ Bethuya.Hybrid.Web.Client   # Blazor WebAssembly client
  ├─ Bethuya.Hybrid.Shared       # Shared Razor component library
  ├─ Bethuya.Hybrid              # .NET MAUI Blazor Hybrid (Android/iOS/macOS/Windows)
  ├─ Bethuya.ApiService          # Minimal API for core logic (Refit-ready)
  ├─ Bethuya.MigrationService    # SQL Migration Worker (Local-to-Cloud parity)
  └─ ServiceDefaults             # Aspire shared: resilience, service  discovery, OpenTelemetry
  └─ Scalar                      # Integrated via Aspire for API exploration
```

Planned additions (scaffold in `src/`):
- `Bethuya.Core` — Domain: Events, Registrations, Decisions, FairnessBudget
- `Bethuya.Agents` — Planner, Curator, Facilitator, Reporter agents
- `Bethuya.AI` — Provider router (Foundry Local / Ollama / Azure OpenAI / OpenAI), prompts, memory
- `Bethuya.Backend` — ASP.NET Core API (Aspire-connected)
- `Bethuya.Infrastructure` — Storage (Azure SQL), repos, platform adapters

---

## Technology Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10, C# 14 |
| Orchestration | .NET Aspire 13 (File-based Resource Definition) |
| API Docs | Scalar (Aspire Integrations) |
| API Layer | Refit (Type-safe contracts) |
| Data Engine | Data API Builder (DAB) — Native MCP Server support |
| Identity | Vogen (Zero-allocation Value Objects) |
| Mobile/Desktop | .NET MAUI Blazor Hybrid |
| Web | Blazor Web App (SSR + WASM) |
| UI Components | Blazor Blueprint UI (`BlazorBlueprint.Components`, `BlazorBlueprint.Icons.Lucide`) |
| AI Agents | Microsoft Agent Framework |
| AI Providers | Foundry Local, Microsoft Foundry |
| Testing (unit) | TUnit (NOT xUnit, NOT NUnit) |
| Testing (E2E) | Playwright for .NET |
| Performance | BenchmarkDotNet, NBomber |
| Observability | OpenTelemetry → Aspire Dashboard |
| Package Mgmt | Central Package Management (`Directory.Packages.props`) |

---

## Domain Agents (Human-in-the-Loop)

### Planner Agent
- **Purpose:** Draft event agendas, timings, speaker suggestions.
- **Inputs:** Prior events, interests, venue constraints.
- **Outputs:** Draft agenda → human approves/edits/rejects (diffs + audit trail).
- **Guardrail:** Never publishes without explicit human approval.

### Curator Agent ⚠️ Sensitive
- **Purpose:** Assist humans to select attendees fairly when registrations exceed capacity (often 3×).
- **Inputs:** Registrant profiles (theme alignment, self-reported DEI fields with consent).
- **Outputs:** `AttendanceProposal`, `WaitlistProposal`, `CurationInsights`, `FairnessBudget`.
- **Hard guardrails:**
  - **NEVER** auto-accepts or auto-rejects attendees.
  - **NEVER** infers sensitive traits not explicitly provided.
  - **NEVER** hides reasoning.
  - Uses **Foundry Local** for all PII processing (on-device, offline).
  - DEI nudges only use consented fields.

### Facilitator Agent
- **Purpose:** Live session assistance — prompts, Q&A suggestions, notes capture.
- **Inputs:** Session agenda, live transcript (opt-in).
- **Outputs:** Suggested prompts, Q&A queue, captured notes (organizer-controlled publish).
- **Guardrail:** All capture is opt-in; organizer controls publication.

### Reporter Agent
- **Purpose:** Draft post-event summaries, highlights, action items.
- **Inputs:** Session notes, attendance data, feedback.
- **Outputs:** Summary draft → human edits → publish with attribution.
- **Guardrail:** Never publishes without human edit pass.

---

## AI Provider Routing

Route AI calls by data sensitivity:

| Provider | Use Case | Notes |
|---|---|---|
| **Foundry Local** | Attendee curation (PII), sensitive data | On-device, offline, OpenAI-compatible API |
| **Ollama** | Local LLMs for development | Dev/test use |
| **Azure OpenAI** | Non-sensitive, enterprise boundary | Public event drafts, summaries |
| **OpenAI** | Optional non-sensitive public content | Fallback |

---

## Coding Standards

### General

- **Nullable reference types:** Always enabled (`<Nullable>enable</Nullable>`).
- **Warnings as errors:** `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — fix all warnings; never suppress without documented justification.
- **Analysis level:** `latest-Recommended` — heed analyzer suggestions.
- **Code style:** Enforced in build (`<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`).
- Suppressed globally: `CA1716` (reserved keywords), `CA1711` (name suffixes) — do not add others without discussion.

### API & Communication

Documentation: All APIs MUST be defined via Scalar. Use the Aspire.Hosting.Scalar integration for service discovery.

Contracts: Never use raw HttpClient. Always use Refit interfaces shared in Bethuya.Shared.

Identity: Use Vogen for all domain IDs (AttendeeId, EventId). Raw primitives are prohibited.

### C# Style

- Use **file-scoped namespaces** (`namespace Foo;` not `namespace Foo { }`).
- Use **primary constructors** where appropriate (C# 14).
- Use **collection expressions** (`[1, 2, 3]` not `new List<int> { 1, 2, 3 }`).
- Use **pattern matching** over type-checking casts.
- Private fields: `_camelCase`. Public members: `PascalCase`. Locals: `camelCase`.
- XML doc comments on all public APIs.

### UI — Blazor Blueprint First

- **Always use Blazor Blueprint (BB) components before writing custom CSS or HTML.**
- **Custom CSS requires a comment explaining why BB couldn't handle it.** Each custom CSS block must have a one-line comment stating what BB lacks (e.g., "BB ShowPreview only supports local IBrowserFile — remote URL preview is custom").
- **BB form-field wrappers** (wrap label + input + helper text + validation + ARIA):
  - `BbFormFieldInput<TValue>` — text, email, number, URL inputs
  - `BbFormFieldSelect<TValue>` — dropdown (Options mode or compositional)
  - `BbFormSection` — group related fields with title/description
- **BB standalone components** (no built-in label/validation wrapper — use manual `<div class="form-group">` + `BbLabel` + `ValidationMessage`):
  - `BbTextarea` — multi-line text with `MaxLength`/`ShowCharacterCount`
  - `BbNumericInput<TValue>` — numeric with `Min`/`Max`/`Step`/`ShowButtons`
  - `BbFileUpload` — drag-and-drop file upload with `Accept`/`MaxFileSize`/`MaxFileCount`/`OnValidationError`
  - `BbDatePicker`, `BbTimePicker` — date/time pickers
- **Manual `<div class="form-group">` wrappers** are for fields that lack a `BbFormField*` wrapper (textarea, numeric, file upload, date/time pickers, inline adornment inputs like hashtag `#` prefix).
- Use `data-test` attributes on all interactive elements — never CSS classes for E2E selectors.

### AI & Privacy

PII: All sensitive curation is routed to Foundry Local (on-device). Non-sensitive orchestration uses Microsoft Foundry.

### Packages

- **Central Package Management is enforced.** Never add `Version=""` to `<PackageReference>` in `.csproj` files. All versions go in `Directory.Packages.props`.
- Always add new packages to `Directory.Packages.props` first, then reference without version.

### Testing

- **Unit/integration tests:** Use **TUnit** — not xUnit, not NUnit.
- **Test-first:** Every feature begins with a TUnit test.
- **E2E:** Use **Playwright for .NET**. Always use `data-test` selectors (not CSS classes) for stability.
- **Performance:** Use BenchmarkDotNet for micro-benchmarks; NBomber for load tests.

### Performance Targets

| Metric | Target |
|---|---|
| Hot path latency (p99) | < 180ms @ 2,500 RPS |
| Memory allocation (hot path) | 0 B (enforced via Vogen and BenchmarkDotNet). |
| Cache hit rate | > 90% |
| Memory (steady state) | < 65% allocated RAM |

---

## Development Protocol

1. **Plan First:** Before executing any task, add it to `tasks/todo.md`. No code changes without a plan entry.
2. **No Hacks:** Identify root causes. Temporary workarounds are not acceptable.
3. **Record Lessons:** Every mistake or unexpected discovery goes in `tasks/lessons.md` to prevent recurrence.
4. **Autonomous Fixes:** Agents are authorized to resolve failing tests and CI without manual intervention — but must record reasoning.
5. **Playwright Visual Proof:** Capture screenshots of UI changes before marking tasks done.
6. **Diff Reviews:** Always run `/explain-diff` before opening a PR.

---

## Repository Layout

``` folder structure
/ (root)
├─ Bethuya.slnx
├─ Directory.Build.props          # Global MSBuild properties
├─ Directory.Packages.props       # Central Package Management
├─ global.json                    # SDK version pin (≥10.0.100, latestFeature)
├─ .editorconfig                  # Code style enforcement
├─ AGENTS.md                      # This file — AI agent context
├─ CLAUDE.md                      # Claude-specific mirror
│
├─ AppHost/AppHost/               # .NET Aspire AppHost
├─ ServiceDefaults/               # Aspire shared project
├─ src/Bethuya.Hybrid/            # MAUI + Blazor Web + Shared
│
├─ tasks/
│  ├─ todo.md                     # Plan-first task tracker
│  └─ lessons.md                  # Self-correction log
│
├─ copilot/skills/                # GitHub Copilot SDK skills
│  ├─ seed-db/SKILL.md            # Seed dev data (requires Backend project)
│  ├─ curate-attendees/SKILL.md   # Scaffold Curator pipeline & TUnit tests
│  ├─ run-e2e/SKILL.md            # Run Playwright E2E suite
│  ├─ explain-diff/SKILL.md       # PR summary & risk callouts
│  ├─ run-benchmarks/SKILL.md     # BenchmarkDotNet + NBomber perf suite
│  ├─ scaffold-agent/SKILL.md     # Scaffold a new MAF agent
│  ├─ setup-ai-providers/SKILL.md # Configure Foundry Local / Ollama / Azure OpenAI
│  ├─ run-tunit/SKILL.md          # TUnit TDD loop (watch mode + filtering)
│  └─ check-security/SKILL.md     # Security review: auth, PII, render modes, prompt injection
│
├─ .github/
│  ├─ copilot-instructions.md     # Copilot Chat/CLI workspace instructions
│  ├─ agents/                     # Agent persona files
│  │  ├─ planner.md               # Planner Agent — agenda drafting (Azure OpenAI)
│  │  ├─ curator.md               # Curator Agent — attendee curation (Foundry Local, PII)
│  │  ├─ facilitator.md           # Facilitator Agent — live assistance (Foundry Local, real-time)
│  │  ├─ reporter.md              # Reporter Agent — post-event summaries (Azure OpenAI)
│  │  ├─ approver.md              # Approver — human-in-the-loop approval workflow
│  │  └─ dotnet-dev.md            # .NET Developer — coding standards & patterns
│  └─ workflows/
│     ├─ ci.yml                   # Build + test CI
│     └─ security.yml             # CodeQL + vulnerable package scan
│
└─ assets/                        # Branding, screenshots, sample data
```

---

## Quick Commands

```bash
# Run all TUnit tests
dotnet test

# Build full solution
dotnet build

# Run Aspire AppHost (local orchestration)
dotnet run --project AppHost/AppHost

# Run E2E Playwright tests
dotnet test tests/

# Watch tests during TDD
dotnet watch test
Oh, we need to. I. I. Play.
# Security: check for vulnerable packages
dotnet list package --vulnerable --include-transitive
Move to.
# Security: run check-security skill
# (use Copilot CLI: /check-security)
```

---

## Security

### Authentication

Auth is implemented on three interchangeable feature branches:
- `feature/auth/entra` — Microsoft Entra External ID
- `feature/auth/auth0` — Auth0
- `feature/auth/keycloak` — Keycloak (self-hosted OIDC)

On `main`, `NullCurrentUserService` is registered as the `ICurrentUserService` placeholder. Merge the desired auth branch when ready.

### Authorization

- All non-public Blazor pages must have `[Authorize]` or `<AuthorizeView>`
- Authorization policies use `BethuyaRoles` constants (Admin, Organizer, Curator, Attendee)
- Policies: `RequireOrganizer`, `RequireCurator`, `RequireAttendee` — defined in auth branches

### Blazor Render Mode Rule

> **Login, auth, PII, organizer, and agent control pages = `@rendermode InteractiveServer` only.**
> WASM code is client-inspectable. Sensitive pages MUST be server-side.

### Security Infrastructure (active on main)

- **Security headers:** CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy — added via `app.UseSecurityDefaults()` from ServiceDefaults
- **Rate limiting:** 100 req/min (general), 20 req/min for `RateLimitPolicies.Ai` — configured in ServiceDefaults
- **CORS:** Named policy `"BethuyaMobileClients"`, origins from `appsettings.json` `Cors:AllowedOrigins`
- **AllowedHosts:** Locked to `localhost` — must be set to actual domain in production `appsettings.json`
- **CodeQL:** SAST analysis on all PRs (`.github/workflows/security.yml`)
- **Dependabot:** Weekly NuGet + Actions updates (`.github/dependabot.yml`)
- **Vulnerable package gate:** `dotnet list package --vulnerable` blocks CI on CVEs
- **Responsible disclosure:** `SECURITY.md`

### PII Routing

The Curator Agent processes attendee PII exclusively via **Foundry Local** (on-device). PII never reaches any cloud endpoint. See `curator.md` for guardrails.

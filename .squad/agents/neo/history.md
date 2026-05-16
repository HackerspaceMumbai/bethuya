# Project Context

- **Owner:** Augustine Correa
- **Project:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Created:** 2026-03-21T11:50:19.882Z

## Learnings

- Bethuya has strict privacy guardrails, human-in-the-loop agent workflows, and `InteractiveServer` requirements for auth/PII pages.
- **DDD refactoring "Create Event" → "Schedule Event" (2026-04-04):** Produced architecture proposal in `.squad/decisions/inbox/neo-schedule-event-ddd.md`. Key decisions:
  - Phase 1 includes `EventDateRange` value object to avoid pure rename theater - centralizes `End ≥ Start` invariant currently duplicated in `CreateEventFormModel.cs:44-58` and `EventEndpoints.cs:57-60`.
  - API route stays `POST /api/events` (RESTful); DDD intent captured in code names (`ScheduleEventRequest`, `ScheduleAsync`).
  - API boundary types use `*Request` suffix (not `*Command`) since there's no application/handler layer yet.
  - Backward-compat `@page "/events/create"` kept for one release to avoid broken deep links.
  - Execution sequenced: Tank (shared contracts) → Trinity (frontend) → Switch (tests) to avoid merge conflicts on `IEventApi.cs`.
  - Critic flagged semantic tension: "Schedule" implies dates are set, but current flow creates a `Draft`. Flagged for stakeholder alignment.
  - Exhaustive file list: 6 files to rename, 7 classes/records to rename, 20+ in-file references, 2 E2E test files, 1 CI workflow line.
- **Key file paths for this feature:**
  - Frontend: `src/Bethuya.Hybrid/Bethuya.Hybrid.Shared/Pages/CreateEvent.razor` (+ .css), `Models/CreateEventFormModel.cs`
  - Backend contracts: `src/Hackmum.Bethuya.Backend/Contracts/EventContracts.cs`, `src/Bethuya.Hybrid/Bethuya.Hybrid.Shared/Services/IEventApi.cs`
  - Backend endpoint: `src/Hackmum.Bethuya.Backend/Endpoints/EventEndpoints.cs`
  - Core model: `src/Hackmum.Bethuya.Core/Models/Event.cs`
  - Tests: `tests/Hackmum.Bethuya.Tests/UI/CreateEventRenderTests.cs`, `tests/Hackmum.Bethuya.Tests/Domain/CreateEventFormModelTests.cs`, `tests/Hackmum.Bethuya.Tests/Domain/EventCreationTests.cs`
  - E2E: `tests/Hackmum.Bethuya.E2E/Tests/EventFlowTests.cs`, `tests/Hackmum.Bethuya.E2E/Tests/CoverImageFlowTests.cs`
  - CI: `.github/workflows/ci.yml:135`
  - Navigation callers: `Home.razor`, `Events.razor`, `FeaturedEventCard.razor`
- **AI provider wiring pattern (2026-05-07):** AI provider settings (endpoints, API keys, model IDs) must flow through AppHost `AddParameter` → `WithEnvironment` to services. Local providers (FoundryLocal, Ollama) have safe defaults in `AppHost/appsettings.json`. Cloud secrets (AzureOpenAI, OpenAI) must be set via `dotnet user-secrets` on the AppHost project. Key parameter names: `ai-foundrylocal-endpoint`, `ai-ollama-endpoint`, `ai-azure-openai-endpoint`, `ai-azure-openai-key`, `ai-azure-openai-model`, `ai-openai-key`, `ai-openai-model`. These map to env vars `AI__Providers__<Name>__<Field>` in the backend.
- **AppHost secret: true pattern:** `secret: true` in `AddParameter` is NOT used in this project - it causes "Unsupported value type System.Boolean" in Azure Container Apps deployment. All parameters use default `AddParameter(name)` without secret flag. Cloud secrets are instead routed through Key Vault in publish mode.
- **README Configuration Reference:** Added `## 🔧 Configuration Reference` section with tables for all runtime params grouped by category (AI Providers, Image Upload, Social Login, Auth). This is the canonical doc for devs onboarding the project.
  - **Vogen IDs only** - raw Guid/int is build-breaking offense. AttendeeId, EventId, UserId are Vogen structs. Non-negotiable.
  - **Central Package Management enforced** - all versions in Directory.Packages.props, never `Version=""` in .csproj files.
  - **No EF Core migrations until formal release** - Augustine''s directive. Delete Migrations/ folder if present; never regenerate before a formal milestone.
  - **Aspire-first orchestration** - file-based resource definition, local-to-cloud parity (Azure Container Apps target).
  - **Scalar API docs mandatory** - every new endpoint must be verifiable via Scalar UI in Aspire Dashboard.
  - **Refit for all HTTP** - no raw HttpClient; use shared interfaces in Bethuya.Hybrid.Shared.
  - **InteractiveServer global default** - use global render mode assignment on Routes in App.razor (not per-page), sensitive pages: InteractiveServer; public: InteractiveAuto.
  - **File-scoped namespaces, primary constructors, collection expressions** - enforce C# 14 idioms.
  - **Nullable enabled, TreatWarningsAsErrors** - fix all warnings, never suppress without documented justification.
  - **0 B hot-path allocations via Vogen** - performance target: p99 < 180ms @ 2,500 RPS, >90% cache hit rate.

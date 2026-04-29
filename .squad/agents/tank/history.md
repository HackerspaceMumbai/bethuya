# Project Context

- **Owner:** Augustine Correa
- **Project:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Created:** 2026-03-21T11:50:19.882Z

## Learnings

- Bethuya uses central package management in `Directory.Packages.props`, warnings-as-errors, and shared auth abstractions in `Bethuya.Hybrid.Shared`.
- Auth is fully provider-pluggable on `main` via `Authentication:Provider` (None/Entra/Auth0/Keycloak). Code lives in `ServiceDefaults/Auth/` (extensions, options, provider enum) and `src/Bethuya.Hybrid/Bethuya.Hybrid.Web/Auth/` (state providers, user service).
- `Aspire.Hosting.Keycloak` is preview-only for Aspire 13.x. Used version `13.1.2-preview.1.26125.13` — pinned in `Directory.Packages.props`.
- Keycloak container added to AppHost on stable port `8080` (avoids browser cookie/OIDC token issues). Wired as a reference to both `web` and `backend`.
- Role claim types differ by provider: Entra=`roles`, Auth0=`https://bethuya.dev/roles`, Keycloak=`realm_access`. Mapped in `BethuyaAuthenticationExtensions.cs`.
- README repository structure was stale (referenced old `aspire/`, `Hackmum.Bethuya.App` paths). Updated to reflect actual layout.
- Key auth files: `AuthProviderType.cs`, `BethuyaAuthOptions.cs`, `BethuyaAuthenticationExtensions.cs`, `BethuyaAuthorizationExtensions.cs` in `ServiceDefaults/Auth/`.
- Backend event endpoints hardened (2026-03-21): Added `EventResponse` DTO in `EventContracts.cs` to decouple API responses from domain entities. All GET/POST/PUT endpoints now return consistent DTOs with enums serialized as strings (Type, Status).
- Server-side validation added to POST/PUT `/api/events`: Title required + max 200 chars, Capacity 1-10,000, EndDate >= StartDate, CreatedBy required. Returns `Results.ValidationProblem()` with field-level errors.
- `JsonStringEnumConverter` registered in `Program.cs` (line 10-11) enables frontend to send `Type: "Meetup"` string, Backend deserializes to `EventType.Meetup` enum correctly.
- Key Backend files: `src/Hackmum.Bethuya.Backend/Endpoints/EventEndpoints.cs`, `src/Hackmum.Bethuya.Backend/Contracts/EventContracts.cs`, `src/Hackmum.Bethuya.Backend/Program.cs`.
- Frontend API contract: `src/Bethuya.Hybrid/Bethuya.Hybrid.Shared/Services/IEventApi.cs` defines `EventDto` and `CreateEventDto` for Refit client.
- Cloudinary image upload integrated (2026-03-21): Added `CloudinaryDotNet` 1.26.2 via Central Package Management. New `IImageUploadService` interface in Core, `CloudinaryImageUploadService` + `CloudinaryOptions` in Infrastructure, `ImageEndpoints` (POST /api/images/upload) in Backend. `CoverImageUrl` property added to Event model, all contracts (Create/Update/Response), EF config (max 2048), Refit DTOs, and endpoint mappers. Image upload validates file type (JPEG/PNG/WebP/GIF) and size (5 MB max). DI registered as singleton in `InfrastructureServiceExtensions`. Cloudinary credentials expected in `appsettings.json` under `Cloudinary` section (CloudName, ApiKey, ApiSecret).
- GitHub Copilot SDK date recommendation backend (2026-04-04, retroactive): Built `DateRecommendationService` wrapping `CopilotClient` v0.2.1, seed data in `CommunityEventPatterns.cs` (Eventbrite/Meetup patterns), `DateRecommendationContracts.cs` request/response types, and POST `/api/agents/recommend-dates` endpoint. Singleton pattern with session-per-request. Non-PII (no IAIRouter needed). DI registered in `AIServiceExtensions.cs`. Integrated into `AgentEndpoints.cs`. Build: 0 errors/0 warnings.
- DDD-friendly naming refactor "Create Event" → "Plan Event" (2026-04-04): Renamed `CreateEventDto` → `PlanEventDto` and `CreateAsync` → `PlanAsync` in `IEventApi.cs` (shared Refit contract). Renamed `CreateEventRequest` → `PlanEventRequest` in `EventContracts.cs` and its usage in `EventEndpoints.cs` line 39. HTTP route `POST /api/events` unchanged (RESTful). Repository `CreateAsync` methods untouched (generic CRUD, not domain naming). Backend builds clean: 0 errors/0 warnings. Frontend `.razor` files, form models, and tests left for Trinity/Switch.
- Integration test packages added (2026-04-08): Added `Aspire.Hosting.Testing` 13.2.1 (matches all existing Aspire package versions) and `Respawn` 6.2.1 to `Directory.Packages.props` in the Testing section, after the TUnit entry. Added `tests/Bethuya.IntegrationTests/Bethuya.IntegrationTests.csproj` to `/tests/` folder in `Bethuya.slnx`. Fixed stray `1` on line 20 of `AppHost/AppHost/AppHost.cs` (between `cloudinaryApiSecret` parameter and the AI provider comment block). Build fails only due to missing `.csproj` file — expected race condition with Switch creating the project in parallel.
- Aspire `AddParameter(secret: true)` Azure deployment bug (2026-04-09): The `secret: true` flag causes `[ERR] Unsupported value type System.Boolean` during `azd deploy` / `aspire deploy` to Azure Container Apps because the boolean is serialized into the manifest but ACA deployment pipeline doesn't support it. **Fix:** Remove `secret: true` from all `AddParameter` calls in AppHost.cs. Parameters work correctly without it — they read from user-secrets in dev and environment variables in Azure. **Production pattern:** Use Key Vault `AddSecret` for actual secret management (encryption, rotation, access control, audit logging). Removed `secret: true` from Cloudinary `api-key` and `api-secret` parameters in `AppHost/AppHost/AppHost.cs` lines 24-25. Created skill file at `.squad/skills/aspire-secrets/SKILL.md` documenting the bug, fix, and Key Vault patterns.
- Aspire ACA secrets skill scope correction (2026-04-09): The original `aspire-secrets` skill had incorrect/inverted guidance in the copilot version — it told agents to ADD `secret: true`, which is exactly the bug we fixed by removing it. Both copilot and squad skills renamed to `aspire-aca-secrets` to reflect Azure Container Apps scope. The bug is Azure-specific; deployments to other cloud providers (AWS, GCP) are unaffected. Updated skill content to correctly state: DO NOT use `secret: true` for Azure projects; DO use Key Vault for production. The "Current State of AppHost.cs" table now correctly shows NO `secret: true` on cloudinary params (that's the fix, not the bug). Old directories deleted: `copilot/skills/aspire-secrets/` and `.squad/skills/aspire-secrets/`. New files created: `copilot/skills/aspire-aca-secrets/SKILL.md` and `.squad/skills/aspire-aca-secrets/SKILL.md`. **Committed** (2026-04-09): Git commit `746ad27` — skill rename staged and merged with message documenting the inverted guidance bug and Azure scope clarification. Copilot now correctly advises: remove `secret: true`, use Key Vault for production. Squad skill updated in parallel.
- **AI coding primitives updated (2026-04-09):** Augustine updated README.md, AGENTS.md, and .github/copilot-instructions.md. Key backend/API/data conventions absorbed:
  - **Vogen IDs mandatory** — AttendeeId, EventId, UserId are Vogen structs, not raw Guid/int. Build-breaking offense if violated.
  - **Refit shared contracts only** — all HTTP calls use Refit interfaces in Bethuya.Hybrid.Shared, never raw HttpClient. Type-safe by design.
  - **Scalar API documentation required** — every new endpoint must be verifiable via Scalar UI in Aspire Dashboard.
  - **Central Package Management enforced** — all versions in Directory.Packages.props; never `Version=""` in .csproj.
  - **No EF Core migrations until formal release** — Augustine's directive (2026-04-09). Delete Migrations/ folder; never regenerate before a milestone.
  - **Nullable enabled, TreatWarningsAsErrors** — fix all warnings; never suppress without documented justification.
  - **File-scoped namespaces, primary constructors, collection expressions** — C# 14 style enforced.
  - **0 B hot-path allocations** — Vogen + BenchmarkDotNet ensure p99 < 180ms @ 2,500 RPS, >90% cache hit.
- CI Playwright failure `24238142180` (2026-04-11) was not caused by the transient SQL State 38 startup log. The actual break was E2E harness drift: home CTA selector changed to `plan-event-cta`, Blazor redirects need URL+DOM waits instead of page-load waits, and event-detail coverage must publish first so `view-event-btn` exists.

### Phase 1: Orchestrator Agent & MAF Runtime (2026-04-28)

**Context:** Implemented foundational orchestration layer with SQL schema for workflow state, approval tracking, and audit logging.

**Key Patterns Discovered:**

1. **IAgent<TRequest, TResponse> Contract:**
   - All request types must implement `IAgentRequest` with `AgentName`, `Sensitivity`, `RequestedBy`
   - All response types must implement `IAgentResponse` with `RequiresHumanApproval`, `AgentReasoning`, `GeneratedAt`
   - Records can implement interfaces via explicit properties in the body (not primary constructor parameters)

2. **EF Core Configuration Best Practices:**
   - Use `HasDefaultValueSql("GETUTCDATE()")` for server-side timestamps (consistent across instances)
   - Store enums as strings with `.HasConversion<string>()` for human-readable audit logs
   - Index strategy: foreign keys (EventId) + status/time columns for common query patterns

3. **Aspire Service Discovery:**
   - Agents-as-libraries integrate cleanly via Backend's DI container
   - No need for separate Aspire project if agents don't expose HTTP endpoints
   - Environment variables for AI routing flow through to Backend's configuration

4. **Workflow State Machine Pattern:**
   - Dual persistence (SQL + Redis) specified in design for durability + performance
   - Phase 1 uses SQL only; Redis deferred to Phase 2
   - State transitions require approval checks before advancing

5. **Audit Log Design:**
   - Append-only with `BIGINT IDENTITY` for high write throughput
   - No updates, no deletes — immutable for compliance
   - Indexed on EventId + Timestamp for time-range queries

**Schema Decisions:**

- `WorkflowState.EventId` as PK (one workflow per event)
- `ApprovalState` allows multiple approvals per event (one per WorkflowPhase)
- `AuditLog` tracks all agent actions (Orchestrator spawns, workflow advances, curation decisions)

**Implementation Notes:**

- Orchestrator uses synchronous DbContext calls in `ParseResponse` (base class contract)
- Async methods (`SpawnAgentAsync`, `AdvanceWorkflowAsync`) for public API endpoints
- Logging via structured OpenTelemetry (inherited from AgentBase)

**Dependencies:**

- `Microsoft.EntityFrameworkCore` required in Agents project for DbContext access
- Infrastructure reference added to Agents (one-way: Agents → Infrastructure → Core)

**Next Steps (Phase 2):**

- Extract repository interfaces (`IWorkflowRepository`, `IAuditLogRepository`)
- Wire up Redis for agent memory (Foundry Hosted)
- Implement actual agent spawning (currently stubs)
- Build approval UI and connect to ApprovalState table
- Add TUnit integration tests for workflow transitions

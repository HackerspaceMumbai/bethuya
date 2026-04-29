# Project Context

- **Owner:** Augustine Correa
- **Project:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Created:** 2026-03-21T11:50:19.882Z

## Learnings

- Bethuya follows test-first intent, uses TUnit rather than xUnit/NUnit, and expects visual proof for UI changes before completion.
- E2E tests use Playwright with MSTest (not TUnit) and MUST use `data-test` selectors for stability.
- Key test selector patterns: `[data-test='new-event-btn']`, `[data-test='create-event-submit']`, `[data-test='event-card']`, `[data-test='notification']`.
- Test projects reference multiple dependencies: Core, Backend (for Contracts), Agents, Shared, ServiceDefaults.
- TUnit assertions use `await Assert.That(value).IsEqualTo(expected)` pattern — async by default.
- TUnit test attribute is `[Test]`, not `[Fact]` or `[TestMethod]`.
- Guid.CreateVersion7() generates Version 7 GUIDs — version bits are in byte[7], bits 4-7.
- EventCreationTests.cs covers contract mapping, JSON deserialization of EventType enum, and Guid versioning.
- Build may fail if dependent projects (like Bethuya.Hybrid.Shared) have active Razor compilation errors, but test logic is sound.
- E2E test project (Playwright + MSTest) builds independently and successfully with proper data-test selectors.
- **GitHub Copilot SDK date recommendation tests (2026-04-04, retroactive):** Wrote `DateRecommendationParseTests.cs` with 7 TUnit tests covering ParseResponse parsing (success, malformed JSON, empty dates, invalid format). Wrote 2 bUnit UI tests in `CreateEventRenderTests.cs` testing button render and click handler. All 89/89 tests pass. Tests exercise internal static ParseResponse via `[InternalsVisibleTo]` for unit testing.
- **DDD naming refactor "Create Event" → "Plan Event" (test scope):** Renamed 3 unit test files: `CreateEventFormModelTests.cs` → `PlanEventFormModelTests.cs`, `CreateEventRenderTests.cs` → `PlanEventRenderTests.cs`, `EventCreationTests.cs` → `EventPlanningTests.cs`. Updated all class names, type references (`CreateEventFormModel` → `PlanEventFormModel`, `CreateEventRequest` → `PlanEventRequest`, `RenderComponent<CreateEvent>` → `RenderComponent<PlanEvent>`), and data-test selectors (`create-event-*` → `plan-event-*`). Updated E2E files (EventFlowTests, CoverImageFlowTests) — URLs `/events/create` → `/events/plan`, selectors, method names. DateRecommendationParseTests confirmed clean (no CreateEvent references). Build succeeded, 89/89 tests pass.
- **Integration test infrastructure scaffold (2026-04-08):** Created `tests/Bethuya.IntegrationTests/` with 5 files:
  - `Bethuya.IntegrationTests.csproj` — TUnit + Aspire.Hosting.Testing + Respawn; AppHost ProjectReference; no Backend reference (BP6).
  - `BethuyaAppFixture.cs` — `IAsyncInitializer` + `IAsyncDisposable`; starts real `DistributedApplication`; waits for backend health (BP3); `CreateBackendClient()` and `GetSqlConnectionStringAsync()`.
  - `DatabaseFixture.cs` — wraps `BethuyaAppFixture`; `Respawner` with `DbAdapter.SqlServer`; `ResetAsync()` for `[Before(Test)]` hooks (BP5 anti-pollution).
  - `Backend/BackendHealthTests.cs` — `[ClassDataSource<BethuyaAppFixture>(Shared = SharedType.PerTestSession)]`; tests `/health` and `/alive` endpoints returning 200.
  - `Contracts/ContractNote.cs` — BP6 comment; contract types duplicated here (not referenced from Backend) as compile-time breaking-change sentinel.
  - Key TUnit patterns: `IAsyncInitializer`, `[ClassDataSource<T>(Shared = SharedType.PerTestSession)]`, `[Before(Test)]` hook pattern, `await Assert.That(...).IsEqualTo(...)`.
  - BP reference map: BP3=wait-for-healthy, BP5=no-cross-test-state, BP6=contract-duplication-as-safety-net.

- **AI coding primitives updated (2026-04-09):** Augustine updated README.md, AGENTS.md, and .github/copilot-instructions.md. Key testing/E2E conventions absorbed:
  - **TUnit ONLY** — never xUnit, never NUnit. Unit/integration tests use TUnit. Assertions: `await Assert.That(value).IsEqualTo(expected)` (async by default). Attribute: `[Test]`, not `[Fact]` or `[TestMethod]`.
  - **E2E Playwright with data-test selectors** — MSTest for Playwright tests (E2E, not unit). Always use `data-test` attributes, never CSS classes. Enables stable selectors across layout changes.
  - **Test-first mandate** — every feature begins with TUnit test. No code accepted without passing integration test.
  - **Visual proof required** — Playwright screenshots required for all major UI changes (e.g., registration flow) before marking tasks done.
  - **TUnit patterns:** `IAsyncInitializer`, `[ClassDataSource<T>(Shared = SharedType.PerTestSession)]`, `[Before(Test)]` hooks, async assertions.
  - **No cross-test state pollution** — BP5: `ResetAsync()` in `[Before(Test)]` hooks ensures test isolation.
  - **Contract duplication as safety net** — BP6: duplicate contract types in test projects (not imported) to catch breaking changes at compile-time.
  - **Central Package Management** — all test package versions in Directory.Packages.props; never `Version=""` in .csproj.

- **Phase 1: Orchestrator & Approver Agent test stubs (2026-04-15):** Created TUnit test stubs and builders for multi-agent orchestration acceptance criteria:
  - **Test builders** — `EventDataBuilder.cs`, `WorkflowStateBuilder.cs`, `ApprovalStateBuilder.cs` — fluent builders using existing Core models and enums
  - **Orchestrator tests** — `OrchestratorAgentTests.cs` — 8 test stubs validating existing `OrchestratorAgent` implementation (agent spawning, workflow advancement, approval enforcement, sequencing, audit logging)
  - **Approver tests** — `ApproverAgentTests.cs` — 7 simplified test stubs for future Approver implementation (approval forms, state updates, rejection flows, human edits capture, UI render mode validation)
  - **Aspire fixture** — `AgentRuntimeFixture.cs` — simplified stub for Phase 2 Aspire integration (no IAsyncLifetime for now)
  - **Key discoveries:**
    - Orchestrator already partially implemented with `SpawnAgentAsync`, `AdvanceWorkflowAsync`, and `DraftAsync` methods
    - Existing contracts: `SpawnAgentRequest/Response`, `AdvanceWorkflowRequest/Response`, `OrchestratorRequest/Response`
    - Core models exist: `WorkflowState`, `ApprovalState`, `WorkflowPhase`, `WorkflowStatus`, `ApprovalStatus` enums
    - `ApprovalState.WorkflowPhase` is `string`, not enum; `Edits` property (not `Reason`)
    - `WorkflowState` properties are all `init`-only, uses `DateTime` (not `DateTimeOffset`)
  - **Pre-existing build issues (NOT caused by tests):**
    - CS0122: `Logger` property in `AgentBase<TRequest, TResponse>` is inaccessible (constructor param, not protected property) — OrchestratorAgent tries to use it in lines 86, 101, 117
    - CS8858: `WorkflowState` is not a record — OrchestratorAgent uses `with` syntax on line 128 (invalid)
    - These are implementation bugs in `src/Hackmum.Bethuya.Agents/Implementations/OrchestratorAgent.cs` that need Tank/Trinity fixes
  - **Test design decisions:**
    - All tests marked `[Ignore("Awaiting implementation — Phase 1")]` until DB integration and src bugs fixed
    - Used existing contracts from `Hackmum.Bethuya.Agents.Contracts` namespace (not placeholder interfaces)
    - Tests validate behavior, not just structure — acceptance criteria from `.squad/designs/event-orchestration-agents.md`
    - Simple placeholder types for `AgendaDraft` and `SessionSlot` in Approver tests (no Approver implementation exists yet)
    - No immediate Aspire integration in tests — Phase 2 work for Tank/Trinity


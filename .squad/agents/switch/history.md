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


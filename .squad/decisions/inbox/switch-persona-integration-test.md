# Decision: Layer 2 Integration Test Design — DevelopmentPersonaSwitchingFlowTests.cs

**Author:** Switch (Tester)  
**Date:** 2026-08-04  
**Branch:** `indcoder-developer-identity-switching`  
**Related:** PR #52 (Layer 2: Developer Identity Switching Infrastructure)

## Context

Tank's Layer 2 implementation included 19 TUnit unit tests in `Hackmum.Bethuya.Tests`
proving persona catalog, three-way resolution, and Farah-vs-Vikram policy differences
against an in-process `TestServer`. The Layer 2 spec required Switch to add one additional
deliverable: a full Aspire+Postgres integration test proving the same behavior against
the **real Aspire-orchestrated Backend** (not TestServer), including a persisted
`Decision.DecidedBy` assertion.

## Decisions Made

### 1. Follow the Layer 1 fixture pattern exactly (no DatabaseFixture)

`DevelopmentAuthenticationFlowTests.cs` (Layer 1) uses `[ClassDataSource<BethuyaAppFixture>(Shared = SharedType.PerTestSession)]` directly without `DatabaseFixture`. The Layer 2 tests follow this exact pattern. Rationale: the seeded tests create fresh data with unique IDs per run; no cross-test state pollution risk requiring Respawn between these specific tests.

### 2. Use `/api/dev/curation/seed` for seed data, not direct EF/Npgsql injection

The seeder endpoint already exists and produces consistent, realistic data. Using it avoids tight coupling to the EF schema/model directly from the test project. The seeder is a dev-only endpoint (`/api/dev/...`) that the test can call without any persona header.

### 3. Prove `Decision.DecidedBy` via `GET /api/approvals/registration/{id}`, not DB query

`ApprovalEndpoints.cs` exposes `GET /api/approvals/{entityType}/{entityId}` with no `RequireAuthorization`. After applying a Vikram decision, reading it back via this endpoint is the cleanest proof, avoids raw Npgsql/SQL coupling, and aligns with the "test via HTTP, not DB" principle where possible.

### 4. BP6 constant duplication: hardcode header name and persona values

Per BP6 (Contract duplication as safety net), persona header name (`X-Bethuya-Dev-Persona`), subjects, emails, and role names are hardcoded in the test file — not imported from `ServiceDefaults.Auth.DevelopmentPersonaCatalog`. A rename in ServiceDefaults that breaks this test file is a compile-time signal of a breaking API change.

### 5. Structured log verification scoped to unit tests only (not this integration tier)

`BethuyaAppFixture` provides no log-capture seam. Adding one would be new test infrastructure beyond Layer 2 scope. Log verification for `LoggerMessage.Define` EventId 3100/3101 is documented as Tank's responsibility, verified via fake-ILogger unit tests in `DevelopmentPersonaSwitchingTests.cs`. This decision is explicitly noted in the test file header comment.

### 6. Execution status: build confirmed, runtime requires Docker

Docker 29.6.2 was found available in the sandbox. The build succeeded (0 errors, 0 warnings). Test execution was not run as part of the authoring commit (Switch's deliverable is the buildable, correct test file). An independent CI run with `dotnet test tests/Bethuya.IntegrationTests` should succeed when the Aspire Postgres container starts cleanly.

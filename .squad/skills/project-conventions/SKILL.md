---
name: "project-conventions"
description: "Core conventions and patterns for this codebase"
domain: "project-conventions"
confidence: "medium"
source: "template"
---

## Context

This project is a .NET 10 + Aspire + Blazor (`InteractiveServer` sensitive flows) codebase with TUnit + Playwright for .NET testing.

## Patterns

### Blazor JS Interop (Critical Flows)

- For critical upload/auth-like `InteractiveServer` paths, load scripts from app shell (`App.razor`) and invoke global functions via `IJSRuntime`.
- Avoid runtime `import()` of RCL `_content/...` assets in hot user flows.
- Keep stable `data-test` selectors for upload inputs and error surfaces to support Playwright diagnostics.

### Error Handling

- Triage UI generic errors by checking Aspire `backend` and `web` console logs first.
- Prefer root-cause fixes at failing layer (backend/interop) over masking messages in UI.
- Do not add silent fallbacks; keep explicit error messages and logs.

### Data Access / Transactions

- When SQL retry strategy is enabled, any explicit `BeginTransactionAsync` unit must execute inside:
  - `dbContext.Database.CreateExecutionStrategy().ExecuteAsync(...)`
- Keep create/update attach-marking work in one retriable transaction unit.

### Testing

- Unit/integration: TUnit project under `tests/Hackmum.Bethuya.Tests`.
- E2E: Playwright for .NET under `tests/Hackmum.Bethuya.E2E`.
- Set `BETHUYA_BASE_URL` explicitly for E2E runs targeting a live app URL.
- Add one seam-focused regression test for each fixed integration boundary (e.g., script load, publish transaction path).

### Code Style

- C# 14 conventions, file-scoped namespaces, nullable enabled, warnings-as-errors.
- Refit contracts for API communication; Vogen value objects for IDs in domain layers.

### File Structure

- `src/Hackmum.Bethuya.Backend` — minimal APIs/endpoints.
- `src/Bethuya.Hybrid/Bethuya.Hybrid.Shared` — shared Razor pages/components and frontend JS assets.
- `tests/Hackmum.Bethuya.Tests` — TUnit backend/domain tests.
- `tests/Hackmum.Bethuya.E2E` — Playwright end-to-end checks.

## Examples

```
// Transaction pattern under retry strategy:
var strategy = dbContext.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
    // write operations
    await tx.CommitAsync(ct);
});
```

## Anti-Patterns

- **Runtime JS module import in critical InteractiveServer flows** — brittle `_content` loading seams.
- **User transactions outside execution strategy when retries are active** — causes runtime `InvalidOperationException`.
- **Manual-only verification for integration fixes** — misses regressions that Playwright can catch quickly.

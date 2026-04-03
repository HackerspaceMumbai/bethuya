name trinity-blazor-rendering
description >
  Blazor specialist skill: apply render-mode policy for Blazor Web, maintain
  Hybrid parity, and keep the shared RCL host-agnostic. Ensure offline-heavy
  forms are safe and testable.
domain frontend
confidence high
source manual

# Trinity Blazor Rendering

## When to use

Use this skill for:

- Changes in Blazor Web UI, layout, routing, or render modes
- Offline-heavy or multi-step forms
- Shared Razor components in the RCL
- UI changes requiring Playwright selectors

## Rules

### 1) Render modes are Web-only

- Never implement render-mode logic inside Hybrid code paths.
- Never add render-mode branching inside the shared RCL.

### 2) Sensitive pages must be server-interactive in Web

Treat these as server-interactive only:

- auth and login
- organizer/admin surfaces
- agent-control panels
- any pages that show or process PII

### 3) Offline-heavy forms may use WASM in Web

Allowed:

- multi-step forms that must survive connectivity loss
- local drafts with later sync using browser storage (IndexedDB) in the WASM host

Not allowed:

- moving sensitive flows to WASM
- placing IndexedDB logic in the shared RCL

### 4) RCL boundaries

Allowed in RCL:

- UI components, validation, interfaces, models

Forbidden in RCL:

- IndexedDB or SQLite implementations
- HttpContext usage
- auth provider wiring

## Testing requirements

- Prefer `data-test` selectors for stable Playwright tests.
- When changing UI semantics, update or add E2E coverage.

## Output format

1. Render-mode decision with rationale
2. RCL boundary check results
3. Offline approach (draft storage and sync)
4. Test plan (unit + E2E)

## Authentication and authorization

- Use `IBethuyaAuthStateProvider` abstraction.
- Do not assume a browser-only context. Resolve identity via the provider for
  both ASP.NET and MAUI hosts.

## Shared asset resolution

- CSS and JS must use the RCL static asset path via `_content/...`.
- Verify file existence in the Shared project's `wwwroot` before referencing.
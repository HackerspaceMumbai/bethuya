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
- any pages that show, collect, or process PII

PII handling requirement:

- If a multi-step form handles PII, do not keep drafts only in WASM storage.
- Either host the page as `InteractiveServer` with server-side draft persistence, or sync every PII draft change immediately to the server via a draft API (for example `IRegistrationDraftService`) before any sensitive processing or external redirects.

### 3) Offline-heavy forms may use WASM in Web

Allowed:

- multi-step forms that must survive connectivity loss
- local drafts with later sync using browser storage (`IndexedDB`) in the `WASM` host, only for non-PII data

Not allowed:

- moving sensitive flows to WASM
- storing PII drafts only in `IndexedDB` or the shared RCL
- placing `IndexedDB` logic in the shared RCL

Rule note:

- `WASM` + local storage is allowed only for non-PII drafts.
- Any form that collects or processes PII must either sync drafts immediately to the server (for example via `IRegistrationDraftService`) or run as `InteractiveServer` so persistence stays server-side.

### 4) RCL boundaries

Allowed in RCL:

- UI components, validation, interfaces, models

Forbidden in RCL:

- IndexedDB or SQLite implementations
- HttpContext usage
- auth provider wiring

### 5) Async onboarding states must stay truthful

- Keep card/page structure stable across empty, loading, connected, mixed, and error states.
- Do not reuse "not connected" copy while async host data is still loading.
- Disable OAuth launches and draft edits when a blocking load error means the saved state is unknown.
- Prefer reserving layout space with placeholders so actions stay aligned while status text changes honestly.

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

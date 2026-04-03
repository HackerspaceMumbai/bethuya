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
- Any changes in Blazor Web UI, layout, routing, or render modes
- Offline-heavy or multi-step forms
- Shared Razor components in the RCL
- UI changes requiring Playwright selectors

## Rules
### 1) Render modes are Web-only
- Never implement render-mode logic inside Hybrid code paths.
- Never add render-mode branching inside the shared RCL.

### 2) Sensitive pages must use InteractiveServer render mode in Web
Treat these as InteractiveServer (or InteractiveAuto with server prerendering) only:
- auth and login
- organizer/admin surfaces
- agent-control panels
- any pages that show or process PII
### 3) Offline-heavy forms may use WASM in Web
Allowed:
- multi-step forms that must survive connectivity loss, **only when the form flow never requires a server-side authentication handoff** (i.e., no OAuth redirect mid-flow)
- local drafts with later sync using browser storage (e.g., IndexedDB) in the consuming WASM app

Not allowed:
- moving sensitive flows to WASM
- **using browser storage (IndexedDB) for flows that involve auth redirects** — OAuth redirects destroy the Blazor WASM circuit and all in-memory state; browser storage offers no guarantee of survival across redirect-based auth flows
- placing IndexedDB logic in the RCL (see Rule 4)

**Auth-redirect rule:** If a form flow can trigger an external OAuth redirect (e.g., Auth0 login), **prefer server-side persistence** (e.g., `IRegistrationDraftService`) to save draft state before the redirect so it survives circuit destruction. Do not rely on browser storage or WASM in-memory state for these flows.

### 4) RCL boundaries
Allowed in RCL:
- UI components, validation, interfaces, models
Forbidden in RCL:
- IndexedDB or SQLite implementations — **any IndexedDB usage must live in the consuming WASM app only, not in the shared RCL**
- HttpContext usage
- auth provider wiring

## Testing requirements
- Prefer data-test selectors for stable Playwright tests.
- When changing UI semantics, update or add E2E coverage.

## Output format
1. Render-mode decision with rationale
2. RCL boundary check results
3. Offline approach (draft storage and sync)
4. Test plan (unit + E2E)

## Authentication & Authorization
- Use `IBethuyaAuthStateProvider` abstraction. 
- **Instruction:** "When writing Auth logic, never assume a Browser context. Use the provider to resolve identity regardless of whether the host is ASP.NET or MAUI."

## Shared Asset Resolution
- CSS and JS must use the RCL path: `_content/HackerspaceMumbai.Bethuya.Shared/`.
- Agents must verify file existence in `wwwroot` of the Shared project before referencing.
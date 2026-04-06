# Shared RCL boundaries

## Allowed in the shared RCL

- Razor UI components
- Validation and stateless helpers
- Interfaces and models

## Forbidden in the shared RCL

- `HttpContext` usage
- Direct storage implementations (IndexedDB, SQLite, files)
- Render-mode checks or branching logic
- Auth provider wiring (Auth0, Keycloak, Entra specifics)

## Host responsibilities

- **Web (WASM):** IndexedDB draft store is allowed **only** for
  `InteractiveWebAssembly` render mode, and the IndexedDB implementation must be
  registered in the consuming WASM host via DI.
- **Web (InteractiveServer):** Use a server-side persistence layer (for example,
  `IRegistrationDraftService`) for draft saves, especially before external OAuth
  redirects. External OAuth redirects can destroy the Blazor circuit, so draft
  state must be persisted server-side before redirect.
- **Draft-store abstraction:** Define and depend on a common interface (for
  example, `IOfflineDraftStore<T>` or a registration-draft-specific contract) in
  the shared RCL. Wire the concrete implementation per host/render mode via DI:
  WASM -> IndexedDB implementation in the WASM app, InteractiveServer ->
  server-side persistence implementation in the server app.
- **Hybrid (MAUI):** Provide SQLite/Preferences draft store via DI.

## Hosts

- Web (Server/WASM): provide IndexedDB draft store for WASM pages.
- Hybrid (MAUI): provide SQLite/Preferences draft store via DI.

**References:**

- [Circular Dependencies: ServiceDefaults ↔ Shared RCL](../../tasks/lessons.md#L61) — why ServiceDefaults cannot reference Bethuya.Hybrid.Shared
- [Render Mode Strategy for Shared RCL Pages](../../tasks/lessons.md#L105) — why `<Routes @rendermode>` (not per-page) in Blazor Web App

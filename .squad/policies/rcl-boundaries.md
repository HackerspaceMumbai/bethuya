# Shared RCL Boundaries

Allowed: Razor UI, validation, stateless helpers, service interfaces.
Forbidden: HttpContext, direct storage (IndexedDB/SQLite/files), render-mode checks, auth provider code.

Hosts:

- Web (Server/WASM): provide IndexedDB draft store for WASM pages.
- Hybrid (MAUI): provide SQLite/Preferences draft store via DI.

**References:**
- [Circular Dependencies: ServiceDefaults ↔ Shared RCL](../../tasks/lessons.md#L61) — why ServiceDefaults cannot reference Bethuya.Hybrid.Shared
- [Render Mode Strategy for Shared RCL Pages](../../tasks/lessons.md#L105) — why `<Routes @rendermode>` (not per-page) in Blazor Web App

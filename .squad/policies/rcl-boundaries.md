# Shared RCL Boundaries

Allowed: Razor UI, validation, stateless helpers, service interfaces.
Forbidden: HttpContext, direct storage (IndexedDB/SQLite/files), render-mode checks, auth provider code.

Hosts:

- Web (Server/WASM): provide IndexedDB draft store for WASM pages.
- Hybrid (MAUI): provide SQLite/Preferences draft store via DI.

References: Hybrid+Web+RCL guidance.

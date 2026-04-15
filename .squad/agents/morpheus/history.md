# Project Context

- **Owner:** Augustine Correa
- **Project:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Created:** 2026-03-21T11:50:19.882Z

## Learnings

- Bethuya currently exposes Entra, Auth0, and Keycloak provider branches and must keep login/auth/PII pages on `InteractiveServer`.
- **AI coding primitives updated (2026-04-09):** Augustine updated README.md, AGENTS.md, and .github/copilot-instructions.md. Key security/privacy conventions absorbed:
  - **InteractiveServer for all sensitive pages** — auth/login/PII/organizer/agent control pages MUST use `@rendermode InteractiveServer` (global assignment on Routes in App.razor). WASM code is client-inspectable; sensitive pages must be server-side only.
  - **Foundry Local for all PII** — Curator Agent processes attendee PII exclusively via Foundry Local (on-device, offline). PII never reaches any cloud endpoint. Only consented DEI fields used; never infer sensitive traits.
  - **PII routing guardrail** — all attendee data routed via Foundry Local. Non-sensitive orchestration uses Microsoft Foundry or Azure OpenAI.
  - **Vogen IDs prevent PII leaks** — AttendeeId, EventId, UserId are Vogen structs (zero-allocation), not raw Guid/int. Ensures type safety on sensitive data boundaries.
  - **Security headers enforced** — CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy via `app.UseSecurityDefaults()` from ServiceDefaults.
  - **Rate limiting for AI calls** — 100 req/min (general), 20 req/min for `RateLimitPolicies.Ai` (sensitive operations).
  - **Nullable enabled, TreatWarningsAsErrors** — fix all warnings; never suppress without documented justification.
   - **Responsible disclosure** — see SECURITY.md; Dependabot weekly NuGet + Actions updates; `dotnet list package --vulnerable` blocks CI on CVEs.

## Session Work Log: Dev-Mode Auth Boundary Fix (2026-04-XX)

**Incident:** Navigation to `/registration/mandatory` failed with `IAuthenticationService` errors in dev mode (AuthProviderType.None).

**Root Cause:** Auth boundary gap in dev mode. `DevelopmentAuthenticationStateProvider` (frontend) creates fake authenticated state for [Authorize] Blazor pages, but Refit clients (separate HTTP calls) had no auth headers to send to backend APIs. Backend called `context.User.Identity?.IsAuthenticated` and failed because incoming requests were unauthenticated.

**Minimal Safe Fix (implemented & verified):**

1. **Frontend: `DevelopmentAuthorizationHandler.cs`** — HTTP message handler (21 lines)
   - Injects "Bearer dev-token" into all outgoing Refit requests when `authOptions.Provider == AuthProviderType.None`
   - Registered conditionally in Program.cs only in dev mode
   - Wired into IEventApi and IProfileApi Refit clients via `AddHttpMessageHandler()`

2. **Backend: `DevelopmentAuthenticationMiddleware.cs`** — Middleware + extension (58 lines)
   - Accepts dev bearer token (`"Bearer dev-token"`) and validates
   - Creates dev principal with identical claims structure to frontend (sub, name, email, roles)
   - Allows backend logic (ProfileEndpoints GetUserId, claim lookups) to work without modification
   - Registered conditionally: `app.UseDevelopmentAuthentication(app.Environment.IsDevelopment());` before real auth

3. **Program.cs changes** (6 lines total)
   - Frontend: Added using, registered handler, wired into Refit clients
   - Backend: Added using, added middleware to pipeline

**Security Properties:**
- Dev token is **hardcoded, local-only** — never reaches production (dev mode only)
- Frontend handler conditional: only injects token when `AuthProviderType.None`
- Backend middleware conditional: only active in `IsDevelopment()`
- Claim parity with frontend ensures ProfileEndpoints logic unchanged
- No breaking changes to real auth flows (Entra/Auth0/Keycloak branches unaffected)

**Verification:**
- Build: ✅ 0 errors, 0 warnings
- Tests: ✅ 103 passed (including DevelopmentAuthenticationTests: WebAuthentication_NoneProvider_AllowsAuthorizedEndpoint, ApiAuthentication_NoneProvider_UsesDevelopmentUserForProfileStatus, ApiAuthentication_NoneProvider_AllowsSavingMandatoryProfile)
- End-to-end: Ready for manual verification (start Aspire, navigate to /registration/mandatory, submit form)

**Remaining Work:**
- [ ] Manual E2E: Start Aspire, navigate `/registration/mandatory`, verify form submission succeeds
- [ ] Search codebase for other IAuthenticationService dependencies or Refit client injection points that may need similar fix
- [ ] Update tasks/todo.md task status to "done"
- [ ] Consider: should dev-token be configurable via appsettings instead of hardcoded? (Low priority for now)

---

## Session Work Log: Transitive Dependency Security Boundary (2026-04-15)

**Scope:** Dependency vulnerability remediation for `Bethuya.MigrationService` NU1901 gate.

**Incident:** `Bethuya.MigrationService` and `AppHost` restore were failing on NU1901 for transitive `System.Security.Cryptography.Xml` 9.0.0 through `ServiceDefaults` → `Microsoft.Identity.Web` → `Microsoft.AspNetCore.DataProtection`.

**Tank's Fix:** Applied central transitive pin in `Directory.Packages.props` to `System.Security.Cryptography.Xml` `10.0.6` (correct .NET 10 servicing line).

**Security Boundary Review (Morpheus):**
- ✅ Central transitive pinning is the correct immediate remediation mechanism for shared infrastructure dependencies
- ✅ Minimum safe version for .NET 10 repo is `10.0.6` (active advisory fix on .NET 10 train)
- ✅ Fix placement at package-management boundary keeps trust boundary clean — not scattered across leaf projects
- ✅ NU1901 gate and warnings-as-errors remain intact; no suppressions or loosenings
- ✅ Pre-existing duplicate assembly-attribute errors (CS0579) are unrelated to this fix and do not block approval
- **Risk assessment:** LOW — minimal, scoped, preserves all guardrails

**Verification:**
- Build: ✅ `dotnet restore` for both `Bethuya.MigrationService` and `AppHost` — **PASS** (no NU1901)
- Package resolution: ✅ `System.Security.Cryptography.Xml` → `10.0.6`
- Vulnerability scan: ✅ **CLEAN**
- Reviewers: Code review clean; Morpheus approved security boundary

**Decision recorded:** `.squad/decisions.md` (merged from inbox, 2026-04-15)


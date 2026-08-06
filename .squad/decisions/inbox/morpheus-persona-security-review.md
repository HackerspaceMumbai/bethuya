# Security Review: Developer Persona Identity Switching (Layer 2)

**Date:** 2026-08-04
**Reviewer:** Morpheus (Security Engineer)
**PR:** #52 — `feat(auth): Layer 2 - Developer Identity Switching Infrastructure`
**Commit:** `3c793da`
**Branch:** `indcoder-developer-identity-switching`
**Status:** ✅ APPROVED WITH FIXES (3 total fixes)

---

## Verdict

**APPROVED WITH FIXES (3 total fixes).** One defense-in-depth defect was found and fixed by the
reviewer (commit `95b7234`). Two additional HIGH/MEDIUM findings were identified by an independent
code-review pass and fixed (see Additional Findings section below). All seven original threat-model
items pass. The implementation is principled and the three-way resolution contract is sound.

---

## Threat Model Results

### TM-1 — No privilege escalation via unknown/malformed persona key

**PASS**

Confirmed exhaustively:

| Input | Path | Outcome |
|---|---|---|
| Empty string `""` | `string.IsNullOrEmpty(personaKey)` branch | Falls through to cookie, then legacy default if cookie also absent |
| Null (header absent) | `TryGetValue` default + `FirstOrDefault()` = null → `IsNullOrEmpty` | Falls through to cookie |
| Whitespace `"   "` | Passes `IsNullOrEmpty` → `TryGet` → `IsNullOrWhiteSpace` returns true → false | Case 3: fail-closed, zero roles |
| Case-mangled valid key `"aNiSh"` | `StringComparer.OrdinalIgnoreCase` in `Dictionary` | Resolves to Anish persona (by design, documented) |
| Legacy subject `"dev-user-001"` | Not in catalog | Case 3: fail-closed ✓ (test at `Catalog_TryGet_ReturnsFalseForUnknownKeys` line 99) |
| Extremely long string | `TryGetValue` + `TryGet` → dictionary miss | Case 3: fail-closed |
| Multi-value header `[Farah, Vikram]` | `FirstOrDefault()` → `"Farah"` (first wins) | Case 2: Farah persona (both are valid; first is used — no bypass) |
| Comma-separated single-header `"Farah, Vikram"` | Full string → no catalog match | Case 3: fail-closed |
| Attacker sends `"admin"` | Not in catalog | Case 3: fail-closed ✓ |

Unknown persona principals carry exactly zero role claims (confirmed by
`CreateUnknownPersonaPrincipal_IsAuthenticatedButHasZeroRoles` and
`ApiAuthentication_UnknownPersonaHeader_FailsClosed_NoRoles_403OnAllPolicies` which asserts 403
on RequireAdmin, RequireOrganizer, RequireCurator, RequireOrganizerOrCurator, **and RequireAttendee**).

---

### TM-2 — No caller-supplied claims/roles cross the trust boundary

**PASS**

`DevelopmentAuthenticationHandler.HandleAuthenticateAsync` reads exactly two things from the
inbound request: the `X-Bethuya-Dev-Persona` header value and the `bethuya-dev-persona` cookie
value. Both surfaces carry only the **opaque persona key** — never roles, email, name, or any
claim. All claims are constructed by `DevelopmentAuthenticationDefaults.CreatePersonaPrincipal`
or `CreateUnknownPersonaPrincipal` from the immutable catalog record. No code path reads roles
or profile data off the incoming request.

Grep-verified: no `FindFirst`, `ClaimTypes`, or claim-reading code in the handler or catalog.

---

### TM-3 — Mechanism truly inert outside Development+Provider=None

**PASS**

Three independent gates confirmed:

**(a) Handler's defensive re-check (belt-and-suspenders):**
`DevelopmentAuthenticationHandler` checks both `environment.IsDevelopment()` AND
`Configuration["Authentication:Provider"] == "None"` on every request (lines 60–71).
If either fails, it returns the legacy fixed principal without reading any cookie or header.
Covered by `ApiAuthentication_ProductionEnv_PersonaHeaderIgnored_LegacyDefault`.

**(b) `DevPersonaPropagationHandler` registration:**
`Program.cs` lines 95–98 register it only when `authOptions.Provider == AuthProviderType.None
AND builder.Environment.IsDevelopment()`. Real-provider flows (`Provider != None`) skip the
entire `if (authOptions.Provider == AuthProviderType.None)` block entirely — no registration,
no DI entry, no request-time invocation.

**(c) `DevPersonaEndpointExtensions` mapping:**
`Program.cs` lines 281–284 call `MapDevPersonaEndpoints()` only when
`authOptions.Provider == AuthProviderType.None && app.Environment.IsDevelopment()`. Outside
that block the routes are never registered.

**(d) `EnsureInsecureDevAuthAllowed`:**
Unchanged — still throws `InvalidOperationException` when `Provider=None` is used outside
Development without the explicit `AllowInsecureDevAuth=true` opt-in flag.

---

### TM-4 — Cookie hygiene

**PASS**

| Attribute | Value | Assessment |
|---|---|---|
| `HttpOnly` | `true` | ✓ Cannot be read by JS |
| `SameSite` | `Lax` | Acceptable for dev. `Strict` would be marginally better (prevents cross-site GET CSRF to the persona endpoint) but the security benefit on localhost is negligible — the attacker would need to know the exact port, and the "attack" only changes the developer's active persona (no sensitive data modified). **Not a blocker.** |
| `Secure` | `context.Request.IsHttps` | Pragmatic for dev. `UseHttpsRedirection()` is active in Development (Program.cs line 250), so in practice the persona endpoint is only reachable over HTTPS after the initial redirect, meaning `IsHttps=true` when the cookie is set. |
| `Expires`/`MaxAge` | None (session cookie) | ✓ Correct — session cookies expire on browser close, preventing stale persona state from persisting across developer sessions. |

---

### TM-5 — Header/cookie value confusion and log injection

**PASS**

`DevelopmentAuthenticationHandler` reads header first, cookie second (priority order).
Confusion is not exploitable: whatever value arrives from either surface is validated against the
catalog allowlist. A cookie-injected header name or header-injected cookie name would simply be
an unknown key → Case 3 (fail-closed).

Log injection: both `LoggerMessage.Define` delegates (`s_personaResolved` and `s_personaUnknown`)
use static message templates compiled at startup. The raw `personaKey` is passed as a **typed**
`string` argument (position 0), not interpolated into the template string. Structured log sinks
store it as a property; text formatters substitute the value but cannot mutate the template.
This is the `CA1848`-compliant pattern. ✓

---

### TM-6 — `Decision.DecidedBy` claim end-to-end

**PASS**

`CurationEndpoints.cs` uses `user.FindFirst("email")?.Value`. Personas set the `"email"` claim
type (short form, same as the `ClaimsIdentity(nameType:"name", roleType:"role")` convention).
Once the Backend's `DevelopmentAuthenticationHandler` resolves a named persona (Case 2),
`HttpContext.User.FindFirst("email")` returns the catalog email (e.g. `vikram@bethuya.dev`),
which becomes `DecidedBy` in persisted `Decision` records. No gap in the chain.

End-to-end chain: Web SSR `bethuya-dev-persona` cookie → `DevPersonaPropagationHandler` reads
cookie, sets `X-Bethuya-Dev-Persona` header on Refit request → Backend
`DevelopmentAuthenticationHandler` reads header, resolves catalog principal → `HttpContext.User`
has `email=vikram@bethuya.dev` → `CurationEndpoints` derives `decidedBy`. ✓

---

### TM-7 — Existing characterization tests pass and default principal is unambiguous

**PASS**

`DevelopmentAuthenticationBoundaryTests` sends requests with **no persona header or cookie**
and asserts `sub=dev-user-001` with all four roles. These tests still pass (311/311 before fix,
314/314 after fix with 3 new guard tests).

"No persona" (Case 1) and "unknown persona" (Case 3) are genuinely distinguishable:
- Case 1: `personaKey` is null/empty → `CreatePrincipal()` → `sub=dev-user-001`
- Case 3: `personaKey` is non-empty but not in catalog → `CreateUnknownPersonaPrincipal()` → `sub=dev-persona-unknown`

They produce different principals with different subjects. `DevelopmentAuthenticationBoundaryTests`
exercises Case 1 by sending no persona header; the new
`ApiAuthentication_UnknownPersonaHeader_FailsClosed_NoRoles_403OnAllPolicies` test asserts the
different Case 3 outcome explicitly.

---

### TM-7b — Real-provider paths (Entra/Auth0/Keycloak) completely unaffected

**PASS**

For `Provider != None`, the `if (authOptions.Provider == AuthProviderType.None)` block in
`Program.cs` is entirely skipped:
- `DevPersonaPropagationHandler` is never registered in DI
- `MapDevPersonaEndpoints()` is never called
- `DevelopmentAuthenticationHandler` is never added to the auth scheme

The JWT Bearer path registers its own scheme independently. Zero overlap.

---

## Fix Applied

**Defect:** `DevelopmentEndpoints.MapDevelopmentEndpoints` had no internal environment guard.
The call-site in `Program.cs` had a `if (app.Environment.IsDevelopment())` block, but that
single guard is fragile — if it is ever inadvertently removed, the `/api/dev/curation/seed`
(data mutator, no auth) and `/api/dev/identity` (identity diagnostic, no auth) endpoints would
be mapped in production.

**Fix (defense-in-depth):** Added `if (!app.Environment.IsDevelopment()) throw InvalidOperationException`
at the entry of `MapDevelopmentEndpoints`. This mirrors the `EnsureInsecureDevAuthAllowed`
pattern already established in `BethuyaAuthenticationExtensions`.

**Proof:** Added `DevelopmentEndpointGuardTests.cs` (3 TUnit tests):
- `MapDevelopmentEndpoints_Production_Throws` — asserts throw in Production
- `MapDevelopmentEndpoints_Staging_Throws` — asserts throw in Staging
- `MapDevelopmentEndpoints_Development_DoesNotThrow` — asserts no throw in Development

**Test delta:** 311 → 314 → 321 passed (0 failed, 0 regressions).

---

## Additional Findings (Post-Review Pass)

These two findings were identified by an independent code-review pass after the initial 7-item
threat-model review. Both were fixed directly by the security reviewer per commit authority.

---

### F1 — CSRF via Cross-Origin Drive-By Cookie Plant (HIGH)

**Affected file:** `src/Bethuya.Hybrid/Bethuya.Hybrid.Web/Auth/DevPersonaEndpointExtensions.cs`

**Vector:**
`GET /dev/persona/{key}` was `.AllowAnonymous()` with no cross-origin check. `SameSite=Lax` only
restricts when the cookie is **sent back** in subsequent requests — it does NOT prevent a
cross-origin response from **setting** the cookie on the developer's browser. A hostile page open
in the same browser (e.g. a compromised npm package's dev server) could escalate the dev session
to full-admin with:

```html
<img src="http://localhost:PORT/dev/persona/Vikram">
```

No click or interaction required. This is exactly the "silent escalation to admin" scenario the
original spec said must never happen, just via a cross-origin GET vector rather than a malformed
key.

**Assessment:** HIGH severity within the dev-only threat model. The endpoint is only reachable in
`Development+Provider=None`, so production is unaffected. But within a developer's active session,
this is a meaningful trust boundary violation.

**Fix:**
Added `Sec-Fetch-Site` header check at the top of both `/{key}` and `/clear` handlers:

```csharp
var fetchSite = context.Request.Headers["Sec-Fetch-Site"].FirstOrDefault();
if (string.Equals(fetchSite, "cross-site", StringComparison.Ordinal))
    return Results.StatusCode(StatusCodes.Status403Forbidden);
```

**Rationale for `Sec-Fetch-Site` over POST+antiforgery:**
- `Sec-Fetch-Site` cannot be forged by cross-origin scripts/images/iframes — it is browser-set
  from the browser's Fetch metadata spec (RFC 8942-style)
- Absent header = direct navigation (typed URL, bookmark, curl) — the primary developer UX must
  be preserved; absent must allow
- `cross-site` is the only blocked value; `same-origin`, `same-site`, and absent all allow
- Switching to POST+antiforgery would break the `GET /dev/persona/{key}` URL-navigation UX that
  is part of the deliverable's described "redirects/reloads" flow — I judged `Sec-Fetch-Site`
  sufficient for a dev-only endpoint where the threat model is "rogue page in same browser session"

**Proof tests (added to `DevelopmentPersonaSwitchingTests.cs`):**
- `PersonaEndpoint_CrossSiteRequest_Returns403`
- `PersonaEndpoint_CrossSiteRequest_ClearAlsoReturns403`
- `PersonaEndpoint_SameOriginRequest_SetsPersonaCookieAndRedirects`
- `PersonaEndpoint_NoFetchSiteHeader_DirectNavigation_SetsPersonaCookieAndRedirects`

---

### F2 — Open Redirect via Unvalidated `returnUrl` (MEDIUM, compounds F1)

**Affected file:** `src/Bethuya.Hybrid/Bethuya.Hybrid.Web/Auth/DevPersonaEndpointExtensions.cs`

**Vector:**
The original `returnUrl` handler used `Results.Redirect(returnUrl)` without validating whether the
URL is local. An attacker could craft:

```
GET /dev/persona/Vikram?returnUrl=https://evil.example/phish
```

This both escalates the developer to admin (F1) AND redirects their browser to an attacker
page in a single request.

**Fix:**
Added `IsLocalUrl` private helper (accepts only paths starting with `/` but not `//` — blocking
both absolute URLs and protocol-relative URLs):

```csharp
private static bool IsLocalUrl([NotNullWhen(true)] string? url) =>
    !string.IsNullOrWhiteSpace(url)
    && url.StartsWith('/')
    && !url.StartsWith("//", StringComparison.Ordinal);
```

Changed the redirect to:

```csharp
var safeReturnUrl = IsLocalUrl(returnUrl) ? returnUrl! : "/";
return Results.LocalRedirect(safeReturnUrl);
```

`Results.LocalRedirect` is an additional backstop — it throws `InvalidOperationException` for
non-local URLs. The `IsLocalUrl` guard filters before reaching it so the exception path is never
hit in practice. The clear endpoint already redirected to `"/"` (literal) so that was updated to
`Results.LocalRedirect("/")` for consistency.

**Proof tests (added to `DevelopmentPersonaSwitchingTests.cs`):**
- `PersonaEndpoint_AbsoluteReturnUrl_RedirectsToSafeRoot`
- `PersonaEndpoint_ProtocolRelativeReturnUrl_RedirectsToSafeRoot`
- `PersonaEndpoint_ValidLocalReturnUrl_RedirectsToIt`

---

## Build / Anvil Evidence (Morpheus-run — Final)

```
dotnet build Bethuya.slnx
  Build succeeded — 0 Warning(s), 0 Error(s)

dotnet test tests/Hackmum.Bethuya.Tests
  Before initial fix:             311/311 passed
  After Fix #1 (env guard):       314/314 passed (+3 guard tests)
  After Fix #2+3 (CSRF, redirect): 321/321 passed (+7 new endpoint security tests)
```

---

## Agents Who Need to Know

- **Neo** — architecture review still outstanding. The three-way contract is sound; the guard
  fix is a small surgical addition. No architectural change to the layering.
- **Switch** — integration test layer (Farah→403 vs Vikram→200 with persisted Decision.DecidedBy)
  is still out of scope for this PR per Tank's decision record.
- **Scribe** — please merge this decision record into `.squad/decisions.md`.

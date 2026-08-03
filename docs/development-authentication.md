# Development Authentication — Current Architecture (Characterization)

> **Status:** Characterization only. This document describes the authentication and
> authorization architecture **as it exists today** (`Authentication:Provider=None`,
> local development). It records a known defect (fixed shared admin identity) so it can be
> used as the baseline for a future persona-switching design. **It does not implement, and
> is not a prerequisite for approving, any persona switching, toolbar, seeder, new roles, or
> v2 domain changes.** Those are out of scope for this layer (PR1 of the Developer Testing
> Harness stack) and are tracked separately — see the proposed boundary recorded in
> `.squad/decisions/inbox/copilot-dev-persona-auth.md`.

## Why this document exists

Before any persona-switching infrastructure is designed, the team needs a precise,
verifiable description of the current dev-auth path and its authorization boundaries —
including the parts that don't behave the way a casual reading of the code might suggest.
The single most important fact in this document is:

> **When `Authentication:Provider=None`, the Web host and the Backend host each
> independently manufacture the same fixed principal (`dev-user-001` /
> `dev@bethuya.local`, holding *all four* platform roles). No identity or persona
> information is transmitted between them at all.** The Backend's authorization
> decisions are never actually influenced by anything the Web tier believes about the
> current user, because the Web tier attaches no token and no persona context to
> outbound Refit calls in this mode.

## End-to-end path

```
Browser
  │  (renders Blazor Web App, InteractiveServer)
  ▼
Web host — Bethuya.Hybrid.Web (Program.cs)
  │
  ├─ builder.AddBethuyaWebAuthentication()
  │     Authentication:Provider == None
  │       → registers ServiceDefaults.Auth.DevelopmentAuthenticationHandler
  │         under scheme "Development" (BethuyaAuthenticationExtensions.cs)
  │
  ├─ AuthenticationStateProvider registration (Program.cs, line ~85-91)
  │     Authentication:Provider == None
  │       → Bethuya.Hybrid.Web.Auth.DevelopmentAuthenticationStateProvider
  │         (returns a *cached, static* AuthenticationState built once from
  │          DevelopmentAuthenticationDefaults.CreatePrincipal() — every
  │          Blazor circuit sees the exact same ClaimsPrincipal instance)
  │
  ├─ ICurrentUserService registration
  │     Authentication:Provider == None → ClaimsCurrentUserService
  │       (reads sub/email/role claims off HttpContext.User — for the
  │        Development scheme this is always the fixed dev principal)
  │
  ▼
Refit typed clients (IEventApi, IProfileApi, ICurationApi, …)
  │
  ├─ ConfigureBackendAuth(...) (Program.cs, line ~106-114)
  │     Authentication:Provider == None
  │       → BackendAccessTokenHandler is *not* registered on the HttpClient
  │         pipeline. No Authorization header, no persona header, no claim
  │         of any kind is attached to the outbound HTTP request.
  │       → base address uses the flexible "https+http://backend" Aspire
  │         service-discovery scheme (HTTPS is not force-required because
  │         there is no bearer token to protect in transit)
  │
  ▼
Backend host — Hackmum.Bethuya.Backend (independent ASP.NET Core service)
  │
  ├─ builder.AddBethuyaApiAuthentication()
  │     Authentication:Provider == None
  │       → registers the *same* ServiceDefaults.Auth.DevelopmentAuthenticationHandler
  │         under the "Development" scheme — this is a wholly separate handler
  │         instance from the Web host's; the two hosts never share state.
  │
  ├─ DevelopmentAuthenticationHandler.HandleAuthenticateAsync()
  │       → Every single inbound request (regardless of headers, cookies, or
  │         absence thereof) succeeds authentication and is assigned
  │         DevelopmentAuthenticationDefaults.CreatePrincipal():
  │           sub   = "dev-user-001"
  │           name  = "Dev User"
  │           email = "dev@bethuya.local"
  │           role  = Admin, Organizer, Curator, Attendee   (all four, always)
  │
  ▼
ASP.NET Core authorization policies (BethuyaAuthorizationExtensions.cs)
  │
  ├─ RequireAdmin, RequireOrganizer, RequireCurator,
  │  RequireOrganizerOrCurator, RequireAttendee
  │       → All of these evaluate true for the fixed dev principal because it
  │         holds every role simultaneously. In Provider=None, role-gated
  │         endpoints (e.g. CurationEndpoints [RequireOrganizerOrCurator],
  │         CommunityPassportEndpoints admin routes [RequireOrganizer],
  │         MentorshipEndpoints [RequireAttendee]) cannot meaningfully be
  │         exercised as different personas — there is only ever one identity,
  │         and it always passes every role check.
  │
  ▼
Audit / Decision record path (Hackmum.Bethuya.Core.Models.Decision)
        → Decision.DecidedBy IS derived from the authenticated ClaimsPrincipal at
          the three live call sites that create Decisions today:
            - CurationEndpoints.cs: decidedBy = user.FindFirst("email")?.Value
              ?? user.Identity?.Name ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
              ?? "curation-ui"
            - MentorshipEndpoints.cs → MentorshipService.cs: requestedBy =
              user.GetSubject().Email ?? ...DisplayName ?? ...UserId
            - CommunityPassportEndpoints.cs → CommunityRecommendationService.cs:
              same GetSubject() pattern
          In Provider=None this always resolves to the fixed dev principal's
          claims (dev@bethuya.local / dev-user-001) — reinforcing, not
          contradicting, the "one fixed identity everywhere" narrative above.
          The only method that accepts an arbitrary caller-supplied
          `decidedBy` string with no claims derivation is
          ApprovalWorkflow.CreateDecision(string decidedBy, ...), which has
          **no callers anywhere in `src/` today** — it is presently dead code,
          not an active audit-trail weakness. The narrower, real gap is that
          ApprovalEndpoints (`/api/approvals/**`) has no
          `RequireAuthorization()` policy at all, so the Approve/Reject
          actions it exposes are not role-gated even though they mutate
          Decision.Status. This is a pre-existing characteristic being
          documented here, not something introduced or fixed by this layer.
```

## Key files

| Concern | File |
|---|---|
| Shared fixed dev principal | `ServiceDefaults/Auth/DevelopmentAuthenticationDefaults.cs` |
| Backend "authenticate everyone" handler | `ServiceDefaults/Auth/DevelopmentAuthenticationHandler.cs` |
| Web/Backend auth provider selection | `ServiceDefaults/Auth/BethuyaAuthenticationExtensions.cs` |
| Web Blazor auth state (dev mode) | `src/Bethuya.Hybrid/Bethuya.Hybrid.Web/Auth/DevelopmentAuthenticationStateProvider.cs` |
| Web `ICurrentUserService` (real claims reader) | `src/Bethuya.Hybrid/Bethuya.Hybrid.Web/Auth/ClaimsCurrentUserService.cs` |
| Token-forwarding handler (real providers only) | `src/Bethuya.Hybrid/Bethuya.Hybrid.Web/Auth/BackendAccessTokenHandler.cs` |
| Refit client wiring / provider branching | `src/Bethuya.Hybrid/Bethuya.Hybrid.Web/Program.cs` (see `ConfigureBackendAuth`) |
| Authorization policies | `ServiceDefaults/Auth/BethuyaAuthorizationExtensions.cs` |
| Canonical role names | `ServiceDefaults/Auth/BethuyaRoleNames.cs` |
| Environment/provider fail-closed guard | `BethuyaAuthenticationExtensions.EnsureInsecureDevAuthAllowed` |

## The fixed-admin defect, precisely stated

1. **Single identity, all roles, always.** `DevelopmentAuthenticationDefaults.CreatePrincipal()`
   attaches all four `BethuyaRoleNames` (`Admin`, `Organizer`, `Curator`, `Attendee`) to
   one static `ClaimsPrincipal`. There is no configuration knob, header, cookie, or query
   parameter that changes this. Every request authenticated via the `Development` scheme —
   on the Web host or the Backend host — gets exactly this principal.
2. **The two hosts do not communicate identity.** The Web host's
   `DevelopmentAuthenticationStateProvider` and `ClaimsCurrentUserService` describe what the
   *Blazor UI* believes about "the current user," but because `ConfigureBackendAuth` never
   attaches `BackendAccessTokenHandler` when `Provider=None`, none of that belief is ever
   transmitted to the Backend. The Backend's `DevelopmentAuthenticationHandler` authenticates
   purely from its own local logic — it would produce the identical fixed principal even if
   the Web host, or a `curl`/Postman client, sent no request headers at all, garbage headers,
   or headers claiming to be a different user.
3. **Therefore "switching personas" today is impossible, and dangerous to fake.** Any
   attempt to make the Web UI merely *display* a different persona (e.g. a client-side
   toggle) would create a **false sense of security/testing coverage**: the Backend would
   continue to authorize every request as the fixed admin regardless of what the toolbar
   claims, silently defeating the point of persona-based test coverage. This is the central
   reason a future persona-switching layer must change the Backend's authentication
   decision, not just the Web UI's display state — see the proposed decision in
   `.squad/decisions/inbox/copilot-dev-persona-auth.md`.
4. **The fail-closed guard is a separate, already-hardened control.**
   `EnsureInsecureDevAuthAllowed` in `BethuyaAuthenticationExtensions` throws
   `InvalidOperationException` if `Authentication:Provider=None` is selected outside the
   `Development` `IHostEnvironment` **unless** `Authentication:AllowInsecureDevAuth=true` is
   explicitly set. This prevents the fixed-admin defect described above from silently
   reaching a non-Development deployment. It is orthogonal to (and does not mitigate) the
   Web/Backend identity-decoupling issue above, which is present in every Provider=None
   environment, Development included.

## Authorization-boundary implications for a persona-switching design (informational only)

These are observations to inform the *future* design; no behavior described in this section
exists in the codebase today.

- Any persona switch must be enforced by changing what the **Backend** authenticates the
  caller as — a Web-only UI toggle without a corresponding Backend change would not test
  anything real about authorization boundaries, per point 3 above.
- The `ApprovalEndpoints` missing-`RequireAuthorization()` gap (see "Audit / Decision record
  path" above) exists independently of persona switching and should be tracked as a separate
  hardening item; a persona-switching harness must not be assumed to fix or paper over it. In
  Provider=None, `Decision.DecidedBy` at the three live call sites already resolves from
  claims, so it will correctly reflect whichever fixed persona a future design authenticates
  as — it does not need to be redesigned as part of persona switching itself.
- The current architecture is a genuine "Provider=None ⇒ full admin" development
  convenience, not a partial or role-scoped fake identity. Any future design that wants to
  test non-admin behavior must explicitly construct a fixed, allowlisted, lesser-privileged
  principal server-side — it cannot start from "remove some claims from the existing admin
  principal" in the Web tier, because the Web tier's belief about identity is not consulted
  by the Backend at all.

## Explicitly out of scope for this layer (PR1)

- Persona switching UI/toolbar
- A persona/seed data catalog or seeder
- New roles beyond the existing `Admin` / `Organizer` / `Curator` / `Attendee`
- Any propagation mechanism (header, claim, cookie) between Web and Backend for dev personas
- v2 domain modeling

These are reserved for a later layer once this characterization has been reviewed by Neo
(architecture) and Morpheus (security/auth boundaries).

## Test evidence for this characterization

- `tests/Hackmum.Bethuya.Tests/Auth/DevelopmentAuthenticationBoundaryTests.cs` — TUnit
  characterization tests, run against an in-process `TestServer`, proving: the fixed principal
  always carries all four roles; the Backend ignores any client-supplied `Authorization`
  header when resolving identity; a single fixed principal satisfies every role-gated policy
  (`RequireAdmin`, `RequireOrganizer`, `RequireCurator`, `RequireAttendee`) simultaneously; and
  there is no reachable "unauthenticated"/anonymous state via the `Development` scheme.
- `tests/Bethuya.IntegrationTests/DevelopmentAuthenticationFlowTests.cs` — exercises the real,
  Aspire-orchestrated Backend (with Postgres) directly. **Scope limitation:** this integration
  project references only `AppHost` and `BethuyaAppFixture` exposes an `HttpClient` only for
  the `backend` resource — there is no seam to drive the Blazor `web` resource's rendered UI or
  its Refit calls from this test project without adding new test-only wiring, which would
  itself be "switching infrastructure" and is out of scope for PR1. These tests therefore
  characterize the Backend-observed identity/policy outcome directly: one client writes a
  marked `AttendeeProfile` while presenting a bogus, attendee-claiming bearer token, and a
  second client with no credentials at all reads the same marked data back via
  `GET /api/profile` — a falsifiable proof that both requests resolved to the identical
  backend-authenticated `dev-user-001` principal, not merely that two responses happened to
  look alike. A second test confirms policy authorization succeeds for the fixed principal
  against an existing `RequireOrganizerOrCurator`-gated endpoint. This is precisely what the
  Web tier's identity belief cannot influence today per point 2 above. This test requires a
  container runtime (Docker) for the Aspire-managed Postgres resource; it was built
  successfully in this session but could not be executed in this sandboxed environment (no
  Docker daemon available) — see `tasks/lessons.md` (2026-08-04 entry) for details. It is also
  not currently wired into `.github/workflows/ci.yml` (pre-existing gap, not introduced here).


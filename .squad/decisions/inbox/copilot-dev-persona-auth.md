### 2026-08-04: Proposed dev-persona authentication boundary (future layer, not implemented in PR1)

**By:** Copilot (Developer Auth Review layer, working in Neo/Morpheus review boundary)

**What:** This is a *proposal* for the boundary a future persona-switching layer (PR2+ of the
Developer Testing Harness stack) must respect. Nothing in this decision has been implemented;
PR1 is characterization-only (see `docs/development-authentication.md`).

Current state (characterized, not changed): `Authentication:Provider=None` causes both the Web
host and the Backend host to independently authenticate every request as one static, fixed
principal (`dev-user-001` / `dev@bethuya.local`) holding all four roles (`Admin`, `Organizer`,
`Curator`, `Attendee`) simultaneously. The Web tier attaches no token or persona information to
outbound Refit calls in this mode, so the Backend's authorization decisions are never actually
influenced by anything the Web UI displays or believes about "the current user."

Proposed boundary for the future persona-switching design:

1. **One fixed, allowlisted persona key per request** — not a client-supplied arbitrary
   claim set. The persona is selected from a small, closed enumeration (e.g. `admin`,
   `organizer`, `curator`, `attendee`), never a free-form string, and never derived from
   user-controlled claim values.
2. **Shared persona catalog lives in `ServiceDefaults`** (not duplicated between Web and
   Backend), analogous to how `BethuyaRoleNames` is today the single source of truth mirrored
   into `Bethuya.Hybrid.Shared.Auth` with a parity test. The persona catalog must have the
   same parity-test discipline.
3. **Propagation only in Development, and only when `Authentication:Provider=None`.** The
   persona-switch mechanism must never activate in any other environment or provider
   configuration. It should reuse (not weaken) the existing
   `EnsureInsecureDevAuthAllowed` fail-closed guard pattern in
   `BethuyaAuthenticationExtensions` — i.e., any propagation channel must also refuse to
   activate outside Development unless the same explicit opt-in is set.
4. **The Backend constructs the claims principal from the propagated persona key** — the Web
   tier must never construct or forward a serialized `ClaimsPrincipal`/claim bag itself. This
   preserves the server-authoritative identity model already implicit in the real-provider
   (Entra/Auth0/Keycloak) paths, where the Backend independently validates a JWT rather than
   trusting whatever the Web tier claims.
5. **Only the existing four roles are representable** — `Admin`, `Organizer`, `Curator`,
   `Attendee` (`BethuyaRoleNames`). No arbitrary roles, scopes, or signals may be introduced
   into the dev-persona claims beyond what a real provider could also produce. This keeps the
   dev harness testing the *existing* authorization policies (`BethuyaAuthorizationExtensions`)
   rather than inventing new authorization surface area that diverges from production.
6. **No toolbar/UI work until Backend parity is proven.** A persona-switching UI (toolbar,
   picker, etc.) must not ship until there is verifiable evidence (Anvil-style: build + TUnit +
   integration test showing the Backend's `HttpContext.User` actually changes per persona) that
   the Backend, not just the Web tier's display state, honors the selected persona. This
   directly addresses the "false sense of testing coverage" risk documented in
   `docs/development-authentication.md` (fixed-admin defect, point 3).

**Why:** Building a UI-only toggle without a corresponding Backend authentication change would
let engineers believe they are testing Organizer/Curator/Attendee-only code paths when the
Backend is, in fact, still authorizing every request as full Admin — silently defeating the
purpose of a testing harness and potentially masking real authorization bugs before they reach
a real-provider environment.

**Scope note:** This entry records a proposed boundary for future work only. No code in this
repository implements persona switching, a toolbar, a seeder, new roles, or v2 domains as a
result of this entry. Layer 2+ of the Developer Testing Harness stack must read this file,
`docs/development-authentication.md`, and get explicit Neo (architecture) + Morpheus
(security/auth) sign-off before implementing any part of the propagation mechanism described
above.

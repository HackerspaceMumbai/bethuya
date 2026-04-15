# Lessons Learned

Every mistake, unexpected discovery, or incorrect assumption is recorded here to prevent recurrence.

## Format

```
## [YYYY-MM-DD] Short Title
- **What happened:** Description of the issue
- **Root cause:** Why it happened
- **Fix:** What was done to resolve it
- **Prevention:** How to avoid it in future
```

---

## Log

<!-- Lessons are appended here as they are discovered -->

## [2026-04-15] Social OAuth CTAs must be gated by the provider-owned input state

- **What happened:** The LinkedIn onboarding card let users press “Connect LinkedIn” before entering any public profile URL, which made it possible to return from verification with the field locked but blank.
- **Root cause:** The CTA only respected loading/saving/error guards; it did not also require the LinkedIn card’s own supporting URL input to contain a meaningful trimmed value before launch.
- **Fix:** Added explicit LinkedIn CTA gating based on the normalized URL, kept reconnect available after verified connect, and updated the helper copy/tests so the blank-path state is no longer reachable through normal use.
- **Prevention:** When an OAuth card depends on a user-supplied supporting field, gate the launch action on the normalized value instead of relying on post-return validation or placeholder copy.

## [2026-04-15] Asymmetric verification cards need structural rhythm, not forced alignment

- **What happened:** The LinkedIn card on `/registration/social` grew taller than GitHub once it carried the verified-member status plus the editable/lockable LinkedIn URL field, so the side-by-side layout started to look accidental and visually strained.
- **Root cause:** The earlier equal-height card treatment was optimized for button alignment, but the two providers no longer have symmetric content or behavior after LinkedIn gained the extra URL workflow.
- **Fix:** Restacked the provider cards vertically, kept the truthful loading/error state contract inside each card, and gave the lighter GitHub card supportive copy so the stack still reads as balanced.
- **Prevention:** When onboarding providers diverge in required fields or security behavior, prefer a deliberate vertical stack over forcing horizontal symmetry that makes one card look empty and the other cramped.

## [2026-04-15] Shared auth packages can trip vulnerability gates in worker projects

- **What happened:** `Bethuya.MigrationService` failed restore on NU1901 for `System.Security.Cryptography.Xml` even though the worker did not reference that package directly.
- **Root cause:** The worker references `ServiceDefaults`, which carries `Microsoft.Identity.Web`; that shared auth package graph pulled `Microsoft.Identity.Web.TokenCache` / `Microsoft.AspNetCore.DataProtection`, which in turn restored the vulnerable `System.Security.Cryptography.Xml` version transitively.
- **Fix:** Traced the path from `project.assets.json` and the resolved package graph, then used central transitive pinning in `Directory.Packages.props` to move `System.Security.Cryptography.Xml` to `10.0.6` without suppressing NU1901.
- **Prevention:** When a shared infrastructure project brings auth packages into non-auth apps, inspect the transitive graph before suppressing package warnings; prefer a central patched pin over weakening the vulnerability gate.

## [2026-04-14] New LinkedIn self-serve apps expect OIDC scopes by default

- **What happened:** Local LinkedIn onboarding failed with `unauthorized_scope_error` because the app rejected `r_liteprofile` during the OAuth callback.
- **Root cause:** Bethuya's runtime config still defaulted LinkedIn to the legacy `r_liteprofile` scope, but newer self-serve LinkedIn apps are commonly provisioned with the “Sign in with LinkedIn using OpenID Connect” product that authorizes `openid` and `profile` instead.
- **Fix:** Defaulted AppHost-managed LinkedIn scopes to `openid` + `profile`, kept the web auth flow compatible with explicit legacy scope overrides, and preserved actionable redirect messaging for scope-authorization failures.
- **Prevention:** For local LinkedIn setup, enable the OpenID Connect product first and treat `r_liteprofile` as an explicit legacy override rather than the default.

## [2026-04-12] External OAuth must live on its own saved onboarding step

- **What happened:** The first version put mandatory profile fields and GitHub/LinkedIn connect on the same Blazor page. After the OAuth redirect returned, the Blazor circuit restarted and all unsaved required form data was gone.
- **Root cause:** External OAuth is a full-page roundtrip, not an in-circuit UI action. Keeping required unsaved form state on the same page made the flow inherently fragile.
- **Fix:** Split onboarding into three steps: save mandatory details first, perform verified social connect on `/registration/social`, then continue to optional AIDE. The backend/shared contracts were split so mandatory save and social save are independent.
- **Prevention:** Never couple external OAuth callbacks to unsaved mandatory Blazor form state. Either persist the draft explicitly before redirect or, preferably for onboarding, move the OAuth interaction onto its own saved step.

## [2026-04-12] Custom onboarding layouts must include Blazor Blueprint portal hosts

- **What happened:** The AIDE page rendered its select fields, but clicking them did not show any options.
- **Root cause:** `AideProfile.razor` was moved onto `OnboardingLayout.razor`, but that custom layout did not render `BbPortalHost` and `BbDialogProvider`. Blazor Blueprint select/popover content depends on those layout-level hosts.
- **Fix:** Added `BbPortalHost` and `BbDialogProvider` back to `OnboardingLayout.razor`.
- **Prevention:** Any custom layout that hosts Blazor Blueprint interactive overlays (selects, dialogs, popovers) must include the same portal/dialog host components as `MainLayout`, or the trigger will render while the popup content silently fails to appear.

## [2026-04-11] Focused onboarding works best as its own layout, not a hidden sidebar variant

- **What happened:** The first pass mixed onboarding-specific chrome into `MainLayout` while also creating a dedicated `OnboardingLayout`, which left dead route-check logic behind and muddied the intended shell for sensitive setup pages.
- **Root cause:** It is tempting to toggle dashboard chrome with route checks inside the shared layout, but explicit `@layout` usage on onboarding routes already provides a cleaner and more durable boundary.
- **Fix:** Kept onboarding pages on a dedicated `OnboardingLayout`, removed the unreachable onboarding branch from `MainLayout`, and deleted the orphaned onboarding nav partial so the code matches the UX intent.
- **Prevention:** When a flow truly needs different chrome, prefer a dedicated layout over conditionals inside the default layout. It keeps auth-sensitive UX easier to reason about, test, and review.

## [2026-04-11] Onboarding Nav Visibility Security Hardening — Layout Suppression is the Right Pattern

- **What happened:** New users during onboarding could see navigation links to organizer-only features (`/agents`, `/curation`) even though the pages themselves were authorization-protected. This created trust boundary confusion and poor UX during sensitive profile-completion flow.
- **Root cause:** `NavMenu.razor` rendered all nav sections unconditionally. Initial attempt used `<AuthorizeView Roles="...">` but this **fails in dev mode because the dev principal carries all roles**. AuthorizeView is role-dependent; the dev principal (`DevelopmentAuthenticationDefaults.CreatePrincipal()`) includes Admin, Organizer, Curator, AND Attendee roles by design, making all role checks pass uniformly.
- **Fix:** Created `OnboardingLayout.razor` as a focused shell for onboarding routes and kept `NewUserProfile.razor` / `AideProfile.razor` on that explicit layout. This is a **structural code choice, not role-dependent** — the layout applies uniformly regardless of user identity and cannot be bypassed. Secondary hardening: `NavMenu.razor` still scopes organizer and curator links with separate `<AuthorizeView>` role checks for non-onboarding routes. Added `@rendermode InteractiveServer` to `Home.razor` for server-only dashboard logic.
- **Prevention:** When securing onboarding flows: layout suppression (structural choice) is more secure than role-gating (role-dependent). In dev mode, role-gating is ineffective because the dev principal has all roles. Use layouts to structurally separate onboarding navigation from main navigation; use role-gating as a secondary layer for defense-in-depth. Always verify security patterns work in dev mode before shipping — dev principal role blocker is a common gotcha.
- **Why this pattern:** Layout suppression works uniformly in dev and prod; it's not role-dependent and cannot be bypassed by user claims. Defense-in-depth: main nav still has AuthorizeView for additional protection. Dev testing now accurately represents prod UX (no organizer nav visible on onboarding routes regardless of dev principal roles).


- **What happened:** Running `Hackmum.Bethuya.E2E` without the Bethuya web app listening on `https://localhost:7112` caused nearly the whole suite to fail with `net::ERR_CONNECTION_REFUSED`.
- **Root cause:** `BethuyaE2ETest` hardcodes `BETHUYA_BASE_URL`/`https://localhost:7112` and navigates directly with Playwright; it does not boot AppHost or the web app for the test run.
- **Fix:** Kept the onboarding regression coverage at the bUnit/TUnit layer for this change and recorded the missing live-host requirement as the blocker for broader E2E verification.
- **Prevention:** Before relying on Playwright coverage, start the target web app (or wire the suite to provision it automatically) and set `BETHUYA_BASE_URL` explicitly for the running instance.

## [2026-04-11] Dev-mode Blazor auth needs a real authentication scheme, not only an AuthenticationStateProvider
- **What happened:** Navigating to `/registration/mandatory` in local mode threw `InvalidOperationException: Unable to find the required 'IAuthenticationService' service`.
- **Root cause:** The web app used `DevelopmentAuthenticationStateProvider` to make Blazor components think the user was authenticated, but `Authentication:Provider=None` skipped `AddAuthentication(...)` entirely. `[Authorize]` pages and authorization middleware still require a real `IAuthenticationService` in DI. The backend had the same gap for profile endpoints.
- **Fix:** Added a shared development authentication scheme and handler in `ServiceDefaults/Auth`, enabled it for both web and backend when `Authentication:Provider=None`, switched the web app to `ClaimsCurrentUserService` in dev mode, and added regression tests for protected routes and profile endpoints.
- **Prevention:** If local dev mode must exercise `[Authorize]` pages or authenticated APIs, always register a concrete authentication scheme and middleware on every participating app, even when the identity provider is disabled.

## [2026-04-11] BbFormFieldInput rejects InputType and crashes Razor page rendering
- **What happened:** After the auth DI fix, `/registration/mandatory` still failed to render and threw a component parameter exception for `BbFormFieldInput`.
- **Root cause:** `BbFormFieldInput<TValue>` in the current Blazor Blueprint package does not expose an `InputType` parameter, so adding `InputType="InputType.Email"` / `InputType="InputType.Tel"` causes runtime rendering failure instead of a compile-time error.
- **Fix:** Removed the unsupported `InputType` attributes from `src/Bethuya.Hybrid/Bethuya.Hybrid.Shared/Pages/NewUserProfile.razor` and re-verified the page over HTTP.
- **Prevention:** When using Blazor Blueprint wrappers, only pass parameters already proven in repo usage or package docs; unsupported attributes fail at runtime for Razor components.

## [2026-03-26] ALL fixes were committed to main; worktree branch never received them
- **What happened:** Every bug fix was committed and verified on `main`. The running application was in a worktree on branch `copilot/worktree-2026-03-24T09-26-45`. Fixes had zero effect on the user's running app for the entire session.
- **Root cause:** Git worktrees share a `.git` but each is on an independent branch. Commits to `main` are invisible to any other worktree branch until explicitly merged.
- **Fix:** `git merge main --no-ff` in the worktree.
- **Prevention:** **Always check the current working directory and active branch before making changes.** If the session CWD is a worktree, that is the branch the user's app runs from. Commit and build-verify there, or merge main into it before declaring work done.


- **What happened:** After adding `<BbCategoryPortalHost />` to fix `BbDialog`, ALL buttons on the page became unclickable — even after replacing `BbDialog` with a plain HTML modal.
- **Root cause:** `BbCategoryPortalHost` renders a transparent full-screen overlay div (position:fixed, z-index > content) used to position portaled dialogs. This overlay intercepts all mouse events on the page even when no dialog is open.
- **Fix:** Remove `BbCategoryPortalHost` from `MainLayout.razor`. Use native CSS modals (`position:fixed` backdrop with `@if`) instead of any BlazorBlueprint dialog/portal components.
- **Prevention:** Never add portal host components from UI libraries unless you understand what DOM elements they inject. Check the rendered HTML in browser DevTools for transparent overlay divs when buttons become mysteriously unclickable.

## [2025-03-24] E2E tests must cover Home page buttons specifically, with ToBeEnabledAsync before clicking
- **What happened:** The E2E tests only covered the `/events` page button, not the `/` (Home) dashboard button. The Home button was broken for weeks without automated detection.
- **Root cause:** `HomeCreateButton_ShouldOpenDialog` test was never written. Even the events test lacked `ToBeEnabledAsync()` — a click on a disabled/covered element succeeds silently in Playwright.
- **Fix:** Added `HomeCreateButton_ShouldOpenDialog` test targeting `/` + `[data-test='create-event-btn']`. Added `ToBeEnabledAsync()` before every `ClickAsync()`. Added short `Timeout` on dialog-visible assertion to fail fast.
- **Prevention:** Every interactive feature needs an E2E test on EVERY page it appears on. Always assert `ToBeEnabledAsync()` before clicking and assert visible state AFTER clicking with a tight timeout.


- **What happened:** `@onclick` handlers never bound despite WebSocket connected. Page was visible but completely non-interactive.
- **Root cause:** With `prerender: true` (default), Blazor renders static HTML on the server, sends it to the browser, then "activates" the DOM via WebSocket. If the server-side pre-render pass calls an async API (`EventApi.GetAllAsync()`) and the interactive render produces different output, Blazor's DOM reconciliation silently fails — handlers aren't registered.
- **Fix:** `new InteractiveServerRenderMode(prerender: false)` on `<Routes>` and `<HeadOutlet>` in App.razor. Eliminates the two-pass render; components render once, fully interactively.
- **Prevention:** When using `InteractiveServer` with async data-fetching on page load, either disable prerendering or use `OnAfterRenderAsync(firstRender)` for the API call (static pre-render won't call it, preventing mismatch).

- **What happened:** `BbButton OnClick="@(() => ...)"` rendered the button visually but click events never fired, even with Blazor WebSocket confirmed connected.
- **Root cause:** Unknown — `BbButton`'s `OnClick` parameter appears to not wire to the underlying `<button>`'s click event in this usage pattern. The WebSocket being active ruled out render mode issues.
- **Fix:** Replace `BbButton` with native `<button @onclick="...">` elements. Style directly with CSS classes; no `::deep` needed.
- **Prevention:** When a UI component's event callback silently does nothing despite Blazor being connected, replace with native HTML elements to isolate whether the issue is the component library or Blazor itself. Native elements should always be the first diagnostic step.

- **What happened:** `BbDialog` with `Open`/`OpenChanged` parameters was set up for programmatic control, but clicking the button had no effect — the dialog never opened.
- **Root cause:** `BbDialog` v3.5.2 only works via its `BbDialogTrigger` (uncontrolled pattern). Without a `BbDialogTrigger` nested inside the dialog, external `Open` state changes are ignored. The README only documents the trigger pattern; `Open`/`OpenChanged`/`Controlled` params exist in the binary but are undocumented and non-functional for cross-component scenarios.
- **Fix:** Replace `BbDialog` with a plain CSS `position:fixed` modal overlay (`@if (IsOpen)` + backdrop div). No portal host needed, complete control, no external state.
- **Prevention:** When using a UI library's overlay/dialog component, verify that the desired open/close pattern (trigger vs. programmatic) is explicitly documented. If a component only documents trigger-based opening, don't assume programmatic `Open` works across component boundaries.

## [2026-03-21] Partial Squad initialization needs a structural validation pass
- **What happened:** The repository already had a `.squad/` directory, but `team.md` had no members, `casting/registry.json` was missing, and the Scribe files were only loosely scaffolded.
- **Root cause:** Squad setup stopped after creating baseline files, so Team Mode state existed without a usable roster or casting metadata.
- **Fix:** Rebuilt the roster, routing, casting policy/registry/history, normalized Scribe, and created the core agent charters/histories before routing work.
- **Prevention:** When `.squad/` exists, validate `team.md`, `casting/*.json`, and agent charter/history files together before assuming the squad is operational.

## [2026-03-21] ServiceDefaults cannot reference Bethuya.Hybrid.Shared without circular dependencies
- **What happened:** Build error CS0246 — `using Bethuya.Hybrid.Shared.Auth` in ServiceDefaults failed because ServiceDefaults has no project reference to Shared.
- **Root cause:** ServiceDefaults → Web/Backend (shared infra), Shared → Web/Client/MAUI (UI). Adding Shared → ServiceDefaults would drag ASP.NET FrameworkReference into the Razor class library and MAUI project.
- **Fix:** Inlined the role/policy string values in ServiceDefaults extension methods with a sync-note comment. Kept the constants as the source of truth in Shared.
- **Prevention:** ServiceDefaults should only depend on framework packages and NuGet libraries, never on application projects. If shared types are needed, inline them or create a minimal abstractions package.

## [2026-03-21] TUnit TUnitAssertions0005 — cannot Assert.That() on const values
- **What happened:** Build errors on `await Assert.That(BethuyaRoles.Admin).IsEqualTo("Admin")` — TUnit's source generator rejects assertions on compile-time constants.
- **Root cause:** TUnit v0.50 analyzer flags `Assert.That(constantValue)` as a code smell since the assertion is always trivially true/false.
- **Fix:** Replaced per-constant assertions with aggregate tests (e.g., "all roles are distinct and non-empty").
- **Prevention:** In TUnit, test computed/runtime values, not compile-time constants. For constant validation, test properties like uniqueness and non-emptiness.

## [2025-07-17] Blazor Blueprint Separator enum is `SeparatorOrientation`, not `Orientation`
- **What happened:** Build error CS0103 — `Orientation` does not exist in context
- **Root cause:** BbSeparator uses `SeparatorOrientation.Vertical` not generic `Orientation.Vertical`. Each BB component has its own prefixed enum.
- **Fix:** Changed to `SeparatorOrientation.Vertical`
- **Prevention:** Check BB DLL for exact enum type names before using. Pattern: `{Component}Orientation`.

## [2025-07-17] Naming conflict between EventCallback parameter and private method
- **What happened:** Build error CS0119 — method `OnOpenChanged` referenced as EventCallback
- **Root cause:** `OpenChanged="@OnOpenChanged"` in Razor binding tried to use a method named `OnOpenChanged` but it conflicted with the EventCallback resolution.
- **Fix:** Renamed private method to `HandleOpenChanged` to avoid namespace clash.
- **Prevention:** Never name private methods the same as EventCallback parameter names. Use `Handle*` prefix for local handlers.

## [2025-07-17] Blazor Blueprint components don't support arbitrary HTML attributes
- **What happened:** Runtime `InvalidOperationException` — `BbAlert` does not have property `data-test`
- **Root cause:** BB components (BbCard, BbAlert, BbButton, etc.) don't use `@attributes` splatting, so `data-test="..."` is treated as a component parameter and fails.
- **Fix:** Wrapped BB components in plain HTML elements (`<div data-test="...">` or `<article data-test="...">`), or placed `data-test` on inner `<span>` children.
- **Prevention:** Always place `data-test` selectors on standard HTML elements, never directly on Blazor component tags.

## [2025-07-18] AlertVariant.Danger not Destructive; BbAlert has no AutoDismissAfter
- **What happened:** Build errors using `AlertVariant.Destructive` and non-existent `BbAlert` properties (`AutoDismissAfter`, `PauseOnHover`, `ShowCountdown`, `AccentBorder`).
- **Root cause:** `BadgeVariant` has `Destructive` but `AlertVariant` uses `Danger`. BB component properties were hallucinated by agents — `BbAlert` only supports `Variant`, `Dismissible`, `OnDismiss`, `Class`.
- **Fix:** Changed to `AlertVariant.Danger`. Rewrote Notification component to use `Task.Delay` + `CancellationTokenSource` for auto-dismiss instead of non-existent props.
- **Prevention:** Always verify BB enum/property names against the actual DLL. `AlertVariant` values: `Default, Success, Info, Warning, Danger`. For auto-dismiss, implement manually with `Task.Delay`.

## [2026-03-26] BbCategoryPortalHost required in MainLayout for dialogs/overlays
- **What happened:** `BbDialog` backdrop rendered as an invisible full-screen overlay, silently blocking all clicks on the page. No visual indication — button appeared clickable but never fired.
- **Root cause:** BlazorBlueprint overlay components (Dialog, Sheet, Popover, Tooltip) use a portal service to render outside the component tree. Without `<BbCategoryPortalHost />` in `MainLayout.razor`, the portal falls back to inline rendering — placing the full-screen dialog overlay directly in the DOM where it intercepts every mouse event.
- **Fix:** Added `@using BlazorBlueprint.Primitives.Services` and `<BbCategoryPortalHost />` to `MainLayout.razor`. Also added `@rendermode="InteractiveServer"` to `<HeadOutlet>` and `<Routes>` in `App.razor` per BlazorBlueprint docs.
- **Prevention:** Any app using BlazorBlueprint overlay components MUST have `<BbCategoryPortalHost />` in the root layout. Note: the README says `<BbPortalHost />` but the actual component in v3.5.2 is `BbCategoryPortalHost` from `BlazorBlueprint.Primitives.Services`.


- **What happened:** The "New Event" button on Events.razor did nothing at runtime. No JS errors, no visual feedback.
- **Root cause:** `<Routes/>` in `App.razor` had no `@rendermode`, so the router ran in static SSR. Per-page `@rendermode InteractiveServer` directives on shared-RCL pages are unreliable when routed through `AuthorizeRouteView` in static SSR mode — the render mode may not be picked up.
- **Fix:** Set `@rendermode="InteractiveServer"` on `<Routes>` in `App.razor` (Web project only — MAUI has its own app entry). This makes the entire web app interactive globally. Per-page directives in shared pages were then redundant and removed.
- **Prevention:** For a Blazor Web App where all pages need interactivity, set the render mode at the router (`<Routes @rendermode="InteractiveServer"/>`) in `App.razor`, not on individual shared-library pages. Per-page `@rendermode` in a shared RCL with `AuthorizeRouteView` is fragile.

## [2026-03-26] E2E tests must always run alongside unit tests in CI verification
- **What happened:** `dotnet test tests/Hackmum.Bethuya.Tests/` only ran TUnit unit tests. The Playwright MSTest project (`Hackmum.Bethuya.E2E`) was never executed, so the static SSR render mode bug shipped undetected.
- **Root cause:** The E2E project uses MSTest and requires a live Aspire stack — it was excluded from the unit test run command by targeting the wrong project path.
- **Fix:** Fixed E2E tests to assert dialog visibility immediately after button click, added `data-test="create-dialog"` wrapper, corrected `event-card`→`event-row` and `"View →"`→`data-test="view-event-btn"` selector mismatches.
- **Prevention:** CI verification must run both `dotnet test tests/Hackmum.Bethuya.Tests/` (TUnit) AND `dotnet test tests/Hackmum.Bethuya.E2E/` (Playwright, requires live stack). Always add a post-click `ToBeVisibleAsync()` assertion for any dialog or panel opened by a button — this is the cheapest guard against static SSR regressions.

## [2026-03-28] Blazor Blueprint requires a theme CSS file — no built-in defaults for --primary etc.
- **What happened:** "Create New Event" button rendered blue instead of the Hackerspace Mumbai gold. BbButton Default variant uses Tailwind `bg-primary` which resolves to `var(--primary)`, but `--primary` was never defined.
- **Root cause:** Blazor Blueprint v3.5.2 does NOT ship a `themes.css` (that's a newer website feature). The compiled `blazorblueprint.css` references `var(--primary)`, `var(--background)`, etc. via `@theme inline`, but **the consumer must define these variables** in their own theme CSS file. Without a theme file, `--primary` is undefined → falls back to browser default or gets overridden by Bootstrap's `!important` `.bg-primary`.
- **Fix:** Created `bethuya-theme.css` defining all BB CSS variables (`--primary: #D4A830`, `--background: #0d0b09`, etc.) mapped to the Hackerspace Mumbai dark gold design system. Added `class="dark"` to `<html>`. Removed Bootstrap CSS entirely (zero classes used, `!important` conflict).
- **Prevention:** When adding Blazor Blueprint to a project, **always** create a theme CSS file first (per https://blazorblueprintui.com/docs/installation Step 3). Define at minimum: `--primary`, `--primary-foreground`, `--background`, `--foreground`, `--card`, `--border`, `--ring`, `--radius`. Load it BEFORE `blazorblueprint.css`. Never mix Bootstrap with BB — they both define `.bg-primary` and Bootstrap uses `!important`.

## [2026-03-28] BbDialog Open/OpenChanged is non-functional for programmatic control in v3.5.2
- **What happened:** Restored `BbDialog Open="@IsOpen" OpenChanged="@HandleOpenChanged"` after confirming per-page `@rendermode InteractiveServer` works and BbButton OnClick fires (both verified via diagnostic counter test). Clicking "Create New Event" still did nothing — the dialog never opened.
- **Root cause:** `BbDialog` in BlazorBlueprint v3.5.2 only opens via `BbDialogTrigger` nested inside the component tree. The `Open`/`OpenChanged` parameters exist in the compiled binary but are **not wired to internal state** — external parameter changes are silently ignored.
- **Fix:** Replaced `BbDialog` with `@if (IsOpen)` custom CSS modal overlay. BB form components (BbLabel, BbInput, BbTextarea, BbButton, BbAlert) work perfectly inside the custom modal.
- **Prevention:** Never use `BbDialog` for programmatic/controlled open state. Use `BbDialogTrigger` (uncontrolled, trigger must be inside BbDialog tree) or replace with `@if (IsOpen)` + custom modal CSS. File issue with BlazorBlueprint for programmatic dialog support.

## [2026-03-29] BlazorBlueprint Tailwind v4 @layer utilities lose to unlayered Blazor CSS
- **What happened:** CreateEvent form fields were severely misaligned despite using BB Tailwind utility classes (`grid grid-cols-2 gap-4`, `space-y-4`, `flex`, etc.) — everything stacked vertically, fields overflowed the card.
- **Root cause:** BB v3.5.2 bundles **Tailwind CSS v4.1.16** which uses `@layer utilities { .grid { display: grid; } }`. CSS cascade spec: **unlayered CSS always beats layered CSS** regardless of specificity. Blazor's scoped CSS bundle (`*.styles.css`) and `app.css` are unlayered → they win over BB's Tailwind utilities for any property they also target.
- **Fix:** Wrote explicit scoped CSS classes in `.razor.css` (which is unlayered) to replace Tailwind utility classes for layout (`display:grid`, `display:flex`, `gap`, `grid-template-columns`). BB Tailwind utilities still work when no competing unlayered CSS targets the same property on the same element (e.g., `mt-6`, `w-full`, `text-2xl` on BB components).
- **Prevention:** In Blazor projects using BB, don't rely on Tailwind utility classes for layout on elements that have Blazor scoped CSS. Either: (1) use explicit scoped CSS in `.razor.css`, or (2) pass Tailwind utilities via `Class=` parameter on BB components (which renders in the component's own DOM, avoiding Blazor's scoped bundle collision).

## [2026-03-29] Blazor scoped CSS doesn't reach child component root elements
- **What happened:** `.card-top[b-n9etwsyia6]` targeted `BbCard` but had no effect — BbCard's rendered `<div class="rounded-lg border bg-card...">` lacked the `b-n9etwsyia6` scope attribute.
- **Root cause:** Blazor CSS isolation adds scope attributes only to HTML elements **directly rendered by the component's own Razor template**. Child component root elements (e.g., BbCard's div) are rendered by the child component, so they don't get the parent's scope attribute.
- **Fix:** Use BB's `Class=` parameter to pass styling to child components (e.g., `BbCard Class="mt-6"`), or use `::deep` combinator. Don't put scoped CSS class names on BB component tags expecting them to match rendered HTML.
- **Prevention:** When styling BB (or any) child components from parent scoped CSS, either: (1) wrap in a `<div>` you control, (2) use `::deep`, or (3) pass styling via the component's `Class` parameter.

## [2026-03-29] BlazorBlueprint components reject arbitrary HTML attributes at runtime
- **What happened:** `InvalidOperationException: Object of type 'BbTimePicker' does not have a property matching the name 'data-test'` — occurred twice across sessions.
- **Root cause:** BB components don't use `[Parameter(CaptureUnmatchedValues = true)]`, so any unknown attribute (including `data-test`) throws at runtime. **This is invisible at build time** — the Razor compiler doesn't validate component parameter names.
- **Fix:** Always wrap BB components in `<div data-test="...">` wrappers. Added bUnit render test (`CreateEventRenderTests.cs`) that catches this at test time.
- **Prevention:** Never put `data-test` or any custom HTML attribute directly on a BB component. Always wrap in a plain HTML element. The bUnit test catches this regression — ensure it runs in CI.

## [2026-03-30] bethuya-theme.css uses rgba/hex — never wrap in hsl()
- **What happened:** Capacity InputNumber had white background in dark theme — visually jarring vs all other dark-themed inputs.
- **Root cause:** `.field-input` CSS used `background-color: hsl(var(--input))` but `bethuya-theme.css` defines `--input: rgba(212, 168, 48, 0.2)` — an rgba value, NOT an HSL component. `hsl(rgba(...))` is invalid CSS → browser falls back to default (white for input elements). Same issue for `--border`, `--foreground`, `--ring`.
- **Fix:** Changed all color references in `.field-input` to use `var(--border)`, `var(--background)`, `var(--foreground)`, `var(--ring)` directly without any `hsl()` wrapper.
- **Prevention:** **Always check bethuya-theme.css variable format before writing CSS.** The theme uses full color values (hex `#0d0b09`, rgba `rgba(212,168,48,0.2)`), NOT HSL components. Use `var(--name)` directly. The `hsl()` wrapping pattern is from ShadCN/Tailwind docs that use `--primary: 210 40% 98%` — bethuya-theme.css does NOT follow that convention.

## [2026-03-30] Blazor Blueprint MCP docs report non-existent components
- **What happened:** Refactored CreateEvent.razor to use `BbFormFieldTextarea`, `BbFormFieldNumericInput`, and `BbFormFieldFileUpload` — all three failed with RZ10012 "unexpected name".
- **Root cause:** The BB MCP tool (`blazorblueprint-get_component`) returned documentation for these components, but they **do not exist** in BlazorBlueprint.Components v3.5.2. Binary search of the DLL confirmed only `BbFormFieldInput`, `BbFormFieldSelect`, and `BbFormSection` exist as form-field wrappers.
- **Fix:** Replaced with standalone components (`BbTextarea`, `BbNumericInput`, `BbFileUpload`) wrapped manually with `<div class="form-group">` + `BbLabel` + `ValidationMessage`.
- **Prevention:** **Always verify BB component existence by binary-searching the DLL** before using components documented only by the MCP tool. The MCP docs may be aspirational/ahead of the installed version. Updated AGENTS.md, CLAUDE.md, and copilot-instructions.md to list the *actual* form-field wrappers vs standalone components.

## [2026-03-30] Use Aspire 13.2 CLI detached mode instead of manual PID killing
- **What happened:** During the Event Cover Pic feature build, every `dotnet build` while Aspire was running failed with locked DLL errors. Workaround was to manually stop the Aspire shell session, find locked PIDs via `Get-Process`, kill them with `Stop-Process`, rebuild, then restart Aspire — wasting significant time across multiple iterations.
- **Root cause:** Running Aspire via `dotnet run --project AppHost` holds locks on all project DLLs. There was no clean stop/start mechanism being used.
- **Fix:** Aspire 13.2 introduced dedicated CLI commands for this exact scenario:
  - `aspire start` — run AppHost in detached (background) mode
  - `aspire stop` — cleanly stop the running AppHost (releases all DLL locks)
  - `aspire ps` — list running AppHosts
  - `aspire resource <name> rebuild` — rebuild a specific resource without stopping everything
  - `--isolated` flag — randomized ports + isolated user-secrets for parallel instances (great for worktrees)
  - `aspire describe --follow` — stream resource state changes from CLI (useful for agents)
  - `aspire wait <resource> --status healthy` — block until a resource is ready (useful for automation)
- **Prevention:** **Always use `aspire start` / `aspire stop` instead of `dotnet run --project AppHost`.** Before building, run `aspire stop` to release locks, build, then `aspire start` to resume. For worktrees or parallel agent instances, use `aspire start --isolated`. See: https://aspire.dev/whats-new/aspire-13-2/#detached-mode-and-process-management

## [2026-03-31] Use Playwright for .NET — not npx playwright
- **What happened:** Attempted to install Playwright multiple times via `npx playwright install` (Node.js). The commands failed or were unnecessary because this is a .NET project that uses **Playwright for .NET** (`Microsoft.Playwright`), not the Node.js version.
- **Root cause:** Defaulted to the more commonly seen Node.js Playwright installation path instead of checking the project's actual test stack. Bethuya uses `Microsoft.Playwright` NuGet package — the .NET bindings.
- **Fix:** For Playwright browser installation in .NET projects, use the PowerShell script generated by the build:
  ```powershell
  pwsh bin/Debug/net10.0/playwright.ps1 install
  ```
  Or reference the `Microsoft.Playwright` package and let the test framework handle browser provisioning.
- **Prevention:** **Always check the project's test framework before installing Playwright.** If the project uses `Microsoft.Playwright` (NuGet), use `playwright.ps1 install` from the build output — never `npx`. The `AGENTS.md` and `CLAUDE.md` files both specify "Playwright for .NET" as the E2E framework. Read them first.

---

### Lesson: Microsoft.Playwright.MSTest is binary-incompatible with MSTest 4.x on .NET 10
- **Date:** 2026-03-31
- **Context:** E2E tests using `Microsoft.Playwright.MSTest` 1.52.0 with `MSTest.Sdk/4.1.0` discovered 0 tests.
- **Root Cause:** `Microsoft.Playwright.MSTest` depends on `MSTest.TestFramework` 2.2.7. The `PageTest` base class is compiled against MSTest 2.x attribute types. MSTest 4.x source generators can't recognize `[TestClass]` from classes inheriting `PageTest` because the attribute types were compiled against MSTest 2.x assemblies. Standalone test classes (no PageTest) are discovered fine.
- **Fix:** Replace `Microsoft.Playwright.MSTest` with `Microsoft.Playwright` directly. Manage browser lifecycle manually in `[TestInitialize]`/`[TestCleanup]` (create Playwright→Browser→Context→Page, dispose in reverse). Replace `Expect()` (inherited from PageTest) with `Assertions.Expect()` (static from Microsoft.Playwright).
- **Prevention:** When using MSTest 4.x (`MSTest.Sdk`), never depend on packages compiled against MSTest 2.x base classes. Check NuGet dependency graphs for MSTest.TestFramework version conflicts.

---

### Lesson: .NET 10 SDK requires Microsoft Testing Platform (MTP) mode for `dotnet test`
- **Date:** 2026-03-31
- **Context:** `dotnet test` failed with: "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK"
- **Root Cause:** .NET 10 removes VSTest adapter support. The `dotnet test` command requires MTP mode to discover and run tests.
- **Fix:** Add `"test": { "runner": "Microsoft.Testing.Platform" }` to `global.json`. The `TestingPlatformDotnetTestSupport` MSBuild property is NOT needed with MTP mode. The `--timeout` flag uses format like `2m` or `30s` (not milliseconds).
- **Prevention:** When targeting .NET 10, always configure MTP mode in `global.json` before running `dotnet test`.

---

### Lesson: Blazor InteractiveServer pages need SignalR circuit before form interaction
- **Date:** 2026-03-31
- **Context:** E2E tests navigated to `/events/create` and immediately filled form fields, but clicks on submit had no effect.
- **Root Cause:** `@rendermode InteractiveServer` pages render SSR HTML first, then establish a SignalR circuit. The form HTML is present but not wired up until the circuit connects. Filling fields before the circuit is established results in the form submission being a no-op.
- **Fix:** Use `WaitUntil = WaitUntilState.NetworkIdle` in `GotoAsync` to wait for SignalR handshake. Additionally, wait for an interactive-only element (e.g., submit button) to be enabled before filling fields: `await Assertions.Expect(submitBtn).ToBeEnabledAsync()`.
- **Prevention:** In E2E tests for Blazor Server pages, always verify interactivity before form interactions. Click the actual `<button>` inside wrapper divs, not just the wrapper div.

---

### Lesson: Use unique identifiers in E2E test data to avoid strict mode violations
- **Date:** 2026-03-31
- **Context:** E2E test `CreateEvent_OnEventsPage_ShouldShowInList` failed with "strict mode violation: resolved to 2 elements" because multiple events had the same title from previous test runs.
- **Root Cause:** Test data persists in the database across test runs. Hardcoded titles like "Test Community Meetup" accumulate duplicates.
- **Fix:** Use unique titles with embedded GUIDs: `var uniqueTitle = $"E2E Meetup {Guid.NewGuid().ToString("N")[..8]}";` Use `.First` on locators when strict mode is a concern.
- **Prevention:** Always use unique test data in E2E tests that create persistent resources. Use `.First` on locators when testing against lists that may have prior data.

---

### Lesson: NBomber scenarios must not use `using var` for HttpClient in factory methods
- **Date:** 2026-03-31
- **Context:** `ImageUploadScenario.Create()` and `EventApiScenario.CreateListScenario()`/`CreatePostScenario()` declared `using var httpClient` inside the factory method, but the NBomber scenario lambda captured it for later execution.
- **Root Cause:** `using var` disposes at end of enclosing scope. The factory method returns a `ScenarioProps` — the `HttpClient` is disposed when the method returns, but the lambda still references it. During the actual load test run, every HTTP call uses a disposed `HttpClient` → `ObjectDisposedException`.
- **Fix:** Declare `HttpClient? httpClient = null;` without `using`. Initialize it in `.WithInit()` hook (runs before scenario execution) and dispose in `.WithClean()` hook (runs after). This ensures the client is alive for the entire test duration and properly cleaned up.
- **Prevention:** In NBomber scenarios, **never use `using var` for resources captured by the scenario lambda**. Always manage lifecycle via `WithInit`/`WithClean` hooks. This applies to any IDisposable shared across the scenario boundary (HttpClient, database connections, etc.).

---

### Lesson: Run code-review agent and explain-diff skill BEFORE committing — not after
- **Date:** 2026-03-31
- **Context:** The `using var` HttpClient bug, misleading RPS comments, and other issues in the load test code were only caught because the user manually ran a code review and sent findings one-by-one. Multiple built-in tools were available that should have caught these proactively.
- **Root Cause:** Skipped the pre-commit review step. Did not use any of the available analysis tools before pushing code.
- **Available tools that should have been used:**
  1. **`code-review` agent** — reviews staged/unstaged changes for bugs, security vulnerabilities, logic errors. Would have caught the `using var` use-after-dispose.
  2. **`dotnet-diag:optimizing-dotnet-performance` agent** — scans for ~50 .NET anti-patterns across async, memory, collections, I/O. Would have flagged the HttpClient lifecycle issue.
  3. **`explain-diff` skill** — PR summary & risk callouts (Curator guardrails, AppHost wiring, AI routing). Should run before every PR.
- **Fix:** Added mandatory pre-commit protocol below.
- **Prevention — Mandatory Pre-Commit Protocol:**
  1. **Before every commit:** Run `code-review` agent on staged changes.
  2. **For .NET code changes:** Run `dotnet-diag:optimizing-dotnet-performance` agent on modified files.
  3. **Before opening/updating a PR:** Run `/explain-diff` skill for risk callouts.
  4. **Never rely on the user to send individual review findings** — catch them proactively with the tools available.

## [2026-04-06] azd + Aspire azure.yaml: `host` must be `containerapp`, not `apphost`
- **What happened:** `azd up` and `azd infra gen` failed with "Aspire services must be configured to target the container app host at this time."
- **Root cause:** In azd's `importer.go`, when an Aspire AppHost is detected (`IsAspireHost: true`), the service config's `Host` field is checked against `ContainerAppTarget` (the string `"containerapp"`). If `host: apphost` is used instead, the check fails. Without any `services` section, azd falls back to looking for `infra/main.bicep`.
- **Fix:** Change `host: apphost` to `host: containerapp` in azure.yaml.
- **Prevention:** The correct azure.yaml for an Aspire project is:
  ```yaml
  name: bethuya
  services:
    app:
      language: dotnet
      project: AppHost/AppHost/AppHost.csproj
      host: containerapp   # NOT host: apphost
  ```

## [2026-04-06] EF Core auto-generated migration files violate IDE0161 (block-scoped namespace)
- **What happened:** `azd up` failed generating the Aspire manifest because `dotnet run --publisher manifest` built the Infrastructure project, which now contained the auto-generated Migrations directory. The generated `.cs` files use `namespace Foo { }` block syntax, violating `IDE0161` (enforced as error via `TreatWarningsAsErrors`).
- **Root cause:** `dotnet ef migrations add` generates C# files using block-scoped namespaces. There is no flag to emit file-scoped namespaces. These files are regenerated on every `dotnet ef migrations add`, so manual fixes get overwritten.
- **Fix:** Added to `.editorconfig`:
  ```
  [**/Migrations/**.cs]
  generated_code = true
  csharp_style_namespace_declarations = block_scoped:none
  dotnet_diagnostic.IDE0161.severity = none
  ```
- **Prevention:** Always add this `.editorconfig` rule before creating the first EF Core migration. It persists across all future migration additions without requiring manual intervention.

## [2026-07-25] BbFormFieldSelect<T> Options attribute vs child element syntax

- **What happened:** `AideProfile.razor` used `<Options>` as a child element inside `<BbFormFieldSelect>`, causing build error RZ10012.
- **Root cause:** `Options` is a component *parameter* of type `IEnumerable<SelectOption<T>>`, not a child element. It must be passed as a C# attribute value, not markup.
- **Fix:** `Options="@_genderOptions"` where `_genderOptions` is an `IReadOnlyList<SelectOption<string>>`. Constructor: `new SelectOption<string>(value, displayText)`.
- **Prevention:** Always define option lists as `IReadOnlyList<SelectOption<T>>` fields and pass via `Options="@field"` attribute. `@using BlazorBlueprint.Primitives` is required for `SelectOption<T>`.

## [2026-07-25] BbButton type="submit" fails at build — use native <button> for form submission

- **What happened:** `<BbButton type="submit">` caused CS0103: "The name 'submit' does not exist".
- **Root cause:** BB components don't support arbitrary HTML attributes. `type="submit"` is interpreted as a C# expression — `submit` is evaluated as a variable, not an HTML string.
- **Fix:** Use `<button type="submit" class="bb-button bb-button-default" ...>`. Native HTML button with BB styling classes works identically.
- **Prevention:** Never write HTML attribute names on BB component tags. For form submission, always use native `<button type="submit">`.

## [2026-07-25] EnsureCreatedAsync will not add new tables to an existing dev database

- **What happened:** Adding `AttendeeProfiles` table won't appear in existing dev databases — `EnsureCreatedAsync` is a no-op when the DB exists.
- **Root cause:** `EnsureCreatedAsync` only creates the schema on first run. It does not diff or alter an existing schema.
- **Fix:** Developers must drop and recreate their local dev database after adding new entities.
- **Prevention:** Every new `DbSet` should have an EF Core migration added. Note in PR description that existing dev DBs must be dropped. Long-term: replace `EnsureCreatedAsync` with `MigrateAsync`.
- **What happened:** CI E2E job (Playwright) failed with SQL error 18456 State 38 — `Login failed for user 'sa'. Reason: Failed to open the explicitly specified database 'BethuyaE2EDb'`. SQL Server was running; the database just didn't exist.
- **Root cause:** Deleting the Migrations folder (per no-migrations directive) made `GetPendingMigrationsAsync` return 0, so `MigrateAsync` was never called, so EF never created the database. The CI workflow also didn't run MigrationService — it started the backend directly, which can't create the schema.
- **Fix:** (1) Changed `MigrationWorker` from `MigrateAsync` to `EnsureCreatedAsync` — creates the database and all tables from the current EF model without any migration tracking. (2) Added a "Create E2E database schema" step in ci.yml that runs MigrationService (with the E2E connection string) before starting the backend API.
- **Prevention:** When no EF migrations exist, `MigrateAsync` is a no-op — it does NOT create the database. Use `EnsureCreatedAsync` for pre-migration / no-migration scenarios. `EnsureCreatedAsync` creates the DB + full schema from the model. Caveat: when migrations are eventually added at formal release, the existing `EnsureCreated`-provisioned database will need to be dropped/recreated (it has no `__EFMigrationsHistory` table).

## [2026-04-15] Sensitive onboarding cards must render truthful loading and load-error states

- **What happened:** The `/registration/social` LinkedIn URL WIP rendered the same "not connected yet" affordances before the saved social state had loaded, which made the page look disconnected even while the server roundtrip was still in flight.
- **Root cause:** The shared card layout reserved stable space for empty and connected states, but async hydration was still falling through to the default disconnected UI. That leaked the wrong story into a sensitive onboarding step and left connect actions clickable before the host had confirmed the saved state.
- **Fix:** Keep the card skeleton stable, but swap in explicit loading/error copy and disable OAuth/edit controls until the server has either hydrated the saved state or surfaced a blocking load error.
- **Prevention:** For async onboarding surfaces, never let loading reuse the disconnected state. Preserve layout rhythm with placeholders, but make status text truthful and gate primary actions until the saved state is known.

## [2026-04-15] Asymmetric onboarding cards need semantic tests, not symmetry assumptions

- **What happened:** The social onboarding step now gives LinkedIn more content and behavior than GitHub, so older “aligned cards” assertions were no longer the best regression net for the intended stacked layout.
- **Root cause:** The previous coverage was optimized for equal-height card rhythm and placeholder spacing, which is brittle once one card legitimately owns an extra field and longer copy.
- **Fix:** Assert stable DOM order, per-card ownership of the LinkedIn URL field, and provider-specific CTA/status copy instead of relying on visual symmetry assumptions.
- **Prevention:** When cards intentionally diverge, test the user-visible semantics each card owns and the reading order they appear in; leave pixel-perfect layout proof to screenshots, not unit-style render tests.

## [2026-04-15] Tall stacked onboarding cards need explicit continuation copy

- **What happened:** On shorter viewports, the taller LinkedIn card can push the GitHub card below the fold, so correct DOM order alone is not enough to tell users another required card still follows.
- **Root cause:** The stacked layout solved card asymmetry, but discoverability still depended on scroll affordance instead of durable visible copy.
- **Fix:** Add regression coverage for visible stack text that mentions GitHub and that it continues below, alongside the CTA gating tests.
- **Prevention:** When one stacked card can legitimately dominate the viewport, require an explicit continuation cue and test it semantically rather than relying on screenshots.

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

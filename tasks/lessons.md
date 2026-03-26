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

## [2026-03-26] Missing @rendermode silently kills all button clicks
- **What happened:** The "New Event" button on Events.razor did nothing at runtime. No JS errors, no visual feedback.
- **Root cause:** Pages without `@rendermode InteractiveServer` are rendered as static SSR. Event handlers (`OnClick`, `@bind`, etc.) are stripped — the browser never wires them up.
- **Fix:** Added `@rendermode InteractiveServer` to `Events.razor` and `Home.razor`.
- **Prevention:** Every page with any interactivity (buttons, dialogs, forms, two-way binding) **must** have `@rendermode InteractiveServer`. Make this a PR checklist item. E2E tests must assert dialogs opened after button click — a `ToBeVisibleAsync()` on the dialog immediately after the click would catch this within seconds.

## [2026-03-26] E2E tests must always run alongside unit tests in CI verification
- **What happened:** `dotnet test tests/Hackmum.Bethuya.Tests/` only ran TUnit unit tests. The Playwright MSTest project (`Hackmum.Bethuya.E2E`) was never executed, so the static SSR render mode bug shipped undetected.
- **Root cause:** The E2E project uses MSTest and requires a live Aspire stack — it was excluded from the unit test run command by targeting the wrong project path.
- **Fix:** Fixed E2E tests to assert dialog visibility immediately after button click, added `data-test="create-dialog"` wrapper, corrected `event-card`→`event-row` and `"View →"`→`data-test="view-event-btn"` selector mismatches.
- **Prevention:** CI verification must run both `dotnet test tests/Hackmum.Bethuya.Tests/` (TUnit) AND `dotnet test tests/Hackmum.Bethuya.E2E/` (Playwright, requires live stack). Always add a post-click `ToBeVisibleAsync()` assertion for any dialog or panel opened by a button — this is the cheapest guard against static SSR regressions.

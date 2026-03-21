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

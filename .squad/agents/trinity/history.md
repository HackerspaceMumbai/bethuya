# Project Context

- **Owner:** Augustine Correa
- **Project:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Created:** 2026-03-21T11:50:19.882Z

## Learnings

- **Event detail agent-console layout (2026-05-25T19:09:18.171+05:30):** `EventDetail.razor` now treats the planner workflow as a three-panel command surface: left for Planner/User conversation and cycle IDs, center for the dominant timeline editor, right for readable JSON-derived reasoning.
  - Keep the lifecycle controls split by intent: draft/context on the left, human markdown approval under the timeline, publish/new-cycle on the right.
  - Timeline blocks preserve `data-test="session-card"` while showing time axis, format tag, and inline edit/move/delete controls, so E2E selectors survive visual refactors.
  - Planner JSON should stay off the UI; surface `Constraints`, `Rationale`, and `Risks & Mitigations` through typed DTO helpers from `IPlanningCycleApi` instead.

- **Profile edit entry routing (2026-04-15):** Dashboard `Profile` nav should enter a resolver route first instead of hard-linking to the first onboarding page.
  - Use a dedicated `/profile` entry surface to decide whether the user must resume mandatory/social onboarding or can re-enter the saved edit flow.
  - When onboarding pages double as edit screens, keep the layout stable with explicit loading copy and disabled primary actions until saved values hydrate.
  - Mandatory and AIDE edits now rely on saved-profile GET contracts; if the step-gating lookup fails, AIDE should still attempt hydration rather than silently skipping the saved data call.

- Sensitive Bethuya pages must use `@rendermode InteractiveServer`; `data-test` selectors belong on plain HTML elements, not Blazor Blueprint components.
- **Create Event Flow (2026-03-21):** Integrated CreateEventDialog into both Home.razor and Events.razor with full notification support.
  - Added `OnNotification` EventCallback to CreateEventDialog for success/error feedback
  - Notification component requires `@using Bethuya.Hybrid.Shared.Components` directive
  - Created reusable pattern: dialog emits notifications, parent page renders Notification component with two-way binding
  - Events.razor now has "New Event" button with calendar-plus icon (data-test="new-event-btn")
  - All submit/cancel buttons have proper data-test wrappers for E2E tests
  - Form resets automatically after successful creation via ResetForm() method
- **GitHub Copilot SDK "Suggest Dates" button (2026-04-04, retroactive):** Added "✨ Suggest Dates" button to CreateEvent.razor. Button calls `IEventApi.RecommendDates()` (Refit client), displays loading state, auto-fills suggested dates into form on success, shows error message on failure. Styled with Blazor Blueprint components. Custom CSS for button spacing. 2 bUnit UI tests cover button render, click handler, and error state. Build: 0 errors/0 warnings.
- **DDD Rename: Create → Plan (2026-07-22):** Renamed all frontend "Create Event" references to "Plan Event" to align with DDD ubiquitous language.
  - Files renamed: `CreateEvent.razor` → `PlanEvent.razor`, `CreateEvent.razor.css` → `PlanEvent.razor.css`, `CreateEventFormModel.cs` → `PlanEventFormModel.cs`
  - Class renamed: `CreateEventFormModel` → `PlanEventFormModel`
  - Primary route: `/events/plan`; backward-compat route `/events/create` kept as second `@page` directive
  - Updated references: `PlanEventDto`, `EventApi.PlanAsync`, `FormName="plan-event"`, data-test attrs (`plan-event-page`, `plan-event-form`, `plan-event-submit`, `plan-error`)
  - Navigation updated in Events.razor and Home.razor to point to `/events/plan`
  - CSS class renamed `.create-event-page` → `.plan-event-page`
  - Build: 0 errors, 0 warnings (frontend). Test file `EventPlanningTests.cs` has stale `CreateEventRequest` ref from Tank's backend rename — Switch's scope.
- **AI coding primitives updated (2026-04-09):** Augustine updated README.md, AGENTS.md, and .github/copilot-instructions.md. Key UI/frontend conventions absorbed:
  - **Blazor Blueprint components ALWAYS first** — never custom CSS until BB components are exhausted. BbFormFieldInput, BbFormFieldSelect, BbFormSection for wrapped fields. BbTextarea, BbNumericInput, BbFileUpload, BbDatePicker, BbTimePicker require manual form-group + BbLabel wrapper.
  - **BB components don''t accept data-test directly** — wrap in `<div data-test="...">` to place test attribute on container, not on BB component itself.
  - **Custom CSS requires explanatory comment** — each custom CSS block must state why BB couldn''t handle it (e.g., "BB ShowPreview only supports local IBrowserFile — remote URL preview is custom").
  - **data-test selectors for all E2E** — never CSS classes. Enables stable Playwright selectors.
  - **InteractiveServer for sensitive pages** — auth/PII pages MUST use `@rendermode InteractiveServer` (global assignment on Routes in App.razor, not per-page).
  - **File-scoped namespaces, primary constructors, collection expressions** — C# 14 idioms enforced.
  - **Nullable enabled, TreatWarningsAsErrors** — fix all warnings; never suppress without documented justification.
- **LinkedIn social onboarding state-stability (2026-04-15):** `/registration/social` cannot let async hydration masquerade as the disconnected state.
  - Keep the LinkedIn public profile URL field visible in every state, but disable edits and OAuth buttons until saved social data has loaded or a blocking load error has been surfaced.
  - Reserve card status and feedback space so GitHub and LinkedIn actions stay aligned even when only one provider is connected or a provider-specific error appears.
  - Trim the typed LinkedIn URL when carrying it through the OAuth return URL and final save, but never treat that typed URL as completion without the verified LinkedIn member ID.
- **Asymmetric social cards should stack once provider behavior diverges (2026-04-15):** `/registration/social` reads more intentionally when LinkedIn and GitHub are stacked vertically instead of forced into equal-width columns.
  - Keep the LinkedIn URL field and lock-after-verification behavior intact inside the first card; do not flatten it to match GitHub.
  - Use supporting GitHub copy plus capped CTA width to keep the simpler second card from feeling sparse in the stacked layout.
  - Preserve the existing truthful loading/error placeholders inside each card so state changes do not make the stack jump or lie.
- **Stacked social cards (2026-04-15):** Stacked LinkedIn and GitHub verification cards vertically on /registration/social. LinkedIn card owns the public-profile URL field and preserves lock-after-verify behavior. GitHub card gains supportive copy to balance the stack. Commit e8dccdc607d683fdec3539a56a4f7f642d3eec53. Scoped code review clean; performance no regressions. Aspire web rebuild succeeded. Test execution blocked by pre-existing auth test compile failures (HasCount in UserInfoTests.cs and AuthProviderTypeTests.cs).
- **LinkedIn connect gating and stacked-card cue (2026-04-15):** `/registration/social` now keeps the LinkedIn CTA disabled until a non-empty trimmed public profile URL exists for the unverified state, which closes the blank-locked-URL path without weakening the verified member-ID rule.
  - Added a compact stack-intro cue so users immediately understand GitHub verification continues below the LinkedIn card in the vertical layout.
  - Added targeted render tests for LinkedIn CTA enablement and the stacked GitHub continuation cue.
  - Verification: `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` ✅, live `/registration/social` markup markers ✅, screenshot `artifacts\social-connect-ui.png` ✅. Targeted test project remains blocked by pre-existing `HasCount` compile failures in `Auth\UserInfoTests.cs` and `Auth\AuthProviderTypeTests.cs`; full test invocations that rebuild the web host also hit locked-assembly MSB3021/MSB3027 failures while Aspire `web` is running.

   - **Orchestration (2026-04-15T10:44:07Z):** Implementation committed as 1ee6bcb2e84998d323ea4a5a5a1f63af0d115a30. Build ✅, live UI ✅, screenshot ✅, code review ✅, Anvil verification ✅, performance review ✅. Orchestration log: .squad/orchestration-log/2026-04-15T10-44-07Z-trinity.md.

- **Visual continuation for stacked social cards (2026-04-16):** `/registration/social` keeps LinkedIn first and GitHub second in every state; the stronger follow-up cue is now visual instead of depending on text or card reordering.
  - Use a compact top-of-stack flow map plus an inter-card bridge (`social-stack-path`, `github-stack-bridge`) so users see the fixed LinkedIn → GitHub progression before and after data loads.
  - Intensify the bridge and GitHub follow-up accent only when LinkedIn is already connected and GitHub is still pending; this keeps the default disconnected state professional while still pulling attention downward at the right moment.
  - Key paths: `src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Pages\SocialProfileConnections.razor`, `src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Pages\SocialProfileConnections.razor.css`, `tests\Hackmum.Bethuya.Tests\UI\OnboardingNavigationRenderTests.cs`, `artifacts\social-connect-ui-continuation.png`.
- **Event detail event-flow timeline (2026-05-26T10:43:29.769+05:30):** The middle schedule panel now uses a left-aligned time axis with typed agenda nodes and a deterministic review focus (first non-break block, otherwise first block). Preserve data-test="session-card" on the timeline item wrapper and keep lifecycle controls outside the middle-panel visual refinement. Validation: dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal passed.
- **Timeline axis polish (2026-05-26):** Switch review caught that grid-based time labels need `align-content: space-between` so end times pin to the lower card axis instead of clustering under start times. Validation: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` passed.

- **Event Detail micro-interactions (2026-05-26T12:30+05:30):** Added shimmer skeletons, entrance animation, and AI-block reveal to the Event Detail timeline.
  - `_newlyGeneratedBlockIds: HashSet<Guid>` is set in `DraftWithAiAsync` after `ApplyDraftToUi`; cleared on approve, start-new-cycle, clear-all, and per-block on edit/move/delete. CSS animation on `.event-flow-card-shell.is-new-block` handles the visual fade-out — no JS timers needed.
  - Skeleton loaders render when `isAiDrafting && sessions.Count == 0` using `.event-flow-item` grid layout reuse; `.timeline-sk` shimmer uses `background-size: 200%` + `background-position` keyframe (no JS).
  - `@key="session.Id"` on the `<article>` ensures Blazor creates fresh DOM nodes for AI-generated items, so CSS entrance animation (`eventDetailBlockEnter`) fires automatically on generation without any JS.
  - All `@keyframes` names are prefixed `eventDetail*` to avoid collisions in the global CSS scope from Blazor's scoped stylesheet output.
  - `@media (prefers-reduced-motion: reduce)` disables shimmer, entrance, and reveal; replaces reveal with a static `outline`.
  - Validation: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

- **Agent Session status bar (2026-05-26T12:15:52.455+05:30):** Added a compact horizontal telemetry strip above the three-column schedule editor to expose hidden agent state.
  - `GetShortCycleId()` slices the first 8 hex chars of the Guid (`ToString("N")[..8]`) — no separator noise, immediately scannable.
  - Status transitions (`idle` → `generating` → `completed` → `locked`) are computed from existing state (`isAiDrafting`, `isCyclePublished`, `ActiveDraft`) so no new state fields are needed; pure derived rendering.
  - Animated pulse (`.agent-session-pulse`) is rendered conditionally only inside the status pill during `isAiDrafting` — keeps the bar visually silent when the agent is not running.
  - `data-test` selectors on the bar and each pill (`agent-session-status`, `agent-session-cycle-id`, `agent-session-thread-id`, `agent-session-agent-status`) are stable targets for E2E assertions.
  - Validation: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.** Replaced the two-card static Agent Interaction / Conversation Thread panel with a unified live Agent Console.
  - Draft button is the primary CTA at panel top (`data-test="ai-draft-btn"` preserved). The status dot, console header, and scrollable chat thread are all in one card so the hierarchy is immediately legible.
  - `_consoleMessages: List<ConsoleMessage>` accumulates through the lifecycle: `InitConsoleMessages()` seeds from current cycle state on page/reload; draft, approve, publish, and start-new-cycle each append or reset the thread appropriately.
  - Typing indicator (`agent-console-typing-indicator`) is an extra `.agent-console-msg--planner` row appended only while `isAiDrafting`, keeping the thread as the single source of truth for streaming state.
  - Prompt chip `@onclick` handlers are dedicated private methods (`ChipShortenEvent`, `ChipAddNetworking`, `ChipRebalance`) — avoids Razor parser failures that occur when string literals with double-quotes appear inside `@onclick="@(() => ...)"` attribute values.
  - `_targetDurationMinutes` (int?) uses a native `<input type="number">` with `UpdateTargetDuration(ChangeEventArgs)` rather than `BbNumericInput<int?>` — the generic type param syntax for nullable int is ambiguous without a live build check.
  - All CSS uses `hsl(var(--...))` theme variables; only the active-dot green (`hsl(142 71% 45%)`) is hardcoded since it's intentionally status-signal coloring that should read in both light and dark.
  - Validation: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

- **Event detail AI-first visual hierarchy (2026-05-26T11:58:23.304+05:30):** Scoped CSS `::deep` selectors on BB component wrappers (e.g. `.ai-draft-cta ::deep button`) work in Blazor scoped CSS — Razor emits the scoped attribute on the wrapper div, and `::deep` pierces into the shadow/child DOM rendered by the BB component so `width: 100%` and padding overrides take effect without touching BB source.
  - `opacity` + `:has(hover)` / `focus-within` on the CRUD action group (`.session-actions`) is the cleanest way to make utility controls secondary without removing them or adding JS — the row's `event-flow-card-shell` context already captures hover.
  - When a status badge needs custom rendering for one variant, render a conditional `<span>` with bespoke CSS instead of trying to parameterise `BbBadge`; keep the `BbBadge` path for all other variants so BB styling stays for the mainstream cases.

** Replaced the bare markdown-editor-only Human Review card with a structured insight deck above the draft editor.
  - Four semantically-colored cards (objectives=blue, constraints=yellow, risks=red, next-actions=green) extracted from `ActiveAgendaJson`; each card only renders when its data is non-empty.
  - Raw schema stays accessible via a collapsible `View Raw Schema` toggle (`_showRawSchema` bool + `ToggleRawSchema()`), hidden by default, with `data-test="review-raw-schema-toggle"` and `data-test="review-raw-schema-viewer"`.
  - `GetObjectives()` and `GetNextActions()` added alongside existing `GetConstraints()`/`GetRiskItems()`; `GetRawSchemaJson()` serializes via `System.Text.Json` with `WriteIndented=true`.
  - CSS uses `--review-insight-accent` RGB triples inside `rgb(var(...) / alpha)` for full dark/light parity — no raw light-only colors.
  - Right-side Agent Reasoning panel (Constraints, Rationale, Risks) preserved unchanged.
  - Validation: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

# Task Tracker

All work items must be added here **before** writing code (plan-first protocol).

## Format

```
## [YYYY-MM-DD] Task Title
- **Status:** pending | in-progress | done | blocked
- **Agent/Owner:** (who is doing this)
- **Description:** What needs to be done
- **Acceptance:** How we know it's done
```

---

## Active Tasks

<<<<<<< HEAD
## [2026-05-28] Fix live curation split-pane scrolling
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Use live browser inspection to fix the remaining curation layout issue where the queue/profile panes expand with their content instead of scrolling independently.
- **Acceptance:** ✅ The document no longer owns the curation scroll (`scrollHeight == clientHeight`). ✅ The Fairness Budget and decision tray stay visible around the bounded workbench. ✅ Live Playwright inspection proves the queue list scrolls independently (`scrollTop` changes with content taller than the viewport) and the profile detail pane scrolls independently after selecting a registrant. ✅ `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj --no-restore -v quiet` and `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore -v quiet` passed (184/184).

## [2026-05-28] Fix fixed workbench scrolling
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Replace remaining page-level scrolling behavior by making the curation workbench itself fixed between the Fairness Budget rail and decision rail.
- **Acceptance:** ✅ `.workbench-grid` is now `position: fixed` with `top: var(--curation-topbar-height)` and `bottom: var(--curation-tray-height)`, so the browser page cannot scroll queue/profile together. ✅ Increased top rail height variables so the queue/profile panes start below the full Fairness Budget. ✅ Restarted Aspire `web`. ✅ `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj --no-restore -v quiet` passed. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore -v quiet` passed (184/184).

## [2026-05-28] Fix curation scroll container regression
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Fix the regression where the queue/profile still scrolled together by correcting the fixed-rail shell box model and adding regression coverage for the split scroll containers.
- **Acceptance:** ✅ `.curation-shell` now uses `box-sizing: border-box` so reserved top/bottom rail padding stays inside the viewport height instead of creating page-level scroll. ✅ Added a CSS regression test for bounded curation scroll panes. ✅ Restarted the Aspire `web` resource. ✅ `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj -v minimal` passed. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` passed (184/184).

## [2026-05-28] Make curation panes scroll independently
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Keep the selected registrant card visible in the left queue while scrolling the right profile details by making the queue and profile panes independent scroll containers.
- **Acceptance:** ✅ `.curation-shell` now prevents page-level scrolling between the fixed rails, `.workbench-grid` is bounded to the remaining viewport height, `.queue-list` owns left-pane scrolling, and `.detail-stack` owns right-pane scrolling. ✅ Restarted the Aspire `web` resource. ✅ `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj -v minimal` passed. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` passed (183/183).

## [2026-05-28] Fix Fairness Budget CSS isolation
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Fix the top rail still appearing in normal document flow by ensuring fixed positioning applies to a native element scoped by the page CSS, not only to the Blazor Blueprint card root.
- **Acceptance:** ✅ Wrapped the Fairness Budget `BbCard` in a native `.curation-topbar` section and moved the Blueprint card styling to `.curation-topbar-card`, so fixed positioning is applied by the page-scoped CSS. ✅ Restarted the Aspire `web` resource after clearing the locked process. ✅ `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj -v minimal` passed. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` passed (183/183).

## [2026-05-28] Restore curation fixed rails
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Fix the regression where the Fairness Budget still vanished while scrolling and the bottom decision tray disappeared after the scroll-container change.
- **Acceptance:** ✅ Replaced fragile sticky behavior with viewport-fixed top and bottom rails. ✅ Restored reserved top/bottom spacing so content scrolls under the pinned Fairness Budget and decision tray without hiding them. ✅ `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj -v minimal` passed. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` passed (183/183).

## [2026-05-28] Make fairness budget sticky
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Keep the Fairness Budget visible while scrolling the curation workbench so selecting registrants in the queue shows immediate top-bar impact context.
- **Acceptance:** ✅ Updated `.curation-topbar` sticky behavior with explicit sticky inset and higher z-index/layering so it remains pinned above scrolling content. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` passed (183/183). ✅ `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj -v minimal` passed.

## [2026-05-28] Fix fairness sticky follow-up
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Address the follow-up where the fairness budget still scrolled out of view by making the curation shell itself the scroll container and binding the workbench to available viewport height.
- **Acceptance:** ✅ `.curation-shell` now uses fixed viewport height with internal vertical scrolling, and `.workbench-grid` now uses `flex: 1; min-height: 0;` to keep the top bar pinned while content scrolls beneath it. ✅ `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj -v minimal` passed. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` passed (183/183).

## [2026-05-28] Rework selection-driven fairness budget
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Update the curation top bar to match the requested Fairness Budget model: Core/Gender/First Timer/Underrep, Diversity/Geo/Lang/Edu/Inclusion, Cohort Health/Org/Reliability. It should show current cohort state by default and reveal deltas, chip glow, and affected/unaffected emphasis only after selecting a registrant.
- **Acceptance:** ✅ The top bar is grouped as Core, Diversity, and Cohort Health with Gender, First Timer, Underrep, Geo, Lang, Edu, Inclusion, Org, and Reliability chips. ✅ Deltas are hidden until a registrant is selected; selection reveals deltas, affected glow, and unaffected dimming. ✅ Regression coverage, `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal`, `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj -v minimal`, and `dotnet build AppHost\AppHost\AppHost.csproj -v minimal` pass.

## [2026-05-28] Subdue neutral curation impact copy
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Align queue-card impact copy with the wireframe by keeping green treatment only for positive fairness deltas and muting the "No positive fairness delta" state.
- **Acceptance:** ✅ Queue cards now apply `impact-line positive` only when there is a positive fairness delta and `impact-line subdued` for neutral/no-positive copy. ✅ Regression coverage asserts the subdued class for no-positive-delta text. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` passed (183/183) and `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj -v minimal` passed.

## [2026-05-28] Add curation gender core chip and click feedback
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Match the wireframe more closely by adding the missing Gender chip as the first Core metric without exposing per-registrant gender, and improve the queue-card click microinteraction so the active registrant clearly drives the projection/detail state.
- **Acceptance:** ✅ Gender appears first in the Core metric row as a k-anonymized aggregate signal derived from consented profile data, without exposing per-registrant gender. ✅ Clicking a registrant sets `aria-pressed`, highlights the selected card, shows "Projection live", and updates the projection banner/detail state. ✅ Regression coverage passed with `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` (182/182), plus web and AppHost builds.

## [2026-05-28] Align standout with organizer-marked contribution
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Ensure curation only presents a registrant as a standout when an organizer explicitly marked a past meetup contribution as standout; high intent, fairness lift, or attendance history alone should use a different label.
- **Acceptance:** ✅ Added an explicit `OrganizerMarkedStandout` signal and surfaced it as `HasOrganizerStandoutContribution` in curation profile summaries. ✅ Backend recommendations and the queue badge only show Standout when that organizer-marked contribution exists. ✅ Regression coverage proves no-history positive candidates use Strong new, while organizer-marked returning candidates use Returning standout. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` passed (180/180), `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj -v minimal` passed, and `dotnet build AppHost\AppHost\AppHost.csproj -v minimal` passed after clearing running resource locks.

## [2026-05-28] Enforce oversubscribed curation seed data
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Ensure the dev curation seeder always creates more mock registrants than the sandbox venue capacity so the generated event actually requires curation, even when a low reviewable count is requested.
- **Acceptance:** ✅ Seeder clamps requested reviewable registrants above venue capacity, so even low requests generate a curation-worthy oversubscribed event. ✅ Regression coverage proves low requests still create more reviewable registrants than capacity. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` passed (176/176).


## [2026-05-26] Add Show Agent Timeline overlay
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Add a toggle to Event Detail timeline that overlays agent planning intelligence, including energy levels, track balancing, and pacing decisions on each timeline block.
- **Acceptance:** ✅ `Show Agent Timeline` toggle appears in the timeline header. ✅ Enabling it adds visible energy, track-balance, and pacing-decision overlays to blocks. ✅ Default timeline remains clean when disabled. ✅ Dark-theme compatibility is preserved. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Enhance timeline continuity and duration scaling
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Strengthen the Event Detail schedule timeline so it reads as a continuous event flow, with stronger vertical connection, subtle gradient flow line, duration-aware block spacing, and updated type colors (talk blue, workshop purple, break neutral).
- **Acceptance:** ✅ Timeline axis is visually stronger and continuous with a subtle gradient flow line. ✅ Block spacing/height reflects session duration via per-block CSS variables. ✅ Type colors match the requested mapping: talk blue, workshop purple, break neutral. ✅ Dark-theme compatibility is preserved. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Refactor Refine Schedule into actionable steering panel
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Convert Event Detail refinement chips from passive local notes into actionable planner steering controls that show inline applying state, append conversational prompts, and invoke the planner with constraints.
- **Acceptance:** ✅ Shorten/add-networking/rebalance chips invoke planner generation with steering constraints. ✅ Inline `Applying: ...` status appears during execution. ✅ Add networking is sent as a schedule-modifying constraint. ✅ Chips disable during generation/published cycles. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Add visual diff highlights to agent-generated schedule blocks
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Mark newly generated and updated Event Detail timeline blocks after planner drafts with visible tags, accent borders/glow, and insertion animation so users can immediately see the agent's impact.
- **Acceptance:** ✅ New blocks show a `New` tag and glow. ✅ Modified blocks show an `Updated` tag and accent border. ✅ Generated insertions animate using the existing timeline entrance/reveal motion. ✅ Previous schedule state is compared before applying a redraft using stable block IDs. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Add Agent Reasoning layer to timeline blocks
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Add per-block reasoning metadata to Event Detail timeline cards, including why each block was placed there, its role in the event flow, and a compact visual indicator such as optimized/balanced.
- **Acceptance:** ✅ Each timeline block renders accessible expandable reasoning metadata. ✅ Reasoning includes flow role and placement rationale, including early keynote and talk-to-workshop continuity cases. ✅ Compact optimized/balanced indicators are shown on cards. ✅ Dark-theme-compatible CSS added. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Consolidate Agent Console cycle context metadata
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Remove the separate Cycle Context card from Event Detail and move Cycle ID, Thread ID, and agent status into compact metadata badges in the Agent Console header. Preserve the existing `planner-cycle-meta` selector on the new inline metadata container.
- **Acceptance:** ✅ Agent Console header contains cycle/thread/status metadata. ✅ Separate Cycle Context card is removed. ✅ `planner-cycle-meta` remains on the inline metadata container. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Make Event Detail Agent Console feel live while drafting
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Enhance the Event Detail Agent Console drafting flow with progressive "Planner is generating schedule..." state, animated dots, intermediate reasoning steps, and incremental typing messages before the planner API result is applied. Preserve the existing planner lifecycle and selectors.
- **Acceptance:** ✅ Draft CTA uses the existing generating state while the console shows live planner activity. ✅ Planner messages type in incrementally with a caret. ✅ Intermediate reasoning steps render (`Analyzing constraints...`, `Balancing session types...`, `Optimizing flow for engagement...`) with active/completed states. ✅ Subtle delay simulation precedes the planner API call without changing the backend contract. ✅ Reduced-motion handling disables new animations. ✅ Build: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

## [2026-05-26] Add micro-interactions to Event Detail for perceived responsiveness
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Add (1) explicit "Generating draft…" CTA state, (2) shimmer skeleton loaders in timeline while `isAiDrafting && sessions empty`, (3) CSS-only entrance transitions on new timeline blocks, (4) `is-new-block` reveal highlight on AI-generated cards cleared on interaction, (5) `timeline-is-updating` overlay when re-drafting over existing sessions. Respect `prefers-reduced-motion`. Preserve all data-test selectors and lifecycle behavior.
- **Acceptance:** ✅ ai-draft-btn shows "⏳ Generating draft…" while drafting. ✅ Skeleton renders in middle panel under `data-test="timeline-skeleton"` when `isAiDrafting && sessions empty`. ✅ New AI blocks have `is-new-block` card-shell class set from `_newlyGeneratedBlockIds`; cleared on edit/move/delete/approve/clear-all. ✅ `event-flow-item` entrance animation plays on new DOM nodes. ✅ `prefers-reduced-motion` block disables all animations. ✅ Build: 0 errors, 0 warnings.

## [2026-05-26] Add Agent Session status bar to Event Detail page
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Add a compact horizontal `Agent Session` status bar above the three-column schedule editor. Show shortened PlanningCycle ID (8 hex chars, or `no cycle`), truncated ConversationId (12 chars), and agent status (`idle` / `generating` / `completed` / `locked`) with an animated pulse during generation. Use monospaced pills, accent colors, and dark-mode-compatible theme tokens. Preserve all existing selectors and lifecycle behavior.
- **Acceptance:** ✅ Status bar renders above schedule-editor grid with `data-test="agent-session-status"`. ✅ Cycle pill (`data-test="agent-session-cycle-id"`), Thread pill (`data-test="agent-session-thread-id"`), Status pill (`data-test="agent-session-agent-status"`) all present. ✅ Animated pulse present only during `isAiDrafting`. ✅ All required selectors preserved. ✅ Build: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

## [2026-05-26] Improve Event Detail visual hierarchy — AI-first command center
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Make agent CTA primary/accented and full-width in Agent Console; add icon pill to "Proposed by Agent" badge; subdue timeline CRUD icons to utility weight; accent agent-driven elements (console header, thread, review insights, focus block) with restrained luminous primary color. Preserve all selectors, lifecycle behavior, dark mode.
- **Acceptance:** ✅ Draft CTA full-width + clearly accented (`ai-draft-cta` with primary gradient border/glow, inner button stretched to 100%). ✅ ProposedByAgent renders as `.status-badge-proposed-by-agent` icon pill with 🤖 prefix in both page header and Agent Console badge. ✅ Session CRUD buttons muted to `opacity: 0.38` at rest, restore to full on row hover/focus-within (`session-actions`). ✅ Agent Console card has primary accent border + glow (`agent-console-card`); header has accent underline rule (`agent-console-header`); Planner Insights panel has accent border (`agent-reasoning-card`); Review card header has accent left-edge strip (`review-card-header`). ✅ All required selectors preserved. ✅ Build: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

## [2026-05-26] Replace JSON schema display with structured review insight cards
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** In the Human Review section of `EventDetail.razor`, replace any raw JSON/schema-first display with structured insight cards (Objectives → blue bullet list, Constraints → yellow warning cards, Risks → red list with mitigation highlights, Next Actions → checklist). Keep raw JSON accessible via a collapsible `View Raw Schema` toggle hidden by default. Preserve all selectors, lifecycle behavior, dark mode, and right-side Agent Reasoning panel.
- **Acceptance:** ✅ New `data-test` selectors added: `review-insights`, `review-raw-schema-toggle`, `review-raw-schema-viewer`, `review-insight-objectives`, `review-insight-constraints`, `review-insight-risks`, `review-insight-next-actions`. ✅ All required selectors preserved. ✅ Raw schema collapsible (`_showRawSchema` bool + `ToggleRawSchema()`). ✅ Build: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

## [2026-05-26] Refactor left panel into interactive Agent Console
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Refactor the AI Planner / Agent Interaction left panel in `EventDetail.razor` into a live Agent Console. Move "Draft Schedule with AI" to prominent primary button at panel top; replace static planner card with conversational thread showing user+agent messages; add typing indicator when `isAiDrafting`; add Refine Schedule inputs (organizer constraints + target duration, UI-state only); add prompt chips (shorten event, add networking block, rebalance sessions) that update local thread state; keep refined command-center aesthetic compatible with existing timeline pass and dark mode.
- **Acceptance:** ✅ All required data-test selectors preserved (`schedule-editor`, `session-card`, `add-session-btn`, `ai-draft-btn`, `planner-cycle-meta`, `approve-btn`, `publish-btn`, `start-new-cycle-btn`). ✅ New selectors added: `agent-console-thread`, `refine-inputs`, `prompt-chips`, `chip-shorten-event`, `chip-add-networking`, `chip-rebalance`. ✅ Existing lifecycle behavior (draft/approve/publish/reload/start-new-cycle/clear-all/add/edit/move/delete) unchanged. ✅ `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` passed — 0 errors, 0 warnings.

## [2026-05-26] Refine event detail schedule into event-flow timeline
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Transform only the middle Event Detail schedule panel from stacked cards into a stronger left-axis vertical timeline while preserving planner lifecycle behavior and required `data-test` selectors.
- **Acceptance:** ✅ Timeline uses a left-aligned time axis with start/end alignment, deterministic review focus (first non-break block, else first block), and type-safe accents for talk/workshop/break/other. ✅ Existing draft/approve/publish/reload/start-new-cycle/clear-all/add/edit/move/delete behavior and required selectors are preserved. ✅ Validation passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-25] Refactor event detail planner into agent-console layout
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Refactor `EventDetail.razor` so the planner flow uses a cohesive three-column command-center layout: agent interaction and cycle context on the left, a dominant timeline agenda editor in the center, and readable reasoning insights from planner JSON on the right.
- **Acceptance:** ✅ `EventDetail.razor` now uses a responsive three-column agent-console layout with a dominant timeline agenda editor, left-side Planner/User thread and cycle context, and right-side readable constraints/rationale/risks insight cards instead of raw JSON/schema tabs. ✅ Preserved planner draft, approve, publish, reload, start-new-cycle, clear-all, add/edit/move/delete session actions and required `data-test` selectors. ✅ Updated E2E coverage to assert readable Planner insights and absence of raw JSON UI. ✅ Validation passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`; `dotnet build .\tests\Hackmum.Bethuya.E2E\Hackmum.Bethuya.E2E.csproj --no-restore -v minimal`. ✅ Live Aspire visual proof captured at `artifacts\event-detail-agent-layout.png`.
  
## [2026-05-26] Redesign curation intelligence workbench
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Rework `/curation/{eventId}` to align much more closely with the curation intelligence wireframe using Blazor Blueprint-first composition, minimal page CSS, and real backend/shared contract support for the richer decision panels.
- **Acceptance:** ✅ The curation page now follows the provided command-center wireframe more closely with a sticky fairness top bar, metric clusters, a split queue/detail workbench, selected-attendee intelligence panels, and a sticky human decision bar. ✅ The implementation uses Blazor Blueprint cards, inputs, selects, buttons, badges, and Lucide icons, with page CSS constrained to the split-pane shell and wireframe-specific visual treatments. ✅ `dotnet build src\Bethuya.Hybrid\Bethuya.Hybrid.Web\Bethuya.Hybrid.Web.csproj -v minimal`, `dotnet build AppHost\AppHost\AppHost.csproj -v minimal`, and `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj -v minimal` passed.

## [2026-05-26] Add curation seed command
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Add a dev-only backend seed flow plus an Aspire dashboard command so organizers can generate a fresh curation sandbox event with about 50 varied registrants and historical attendance edge cases.
- **Acceptance:** ✅ Backend exposes a dev-only curation seed action, AppHost surfaces the backend seed command, the seed creates a curation-ready event with reviewable registrants plus fairness/reliability edge cases, and focused/full validation passes.
## [2026-05-31] Refine institution and organization capture in onboarding
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Improve mandatory onboarding employment branching so institution/company data is captured with clearer labels, better role-driven requirements, and lower-friction inputs for curation quality.
- **Acceptance:** ✅ Employment options reduced to Student, Working Professional, and Independent / Freelancer. ✅ Student path now shows only one required field labeled "College / University" with placeholder "Start typing your college name" and college-balance helper text. ✅ Working Professional and Independent / Freelancer paths now show only one required "Company / Organization" field with organization-balance helper text. ✅ Backend mandatory-profile validation and social requirement messaging updated to align with the new statuses while preserving legacy compatibility. ✅ Onboarding/auth tests updated and `dotnet test tests\\Hackmum.Bethuya.Tests\\Hackmum.Bethuya.Tests.csproj -v minimal /p:NuGetAudit=false` passed (169/169).

## [2026-05-31] Redesign social verification step (onboarding step 2)
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Refactor `/registration/social` to remove manual LinkedIn URL/handle-driven UX, present action-first LinkedIn/GitHub verification cards with clear status and motivation messaging, and enforce role-based required-provider gating before continue.
- **Acceptance:** ✅ Removed manual LinkedIn URL entry from Step 2 and kept the flow OAuth action-driven via two provider cards only. ✅ Added verification header (`LinkedIn ✅/❌`, `GitHub ✅/❌`), benefit messaging, and simplified right panel to the requested 3-step “What happens next” sequence. ✅ Cards now show required-role labels (“Required for working professionals” / “Required for students”) with connected/not-connected status and connect actions. ✅ Continue remains disabled until required-provider gating is satisfied (GitHub for students, LinkedIn for working professionals), while both can still be connected for stronger signal. ✅ `dotnet test tests\\Hackmum.Bethuya.Tests\\Hackmum.Bethuya.Tests.csproj -v minimal /p:NuGetAudit=false` passed (168/168).

## [2026-05-31] Simplify mandatory onboarding profile form
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Refactor `/registration/mandatory` to remove heavy identity-verification UI from onboarding, keep profile setup low-friction, and align copy/layout with the new registration-time curation data collection approach.
- **Acceptance:** ✅ Removed the Identity Document inputs (government ID type + last 4 digits) from the onboarding UI and restructured into Personal Details, Professional Context, and minimal Location (city + country). ✅ Updated heading/card/sidebar/footer copy to the requested “Let’s set up your Bethuya profile”, merged “Why we ask” + “Next” guidance, and added the GitHub/LinkedIn next-step hint. ✅ Relaxed mandatory-profile request validation so government-ID fields are no longer required by onboarding saves. ✅ Updated onboarding render + curation E2E tests for the new form shape and messaging. ✅ `dotnet test tests\\Hackmum.Bethuya.Tests\\Hackmum.Bethuya.Tests.csproj -v minimal /p:NuGetAudit=false` passed (172/172).

## [2026-05-31] Revamp registration flow for curation data and dev toggle
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Update the user event-registration flow so curation-relevant data is captured during registration, wire that data through shared/backend contracts into inclusion-signal generation, and add a development toggle that allows running the full onboarding registration flow instead of the default bypass path.
- **Acceptance:** ✅ `/events/{eventId}/registrations` now captures neighborhood, language proficiency, educational background, and socioeconomic background with updated consent copy, then sends them via `CreateRegistrationDto`. ✅ Backend `CreateRegistrationRequest` + registration endpoint now validate curation-source completeness and build inclusion signals from registration data (with authenticated-profile/email fallback when needed). ✅ AppHost now supports `ONBOARDING_ENABLE_FLOW_IN_DEVELOPMENT=true` (or `Onboarding:EnableFlowInDevelopment`) to disable onboarding bypass in development and run the full `/registration/mandatory -> /registration/social -> /registration/aide` flow. ✅ `dotnet test tests\\Hackmum.Bethuya.Tests\\Hackmum.Bethuya.Tests.csproj -v minimal /p:NuGetAudit=false` passed (172/172).
=======
## [2026-06-03] Sync EventDetail ClearAll with cycle/review draft state
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Verify and fix stale cycle/review state after `ClearAll` so header/session bar/review editor align with `Draft` status.
- **Acceptance:** ✅ `ClearAll` now clears `_activeCycle`, `_plannerMarkdown`, review visibility (`_showRawSchema`), and refreshes console thread state via `InitConsoleMessages()` while preserving minimal behavior. ✅ `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` passed.

## [2026-06-03] Deduplicate prompt chip constraints in EventDetail planner request
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Verify and fix duplicate constraint emission when prompt chips append text to `_refineConstraints` and the same steering prompt is passed to `BuildPlannerConstraints`.
- **Acceptance:** ✅ `ApplyPromptChipAsync` only appends new chip text when an equivalent semicolon token is not already present. ✅ `BuildPlannerConstraints` skips appending `steeringPrompt` when an equivalent normalized token already exists in `_refineConstraints`. ✅ `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` passed.
>>>>>>> 4a5e048 (feat(EventDetail): Prevent duplicate prompt chip constraints and enhance ClearAll functionality)

## [2026-05-26] Add Show Agent Timeline overlay
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Add a toggle to Event Detail timeline that overlays agent planning intelligence, including energy levels, track balancing, and pacing decisions on each timeline block.
- **Acceptance:** ✅ `Show Agent Timeline` toggle appears in the timeline header. ✅ Enabling it adds visible energy, track-balance, and pacing-decision overlays to blocks. ✅ Default timeline remains clean when disabled. ✅ Dark-theme compatibility is preserved. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Enhance timeline continuity and duration scaling
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Strengthen the Event Detail schedule timeline so it reads as a continuous event flow, with stronger vertical connection, subtle gradient flow line, duration-aware block spacing, and updated type colors (talk blue, workshop purple, break neutral).
- **Acceptance:** ✅ Timeline axis is visually stronger and continuous with a subtle gradient flow line. ✅ Block spacing/height reflects session duration via per-block CSS variables. ✅ Type colors match the requested mapping: talk blue, workshop purple, break neutral. ✅ Dark-theme compatibility is preserved. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Refactor Refine Schedule into actionable steering panel
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Convert Event Detail refinement chips from passive local notes into actionable planner steering controls that show inline applying state, append conversational prompts, and invoke the planner with constraints.
- **Acceptance:** ✅ Shorten/add-networking/rebalance chips invoke planner generation with steering constraints. ✅ Inline `Applying: ...` status appears during execution. ✅ Add networking is sent as a schedule-modifying constraint. ✅ Chips disable during generation/published cycles. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Add visual diff highlights to agent-generated schedule blocks
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Mark newly generated and updated Event Detail timeline blocks after planner drafts with visible tags, accent borders/glow, and insertion animation so users can immediately see the agent's impact.
- **Acceptance:** ✅ New blocks show a `New` tag and glow. ✅ Modified blocks show an `Updated` tag and accent border. ✅ Generated insertions animate using the existing timeline entrance/reveal motion. ✅ Previous schedule state is compared before applying a redraft using stable block IDs. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Add Agent Reasoning layer to timeline blocks
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Add per-block reasoning metadata to Event Detail timeline cards, including why each block was placed there, its role in the event flow, and a compact visual indicator such as optimized/balanced.
- **Acceptance:** ✅ Each timeline block renders accessible expandable reasoning metadata. ✅ Reasoning includes flow role and placement rationale, including early keynote and talk-to-workshop continuity cases. ✅ Compact optimized/balanced indicators are shown on cards. ✅ Dark-theme-compatible CSS added. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Consolidate Agent Console cycle context metadata
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Remove the separate Cycle Context card from Event Detail and move Cycle ID, Thread ID, and agent status into compact metadata badges in the Agent Console header. Preserve the existing `planner-cycle-meta` selector on the new inline metadata container.
- **Acceptance:** ✅ Agent Console header contains cycle/thread/status metadata. ✅ Separate Cycle Context card is removed. ✅ `planner-cycle-meta` remains on the inline metadata container. ✅ Shared project build passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-26] Make Event Detail Agent Console feel live while drafting
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Enhance the Event Detail Agent Console drafting flow with progressive "Planner is generating schedule..." state, animated dots, intermediate reasoning steps, and incremental typing messages before the planner API result is applied. Preserve the existing planner lifecycle and selectors.
- **Acceptance:** ✅ Draft CTA uses the existing generating state while the console shows live planner activity. ✅ Planner messages type in incrementally with a caret. ✅ Intermediate reasoning steps render (`Analyzing constraints...`, `Balancing session types...`, `Optimizing flow for engagement...`) with active/completed states. ✅ Subtle delay simulation precedes the planner API call without changing the backend contract. ✅ Reduced-motion handling disables new animations. ✅ Build: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

## [2026-05-26] Add micro-interactions to Event Detail for perceived responsiveness
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Add (1) explicit "Generating draft…" CTA state, (2) shimmer skeleton loaders in timeline while `isAiDrafting && sessions empty`, (3) CSS-only entrance transitions on new timeline blocks, (4) `is-new-block` reveal highlight on AI-generated cards cleared on interaction, (5) `timeline-is-updating` overlay when re-drafting over existing sessions. Respect `prefers-reduced-motion`. Preserve all data-test selectors and lifecycle behavior.
- **Acceptance:** ✅ ai-draft-btn shows "⏳ Generating draft…" while drafting. ✅ Skeleton renders in middle panel under `data-test="timeline-skeleton"` when `isAiDrafting && sessions empty`. ✅ New AI blocks have `is-new-block` card-shell class set from `_newlyGeneratedBlockIds`; cleared on edit/move/delete/approve/clear-all. ✅ `event-flow-item` entrance animation plays on new DOM nodes. ✅ `prefers-reduced-motion` block disables all animations. ✅ Build: 0 errors, 0 warnings.

## [2026-05-26] Add Agent Session status bar to Event Detail page
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Add a compact horizontal `Agent Session` status bar above the three-column schedule editor. Show shortened PlanningCycle ID (8 hex chars, or `no cycle`), truncated ConversationId (12 chars), and agent status (`idle` / `generating` / `completed` / `locked`) with an animated pulse during generation. Use monospaced pills, accent colors, and dark-mode-compatible theme tokens. Preserve all existing selectors and lifecycle behavior.
- **Acceptance:** ✅ Status bar renders above schedule-editor grid with `data-test="agent-session-status"`. ✅ Cycle pill (`data-test="agent-session-cycle-id"`), Thread pill (`data-test="agent-session-thread-id"`), Status pill (`data-test="agent-session-agent-status"`) all present. ✅ Animated pulse present only during `isAiDrafting`. ✅ All required selectors preserved. ✅ Build: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

## [2026-05-26] Improve Event Detail visual hierarchy — AI-first command center
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Make agent CTA primary/accented and full-width in Agent Console; add icon pill to "Proposed by Agent" badge; subdue timeline CRUD icons to utility weight; accent agent-driven elements (console header, thread, review insights, focus block) with restrained luminous primary color. Preserve all selectors, lifecycle behavior, dark mode.
- **Acceptance:** ✅ Draft CTA full-width + clearly accented (`ai-draft-cta` with primary gradient border/glow, inner button stretched to 100%). ✅ ProposedByAgent renders as `.status-badge-proposed-by-agent` icon pill with 🤖 prefix in both page header and Agent Console badge. ✅ Session CRUD buttons muted to `opacity: 0.38` at rest, restore to full on row hover/focus-within (`session-actions`). ✅ Agent Console card has primary accent border + glow (`agent-console-card`); header has accent underline rule (`agent-console-header`); Planner Insights panel has accent border (`agent-reasoning-card`); Review card header has accent left-edge strip (`review-card-header`). ✅ All required selectors preserved. ✅ Build: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

## [2026-05-26] Replace JSON schema display with structured review insight cards
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** In the Human Review section of `EventDetail.razor`, replace any raw JSON/schema-first display with structured insight cards (Objectives → blue bullet list, Constraints → yellow warning cards, Risks → red list with mitigation highlights, Next Actions → checklist). Keep raw JSON accessible via a collapsible `View Raw Schema` toggle hidden by default. Preserve all selectors, lifecycle behavior, dark mode, and right-side Agent Reasoning panel.
- **Acceptance:** ✅ New `data-test` selectors added: `review-insights`, `review-raw-schema-toggle`, `review-raw-schema-viewer`, `review-insight-objectives`, `review-insight-constraints`, `review-insight-risks`, `review-insight-next-actions`. ✅ All required selectors preserved. ✅ Raw schema collapsible (`_showRawSchema` bool + `ToggleRawSchema()`). ✅ Build: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` — 0 errors, 0 warnings.

## [2026-05-26] Refactor left panel into interactive Agent Console
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Refactor the AI Planner / Agent Interaction left panel in `EventDetail.razor` into a live Agent Console. Move "Draft Schedule with AI" to prominent primary button at panel top; replace static planner card with conversational thread showing user+agent messages; add typing indicator when `isAiDrafting`; add Refine Schedule inputs (organizer constraints + target duration, UI-state only); add prompt chips (shorten event, add networking block, rebalance sessions) that update local thread state; keep refined command-center aesthetic compatible with existing timeline pass and dark mode.
- **Acceptance:** ✅ All required data-test selectors preserved (`schedule-editor`, `session-card`, `add-session-btn`, `ai-draft-btn`, `planner-cycle-meta`, `approve-btn`, `publish-btn`, `start-new-cycle-btn`). ✅ New selectors added: `agent-console-thread`, `refine-inputs`, `prompt-chips`, `chip-shorten-event`, `chip-add-networking`, `chip-rebalance`. ✅ Existing lifecycle behavior (draft/approve/publish/reload/start-new-cycle/clear-all/add/edit/move/delete) unchanged. ✅ `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` passed — 0 errors, 0 warnings.

## [2026-05-26] Refine event detail schedule into event-flow timeline
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Transform only the middle Event Detail schedule panel from stacked cards into a stronger left-axis vertical timeline while preserving planner lifecycle behavior and required `data-test` selectors.
- **Acceptance:** ✅ Timeline uses a left-aligned time axis with start/end alignment, deterministic review focus (first non-break block, else first block), and type-safe accents for talk/workshop/break/other. ✅ Existing draft/approve/publish/reload/start-new-cycle/clear-all/add/edit/move/delete behavior and required selectors are preserved. ✅ Validation passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`.

## [2026-05-25] Refactor event detail planner into agent-console layout
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Refactor `EventDetail.razor` so the planner flow uses a cohesive three-column command-center layout: agent interaction and cycle context on the left, a dominant timeline agenda editor in the center, and readable reasoning insights from planner JSON on the right.
- **Acceptance:** ✅ `EventDetail.razor` now uses a responsive three-column agent-console layout with a dominant timeline agenda editor, left-side Planner/User thread and cycle context, and right-side readable constraints/rationale/risks insight cards instead of raw JSON/schema tabs. ✅ Preserved planner draft, approve, publish, reload, start-new-cycle, clear-all, add/edit/move/delete session actions and required `data-test` selectors. ✅ Updated E2E coverage to assert readable Planner insights and absence of raw JSON UI. ✅ Validation passed: `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal`; `dotnet build .\tests\Hackmum.Bethuya.E2E\Hackmum.Bethuya.E2E.csproj --no-restore -v minimal`. ✅ Live Aspire visual proof captured at `artifacts\event-detail-agent-layout.png`.

## [2026-05-23] Fix planner draft 404
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Trace why `Draft Schedule with AI` returns `404 NotFound` on the event detail page, restore the missing planning-cycle HTTP wiring, and add regression coverage for the backend route seam.
- **Acceptance:** ✅ Added the missing `app.MapPlanningCycleEndpoints()` route wiring in `Hackmum.Bethuya.Backend`. ✅ Registered `PlanningCycleService` in backend DI so the mapped planning-cycle endpoints can start without startup-time request-delegate inference failures. ✅ Added `PlanningCycleEndpointValidationTests` to prove the HTTP seam reaches the planning-cycle domain handler instead of falling through to a missing-route 404. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore -v minimal` passed (172/172).

## [2026-05-23] Fix View Schedule runtime crash
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Trace the `View Schedule` crash on `/events/{id}`, restore any missing dependency injection or service wiring required by `EventDetail.razor`, and confirm the event detail page renders without the unhandled exception.
- **Acceptance:** ✅ Added the missing `IPlanningCycleApi` Refit registration to `Bethuya.Hybrid.Web`. ✅ Local AppHost validation at `https://localhost:59177/events/019e53cb-9462-7783-8a90-df2561599e57` now returns the schedule editor markup without the `PlanningCycleApi` service-resolution exception. ✅ `dotnet build Bethuya.slnx --no-incremental -v minimal` passed.

## [2026-05-23] Fix solution build blockers
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Investigate the current `dotnet build Bethuya.slnx` failures, remove stale shared API contracts, correct backend compile errors, and rebuild until the solution compiles cleanly or only known unrelated blockers remain.
- **Acceptance:** ✅ Removed the stale `ImageUploadResponse` Refit method from `IEventApi` after confirming the backend no longer exposes `/api/images/upload`. ✅ Fixed the malformed duplicate `group.MapDelete` declaration in `EventEndpoints`. ✅ `dotnet build Bethuya.slnx` now succeeds cleanly.

## [2026-05-16] Add privacy-safe inclusion signals to curation fairness budget
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Extend registration/profile-to-curation flow with derived InclusionSignals, organizer-configurable per-event fairness targets, fairness aggregation + impact preview, and UI updates that never expose raw sensitive AIDE responses.
- **Acceptance:** Backend persists derived geo/language/education/socioeconomic signals and per-event fairness targets; curation dashboard shows current/target/deficit + alerts + impact preview; sensitive raw fields never surface in curation APIs/UI; new unit + E2E coverage passes with curation screenshot artifact.

## [2026-04-11] Fix CI Playwright E2E harness drift
- **Status:** done
- **Agent/Owner:** Tank
- **Description:** Investigate the failing GitHub Actions Playwright E2E job, verify whether the break is infrastructure or test-harness drift, and fix the selector/navigation assertions so they match the current Blazor UI contracts.
- **Acceptance:** ✅ Root cause identified from CI evidence. ✅ E2E tests updated to use the real dashboard selector, client-side navigation waits, and published-event detail flow. ✅ Relevant validation run successfully. ✅ Tank history and lessons updated.

## [2026-04-27] Patch transitive DataProtection restore vulnerability
- **Status:** done
- **Agent/Owner:** Tank (Backend Dev)
- **Description:** Investigate why `Bethuya.MigrationService` and `AppHost` restore still resolve vulnerable `Microsoft.AspNetCore.DataProtection` 10.0.0 transitively, then apply the smallest central package management fix so restore no longer pulls the vulnerable version.
- **Acceptance:** ✅ `Directory.Packages.props` now centrally pins `Microsoft.AspNetCore.DataProtection` `10.0.7`, plus the required companion transitive pins `System.Security.Cryptography.Xml` `10.0.7` and `Microsoft.Extensions.Hosting.Abstractions` `10.0.7`, so the vulnerable `10.0.0` graph is no longer selected. ✅ `dotnet restore` succeeds for both `src\Bethuya.MigrationService\Bethuya.MigrationService.csproj` and `AppHost\AppHost\AppHost.csproj`, and `dotnet build .\src\Bethuya.MigrationService\Bethuya.MigrationService.csproj --no-restore -v minimal` passed. ✅ Recorded the central-package remediation lesson for future dependency fixes.

## [2026-04-16] Strengthen GitHub continuation cue on social onboarding
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Keep the `/registration/social` cards state-stable with LinkedIn first and GitHub second, but replace the weak stacked-card cue with a stronger visual continuation treatment that still feels professional across loading, connected, and error states.
- **Acceptance:** ✅ `/registration/social` now keeps the fixed LinkedIn → GitHub order while adding a visual stack map plus an inter-card bridge that makes the GitHub follow-up obvious without reordering. ✅ Updated onboarding render tests now assert the visual cue structure and the highlighted GitHub follow-up state when LinkedIn is connected but GitHub is still pending. ✅ `dotnet build .\src\Bethuya.Hybrid\Bethuya.Hybrid.Shared\Bethuya.Hybrid.Shared.csproj --no-restore -v minimal` passed, Aspire `web` rebuild/live markup checks passed, and `dotnet test --project .\tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore -v minimal` finished at 154/158 with only the pre-existing LinkedIn bUnit input-event failures remaining. ✅ Visual proof refreshed at `artifacts\social-connect-ui-continuation.png`.

## [2026-04-15] Stabilize profile edit hydration after onboarding completion
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Patch the Blazor onboarding/profile edit flow so the Profile nav lands in the correct edit surface after completion, mandatory/social/AIDE steps load saved values truthfully while hydrating, and incomplete users still resume the correct step.
- **Acceptance:** ✅ Profile nav now routes through `/profile` so incomplete users resume the right step while completed users re-enter the edit flow. ✅ Mandatory and AIDE pages now hydrate saved values with disabled/loading states instead of reopening blank. ✅ Backend + web builds succeeded after clearing Aspire locks, scoped review/perf/Anvil checks were clean, and the full test run stayed at 153/158 with the remaining 5 failures isolated to pre-existing `SocialProfileConnections` regressions.

## [2026-04-15] Rehydrate saved profile data in the onboarding edit flow
- **Status:** done
- **Agent/Owner:** Tank (Backend Dev)
- **Description:** Trace the post-onboarding profile/edit path, fix any backend/shared-contract load gaps so saved mandatory, social, and AIDE profile data rehydrates when users revisit Profile, and preserve the existing three-step completion flow.
- **Acceptance:** ✅ Added dedicated profile read contracts/endpoints so `/registration/mandatory`, `/registration/social`, and `/registration/aide` can rehydrate saved state instead of reopening blank. ✅ Mandatory and AIDE pages now load persisted data before edit-mode submit continues the existing three-step flow. ✅ Backend/shared projects compile successfully when built with isolated output paths to avoid running-dev-process file locks, and the isolated TUnit app now runs with 153/158 passing; the remaining 5 failures are pre-existing `SocialProfileConnections` bUnit regressions around LinkedIn input event wiring/helper-copy assertions.

## [2026-04-15] Guard LinkedIn connect on `/registration/social`
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Disable the LinkedIn connect/reconnect action until a meaningful LinkedIn public profile URL is present when LinkedIn is still unverified, prevent blank locked URL states through the normal onboarding path, and add a clear visual cue that the stacked GitHub card continues below LinkedIn without weakening the verified-member-ID completion rule.
- **Acceptance:** ✅ LinkedIn connect now stays disabled until the unverified card has a non-empty trimmed public profile URL, while verified LinkedIn still keeps the field locked and the reconnect action available. ✅ Added an intentional stack-intro cue so users can see GitHub continues below the LinkedIn card, and verified the new markers on the live `https://localhost:7400/registration/social` page plus `artifacts\social-connect-ui.png`. ⚠️ Targeted `OnboardingNavigationRenderTests` execution is still blocked by the repository’s pre-existing auth-test compile failures (`HasCount` usage in `Auth\UserInfoTests.cs` and `Auth\AuthProviderTypeTests.cs`); separate test attempts that rebuild the web host are additionally blocked when the running `web` resource locks copied assemblies.

## [2026-04-15] Restack social verification cards on `/registration/social`
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Rework the `/registration/social` card layout so LinkedIn and GitHub stack vertically, preserve LinkedIn URL edit/lock behavior across truthful async states, keep the CTA/right-column hierarchy polished, and add targeted regression coverage for the stacked presentation.
- **Acceptance:** ✅ LinkedIn and GitHub now render in a single intentional vertical stack on the live `/registration/social` page while preserving the existing loading, error, mixed, connected, and disconnected state contract. ✅ GitHub gained richer support copy so the simpler card still feels complete beside the LinkedIn URL workflow. ✅ Updated targeted onboarding render tests to assert the stacked wrapper and GitHub support copy. ✅ `aspire` rebuild for `web` succeeded and live HTML at `https://localhost:7400/registration/social` includes the new stacked class/copy markers. ⚠️ `dotnet test --project .\tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore --filter "FullyQualifiedName~SocialProfileConnections|FullyQualifiedName~OnboardingNavigationRenderTests"` remains blocked by pre-existing unrelated auth-test compile failures (`HasCount` usage in `Auth\UserInfoTests.cs` and `Auth\AuthProviderTypeTests.cs`).

## [2026-04-15] Add stacked social-card regression coverage
- **Status:** done
- **Agent/Owner:** Switch (Tester)
- **Description:** Update targeted `/registration/social` regression coverage so the asymmetric LinkedIn and GitHub cards stay semantically stable if Trinity stacks them vertically, with LinkedIn retaining its editable/locked URL field behavior and GitHub keeping clear standalone CTA copy.
- **Acceptance:** ✅ Replaced the older “aligned cards” assertions with focused bUnit checks that lock LinkedIn-first card order, the single LinkedIn URL field, and GitHub-only status/meta/CTA semantics without depending on brittle visual measurements. ✅ Recorded the regression pattern for the squad in `.squad/decisions/inbox/switch-social-card-stack-regression.md`. ⚠️ `dotnet test --project .\tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore --filter "FullyQualifiedName~SocialProfileConnections"` is still blocked by pre-existing unrelated auth-test compile failures (`HasCount` usage in `Auth\UserInfoTests.cs` and `Auth\AuthProviderTypeTests.cs`), so the updated social tests could not be executed end-to-end in this worktree.

## [2026-04-15] Patch transitive System.Security.Cryptography.Xml vulnerability
- **Status:** done
- **Agent/Owner:** Tank (Backend Dev)
- **Description:** Trace the package path pulling `System.Security.Cryptography.Xml` 9.0.0 into `Bethuya.MigrationService`, apply the smallest central-package fix that removes the vulnerable version without weakening NU1901 gating, and validate the AppHost restore/build path as far as existing blockers allow.
- **Acceptance:** ✅ Traced the transitive path as `Bethuya.MigrationService` → `ServiceDefaults` → `Microsoft.Identity.Web` 3.8.4 → `Microsoft.Identity.Web.TokenCache` / `Microsoft.AspNetCore.DataProtection` → `System.Security.Cryptography.Xml`. ✅ Pinned `System.Security.Cryptography.Xml` to `10.0.6` centrally in `Directory.Packages.props` without suppressing NU1901. ✅ `dotnet restore` now succeeds for both `src\Bethuya.MigrationService\Bethuya.MigrationService.csproj` and `AppHost\AppHost\AppHost.csproj`, and the resolved package graph shows `System.Security.Cryptography.Xml` `10.0.6`. ⚠️ `dotnet build AppHost\AppHost\AppHost.csproj --no-restore` still fails on the repository’s pre-existing duplicate assembly-attribute errors in `Hackmum.Bethuya.Core`, `Hackmum.Bethuya.AI`, `ServiceDefaults`, and `Bethuya.Hybrid.Shared`, with no remaining NU1901 failures.

## [2026-04-14] Prepare LinkedIn social onboarding validation coverage
- **Status:** done
- **Agent/Owner:** Switch (Tester)
- **Description:** Review the in-flight `/registration/social` LinkedIn URL UX changes and update targeted tests so Switch can validate alignment, editable/locked URL states, caution copy, and the required full-time-employed flow as soon as Trinity/Tank finish landing their work.
- **Acceptance:** ✅ Added targeted bUnit coverage for the LinkedIn URL field’s editable and locked states, the mixed LinkedIn/GitHub card structure used to keep actions aligned, and the employee-required flow when only a typed LinkedIn URL is present. ✅ File diagnostics are clean for the touched UI/auth test files and `SocialProfileConnections.razor`. ⚠️ `dotnet test --project .\tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore` is still blocked by the repository’s pre-existing duplicate generated-assembly-attribute errors in `Hackmum.Bethuya.Core`, `Hackmum.Bethuya.AI`, `ServiceDefaults`, and `Bethuya.Hybrid.Shared`.

## [2026-04-14] Refine LinkedIn onboarding URL UX
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Replace the LinkedIn post-connect meta copy on `/registration/social` with a real public profile URL field that stays editable until LinkedIn verification succeeds, then locks without disturbing the card rhythm or onboarding layout.
- **Acceptance:** ✅ LinkedIn card now keeps a Blazor Blueprint URL field visible through empty, connected, loading, mixed, and load-error states, while only the verified LinkedIn member ID satisfies completion. ✅ The URL stays editable before verified connect, trims into the OAuth return/save flow, and locks once LinkedIn verification succeeds. ✅ Social card actions stay aligned by reserving stable status/feedback regions and disabling controls during blocking load failures. ✅ Added targeted bUnit coverage for loading-state gating and truthful load-error behavior. ⚠️ `dotnet test --project .\tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore --filter "FullyQualifiedName~SocialProfileConnections|FullyQualifiedName~DevelopmentAuthenticationTests"` is still blocked by the repository's pre-existing duplicate generated-assembly-attribute errors in `Hackmum.Bethuya.Core`, `Hackmum.Bethuya.AI`, `ServiceDefaults`, and `Bethuya.Hybrid.Shared`.

## [2026-04-14] Verify LinkedIn social save contract supports typed profile URLs
- **Status:** done
- **Agent/Owner:** Tank (Backend Dev)
- **Description:** Confirm whether the existing social profile save contract already persists a user-entered LinkedIn profile URL independently from the verified LinkedIn member ID, and add the smallest regression coverage needed without weakening the member-ID verification boundary.
- **Acceptance:** Existing backend/shared contracts are either confirmed as sufficient or patched surgically; regression tests prove a LinkedIn profile URL can be stored alongside a verified member ID while a typed URL alone still fails the LinkedIn-required validation.

## [2026-04-14] Fix LinkedIn OAuth scope mismatch and graceful callback handling
- **Status:** done
- **Agent/Owner:** Tank (Backend Dev)
- **Description:** Investigate the LinkedIn onboarding OAuth failure on `/oauth/linkedin/callback`, determine whether the local app now needs OpenID Connect scopes instead of `r_liteprofile`, update the social-connect flow accordingly, and make remote LinkedIn failures return users to `/registration/social` with actionable provider-specific guidance instead of the developer exception page.
- **Acceptance:** ✅ LinkedIn onboarding now defaults to the OpenID Connect `openid profile` scopes through AppHost-managed runtime config, while the web auth flow still supports explicit legacy scope overrides when needed. ✅ Existing social-connect error handling continues to route LinkedIn scope failures back to `/registration/social` with actionable provider guidance instead of leaving the user on a callback exception page. ✅ Added targeted auth coverage for LinkedIn scope defaults and callback error redirects, and file diagnostics for the touched sources are clean. ⚠️ Full `dotnet build` / `dotnet test` remains blocked by a pre-existing duplicate assembly-attribute generation issue in `ServiceDefaults` and `Bethuya.Hybrid.Shared`.

## [2026-04-13] Align social connect card actions across empty and connected states
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Keep LinkedIn and GitHub social-connect buttons aligned on `/registration/social` before and after verified account data renders, so the cards remain visually balanced and professional even when one provider is connected and the other is not.
- **Acceptance:** ✅ Both social cards now reserve a consistent details area for status + profile metadata, so reconnect buttons stay aligned across mixed states. ✅ Connected and disconnected states use the same structural wrapper and placeholder metadata line to keep the layout stable. ✅ `dotnet test --project .\tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore` passed (128/128). ✅ The running Aspire `web` resource was rebuilt successfully.

## [2026-04-12] Split mandatory onboarding and social verification into separate steps
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Refactor first-login onboarding so required attendee details save on `/registration/mandatory`, verified GitHub/LinkedIn OAuth runs on a dedicated `/registration/social` page, and the user only moves to `/registration/aide` after the social step is saved.
- **Acceptance:** ✅ Mandatory profile save no longer depends on social identities in the shared/backend contracts. ✅ Added dedicated social profile read/write API endpoints and a `/registration/social` onboarding page that survives OAuth roundtrips without losing saved mandatory details. ✅ Home now redirects incomplete users to `/registration/mandatory` or `/registration/social` based on the next missing step. ✅ Updated onboarding render/auth regression tests for the three-step flow. ✅ `dotnet build Bethuya.slnx --no-restore -v minimal` passed. ✅ `dotnet test --project .\tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore` passed (127/127).

## [2026-04-12] Refactor social OAuth for stable AppHost-managed callbacks
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Refactor social connect so AppHost owns the GitHub/LinkedIn OAuth settings, the web app uses a stable externally configured callback URL, and the server-side OAuth roundtrip works reliably under Aspire instead of depending on dynamic launch-profile ports or per-project user secrets.
- **Acceptance:** ✅ AppHost now owns the social OAuth values and injects them into `web` using the existing AppHost secret keys (`oauth-github-clientid` / `oauth-github-clientsecret`, plus `Parameters:` variants, with nested `SocialConnections:*` keys also supported). ✅ The web app now runs on stable HTTPS `https://localhost:7400`, and GitHub callback configuration matches `/oauth/github/callback`. ✅ `Bethuya.Hybrid.Web` no longer depends on its own `UserSecretsId` for social config. ✅ `https://localhost:7400/authentication/social/github/start?returnUrl=%2Fregistration%2Fmandatory` now returns a real GitHub OAuth `302` with `redirect_uri=https://localhost:7400/oauth/github/callback`. ✅ AppHost build and test validation succeeded.

## [2026-04-12] Improve social connect messaging and card alignment
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Make the LinkedIn/GitHub onboarding errors provider-specific and place them adjacent to the social connect cards, strengthen the visual treatment of failed social sign-in states, align the connect buttons across cards, and ensure the web project reads the shared user-secrets store for local social OAuth config.
- **Acceptance:** ✅ Social callback errors now preserve the provider name and render a prominent inline social error block beside the social cards. ✅ LinkedIn and GitHub connect actions now use consistent bottom alignment within equal-height cards. ✅ `dotnet build` for `Bethuya.Hybrid.Web` passed and `dotnet test --project tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore` passed (123/123). ✅ `web` resource rebuild completed successfully. ⚠️ Social secret ownership was later moved fully to AppHost in the stable callback refactor.

## [2026-04-12] Restore local styling and static asset delivery
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Fix the local web app so shared and scoped CSS assets are actually served again after the AppHost launch-profile changes, and verify the styled site renders from the normal local URLs.
- **Acceptance:** ✅ `web` now runs under AppHost with explicit `ASPNETCORE_ENVIRONMENT=Development` and `ASPNETCORE_STATICWEBASSETS` so shared/static CSS is served with real content instead of zero-byte responses. ✅ Full AppHost restart applied the updated resource config. ✅ Live CSS endpoint `https://localhost:7112/_content/Bethuya.Hybrid.Shared/bethuya-theme.css` returned its full stylesheet content again, and both `backend` and `web` are `Running` + `Healthy` on `7092` / `7112`.

## [2026-04-12] Prevent web startup failure when social connect is unconfigured
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Stop the web app and Aspire health checks from failing when GitHub or LinkedIn social-connect OAuth settings are blank locally by only registering those providers when they are configured.
- **Acceptance:** ✅ Social OAuth handlers are only registered when their `ClientId` and `ClientSecret` are configured, so blank local `SocialConnections` settings no longer crash auth middleware. ✅ The AppHost now runs `web` without importing its launch profile and declares the proxied HTTP/HTTPS endpoints explicitly, preventing fixed-port conflicts with Aspire's external endpoint proxy. ✅ After `aspire start --isolated`, the `web` resource reached `Running` + `Healthy`, and its current isolated health endpoint returned `200 Healthy`.

## [2026-04-12] Replace typed social profile fields with verified GitHub and LinkedIn connect
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Replace manual LinkedIn/GitHub profile entry on the mandatory onboarding page with verified OAuth-based connect buttons, populate the form from provider callbacks, and persist the verified social identity values through the shared contracts, backend validation, and attendee profile model.
- **Acceptance:** ✅ Added GitHub and LinkedIn social connection auth options and callback endpoints in `Bethuya.Hybrid.Web`. ✅ Mandatory onboarding now renders verified connect actions instead of freeform social URL inputs and hydrates connected state from callback query values. ✅ Shared DTOs, backend validation, and persistence model/configuration now store verified GitHub and LinkedIn identity data. ✅ `dotnet build` for `Bethuya.Hybrid.Web` passed and `dotnet test --project tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore` passed (122/122) using isolated output paths.

## [2026-04-12] Expand mandatory onboarding profile fields
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Add Visage-aligned mandatory profile fields for government photo ID type + last four digits, employment status with conditional company/institute requirements, and LinkedIn/GitHub profiles across UI, contracts, persistence, and tests.
- **Acceptance:** ✅ Added government-approved photo ID type + last-4 capture, employment-status radio selection, conditional company/institute fields, and LinkedIn/GitHub profile fields to the mandatory onboarding form. ✅ Backend contracts, endpoint validation, and attendee profile persistence now store the new data. ✅ `dotnet build` for `Bethuya.Hybrid.Web` passed and `dotnet test --project tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore` passed (121/121) using isolated output paths.

## [2026-04-12] Restore onboarding select dropdowns on AIDE profile
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Fix the onboarding layout so Blazor Blueprint select menus on `/registration/aide` can render their popup content again, then rebuild and rerun the shared test project.
- **Acceptance:** ✅ Restored `BbPortalHost` and `BbDialogProvider` in `OnboardingLayout.razor`, which is required for Blazor Blueprint popup/select content. ✅ The onboarding layout still builds cleanly. ✅ `dotnet build` for `Bethuya.Hybrid.Web` and `dotnet test --project tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore` both passed using isolated output paths.

## [2026-04-11] Improve onboarding shell and first-login flow clarity
- **Status:** done
- **Agent/Owner:** Trinity (Frontend Dev)
- **Description:** Perform a frontend UX review of onboarding and adjacent dashboard/navigation states, then ship safe improvements to hide privileged navigation during onboarding, strengthen branded onboarding CTAs, and clarify the redirect/setup experience for new users.
- **Acceptance:** Registration routes render in a focused onboarding shell that suppresses sidebar navigation even for elevated local dev users. Organizer-only and curator-only nav entries are role-aware in the shared nav. Mandatory and AIDE onboarding steps now use branded cards, reassurance copy, and stronger primary actions. Dashboard redirect UX clearly hands incomplete users into setup. Evidence captured: screenshots (`tasks/artifacts/onboarding-*.png`), `dotnet build Bethuya.slnx --no-restore` ✅, `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore` ✅ (118/118).

## [2026-04-11] Harden Onboarding Identity Boundary: Role-Based Navigation Visibility
- **Status:** done
- **Agent/Owner:** Morpheus (Security Engineer)
- **Description:** Audit and harden role-based navigation visibility during onboarding to prevent new users seeing admin/organizer-only nav links. Implement minimal safe hardening with explicit render modes.
- **Acceptance:** ✅ `NavMenu.razor` wrapped "AI Agents" and "Curation" sections in `<AuthorizeView Roles="Admin,Organizer,Curator">`. ✅ `Home.razor` now has explicit `@rendermode InteractiveServer`. ✅ NewUserProfile/AideProfile already protected (verified: `@rendermode InteractiveServer` + `[Authorize]`). ✅ Onboarding trust boundary hardened: new users (Attendee role) never see organizer/curator nav links. ✅ No regressions: all other pages maintain intended visibility. ⚠️ Future `/agents` and `/curation` page implementations MUST include `@rendermode InteractiveServer` + role-based `[Authorize]` policies (documented in decisions.md follow-ups).

## [2026-04-11] Cover onboarding and nav regression risk with automated checks
- **Status:** done
- **Agent/Owner:** Switch (Tester)
- **Description:** Review first-login onboarding and nav UX regression risks, fix the highest-confidence navigation issue, and add automated checks around redirect + mandatory/AIDE profile flows.
- **Acceptance:** ✅ Added `OnboardingNavigationRenderTests` covering home redirect, organizer-tool visibility for anonymous/attendee/organizer/curator states, focused onboarding-shell rendering for both registration routes, mandatory profile submit, and onboarding accessibility/primary-action contracts across both profile steps. ✅ Fixed onboarding profile navigation to use `/registration/mandatory` and wired shared layout imports so onboarding pages compile with `OnboardingLayout`. ✅ Latest verification rerun: `dotnet test --project .\tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore` passed (118/118). ⚠️ Broader Playwright coverage is still environment-blocked when no app is listening on `https://localhost:7112`, and Cloudinary-dependent coverage remains inconclusive without credentials.

## [2026-04-11] Fix local onboarding auth wiring for registration flow
- **Status:** done
- **Agent/Owner:** Tank (Backend Dev)
- **Description:** Repair the local `Authentication:Provider=None` path so `/registration/mandatory` no longer throws missing auth services, and make the new-user onboarding API flow work locally or record the remaining blocker with evidence.
- **Acceptance:** ✅ `Authentication:Provider=None` now registers a shared development authentication scheme for both web and backend startup via `ServiceDefaults/Auth`. ✅ `/registration/mandatory` and `/registration/aide` render without the missing `IAuthenticationService` exception. ✅ Backend profile endpoints resolve the shared development user and accept mandatory + AIDE profile saves locally. ✅ Added auth regression tests for a protected web route and profile endpoints (103/103 TUnit tests passed). ✅ Runtime evidence gathered with live Aspire resources: `GET https://localhost:7112/registration/mandatory` returned 200, `GET/POST https://localhost:7092/api/profile*` returned 200. ⚠️ Remaining unrelated blocker for full suite: `dotnet test --solution Bethuya.slnx --no-build` still fails in existing Playwright E2E coverage (network-changed/timeouts and missing Cloudinary credentials).

## [2026-03-21] Repair Squad Setup and Implement Extensible Identity System
- **Status:** done
- **Agent/Owner:** Squad Coordinator
- **Description:** Normalized `.squad/` workspace and implemented production-ready identity system with interchangeable Entra External ID, Auth0, and Keycloak strategies via `Authentication:Provider` config switch.
- **Acceptance:** ✅ Squad repaired. ✅ Identity system implemented with strategy pattern. ✅ `NullCurrentUserService` retained for dev mode. ✅ `InteractiveServer` pages protected with `[Authorize]` policies. ✅ Build: 0 errors, 0 warnings. ✅ Tests: 55/55 pass.

## [2026-03-17] Wire Event Drafting End-to-End (Create + List)
- **Status:** done
- **Agent/Owner:** Squad (Tank, Trinity, Switch) + Copilot CLI Coordinator
- **Description:** Connect Blazor UI to Backend API for event creation and listing. Replace hardcoded mock data with Refit-based API calls. Add DataAnnotations form validation and toast notifications. Covers CreateEventDialog, Home.razor, and Events.razor.
- **Acceptance:** ✅ CreateEventDialog persists events via POST /api/events. ✅ Home and Events pages load from GET /api/events. ✅ "New Event" button added to Events.razor. ✅ Server-side validation on POST/PUT (title, capacity, dates, createdBy). ✅ EventResponse DTO replaces raw entities. ✅ Success/error toast notifications integrated. ✅ data-test selectors on all interactive elements. ✅ E2E selectors fixed. ✅ TUnit tests: 64/64 pass. ✅ Solution builds: 0 errors, 0 warnings.

## [2025-07-17] Dashboard UX Revamp — Blazor Blueprint Components
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Decompose monolithic Home.razor into reusable Razor components using Blazor Blueprint (BbCard, BbButton, BbBadge, BbAlert, BbDialog) and Lucide icons. Match refreshed design mockup with golden glow effects, horizontal meta layout, dark theme.
- **Acceptance:** Home.razor is a thin orchestrator (~100 lines) composing AiInsightsBanner, StatCard, FeaturedEventCard, CreateEventDialog, EventCard components. All inline SVGs replaced with LucideIcon. Solution builds cleanly.

## [2025-07-18] TUnit Tests for Cloudinary Image Upload Feature
- **Status:** done
- **Agent/Owner:** Switch (Tester)
- **Description:** Write TUnit tests covering Event.CoverImageUrl model behaviour and ImageEndpoints validation logic (file size, content type, success path, boundary cases). Uses NSubstitute for IImageUploadService mock and ASP.NET Core TestHost for endpoint integration tests.
- **Acceptance:** ✅ EventCoverImageTests: 4 tests (default null, set via init, update, clear). ✅ ImageEndpointValidationTests: 8 tests (oversized reject, invalid content-type reject, 4 valid types parameterised, URL response, exact-limit accept, 1-byte-over reject, fileName forwarded). ✅ All 80 tests pass. ✅ Build: 0 errors, 0 warnings.

<!-- Add new tasks here -->

## [2026-07-24] Aspire Secrets Audit + aspire-secrets Skill
- **Status:** done
- **Agent/Owner:** Tank (DevOps & Infrastructure)
- **Description:** Audited all `AddParameter()` calls in `AppHost/AppHost/AppHost.cs` for correct use of `secret: true`. Created `copilot/skills/aspire-secrets/SKILL.md` skill to guide future additions.
- **Acceptance:** ✅ AppHost.cs audit complete — `cloudinary-api-key` and `cloudinary-api-secret` already marked `secret: true`; `cloudinary-cloud-name` correctly has no secret flag (public CDN identifier). ✅ `aspire-secrets` skill created with before/after examples, checklist, and user-secrets setup guide. ✅ Build: 0 errors, 0 warnings.

## [2026-04-06] Azure Deployment Readiness
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Prepared Bethuya for Azure deployment with azd + Aspire. Added `azure.yaml` (azd entry point), Azure Key Vault provisioning, AI provider parameters, `Bethuya.MigrationService` (EF Core migrations worker with WaitForCompletion dependency), Azure Monitor / Application Insights telemetry, always-on health check endpoints for Azure Container Apps probes, and `azure-deploy.yml` GitHub Actions workflow using OIDC federated credentials. Fixed `AllowedHosts` from "localhost" to "*" in production appsettings.
- **Acceptance:** ✅ `azure.yaml` created. ✅ Key Vault (`AddAzureKeyVault("vault")`) + AI parameters added to AppHost. ✅ `/health` + `/alive` endpoints exposed in all environments. ✅ Azure Monitor enabled (conditional on `APPLICATIONINSIGHTS_CONNECTION_STRING`). ✅ `AllowedHosts: "*"` in backend + web appsettings.json. ✅ `Bethuya.MigrationService` created + wired to AppHost with `WaitForCompletion`. ✅ `azure-deploy.yml` OIDC workflow created. ✅ Build: 0 errors, 0 warnings.
- **Pre-deploy prerequisite:** `dotnet ef migrations add InitialCreate --project src/Hackmum.Bethuya.Infrastructure --startup-project src/Hackmum.Bethuya.Backend`


## [2026-07-24] AI-Powered Date Recommendation via GitHub Copilot SDK
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Added "✨ Suggest Dates" button to CreateEvent page that calls the GitHub Copilot SDK (.NET) to recommend optimal event dates. Backend service wraps `CopilotClient` with system prompt containing HackerspaceMumbai community event patterns (Eventbrite + Meetup seed data). Architecture: CreateEvent.razor → Refit IEventApi → POST /api/agents/recommend-dates → DateRecommendationService → CopilotClient session. Supports auto-fill of date/time fields, loading/error/reasoning states, graceful degradation.
- **Acceptance:** ✅ `GitHub.Copilot.SDK` v0.2.1 NuGet added. ✅ CommunityEventPatterns seed data with Eventbrite/Meetup patterns. ✅ DateRecommendationService with singleton CopilotClient + session-per-request. ✅ POST /api/agents/recommend-dates endpoint in AgentEndpoints. ✅ Refit DTOs + method in IEventApi. ✅ "Suggest Dates" button with loading spinner, reasoning display, error handling. ✅ 7 TUnit ParseResponse tests + 2 bUnit UI tests. ✅ Build: 0 errors, 0 warnings. ✅ Tests: 89/89 pass.

## [2026-07-25] New User Profile — First-Login Detection & AIDE Registration Flow
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Ported the Visage project's first-login detection and attendee profile flow to Bethuya. On first login, users are redirected to `/registration/mandatory` (name, email, location, occupation) and then `/registration/aide` (optional accessibility, inclusivity, diversity, equity fields). Flow checks profile completion via `GET /api/profile/completion-status` and redirects from `Home.razor`. Mirrors Visage's `User.cs`, `MandatoryRegistration.razor`, and `ProfileCompletionStatusDto` patterns.
- **Acceptance:** ✅ `AttendeeProfile.cs` domain entity (mandatory + 21 AIDE fields). ✅ `IAttendeeProfileRepository` + `AttendeeProfileRepository` (EF Core). ✅ `AttendeeProfileConfiguration` (unique index on UserId). ✅ `DbSet<AttendeeProfile>` in `BethuyaDbContext`. ✅ DI registration. ✅ `ProfileContracts.cs` (backend contracts). ✅ `ProfileEndpoints.cs` (3 endpoints: completion-status, POST profile, POST aide). ✅ `IProfileApi.cs` (Refit + shared DTOs). ✅ `NewUserProfile.razor` + `AideProfile.razor` pages (InteractiveServer, Authorize). ✅ `Home.razor` profile check + redirect. ✅ CSS for both pages. ✅ Build: 0 errors, 0 warnings. ✅ Tests: 100/100 pass.

---

## Completed Tasks

<!-- Move done tasks here -->

## [2026-05-28] Polish curation profile header
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Fix the selected profile header so the avatar and registrant identity align like the wireframe, avoiding the empty horizontal gap caused by the name wrapping below the image.
- **Acceptance:** ✅ The selected profile header now keeps the avatar and identity in a compact side-by-side grid, with the history/projection summary aligned below the identity instead of stealing horizontal space. ✅ Live Playwright inspection on Rohan Das confirmed the identity column is beside the avatar and no longer drops below it. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore -v quiet` passed (184/184).


## [2026-05-28] Polish curation attendance etiquette card
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Redesign the selected profile attendance etiquette card into a compact horizontal reliability bar aligned with the wireframe, reducing wasted space in the profile section.
- **Acceptance:** ✅ The card now shows icon, title/subtitle, reliability score, and attended count in a polished single-row layout. ✅ Live Playwright validation on Naina Patel confirmed the panel is 82px tall with a 48px horizontal bar instead of the previous stacked layout. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore -v quiet` passed (184/184).

## [2026-05-28] Stabilize curation fairness budget chips
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Make Fairness Budget chips keep consistent dimensions across registrant selection changes and remove wasted space in the top rail highlighted in the screenshot.
- **Acceptance:** ✅ Metric chips now have fixed 6.35rem x 3.85rem geometry and stable internal value/label tracks. ✅ Group spacing and topbar padding are more compact, and the reserved rail height now matches the live topbar. ✅ Live Playwright validation confirmed all 9 chips stayed 102x62 before selection and after selecting two different registrants, with no horizontal overflow and a 1px topbar/workbench gap. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore -v quiet` passed (184/184).

## [2026-05-28] Synchronize curation fairness bar deltas
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Fix Fairness Budget chip overlap/gaps and ensure selected registrant impact deltas shown in queue/profile are reflected in the top Fairness Budget chips.
- **Acceptance:** ✅ Underrep/Inclusion no longer overlap, and Diversity/Cohort Health spacing is compact with 11px group gaps. ✅ The top bar now maps `Lang`, `Edu`, and `Inclusion` impact deltas back to the corresponding budget chips and no longer suppresses projections for accepted/selected registrants. ✅ Live validation on Mira Patel showed Gender, Underrep, Geo, Lang, Edu, and Inclusion chips receiving affected state and deltas matching the Impact if Approved panel, with no horizontal overflow. ✅ `dotnet test tests\Hackmum.Bethuya.Tests\Hackmum.Bethuya.Tests.csproj --no-restore -v quiet` passed (184/184).

## [2026-05-28] Reflow fairness budget to two-row layout
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Place Core chips on the first row and Diversity + Cohort Health chips on the second row, keeping full chip names visible and renaming Underrep to Access Equity.
- **Acceptance:** Core renders as row 1, Diversity and Cohort Health render as row 2, chip labels are not ellipsized (except long term replaced by Access Equity), and tests/live UI checks pass.

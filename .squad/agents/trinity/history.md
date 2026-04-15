# Project Context

- **Owner:** Augustine Correa
- **Project:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Created:** 2026-03-21T11:50:19.882Z

## Learnings

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

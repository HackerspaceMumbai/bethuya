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

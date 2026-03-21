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

## [2026-03-17] Wire Event Drafting End-to-End (Create + List)
- **Status:** in-progress
- **Agent/Owner:** Copilot CLI
- **Description:** Connect Blazor UI to Backend API for event creation and listing. Replace hardcoded mock data with Refit-based API calls. Add DataAnnotations form validation and toast notifications. Covers CreateEventDialog, Home.razor, and Events.razor.
- **Acceptance:** CreateEventDialog persists events via POST /api/events. Home and Events pages load from GET /api/events. Form validates with DataAnnotations. Success/error toasts shown. TUnit tests pass. Solution builds cleanly.

## [2025-07-17] Dashboard UX Revamp — Blazor Blueprint Components
- **Status:** in-progress
- **Agent/Owner:** Copilot CLI
- **Description:** Decompose monolithic Home.razor into reusable Razor components using Blazor Blueprint (BbCard, BbButton, BbBadge, BbAlert, BbDialog) and Lucide icons. Match refreshed design mockup with golden glow effects, horizontal meta layout, dark theme.
- **Acceptance:** Home.razor is a thin orchestrator (~100 lines) composing AiInsightsBanner, StatCard, FeaturedEventCard, CreateEventDialog, EventCard components. All inline SVGs replaced with LucideIcon. Solution builds cleanly.

<!-- Add new tasks here -->

---

## Completed Tasks

<!-- Move done tasks here -->

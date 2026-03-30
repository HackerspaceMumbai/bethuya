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
- **Status:** in-progress
- **Agent/Owner:** Copilot CLI
- **Description:** Decompose monolithic Home.razor into reusable Razor components using Blazor Blueprint (BbCard, BbButton, BbBadge, BbAlert, BbDialog) and Lucide icons. Match refreshed design mockup with golden glow effects, horizontal meta layout, dark theme.
- **Acceptance:** Home.razor is a thin orchestrator (~100 lines) composing AiInsightsBanner, StatCard, FeaturedEventCard, CreateEventDialog, EventCard components. All inline SVGs replaced with LucideIcon. Solution builds cleanly.

## [2025-07-18] TUnit Tests for Cloudinary Image Upload Feature
- **Status:** done
- **Agent/Owner:** Switch (Tester)
- **Description:** Write TUnit tests covering Event.CoverImageUrl model behaviour and ImageEndpoints validation logic (file size, content type, success path, boundary cases). Uses NSubstitute for IImageUploadService mock and ASP.NET Core TestHost for endpoint integration tests.
- **Acceptance:** ✅ EventCoverImageTests: 4 tests (default null, set via init, update, clear). ✅ ImageEndpointValidationTests: 8 tests (oversized reject, invalid content-type reject, 4 valid types parameterised, URL response, exact-limit accept, 1-byte-over reject, fileName forwarded). ✅ All 80 tests pass. ✅ Build: 0 errors, 0 warnings.

<!-- Add new tasks here -->

---

## Completed Tasks

<!-- Move done tasks here -->

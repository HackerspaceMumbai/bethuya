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

---

## Completed Tasks

<!-- Move done tasks here -->

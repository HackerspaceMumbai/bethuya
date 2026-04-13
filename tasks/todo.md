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

## [2026-07-25] New User Profile — First-Login Detection & AIDE Registration Flow
- **Status:** done
- **Agent/Owner:** Copilot CLI
- **Description:** Ported the Visage project's first-login detection and attendee profile flow to Bethuya. On first login, users are redirected to `/registration/mandatory` (name, email, location, occupation) and then `/registration/aide` (optional accessibility, inclusivity, diversity, equity fields). Flow checks profile completion via `GET /api/profile/completion-status` and redirects from `Home.razor`. Mirrors Visage's `User.cs`, `MandatoryRegistration.razor`, and `ProfileCompletionStatusDto` patterns.
- **Acceptance:** ✅ `AttendeeProfile.cs` domain entity (mandatory + 21 AIDE fields). ✅ `IAttendeeProfileRepository` + `AttendeeProfileRepository` (EF Core). ✅ `AttendeeProfileConfiguration` (unique index on UserId). ✅ `DbSet<AttendeeProfile>` in `BethuyaDbContext`. ✅ DI registration. ✅ `ProfileContracts.cs` (backend contracts). ✅ `ProfileEndpoints.cs` (3 endpoints: completion-status, POST profile, POST aide). ✅ `IProfileApi.cs` (Refit + shared DTOs). ✅ `NewUserProfile.razor` + `AideProfile.razor` pages (InteractiveServer, Authorize). ✅ `Home.razor` profile check + redirect. ✅ CSS for both pages. ✅ Build: 0 errors, 0 warnings. ✅ Tests: 100/100 pass.

---

## Completed Tasks

<!-- Move done tasks here -->

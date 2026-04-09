# Project Context

- **Owner:** Augustine Correa
- **Project:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Created:** 2026-03-21T11:50:19.882Z

## Learnings

- Bethuya currently exposes Entra, Auth0, and Keycloak provider branches and must keep login/auth/PII pages on `InteractiveServer`.
- **AI coding primitives updated (2026-04-09):** Augustine updated README.md, AGENTS.md, and .github/copilot-instructions.md. Key security/privacy conventions absorbed:
  - **InteractiveServer for all sensitive pages** — auth/login/PII/organizer/agent control pages MUST use `@rendermode InteractiveServer` (global assignment on Routes in App.razor). WASM code is client-inspectable; sensitive pages must be server-side only.
  - **Foundry Local for all PII** — Curator Agent processes attendee PII exclusively via Foundry Local (on-device, offline). PII never reaches any cloud endpoint. Only consented DEI fields used; never infer sensitive traits.
  - **PII routing guardrail** — all attendee data routed via Foundry Local. Non-sensitive orchestration uses Microsoft Foundry or Azure OpenAI.
  - **Vogen IDs prevent PII leaks** — AttendeeId, EventId, UserId are Vogen structs (zero-allocation), not raw Guid/int. Ensures type safety on sensitive data boundaries.
  - **Security headers enforced** — CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy via `app.UseSecurityDefaults()` from ServiceDefaults.
  - **Rate limiting for AI calls** — 100 req/min (general), 20 req/min for `RateLimitPolicies.Ai` (sensitive operations).
  - **Nullable enabled, TreatWarningsAsErrors** — fix all warnings; never suppress without documented justification.
  - **Responsible disclosure** — see SECURITY.md; Dependabot weekly NuGet + Actions updates; `dotnet list package --vulnerable` blocks CI on CVEs.

# Project Context

- **Owner:** Augustine Correa
- **Project:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Created:** 2026-03-21T11:50:19.882Z

## Core Context

Squad setup repaired on 2026-03-21 with a Matrix-themed core team and valid casting metadata.

## Recent Updates

📌 Team roster, routing, and casting state normalized on 2026-03-21.

## Learnings

The repository requires `tasks/todo.md` updates before code changes and `tasks/lessons.md` entries for unexpected discoveries.
- **AI coding primitives updated (2026-04-09):** Augustine updated README.md, AGENTS.md, and .github/copilot-instructions.md with core Bethuya conventions binding all agents. Key coordinator/documentation focus:
  - **Central guardrails that all agents must enforce:** Vogen IDs (no raw Guid/int), TUnit-only testing, Blazor Blueprint-first UI, Refit contracts, Central Package Management, Scalar API docs, InteractiveServer for sensitive pages, data-test selectors for E2E.
  - **No EF Core migrations until formal release** — Augustine''s directive (2026-04-09). Delete Migrations/ folder; never regenerate before a milestone.
  - **File-scoped namespaces, primary constructors, collection expressions** — C# 14 idioms enforced across all .NET code.
  - **Nullable enabled, TreatWarningsAsErrors** — fix all warnings; never suppress without documented justification.
  - **Code review mandatory pre-commit:** Run `code-review` agent, `dotnet-diag:optimizing-dotnet-performance`, `/explain-diff` before PRs. Never rely on humans to catch code issues — use available analysis tools proactively.
  - **Test-first mandate:** Every feature begins with TUnit test. Visual proof (Playwright screenshots) required for UI changes before marking done.
  - **Plan-first protocol:** Add task to `tasks/todo.md` before writing code. Record lessons in `tasks/lessons.md`. No temporary hacks — fix root causes.
  - **Performance targets:** p99 < 180ms @ 2,500 RPS, 0 B hot-path allocations (Vogen), >90% cache hit rate, <65% steady-state memory.
  - **Security:** InteractiveServer for sensitive pages, Foundry Local for all PII, rate limiting on AI calls (20 req/min for `RateLimitPolicies.Ai`), CORS ``"BethuyaMobileClients"``, security headers via ServiceDefaults.

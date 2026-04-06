# Squad Team

> bethuya

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Neo | Lead | `.squad/agents/neo/charter.md` | ✅ Active |
| Trinity | Frontend Dev | `.squad/agents/trinity/charter.md` | ✅ Active |
| Tank | Backend Dev | `.squad/agents/tank/charter.md` | ✅ Active |
| Switch | Tester | `.squad/agents/switch/charter.md` | ✅ Active |
| Morpheus | Security Engineer | `.squad/agents/morpheus/charter.md` | ✅ Active |
| Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Silent |
| Ralph | Work Monitor | — | 🔄 Monitor |

## Coding Agent

<!-- copilot-auto-assign: false -->

| Name | Role | Charter | Status |
|------|------|---------|--------|
| @copilot | Coding Agent | — | 🤖 Coding Agent |

### Capabilities

**🟢 Good fit — auto-route when enabled:**
- Bug fixes with clear reproduction steps
- Test coverage additions and flaky test fixes
- Documentation and small follow-up changes
- Scaffolding that follows existing patterns

**🟡 Needs review — route with squad member review:**
- Medium features with clear acceptance criteria
- Refactoring with strong test coverage
- Isolated API additions following established conventions

**🔴 Not suitable — route to squad members instead:**
- Architecture and security decisions
- Authentication, authorization, encryption, or access control changes
- Multi-system integration requiring cross-agent coordination

## Policies

- `.squad/policies/rendering-policy.md`
- `.squad/policies/rcl-boundaries.md`
## Team Protocol

- **Cross-Platform Rule:** Any UI component shared between Web and MAUI must be placed in `Bethuya.Shared`.
- **State of Truth:** The Aspire Dashboard is the final authority on whether a feature is "Done."

## Project Context

- **Owner:** Augustine Correa
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Description:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Project:** bethuya
- **Created:** 2026-03-21

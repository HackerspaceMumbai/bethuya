name neo-aspire-governance
description >
  Lead-architect governance for Bethuya. Enforce Aspire-first composition,
  Hybrid/Web boundaries, and security posture. Delegate deep checks to
  neo-apphost-consistency and neo-aspire-diff-review.
domain architecture
confidence high
source manual

# Neo Aspire Governance

## When to use
Use this skill for any change that touches:
- AppHost composition or service wiring
- Cross-service communication
- Auth, PII, or render-mode decisions
- Shared RCL boundaries (Hybrid/Web)
- ServiceDefaults, telemetry, resilience

## Outcomes
This skill produces:
- A short architectural verdict (approve, request changes, or escalate)
- A risk summary (low, medium, high)
- A checklist of required fixes with file-level pointers
- Delegation instructions to supporting Neo skills

## Governance rules (hard gates)
Block or escalate if any of these occur:
- Service-to-service URLs are hardcoded (ports, localhost, environment URLs)
- A dependency exists in code but not in AppHost
- Secrets are introduced into source-controlled config
- RCL contains host-specific logic (storage, HttpContext, render-mode branching)
- Sensitive pages are made WASM-interactive in Blazor Web

## Delegation
If you need deep verification, delegate as follows:
- For topology and WithReference consistency:
  use neo-apphost-consistency.
- For PR diffs and anti-pattern detection:
  use neo-aspire-diff-review.

## Review output format
1. Summary (one paragraph)
2. Risk level (low, medium, high)
3. Findings (bullets)
4. Required changes (bullets)
5. Delegations executed (if any)
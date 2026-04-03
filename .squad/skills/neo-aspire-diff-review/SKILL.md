name neo-aspire-diff-review
description >
  Review PR diffs for Aspire anti-patterns: hardcoded endpoints, bypassed
  AppHost references, secret leakage, and observability regressions.
domain architecture
confidence high
source manual

# Neo Aspire Diff Review

## When to use
Use this skill for PR review when diffs include:
- AppHost changes
- Program.cs or DI changes
- HttpClient registrations
- appsettings*.json or environment wiring
- Auth provider config or changes to ServiceDefaults

## Diff review checklist
### Hardcoded endpoints
Reject if you find:
- localhost URLs in code
- explicit ports for inter-service calls
- environment-specific conditional base addresses

### AppHost bypass
Reject if you find:
- services used but not present in AppHost
- dependencies not declared as references
- manual container wiring outside AppHost

### Secrets and sensitive config
Reject if you find:
- API keys, client secrets, connection strings committed
- auth config values that should be user-secrets

### Observability regressions
Flag if:
- telemetry/logging is disabled
- resilience policies are removed without replacement
- health checks are removed

## Output format
1. Top issues (bulleted)
2. File-by-file findings (bulleted)
3. Required changes (bulleted)
4. Suggested safer patterns (short bullets)


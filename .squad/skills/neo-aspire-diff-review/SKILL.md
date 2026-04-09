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

MUST REJECT — Critical security violation:

- API keys, client secrets, connection strings committed
- auth config values that should be user-secrets
- If secrets are detected in the diff AND CI secret-scanning is not enabled, reject the PR.
- If no secrets are detected, but CI secret-scanning is not enabled, mark as warning and recommend enabling scanning.
- If a PR disables CI secret-scanning (e.g., removes or disables secret scanning config in CI), immediately reject and flag for security review. Detect by checking for changes/removal in CI config files (e.g., GitHub Actions, Azure Pipelines).

If any secret is exposed in a diff or history, treat it as compromised: rotate credentials immediately and follow incident response steps (revoke, audit access, assess blast radius, and document remediation).

Reference the CI requirements section for repository-level checks.
## CI requirements

Repository-level checks for all PRs:

- Automated secret-scanning must be enabled in CI (e.g., GitHub Advanced Security, TruffleHog, or similar).
- If secret-scanning is disabled or removed in a PR, immediately reject the PR and flag for security review.
- If secret-scanning is not enabled but no secrets are detected in the diff, mark as warning and recommend enabling scanning.

To detect disabling of CI scanning in a PR, check for changes to CI configuration files (e.g., .github/workflows, azure-pipelines.yml) that remove or disable secret-scanning steps or tools.

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
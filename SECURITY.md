# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| `main` (latest) | ✅ |
| feature branches | Development only — not for production |

## Reporting a Vulnerability

Bethuya is currently in active development. If you discover a security vulnerability, please **do not open a public GitHub issue**.

Instead:

1. **Email:** security@bethuya.dev _(placeholder — update before going public)_
2. Include a description of the vulnerability, steps to reproduce, and potential impact.
3. We will acknowledge receipt within 72 hours and provide a timeline for a fix.

## Scope

In scope:
- Authentication and authorization bypasses
- Cross-site scripting (XSS), CSRF
- Injection vulnerabilities (SQL, command, prompt injection into AI agents)
- Sensitive data exposure (PII, attendee data, organizer credentials)
- Insecure direct object references
- AI agent prompt injection or guardrail bypasses

Out of scope:
- Vulnerabilities in third-party dependencies (report upstream)
- Issues requiring physical access to the device

## Data Sensitivity Note

The Bethuya Curator Agent processes personally identifiable information (PII) locally via Foundry Local (on-device inference). Attendee registration data **never leaves the device** during curation. Any vulnerability that could exfiltrate this data is **critical severity**.

## Security Practices

- CodeQL SAST analysis on all PRs (`.github/workflows/security.yml`)
- Dependabot for NuGet and GitHub Actions weekly updates
- `dotnet list package --vulnerable` gates CI builds
- Auth implemented per auth provider on feature branches: `feature/auth/entra`, `feature/auth/auth0`, `feature/auth/keycloak`

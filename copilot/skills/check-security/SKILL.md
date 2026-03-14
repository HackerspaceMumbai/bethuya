---
name: check-security
description: "Review a PR diff, file, or Blazor component for security issues specific to the Bethuya platform. Checks for missing [Authorize], PII in logs, secrets in source, WASM components handling sensitive data, CORS/AllowedHosts misconfigurations, and AI prompt injection risks."
---

# check-security

Performs a security review of the specified code or diff.

## When to Use

- Before opening any PR that touches authentication, authorization, agent code, or attendee data
- When adding a new Blazor page or component that shows user data
- When modifying `appsettings.json`, CORS config, or AllowedHosts
- When writing AI agent tools or prompts

## Checklist

### 1. Authentication & Authorization

```
□ All non-public Blazor pages have [Authorize] or <AuthorizeView>
□ Login, auth callbacks, and profile pages use @rendermode InteractiveServer
  (NOT InteractiveWebAssembly or InteractiveAuto — WASM is client-inspectable)
□ Pages in Bethuya.Hybrid.Shared never contain auth-sensitive logic
  (Shared is WASM-eligible; sensitive logic must be in Bethuya.Hybrid.Web only)
□ Authorization policies use BethuyaRoles constants, not raw strings
□ JWT tokens are stored in MAUI via SecureStorage, never Preferences
```

### 2. PII & Data Exposure

```
□ Attendee PII (name, email, DEI fields) is never logged
  Use: logger.LogInformation("Processing attendee {Id}", attendee.Id)  ✅
  Not: logger.LogInformation("Processing {Email}", attendee.Email)     ❌
□ Curator Agent inputs/outputs route through Foundry Local (on-device)
  Never send PII to Azure OpenAI or any cloud endpoint
□ No PII in URL path segments or query strings
□ Sensitive config values use user-secrets (dev) or Key Vault (prod)
  Never appear in appsettings.json or source code
```

### 3. Secrets in Source

```
□ No API keys, connection strings, or tokens in .cs files
□ No secrets in appsettings.json (use placeholder or user-secrets reference)
□ No sensitive values in .github/workflows YAML — use GitHub Actions secrets
□ Run: git log --oneline | head -20 and check no accidental commit of secrets
```

### 4. AllowedHosts & CORS

```
□ appsettings.json AllowedHosts is NOT "*"
  Development: "localhost;127.0.0.1"
  Production: restrict to actual domain(s)
□ CORS policy "BethuyaMobileClients" only allows known mobile app origins
□ CORS does not use AllowAnyOrigin() in production
```

### 5. Blazor Render Mode — Sensitive Pages

```
□ Pages handling tokens, user sessions, or organizer operations:
  @rendermode InteractiveServer   ✅
  NOT @rendermode InteractiveWebAssembly   ❌
  NOT @rendermode InteractiveAuto          ❌
□ Components are placed in Bethuya.Hybrid.Web/Components/ not Shared/
□ CascadingAuthenticationState is server-side only
```

### 6. AI Prompt Injection

```
□ All user-supplied text going into AI agent prompts is validated/sanitized first
□ Prompt templates use structured inputs, not raw string interpolation
□ Agent tools validate their parameters before executing
□ Curator Agent: attendee data fields are never concatenated directly into prompts
```

### 7. Security Headers & Rate Limiting

```
□ Program.cs calls app.UseSecurityDefaults() before app.MapRazorComponents<App>()
□ AI/agent API endpoints are tagged with rate limit policy "ai" (20 req/min per IP)
□ Health check endpoints (/health, /alive) are not publicly exposed in production
```

## Running the Check

```bash
# Quick vulnerable package scan
dotnet list package --vulnerable --include-transitive

# Check AllowedHosts in appsettings
grep -r "AllowedHosts" src/ --include="*.json"

# Find Blazor pages missing @rendermode on sensitive routes
grep -rn "@page.*/(auth|profile|organizer|curator|admin)" src/ --include="*.razor" \
  | xargs grep -L "rendermode InteractiveServer"

# Find potential PII in log calls
grep -rn "Log(Information|Warning|Error).*[Ee]mail\|[Nn]ame\|[Pp]hone" src/ --include="*.cs"
```

## References

- `SECURITY.md` — responsible disclosure policy
- `.github/workflows/security.yml` — CodeQL + vulnerable package CI gate
- `.github/agents/curator.md` — PII routing rules for Curator Agent
- `.github/agents/dotnet-dev.md` — Security Coding Standards section

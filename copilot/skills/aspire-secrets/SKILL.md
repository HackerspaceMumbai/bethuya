---
name: aspire-secrets
description: "Audit and fix secret: true on sensitive AddParameter() calls in AppHost. Prevents API keys, passwords, and tokens from being logged or exposed in the Aspire Dashboard. Provides before/after examples, a checklist of common sensitive parameters, and instructions for setting values via user-secrets."
---

# aspire-secrets

Ensures every sensitive `.AddParameter()` call in `AppHost/AppHost/AppHost.cs` uses `secret: true`, so the Aspire Dashboard never logs or renders the raw value.

## Why This Matters

Without `secret: true`, Aspire Dashboard shows parameter values in plaintext in:
- The **Resources** panel environment-variable tab
- **Structured logs** (parameter values are emitted at startup)
- **Distributed traces** that capture environment injection

With `secret: true`, the value is replaced with `*****` everywhere in the dashboard and is never written to any log sink.

## When to Use

- When adding a new `AddParameter()` call for an API key, password, connection string, or token
- When reviewing AppHost before opening a PR (mandatory pre-commit check)
- When onboarding a new external service that requires credentials

## Quick Audit

```bash
# Find all AddParameter calls missing secret: true
grep -n "AddParameter" AppHost/AppHost/AppHost.cs

# Then cross-check each against the checklist below
```

## Parameters That MUST Have `secret: true`

| Category | Examples |
|---|---|
| API keys | `cloudinary-api-key`, `azure-openai-api-key`, `openai-api-key`, `sendgrid-api-key` |
| Secrets / passwords | `cloudinary-api-secret`, `db-password`, `smtp-password`, `webhook-secret` |
| Tokens | `github-token`, `slack-bot-token`, `stripe-secret-key` |
| Connection strings | Any parameter whose value is a full connection string with credentials |
| Client secrets | `entra-client-secret`, `keycloak-client-secret`, `auth0-client-secret` |

## Parameters That Do NOT Need `secret: true`

| Category | Examples | Reason |
|---|---|---|
| Public identifiers | `cloudinary-cloud-name` | Appears in public CDN URLs |
| Endpoints / URLs | `azure-openai-endpoint`, `ollama-endpoint` | Not credential-bearing |
| Non-sensitive config | `app-environment`, `log-level`, `feature-flags` | Safe to log |

## Before / After

### ❌ Before — API key exposed in dashboard

```csharp
var cloudinaryApiKey = builder.AddParameter("cloudinary-api-key");
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret");
```

### ✅ After — Values masked everywhere

```csharp
var cloudinaryApiKey    = builder.AddParameter("cloudinary-api-key",    secret: true);
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret", secret: true);
```

### ✅ Cloud name stays public (no secret flag needed)

```csharp
// cloudinary-cloud-name is a public CDN identifier — not sensitive
var cloudinaryCloudName = builder.AddParameter("cloudinary-cloud-name");
```

## Current State of AppHost.cs

As of the last audit, all sensitive parameters are correctly marked:

| Parameter | `secret: true` | Notes |
|---|---|---|
| `cloudinary-cloud-name` | No (correct) | Public CDN identifier |
| `cloudinary-api-key` | ✅ Yes | Masked in dashboard |
| `cloudinary-api-secret` | ✅ Yes | Masked in dashboard |
| `azure-openai-api-key` *(commented)* | ✅ Yes | Will be correct when uncommented |
| `openai-api-key` *(commented)* | ✅ Yes | Will be correct when uncommented |

## Setting Values Safely (user-secrets)

Never put secret values in `appsettings.json`. Use .NET user-secrets for local dev:

```bash
cd AppHost/AppHost

# Cloudinary
dotnet user-secrets set "Parameters:cloudinary-cloud-name" "your-cloud-name"
dotnet user-secrets set "Parameters:cloudinary-api-key"    "your-api-key"
dotnet user-secrets set "Parameters:cloudinary-api-secret" "your-api-secret"

# Azure OpenAI (when enabled)
dotnet user-secrets set "Parameters:azure-openai-endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "Parameters:azure-openai-api-key"  "your-key"

# OpenAI (when enabled)
dotnet user-secrets set "Parameters:openai-api-key" "your-key"
```

> **Key format:** Aspire resolves `AddParameter("foo-bar")` from the secret key `Parameters:foo-bar`.

## Production (Azure)

In Azure deployments (`azd up`), parameters with `secret: true` are automatically stored in Key Vault by Aspire when `AddAzureKeyVault("vault")` is wired into AppHost. No manual Key Vault configuration is needed.

```csharp
// AppHost.cs — Key Vault is provisioned in publish mode only
IResourceBuilder<AzureKeyVaultResource>? keyVault = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureKeyVault("vault")
    : null;
```

## Adding a New Sensitive Parameter — Checklist

```
□ 1. Add version to Directory.Packages.props if a new NuGet package is needed
□ 2. Add AddParameter("my-param", secret: true) in AppHost.cs
□ 3. Wire into the resource with .WithEnvironment("MY_ENV_VAR", myParam)
□ 4. Set the value with: dotnet user-secrets set "Parameters:my-param" "<value>"
□ 5. For production, verify Key Vault reference in azd output
□ 6. Run the Aspire Dashboard and confirm the value shows as ***** in Resources tab
□ 7. Run check-security skill to validate no secrets leak in logs or traces
```

## Verifying in the Dashboard

1. Start Aspire: `aspire start`  
2. Open the dashboard URL (shown in terminal output)
3. Navigate to **Resources → backend → Environment**
4. Secret parameters must show `*****`, not the raw value

If you see a raw value, the `secret: true` flag is missing — fix it before committing.

## References

- `AppHost/AppHost/AppHost.cs` — parameter declarations
- `AGENTS.md` → *AI Provider Routing* — which providers need secrets
- `copilot/skills/check-security/SKILL.md` — broader security audit checklist
- `copilot/skills/setup-ai-providers/SKILL.md` — AI provider user-secrets setup
- [Aspire docs: External parameters](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/external-parameters)

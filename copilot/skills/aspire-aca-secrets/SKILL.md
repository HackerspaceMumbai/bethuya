---
name: aspire-aca-secrets
description: "Audit and fix AddParameter() calls in AppHost for Azure Container Apps deployment. The secret: true flag breaks ACA deployment with 'Unsupported value type System.Boolean' error. Use Key Vault for production secret management instead."
---

# aspire-aca-secrets

Ensures `.AddParameter()` calls in `AppHost/AppHost/AppHost.cs` do NOT use `secret: true` when deploying to Azure Container Apps. This flag causes Azure deployment failures and should be omitted.

## Why This Matters

**Azure Container Apps Deployment Bug:** When using `.AddParameter("name", secret: true)`, Aspire serializes the boolean into the deployment manifest. The Azure Container Apps deployment pipeline cannot deserialize this boolean value, causing deployment to fail with:

```
[ERR] Unsupported value type System.Boolean
```

**Important Scope Note:** This bug is **Azure Container Apps specific**. Deployments to other cloud providers (AWS, GCP, etc.) are not affected by this issue.

## The Trade-off

- **With `secret: true`:** Values are masked as `*****` in the Aspire Dashboard (local dev only), but Azure deployment fails.
- **Without `secret: true`:** Azure deployment succeeds. Values are NOT masked in the dashboard, but can still be protected using user-secrets locally and Key Vault in production.

**Decision for Azure projects:** Omit `secret: true` to enable deployment. Use Key Vault for actual secret protection in production.

## When to Use This Skill

- When adding a new `AddParameter()` call for an API key, password, or token in an Azure-targeted project
- When reviewing AppHost before deploying to Azure Container Apps
- When troubleshooting `System.Boolean` deployment errors with `azd deploy` or `aspire deploy`
- When migrating from other cloud providers to Azure (ensure `secret: true` is removed)

## Quick Audit

```bash
# Find all AddParameter calls with secret: true (these will break Azure deployment)
grep -n "AddParameter.*secret:" AppHost/AppHost/AppHost.cs

# These should ALL be fixed by removing secret: true
```

## Current State of AppHost.cs

As of the 2026-04-09 fix, all Cloudinary parameters are correctly configured:

| Parameter | `secret: true` | Status |
|---|---|---|
| `cloudinary-cloud-name` | No | ✅ Correct (public CDN identifier) |
| `cloudinary-api-key` | No | ✅ Correct (Azure deployment compatible) |
| `cloudinary-api-secret` | No | ✅ Correct (Azure deployment compatible) |

**Commented-out AI parameters** (lines 32-33) still have `secret: true` in the code — these need the same fix if uncommented.

## Correct Pattern for Azure

### ✅ Development (user-secrets) + Azure (environment variables)

```csharp
// Cloudinary — image upload for event cover images
// secret: true omitted — causes "Unsupported value type System.Boolean" in Azure deployment.
// For production: route secrets through Key Vault via keyVault.AddSecret(...) in publish mode.
var cloudinaryCloudName = builder.AddParameter("cloudinary-cloud-name");
var cloudinaryApiKey = builder.AddParameter("cloudinary-api-key");
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret");

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithEnvironment("Cloudinary__CloudName", cloudinaryCloudName)
    .WithEnvironment("Cloudinary__ApiKey", cloudinaryApiKey)
    .WithEnvironment("Cloudinary__ApiSecret", cloudinaryApiSecret);
```

**How it works:**
- **Local dev:** Parameters read from user-secrets (`dotnet user-secrets set "Parameters:cloudinary-api-key" "value"`)
- **Azure:** Parameters read from environment variables (set via Azure Portal, CLI, or deployment pipeline)
- **Security:** Values are NOT masked in dashboard, but are protected via user-secrets (local) and Key Vault (production)

### ✅ Production Pattern: Key Vault Integration

For production Azure deployments requiring proper secret management:

```csharp
// Provision Key Vault only in publish mode (avoids local dev credential issues)
IResourceBuilder<AzureKeyVaultResource>? keyVault = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureKeyVault("vault")
    : null;

// Development: use parameters (reads from user-secrets)
var cloudinaryApiKey = builder.AddParameter("cloudinary-api-key");
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret");

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithEnvironment("Cloudinary__ApiKey", cloudinaryApiKey)
    .WithEnvironment("Cloudinary__ApiSecret", cloudinaryApiSecret);

// Production: wire Key Vault reference for secret rotation, access control, audit logging
if (keyVault is not null)
{
    backend.WithReference(keyVault);
}
```

Then set secrets in Azure Key Vault:
```bash
az keyvault secret set --vault-name <vault-name> --name cloudinary-api-key --value <value>
az keyvault secret set --vault-name <vault-name> --name cloudinary-api-secret --value <value>
```

**When to use:** Production deployments to Azure requiring encryption, rotation, access policies, and audit logging.

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

## Adding a New Sensitive Parameter — Checklist

```
□ 1. Add version to Directory.Packages.props if a new NuGet package is needed
□ 2. Add AddParameter("my-param") WITHOUT secret: true in AppHost.cs
□ 3. Wire into the resource with .WithEnvironment("MY_ENV_VAR", myParam)
□ 4. Set the value with: dotnet user-secrets set "Parameters:my-param" "<value>"
□ 5. For production, configure Key Vault reference in publish mode (see pattern above)
□ 6. Test Azure deployment with: azd deploy (or aspire deploy)
□ 7. Verify no "System.Boolean" errors in deployment output
```

## Anti-Patterns (Azure Container Apps Specific)

❌ **Never use `AddParameter(secret: true)` when targeting Azure Container Apps**
- Causes `[ERR] Unsupported value type System.Boolean` deployment failure
- The flag provides no actual security benefit (only affects manifest metadata)
- This is an Azure-specific bug — other cloud providers are unaffected

❌ **Don't use plain parameters for production secrets without Key Vault**
- Parameters are passed as environment variables (visible in container logs, process listings)
- No secret rotation capability
- No access control or audit logging

✅ **Do use Key Vault for production secrets on Azure**
- Secrets stored securely with Azure-managed encryption
- Access control via Azure RBAC or access policies
- Audit logging for secret access
- Secret rotation support

## Summary

- **Bug:** `AddParameter(secret: true)` breaks Azure Container Apps deployment with `System.Boolean` error
- **Scope:** Azure-specific — non-Azure deployments (AWS, GCP) are unaffected
- **Quick fix:** Remove `secret: true` — parameters work correctly without it
- **Production pattern:** Use Key Vault `AddSecret` for actual secret management in Azure
- **Local dev:** Use `dotnet user-secrets` — works with both parameters and Key Vault reference patterns

## References

- `AppHost/AppHost/AppHost.cs` — parameter declarations
- `AGENTS.md` → *AI Provider Routing* — which providers need secrets
- `copilot/skills/check-security/SKILL.md` — broader security audit checklist
- `copilot/skills/setup-ai-providers/SKILL.md` — AI provider user-secrets setup
- `.squad/skills/aspire-aca-secrets/SKILL.md` — Squad-level skill with same guidance
- [Aspire docs: External parameters](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/external-parameters)

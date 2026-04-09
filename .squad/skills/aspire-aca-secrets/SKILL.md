---
name: "aspire-aca-secrets"
description: "Safe patterns for secrets in Aspire AppHost when deploying to Azure Container Apps — avoiding the AddParameter(secret: true) deployment bug"
domain: "aspire, azure, deployment, secrets"
confidence: "high"
source: "earned — production deployment failure"
---

# Aspire Secrets: Safe Patterns for Azure Container Apps Deployment

## Context

When using .NET Aspire's `builder.AddParameter()` with the `secret: true` flag, the boolean value is serialized into the Aspire manifest JSON. This manifest is consumed during `azd deploy` / `aspire deploy` to provision resources in Azure Container Apps.

**The bug:** Azure Container Apps deployment pipeline cannot deserialize the `secret: true` boolean field, causing deployment to fail with:
```
[ERR] Unsupported value type System.Boolean
```

**Scope:** This bug is **Azure Container Apps specific**. Deployments to other cloud providers (AWS, GCP, etc.) are not affected by this issue.

This occurs when:
- Using `builder.AddParameter("name", secret: true)` in AppHost.cs
- Running `azd up`, `azd deploy`, or `aspire deploy`
- Deploying to Azure Container Apps

## The Bug

```csharp
// ❌ BROKEN for Azure Container Apps deployment
var apiKey = builder.AddParameter("api-key", secret: true);
```

The `secret: true` flag:
- **Local dev:** Works fine — parameters are read from user-secrets or environment variables
- **Azure deployment:** Fails — boolean serialized into manifest breaks ACA deployment pipeline
- **Actual security impact:** None — the flag doesn't encrypt or protect the value, it only affects IDE hints and manifest metadata

## Patterns

### Simple Fix: Remove `secret: true`

```csharp
// ✅ WORKS for both local and Azure Container Apps deployment
var apiKey = builder.AddParameter("api-key");
```

The parameter still functions correctly:
- Reads from user-secrets in development (`dotnet user-secrets set "Parameters:api-key" "value"`)
- Reads from environment variables in Azure Container Apps
- Gets injected into container environment via `WithEnvironment("ConfigKey", apiKey)`

**When to use:** Development, testing, or when Key Vault integration isn't required.

### Production Pattern: Key Vault Integration

For production deployments, use Azure Key Vault instead of parameters:

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

// Production: wire Key Vault reference
if (keyVault is not null)
{
    backend.WithReference(keyVault);
}
```

In production, set secrets in Azure Key Vault:
```bash
az keyvault secret set --vault-name <vault-name> --name cloudinary-api-key --value <value>
az keyvault secret set --vault-name <vault-name> --name cloudinary-api-secret --value <value>
```

The backend app reads secrets from Key Vault via the reference, using the same configuration keys (`Cloudinary__ApiKey`, `Cloudinary__ApiSecret`).

**When to use:** Production deployments to Azure requiring proper secret management with rotation, access policies, and audit logging.

### Full Key Vault Pattern (Alternative)

If you want Key Vault to fully replace parameters in publish mode:

```csharp
IResourceBuilder<AzureKeyVaultResource>? keyVault = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureKeyVault("vault")
    : null;

ReferenceExpression cloudinaryApiKey;
ReferenceExpression cloudinaryApiSecret;

if (builder.ExecutionContext.IsPublishMode && keyVault is not null)
{
    // Production: read from Key Vault
    cloudinaryApiKey = keyVault.AddSecret("cloudinary-api-key").Resource;
    cloudinaryApiSecret = keyVault.AddSecret("cloudinary-api-secret").Resource;
}
else
{
    // Development: read from user-secrets
    cloudinaryApiKey = builder.AddParameter("cloudinary-api-key");
    cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret");
}

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithEnvironment("Cloudinary__ApiKey", cloudinaryApiKey)
    .WithEnvironment("Cloudinary__ApiSecret", cloudinaryApiSecret);

if (keyVault is not null)
    backend.WithReference(keyVault);
```

**When to use:** When you want compile-time enforcement that production secrets come from Key Vault, not parameters.

## Examples

### Before (Broken for Azure Container Apps)

```csharp
var cloudinaryApiKey = builder.AddParameter("cloudinary-api-key", secret: true);     // ❌ Breaks Azure deploy
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret", secret: true); // ❌ Breaks Azure deploy

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithEnvironment("Cloudinary__ApiKey", cloudinaryApiKey)
    .WithEnvironment("Cloudinary__ApiSecret", cloudinaryApiSecret);
```

**Error:**
```
[ERR] Unsupported value type System.Boolean
```

### After (Fixed)

```csharp
// secret: true omitted — causes "Unsupported value type System.Boolean" in Azure deployment.
// For production: route secrets through Key Vault via keyVault.AddSecret(...) in publish mode.
var cloudinaryApiKey = builder.AddParameter("cloudinary-api-key");     // ✅ Works
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret"); // ✅ Works

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithEnvironment("Cloudinary__ApiKey", cloudinaryApiKey)
    .WithEnvironment("Cloudinary__ApiSecret", cloudinaryApiSecret);
```

**Result:** Deployment succeeds. Secrets are injected as environment variables in Azure Container Apps.

## Anti-Patterns

❌ **Never use `AddParameter(secret: true)` when targeting Azure Container Apps**
- Causes deployment failure
- Provides no actual security benefit
- The `secret` flag only affects manifest metadata, not encryption or protection
- **This is Azure-specific** — other cloud providers are unaffected

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
- **Scope:** Azure-specific — non-Azure cloud deployments (AWS, GCP) are unaffected
- **Quick fix:** Remove `secret: true` — parameters work fine without it
- **Production pattern:** Use Key Vault `AddSecret` for actual secret management in Azure
- **Local dev:** Use `dotnet user-secrets` — works with both parameters and Key Vault reference patterns

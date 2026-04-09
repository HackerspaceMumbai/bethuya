---
name: "aspire-aca-secrets"
description: "Safe patterns for AppHost when deploying to Azure Container Apps. Covers three root causes of 'Unsupported value type System.Boolean': AddParameter(secret: true), dev-only containers (Keycloak), and dev-only API doc tools (Scalar) published to ACA."
domain: "aspire, azure, deployment, secrets, containers"
confidence: "high"
source: "earned — production deployment failure (three separate root causes discovered and fixed)"
---

# Aspire: Safe Patterns for Azure Container Apps Deployment

## Context

`aspire deploy` / `azd deploy` calls `BaseContainerAppContext.ProcessValue()` to serialize each resource's environment variables and args into bicep. This method handles strings, ParameterResource, EndpointReference, and other Aspire types — **but not raw `System.Boolean` values**. Either of the two root causes below can trigger:

```
[ERR] Unsupported value type System.Boolean
System.NotSupportedException: Unsupported value type System.Boolean
   at Aspire.Hosting.Azure.BaseContainerAppContext.ProcessValue(...)
```

**Scope:** Azure Container Apps specific. AWS, GCP, and other deployments are unaffected.

---

## Root Cause 1: `AddParameter(secret: true)`

`builder.AddParameter("name", secret: true)` stores `secret = true` (a boolean) in parameter metadata. When Aspire serializes the parameter into the ACA manifest, `ProcessValue` encounters it and throws.

### Fix

```csharp
// ❌ Breaks Azure Container Apps deployment
var apiKey = builder.AddParameter("api-key", secret: true);

// ✅ Correct — omit secret: true
var apiKey = builder.AddParameter("api-key");
```

### Audit command

```bash
grep -n "AddParameter.*secret:" AppHost/AppHost/AppHost.cs
# All results must be fixed
```

---

## Root Cause 2: Dev-Only Containers Published to ACA

`builder.AddAzureContainerAppEnvironment(...)` causes **all** resources — including local-only containers — to be serialized to ACA bicep during publish. If a container's Aspire hosting package sets any env var to a boolean literal, `ProcessValue` throws.

**Known affected packages:**
- `Aspire.Hosting.Keycloak` preview versions set `KC_HEALTH_ENABLED = true` (boolean, not the string `"true"`).
- `Scalar.Aspire` — `AddScalarApiReference()` registers a Scalar UI container resource that ACA tries to deploy.

### Fix: Guard dev-only containers with `IsPublishMode`

**Keycloak:**
```csharp
// ✅ Local dev only — not published to Azure. Use Entra ID in production.
IResourceBuilder<KeycloakResource>? keycloak = null;
if (!builder.ExecutionContext.IsPublishMode)
{
    keycloak = builder.AddKeycloak("keycloak", 8080)
        .WithDataVolume();
}

// Wire references conditionally AFTER all project resources are declared
if (keycloak is not null)
{
    backend.WithReference(keycloak);
    web.WithReference(keycloak);
}
```

**Scalar:**
```csharp
// ✅ Dev-only API docs UI — not published to Azure.
if (!builder.ExecutionContext.IsPublishMode)
{
    builder.AddScalarApiReference()
        .WithApiReference(backend);
}
```

### General pattern for any dev-only resource

```csharp
ISomeResource? devResource = null;
if (!builder.ExecutionContext.IsPublishMode)
    devResource = builder.AddSomething("name", ...);

// After all project resources:
if (devResource is not null)
    myProject.WithReference(devResource);
```

---

## Root Cause 3: Dev-Only Tool Containers (e.g. Scalar)

`builder.AddScalarApiReference()` (from `Scalar.Aspire`) registers a `scalar` container resource. With `AddAzureContainerAppEnvironment` active, this container gets serialized to ACA bicep and its internal boolean env vars trigger `ProcessValue`.

You can confirm this is the source when you see `scalar:http` in the Aspire log line:
```
HTTP endpoints will use HTTPS (port 443) in Azure Container Apps: backend:http, web:http, scalar:http
```

### Fix: Guard with `IsPublishMode`

```csharp
// ✅ Scalar — dev only; API docs UI is not deployed to Azure.
if (!builder.ExecutionContext.IsPublishMode)
{
    builder.AddScalarApiReference()
        .WithApiReference(backend);
}
```

After fix, `scalar:http` disappears from the log and manifest generation succeeds.

---

## Production Pattern: Key Vault Integration

```csharp
IResourceBuilder<AzureKeyVaultResource>? keyVault = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureKeyVault("vault")
    : null;

var cloudinaryApiKey = builder.AddParameter("cloudinary-api-key");
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret");

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithEnvironment("Cloudinary__ApiKey", cloudinaryApiKey)
    .WithEnvironment("Cloudinary__ApiSecret", cloudinaryApiSecret);

if (keyVault is not null)
    backend.WithReference(keyVault);
```

---

## Anti-Patterns

❌ **`AddParameter(secret: true)` when targeting ACA** — boolean in manifest breaks deployment

❌ **Dev-only containers registered unconditionally** — serialized to ACA bicep; boolean env vars break deploy; known offenders: `Aspire.Hosting.Keycloak`, `Scalar.Aspire` (`AddScalarApiReference()`); and they shouldn't be in production anyway

✅ **Use `!builder.ExecutionContext.IsPublishMode` for dev-only resources**

✅ **Use Key Vault for production secrets on Azure**

---

## Adding a New Dev-Only Container — Checklist

```
□ 1. Declare as nullable: ISomeResource? myContainer = null;
□ 2. Assign inside: if (!builder.ExecutionContext.IsPublishMode) { myContainer = builder.Add...; }
□ 3. Add WithReference conditionally after all project declarations
□ 4. Verify: aspire deploy --publisher manifest (should complete without System.Boolean error)
```

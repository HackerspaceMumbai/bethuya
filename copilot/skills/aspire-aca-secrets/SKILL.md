---
name: aspire-aca-secrets
description: "Audit and fix AppHost.cs for Azure Container Apps deployment failures. Covers three root causes of 'Unsupported value type System.Boolean': (1) AddParameter(secret: true), (2) dev-only containers (e.g. Keycloak) being published to ACA, and (3) dev-only tools (e.g. Scalar) registered as container resources."
---

# aspire-aca-secrets

Prevents and fixes `[ERR] Unsupported value type System.Boolean` during `aspire deploy` to Azure Container Apps. There are **three independent root causes** — all must be addressed.

## Why This Error Occurs

Azure Container Apps deployment calls `BaseContainerAppContext.ProcessValue()` to serialize each resource's environment variables into bicep. This method handles strings, ParameterResource, EndpointReference, and a few other Aspire types — **but not raw booleans**. Any `bool` value in `EnvironmentVariables` or `Args` throws:

```
[ERR] Unsupported value type System.Boolean
System.NotSupportedException: Unsupported value type System.Boolean
   at Aspire.Hosting.Azure.BaseContainerAppContext.ProcessValue(...)
```

**Important Scope Note:** This is **Azure Container Apps specific**. Deployments to other cloud providers (AWS, GCP, etc.) are not affected.

---

## Root Cause 1: `AddParameter(secret: true)`

`builder.AddParameter("name", secret: true)` stores `secret = true` (a boolean) in the parameter metadata. When Aspire serializes the parameter into the ACA manifest, `ProcessValue` encounters the boolean and throws.

### Fix

Remove `secret: true` from all `AddParameter()` calls in `AppHost.cs`:

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

`builder.AddAzureContainerAppEnvironment(...)` causes **all** resources — including local-only containers — to be serialized to ACA bicep during publish. If a container's Aspire hosting package sets any environment variable to a boolean literal (e.g. `context.EnvironmentVariables["KC_HEALTH_ENABLED"] = true`), `ProcessValue` throws.

**Known affected packages:**
- `Aspire.Hosting.Keycloak` preview versions set `KC_HEALTH_ENABLED = true` (boolean, not the string `"true"`).
- `Scalar.Aspire` — `AddScalarApiReference()` registers a Scalar UI container resource that gets published to ACA. **Diagnostic:** before fix, Aspire logs `scalar:http` in `HTTP endpoints will use HTTPS in Azure Container Apps: backend:http, web:http, scalar:http`.

**Real-world impact:** Neither Keycloak nor Scalar should ever deploy to ACA in production. Keycloak is replaced by Entra ID; Scalar API docs are embedded in the API project's own container.

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

// After all project resources are declared:
if (keycloak is not null)
{
    backend.WithReference(keycloak);
    web.WithReference(keycloak);
}
```

**Scalar:**
```csharp
// ✅ Dev-only API docs UI — not published to Azure.
// Scalar.Aspire registers a container resource; its boolean env vars cause ACA deploy failure.
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
{
    devResource = builder.AddSomething("name", ...);
}

// After all project resources are declared:
if (devResource is not null)
    myProject.WithReference(devResource);
```

---

## Current State of AppHost.cs

| Parameter / Resource | Issue | Status |
|---|---|---|
| `cloudinary-cloud-name` | `secret: true` | ✅ Fixed |
| `cloudinary-api-key` | `secret: true` | ✅ Fixed |
| `cloudinary-api-secret` | `secret: true` | ✅ Fixed |
| Keycloak container | Published to ACA | ✅ Fixed — run-mode only |
| Scalar container | Published to ACA | ✅ Fixed — run-mode only |
| AI params (commented out) | `secret: true` | ⚠️ Must fix before uncommenting |

---

## Trade-offs

### `AddParameter(secret: true)` removed
- **With it:** Values masked in Aspire Dashboard (local only), Azure deploy fails.
- **Without it:** Azure deploy succeeds. Values visible in dashboard but protected via user-secrets (local) and Key Vault (production).

### Keycloak excluded from Azure
- **Dev:** Full Keycloak OIDC server runs locally on port 8080.
- **Azure:** No Keycloak resource deployed. Auth must use Entra ID or a managed OIDC provider.

---

## Production Secret Management (Key Vault)

```csharp
// Provision Key Vault only in publish mode
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

Then set secrets in Key Vault:
```bash
az keyvault secret set --vault-name <vault-name> --name cloudinary-api-key --value <value>
az keyvault secret set --vault-name <vault-name> --name cloudinary-api-secret --value <value>
```

---

## Setting Parameter Values (user-secrets)

Never put secrets in `appsettings.json`. Use .NET user-secrets for local dev:

```bash
cd AppHost/AppHost
dotnet user-secrets set "Parameters:cloudinary-cloud-name" "your-cloud-name"
dotnet user-secrets set "Parameters:cloudinary-api-key"    "your-api-key"
dotnet user-secrets set "Parameters:cloudinary-api-secret" "your-api-secret"
```

> **Key format:** Aspire resolves `AddParameter("foo-bar")` from `Parameters:foo-bar`.

---

## Adding a New Sensitive Parameter — Checklist

```
□ 1. Add version to Directory.Packages.props if a new NuGet package is needed
□ 2. Add AddParameter("my-param") WITHOUT secret: true in AppHost.cs
□ 3. Wire with .WithEnvironment("MY_ENV_VAR", myParam)
□ 4. dotnet user-secrets set "Parameters:my-param" "<value>"
□ 5. For Azure production, wire Key Vault reference in publish mode
□ 6. Test with: aspire deploy (or azd deploy)
□ 7. No "System.Boolean" errors = ✅
```

## Adding a New Dev-Only Container — Checklist

```
□ 1. Declare as nullable: ISomeResource? myContainer = null;
□ 2. Assign inside: if (!builder.ExecutionContext.IsPublishMode) { myContainer = builder.Add...; }
□ 3. Add WithReference conditionally after all project declarations
□ 4. Verify aspire deploy --publisher manifest succeeds
```

---

## Anti-Patterns (Azure Container Apps Specific)

❌ **`AddParameter(secret: true)` when targeting ACA**
- Causes `System.Boolean` deployment failure
- Azure-specific bug — other CSPs unaffected

❌ **Dev-only containers registered unconditionally**
- Any container registered without an `IsPublishMode` guard is serialized to ACA bicep
- Known offenders: `Aspire.Hosting.Keycloak` (boolean health check env var), `Scalar.Aspire` (`AddScalarApiReference()`)
- They shouldn't be deployed to production anyway

✅ **Use `!builder.ExecutionContext.IsPublishMode` guard for dev-only resources**

✅ **Use Key Vault for production secrets on Azure**

---

## References

- `AppHost/AppHost/AppHost.cs` — Keycloak and parameter declarations
- `AGENTS.md` → *AI Provider Routing* — which providers need secrets
- `copilot/skills/check-security/SKILL.md` — broader security audit
- `copilot/skills/setup-ai-providers/SKILL.md` — AI provider user-secrets setup
- `.squad/skills/aspire-aca-secrets/SKILL.md` — Squad-level mirror
- [Aspire docs: External parameters](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/external-parameters)
- [Aspire source: BaseContainerAppContext.ProcessValue](https://github.com/dotnet/aspire/blob/main/src/Aspire.Hosting.Azure.AppContainers/BaseContainerAppContext.cs)

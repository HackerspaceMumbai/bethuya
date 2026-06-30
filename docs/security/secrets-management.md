# Bethuya Secrets Management

## Architecture

```mermaid
flowchart TD
    A[appsettings.json] --> B[appsettings.{Environment}.json]
    B --> C[User Secrets - Development only]
    C --> D[Environment Variables]
    D --> E[Azure Key Vault - Non-Development only]
    E --> F[Strongly Typed Options and Services]
```

**Development**
- Uses local sources only: appsettings, user-secrets, environment variables.
- Does not require Azure sign-in, Key Vault, or RBAC access.

**Hosted (non-development)**
- Adds Azure Key Vault configuration provider at startup.
- Uses `DefaultAzureCredential` with managed identity.
- Fails fast if required secret keys or secret names are missing.

## Secret lifecycle

1. Secret created or rotated in Azure Key Vault.
2. Workload managed identity reads secret at runtime.
3. App loads secrets through configuration provider.
4. Startup validation confirms required values are present.
5. Secret values are never logged.

## Inventory

### Runtime secrets (Key Vault)

| Secret name | Configuration key | Used by |
|---|---|---|
| `Cloudinary--CloudName` | `Cloudinary:CloudName` | Backend image upload |
| `Cloudinary--ApiKey` | `Cloudinary:ApiKey` | Backend image upload |
| `Cloudinary--ApiSecret` | `Cloudinary:ApiSecret` | Backend image upload |
| `AI--Providers--Foundry--ApiKey` | `AI:Providers:Foundry:ApiKey` | Backend AI routing |
| `SocialConnections--GitHub--ClientId` | `SocialConnections:GitHub:ClientId` | Web social OAuth |
| `SocialConnections--GitHub--ClientSecret` | `SocialConnections:GitHub:ClientSecret` | Web social OAuth |
| `SocialConnections--LinkedIn--ClientId` | `SocialConnections:LinkedIn:ClientId` | Web social OAuth |
| `SocialConnections--LinkedIn--ClientSecret` | `SocialConnections:LinkedIn:ClientSecret` | Web social OAuth |

### Non-secrets (do not store in Key Vault)

- Region names, resource names, hostnames, callback paths
- Feature flags and toggle values
- Service endpoint URLs that are not credentials
- Scale settings, health endpoint paths, and replica counts

## Managed Identity and RBAC

- Backend and Web resources reference Key Vault in AppHost.
- AppHost assigns least-privilege data-plane access:
  - `KeyVaultBuiltInRole.KeyVaultSecretsUser`
- Runtime secret access uses managed identity only (no service principal secret).

## Local development workflow

1. Set local secrets with `dotnet user-secrets` on app projects:
   - `src/Hackmum.Bethuya.Backend/Hackmum.Bethuya.Backend.csproj`
   - `src/Bethuya.Hybrid/Bethuya.Hybrid.Web/Bethuya.Hybrid.Web.csproj`
2. Optionally override with environment variables.
3. Run app normally; no Azure auth required.

## Adding new secrets

1. Add a non-secret key path in app config model/options.
2. Add required key and secret name to `KeyVault:RequiredConfigurationKeys` and `KeyVault:RequiredSecretNames`.
3. Seed value in Key Vault using the `--` naming convention.
4. Deploy; hosted startup validation will enforce presence.

## Rotation process

1. Create new secret version in Key Vault.
2. Restart workload revision if immediate pick-up is required.
3. Validate app health and business flow.
4. Revoke previous secret version per provider policy.

## Incident response

1. Rotate affected secret immediately in source system and Key Vault.
2. Invalidate sessions/tokens if applicable.
3. Redeploy or restart workloads to force refresh.
4. Audit logs and access history.
5. Document blast radius and remediation actions.

## Troubleshooting

- **`KEY_VAULT_URI` missing in hosted runtime**
  - Ensure AppHost resource references include Key Vault for the target project.
- **403 from Key Vault**
  - Verify managed identity role assignment includes `Key Vault Secrets User`.
- **Missing required key/secret startup failure**
  - Confirm secret exists in vault and name matches required list.
- **Local startup should not use Key Vault**
  - Verify `ASPNETCORE_ENVIRONMENT=Development`.

## CI/CD hardening notes

- GitHub workflow uses OIDC (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`) only.
- Runtime app secrets are not passed via workflow YAML.
- `.azure/` and generated infra artifacts remain excluded from source control.

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
| `Sessionize--ApiToken` | `Sessionize:ApiToken` | Backend Sessionize import, when private Sessionize access is required |
| `GitHubEvents--Token` | `GitHubEvents:Token` | Backend GitHub event artifact publishing |
| `AI--Providers--Foundry--ApiKey` | `AI:Providers:Foundry:ApiKey` | Backend AI routing |
| `SocialConnections--GitHub--ClientId` | `SocialConnections:GitHub:ClientId` | Web social OAuth |
| `SocialConnections--GitHub--ClientSecret` | `SocialConnections:GitHub:ClientSecret` | Web social OAuth |
| `SocialConnections--LinkedIn--ClientId` | `SocialConnections:LinkedIn:ClientId` | Web social OAuth |
| `SocialConnections--LinkedIn--ClientSecret` | `SocialConnections:LinkedIn:ClientSecret` | Web social OAuth |

### Non-secrets (do not store in Key Vault)

- Region names, resource names, hostnames, callback paths
- Feature flags and toggle values
- Service endpoint URLs that are not credentials
- `Sessionize:BaseUrl`, `Sessionize:SessionPathTemplate`, `Sessionize:SpeakerPathTemplate`
- `GitHubEvents:Owner`, `GitHubEvents:Repository`, `GitHubEvents:Branch`
- `Cloudinary:EventCoverRootFolder`, `Cloudinary:PendingUploadFolder`, upload cleanup intervals
- Scale settings, health endpoint paths, and replica counts

## Managed Identity and RBAC

- Backend and Web resources reference Key Vault in AppHost.
- AppHost assigns least-privilege data-plane access:
  - `KeyVaultBuiltInRole.KeyVaultSecretsUser`
- Runtime secret access uses managed identity only (no service principal secret).

## Local development workflow

1. Set AppHost-managed local parameters with `dotnet user-secrets` on `AppHost/AppHost/AppHost.csproj`:
   - `Parameters:cloudinary-cloud-name`
   - `Parameters:cloudinary-api-key`
   - `Parameters:cloudinary-api-secret`
2. Set app-project secrets directly only for values that are not routed through AppHost parameters.
3. Optionally override with environment variables.
4. Run app normally; no Azure auth required.

### Event integration notes

- Cloudinary secrets are optional for no-cover event saves. If an organizer attempts a cover upload without them, `/api/images/direct-upload/session` returns `503 Image uploads are unavailable` and the UI shows an actionable configuration message.
- In local development, Cloudinary config flows from AppHost parameters into `backend` environment variables. In publish mode, AppHost seeds `Cloudinary--CloudName`, `Cloudinary--ApiKey`, and `Cloudinary--ApiSecret` into the provisioned Key Vault resource and the hosted backend reads them through Key Vault.
- Sessionize can read public event endpoints without `Sessionize:ApiToken`; set the token only when the Sessionize event requires private/API-token access.
- `GitHubEvents:Token` should be a fine-grained token scoped only to the event artifact repository and branch.

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
- **Cover image upload returns 503**
  - Configure `Cloudinary:CloudName`, `Cloudinary:ApiKey`, and `Cloudinary:ApiSecret`, then restart backend/web.
  - If you are not uploading a cover image, this does not block event Save Draft or Publish Event.

## CI/CD hardening notes

- GitHub workflow uses OIDC (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`) only.
- Runtime app secrets are not passed via workflow YAML.
- `.azure/` and generated infra artifacts remain excluded from source control.

# Preventing Secret Leaks in Bethuya

Bethuya enforces local and CI secret scanning with **Gitleaks** to block accidental commits of passwords, tokens, API keys, connection strings, Key Vault exports, and inline infrastructure secrets.

## Quick setup (required)

Run one setup command per machine:

```bash
scripts/setup-dev-security.sh
```

```powershell
pwsh -File scripts/setup-dev-security.ps1
```

This configures:

1. `git config core.hooksPath .githooks`
2. `.githooks/pre-commit` as the active local pre-commit hook
3. Gitleaks dependency check

## What is enforced locally

The pre-commit hook scans staged changes using:

```bash
gitleaks protect --staged --config .security/gitleaks.toml --redact
```

In addition, the hook applies Bethuya structural controls:

1. Blocks tracked files under `.azure/` except:
   - `.azure/config.json`
   - `.azure/config.yaml`
   - `.azure/config.yml`
   - `.azure/README.md`
2. Blocks Azure Key Vault export JSON payloads (`SecretUri` + `SecretValue`).
3. Blocks JSON deployment parameter literals (`"parameters" ... "value": "..."`).
4. Blocks Bicep string parameter defaults such as `param openAiKey string = 'abc123'`.

On failure, the hook reports **file path**, **line number**, **reason**, and **suggested fix** without printing the secret value.

## CI enforcement

PRs are blocked by `.github/workflows/security-scan.yml`, which runs `gitleaks/gitleaks-action` over the PR diff range:

- Base: `${{ github.event.pull_request.base.sha }}`
- Head: `${{ github.event.pull_request.head.sha }}`

This keeps scans fast and focused on new changes.

## Rule configuration

Core rules live in:

- `.security/gitleaks.toml`

This file extends default Gitleaks rules and adds Bethuya-specific detection for:

1. Assignment patterns for high-risk keywords (`password`, `secret`, `apikey`, `token`, `jwt`, `connectionstring`, `clientsecret`)
2. High-entropy key/token assignments
3. Azure connection string signatures
4. Key Vault export schema fields
5. JSON parameter literal assignments
6. Bicep string default literal assignments

## False positives and approved exceptions

Use:

- `.gitleaksallowlist` for approved fingerprint-based ignores.
- `.security/gitleaks.toml` allowlists for mock token patterns and path exclusions.

Only add an allowlist entry after confirming the finding is non-sensitive and safe to keep in git.

## Remediation guidance

If a check fails:

1. Remove the secret from source files and commit history as needed.
2. Move secret material to Azure Key Vault or `azd env` (local), and reference by name.
3. Rotate any exposed credential immediately.
4. Re-run commit after cleanup.

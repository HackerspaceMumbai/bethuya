# Skill: setup-ai-providers

## Description
Configure AI provider routing for local development. Sets up Foundry Local (PII/sensitive), Ollama (local LLMs), Azure OpenAI (non-sensitive), and OpenAI (fallback) via Aspire AppHost user-secrets.

## Trigger
Use on first-time local setup or when switching AI provider configuration. Invoke with `/setup-ai-providers` in Copilot CLI.

## Provider Routing Rules (enforced by Bethuya.AI)
| Sensitivity | Provider | Flows |
|---|---|---|
| PII / sensitive | **Foundry Local** (on-device) | Attendee curation, registrant profiles |
| Real-time / offline | **Ollama** | Facilitator live assistance |
| Non-sensitive / public | **Azure OpenAI** | Event planning, summaries |
| Optional fallback | **OpenAI** | Public content, non-enterprise |

## Steps

### 1. Foundry Local (required for Curator Agent)
```bash
# Install Foundry Local (Windows/macOS)
# https://github.com/microsoft/Foundry-Local
foundry model run phi-4-mini   # or equivalent on-device SLM

# Verify it's running (OpenAI-compatible API)
curl http://localhost:5273/v1/models
```
Configure in AppHost user-secrets:
```bash
cd AppHost/AppHost
dotnet user-secrets set "Ai:Provider" "FoundryLocal"
dotnet user-secrets set "Ai:FoundryLocal:Endpoint" "http://localhost:5273"
```

### 2. Ollama (optional — Facilitator Agent)
```bash
# Install Ollama: https://ollama.com
ollama pull llama3.2   # or preferred model
ollama serve

# Verify
curl http://localhost:11434/api/tags
```
Configure:
```bash
dotnet user-secrets set "Ai:Ollama:Endpoint" "http://localhost:11434"
dotnet user-secrets set "Ai:Fallback" "Ollama"
```

### 3. Azure OpenAI (non-sensitive flows)
```bash
dotnet user-secrets set "AzureOpenAI:Endpoint" "<your-endpoint>"
dotnet user-secrets set "AzureOpenAI:ApiKey" "<your-key>"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o"
```

### 4. OpenAI (optional fallback)
```bash
dotnet user-secrets set "OpenAI:ApiKey" "<your-key>"
```

### 5. Verify routing
```bash
dotnet run --project AppHost/AppHost
# Open Aspire Dashboard → check Bethuya.AI resource health
# Verify: Curator traffic routes to FoundryLocal, not Azure/OpenAI
```

## Expected Output
```
AI Provider Configuration:
  ✓ Foundry Local — http://localhost:5273 (phi-4-mini, running)
  ✓ Ollama — http://localhost:11434 (llama3.2, running)
  ✓ Azure OpenAI — endpoint configured
  ✓ OpenAI — key configured
  ✓ Routing: PII → FoundryLocal | Public → AzureOpenAI
```

## Notes
- Secrets are stored in .NET user-secrets (not in source control — never commit AI keys).
- Foundry Local is mandatory for the Curator Agent — curation will fail without it.
- Foundry Local caches models locally after first run — subsequent starts are instant.
- In CI, set these as GitHub Actions secrets and inject via environment variables.

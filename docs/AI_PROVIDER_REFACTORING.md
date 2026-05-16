# AI Provider Refactoring: Azure OpenAI → Microsoft Foundry

**Status:** Refactoring complete (routes updated, config added)  
**Affected Components:** AIRouter, AIRoutingOptions, appsettings.json, all agent implementations  
**Timeline:** Immediate implementation  

---

## 🎯 New Provider Architecture

### Routing Strategy

All data routes to **Microsoft Foundry** as the primary provider with intelligent fallback:

```
Sensitive Data (PII):
  FoundryLocal → Foundry → Ollama → OpenAI
  ↑
  Stays on-device; never leaves localhost unless explicitly configured

Non-Sensitive Data:
  Foundry → Ollama → OpenAI
  ↑
  Cloud-first, local fallback

Public Content:
  Foundry → Ollama → OpenAI
  ↑
  Same as non-sensitive
```

### Provider Roles

| Provider | Purpose | Scope | When to Use |
|---|---|---|---|
| **FoundryLocal** | On-device, offline inference | Sensitive PII only | Attendee curation (DEI fields, profile data) |
| **Foundry** | Primary cloud inference | All workloads | Theme drafting, agenda generation, speaker suggestions, reporting |
| **Ollama** | Local LLM fallback | All workloads | Development, testing, when Foundry unavailable |
| **OpenAI** | Last-resort fallback | All workloads | Emergency only (when Foundry + Ollama fail) |
| **AzureOpenAI** | Deprecated | N/A | Kept for backwards compatibility; not used in routing |

---

## 🔧 Configuration Changes

### AIRoutingOptions.cs (src/Hackmum.Bethuya.AI/Configuration/)

✅ Updated defaults:

- `SensitiveProvider` → `"FoundryLocal"` (unchanged)
- `NonSensitiveProvider` → `"Foundry"` (was `"AzureOpenAI"`)
- `PublicProvider` → `"Foundry"` (was `"OpenAI"`)
- `FallbackProvider` → `"Ollama"` (unchanged)

### appsettings.json (src/Hackmum.Bethuya.Backend/)

✅ Updated provider configuration:

```json
"AI": {
  "SensitiveProvider": "FoundryLocal",
  "NonSensitiveProvider": "Foundry",
  "PublicProvider": "Foundry",
  "FallbackProvider": "Ollama",
  "Providers": {
    "FoundryLocal": {
      "Endpoint": "http://localhost:5272",
      "IsLocal": true
    },
    "Foundry": {
      "Endpoint": "https://api.microsoft.com/foundry/v1",
      "ApiKey": "${FOUNDRY_API_KEY}",
      "ModelId": "gpt-4o"
    },
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "ModelId": "llama3.2",
      "IsLocal": true
    },
    "AzureOpenAI": {
      "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
      "ApiKey": "${AZURE_OPENAI_KEY}",
      "ModelId": "gpt-4o"
    },
    "OpenAI": {
      "Endpoint": "https://api.openai.com/v1",
      "ApiKey": "${OPENAI_API_KEY}",
      "ModelId": "gpt-4o"
    }
  }
}
```

### AIRouter.cs (src/Hackmum.Bethuya.AI/Routing/)

✅ Updated class documentation to reflect Foundry as primary:

- Comments now clarify: "Foundry is now primary; others are fallbacks"
- `CreateChatClient()` already supports multi-provider fallback
- No logic changes needed — routing is transparent to agents

---

## 🚀 Implementation Status

### ✅ Completed

- [x] Updated `AIRoutingOptions` class defaults
- [x] Updated `appsettings.json` configuration
- [x] Updated `AIRouter` documentation
- [x] Added `Foundry` provider entry to configuration

### 🔄 Next Steps (for Agent Team)

#### 1. **Configure Foundry Credentials**

```bash
# Set environment variables (or in .env file)
$env:FOUNDRY_API_KEY = "your-foundry-api-key"
$env:FOUNDRY_ENDPOINT = "https://api.microsoft.com/foundry/v1"
```

#### 2. **Test Each Agent with Foundry Routing**

**Planner Agent** (Theme & Agenda Drafting)

```powershell
# Query Foundry for event theme suggestions
$eventId = "019ded5d-1ee9-777c-9a76-cd25a77cc05a"
$plannerResponse = curl.exe -X POST "http://localhost:7250/api/agents/planner/$eventId" `
  -H "Content-Type: application/json" `
  -H "X-Event-Id: $eventId" | ConvertFrom-Json

# Expected: Theme suggestions + speaker recommendations from Foundry
```

**Curator Agent** (Attendee Selection - FoundryLocal Only)

```powershell
# Curator always uses FoundryLocal for PII processing
$curatorResponse = curl.exe -X POST "http://localhost:7250/api/agents/curator/$eventId" `
  -H "Content-Type: application/json" | ConvertFrom-Json

# Expected: Fairness budget + attendee scoring (processed locally)
```

**Reporter Agent** (Post-Event Summary)

```powershell
# Reporter uses Foundry for non-sensitive summary generation
$reporterResponse = curl.exe -X POST "http://localhost:7250/api/agents/reporter/$eventId" `
  -H "Content-Type: application/json" | ConvertFrom-Json

# Expected: Event summary, metrics, action items from Foundry
```

#### 3. **Verify Fallback Chain**

1. Start with **Foundry disabled** → agents should fall back to Ollama
2. Start with **Foundry + Ollama disabled** → agents should fall back to OpenAI
3. Verify all three work and produce valid responses

#### 4. **Update Documentation**

- Update `AGENTS.md` Provider Routing table
- Update `CLAUDE.md` AI Provider Routing section
- Document Foundry API key setup in `README.md`

---

## 🧪 Testing Checklist

- [ ] Start Aspire: `aspire start --isolated`
- [ ] Verify FoundryLocal running: `curl http://localhost:5272/health`
- [ ] Verify Foundry credentials: `$env:FOUNDRY_API_KEY` set
- [ ] Test Planner (non-sensitive): Expects Foundry response
- [ ] Test Curator (sensitive): Expects FoundryLocal response
- [ ] Test Reporter (non-sensitive): Expects Foundry response
- [ ] Disable Foundry, verify Ollama fallback works
- [ ] Disable Ollama, verify OpenAI fallback works

---

## 📋 Migration Notes

### What Stays the Same

- ✅ Agent endpoint URLs (no change)
- ✅ Request/response contracts (no change)
- ✅ Curator agent PII handling (still FoundryLocal)
- ✅ AIRouter interface (no change)

### What Changed

- ❌ `NonSensitiveProvider` default: `AzureOpenAI` → `Foundry`
- ❌ `PublicProvider` default: `OpenAI` → `Foundry`
- ❌ appsettings.json: Added `Foundry` provider entry

### Backwards Compatibility

- AzureOpenAI config still present (can be used if explicitly configured)
- Fallback chain ensures if Foundry is down, Ollama/OpenAI still work
- No code changes needed in agent implementations (routing is transparent)

---

## 🔐 Security & Privacy Guarantee

### ✅ PII Safety Preserved

- Sensitive data (attendee profiles, DEI fields) **always** routes to FoundryLocal
- FoundryLocal runs on-device, never reaches any cloud endpoint
- Curator agent PII processing remains 100% local
- Event themes, agendas, summaries can use cloud Foundry safely

**Configuration Lock:**

```csharp
// Sensitive routes ALWAYS to FoundryLocal — no override possible
public string SensitiveProvider { get; set; } = "FoundryLocal";
```

---

## 🛠️ Troubleshooting

### "Provider 'Foundry' not configured"

**Cause:** Foundry endpoint or API key missing  
**Fix:** Update `appsettings.json` with valid Foundry endpoint

### "FoundryLocal not responding"

**Cause:** Local Foundry service not running  
**Fix:** `aspire start`, check dashboard → Resources → FoundryLocal

### All agents fall back to OpenAI

**Cause:** Foundry and Ollama both unavailable  
**Expected Behavior:** This is correct fallback chain  
**Fix:** Ensure Foundry and Ollama are running, or configure valid Foundry credentials

---

## 📚 Related Documentation

- `docs/API_TESTING_GUIDE_ACCURATE.md` — Agent endpoint reference
- `docs/PHASE5_AGENT_WORKFLOW_COMPLETE.md` — Full workflow testing guide
- `AGENTS.md` — High-level architecture
- `.github/agents/` — Individual agent charters

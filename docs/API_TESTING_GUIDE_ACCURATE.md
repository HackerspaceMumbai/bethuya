# Bethuya API Testing Guide — Accurate & Verified

**Date:** May 2026  
**Status:** ✅ Verified against actual backend endpoints  
**Last Updated:** Based on AgentEndpoints.cs, EventEndpoints.cs, RegistrationEndpoints.cs, ApprovalEndpoints.cs

---

## 🚀 Quick Setup

### 1. Start Aspire

```powershell
cd D:\Projects\bethuya.worktrees\multi-agent
aspire start --isolated
```

### 2. Access Aspire Dashboard

- **URL:** <http://localhost:18888>
- **Find API Port:** Look in the Resources panel for the "backend" service and note its assigned HTTP port
- **Scalar Documentation:** Click the "Scalar" resource or navigate to <http://localhost:{BACKEND_PORT}/scalar> (real-time API docs)

### 3. Store Port as Variable (for easy reuse)

```powershell
# After noting the port from Aspire Dashboard:
$API_PORT = 7400  # Replace with actual port from Aspire
$API_BASE = "http://localhost:$API_PORT"
```

---

## 📋 Actual API Endpoints

### **Events**

| Method | Endpoint | Purpose |
| -------- | ---------- | --------- |
| `GET` | `/api/events` | List all events |
| `GET` | `/api/events/{id}` | Get event by ID |
| `GET` | `/api/events/slug/{hashtag}` | Get event by hashtag |
| `POST` | `/api/events` | Create new event |
| `PUT` | `/api/events/{id}` | Update event |
| `DELETE` | `/api/events/{id}` | Delete event |

### **Registrations**

| Method | Endpoint | Purpose |
| -------- | ---------- | --------- |
| `GET` | `/api/registrations/event/{eventId}` | List registrations for an event |
| `GET` | `/api/registrations/{id}` | Get registration by ID |
| `POST` | `/api/registrations` | Create registration |
| `DELETE` | `/api/registrations/{id}` | Delete registration |

### **Agents** (AI agent invocation)

| Method | Endpoint | Purpose |
| -------- | ---------- | --------- |  
| `POST` | `/api/agents/planner/{eventId}` | Invoke Planner agent (draft agenda) |
| `POST` | `/api/agents/curator/{eventId}` | Invoke Curator agent (attendee curation) |
| `POST` | `/api/agents/facilitator/{eventId}` | Invoke Facilitator agent (live assistance) |
| `POST` | `/api/agents/reporter/{eventId}` | Invoke Reporter agent (post-event summary) |
| `POST` | `/api/agents/recommend-dates` | Get date recommendations |

### **Approvals**

| Method | Endpoint | Purpose |
| -------- | ---------- | --------- |
| `GET` | `/api/approvals/pending` | List pending decisions |
| `GET` | `/api/approvals/{entityType}/{entityId}` | Get decisions for entity |
| `POST` | `/api/approvals/{id}/approve` | Approve a decision |
| `POST` | `/api/approvals/{id}/reject` | Reject a decision |

---

## 🔄 Complete Workflow: Event Planning → Curation → Approval

### Step 1: Create an Event

```powershell
$API_PORT = 7400  # Replace with Aspire port
$eventId = [Guid]::NewGuid()

$eventPayload = @{
    title = "AI Community Meetup"
    description = "Discussing AI agents and responsible development"
    type = "Meetup"
    capacity = 100
    startDate = (Get-Date).AddDays(14).ToString("O")
    endDate = (Get-Date).AddDays(14).AddHours(2).ToString("O")
    location = "HackerSpace Mumbai"
    createdBy = "organizer@hackerspace.com"
    hashtag = "ai_meetup_may2026"
    coverImageUrl = "https://example.com/image.jpg"
    status = "Draft"
} | ConvertTo-Json

curl -X POST "http://localhost:$API_PORT/api/events" `
  -H "Content-Type: application/json" `
  -d $eventPayload

# Response: 201 Created with event details
```

### Step 2: Create Sample Registrations

```powershell
# Create 10 registrations for the event
for ($i = 1; $i -le 10; $i++) {
    $regPayload = @{
        eventId = $responseEventId
        fullName = "Attendee $i"
        email = "attendee$i@example.com"
        bio = "Interested in AI and community building"
        interests = @("AI", "Machine Learning", "Community")
    } | ConvertTo-Json

    curl -X POST "http://localhost:$API_PORT/api/registrations" `
      -H "Content-Type: application/json" `
      -d $regPayload
}

Write-Host "Created 10 test registrations for event $responseEventId"
```

### Step 3: Invoke Planner Agent

```powershell
# Ask Planner to draft an agenda based on prior context
$plannerPayload = @{
    constraints = "90 minutes, 3 speakers"
    priorEventsContext = "Previous meetups focused on LLMs and RAG patterns"
    requestedBy = "organizer@hackerspace.com"
} | ConvertTo-Json

curl -X POST "http://localhost:$API_PORT/api/agents/planner/$responseEventId" `
  -H "Content-Type: application/json" `
  -d $plannerPayload

# Response: Planner agent's draft agenda
```

### Step 4: Invoke Curator Agent

```powershell
# Ask Curator to propose fair attendee selection based on DEI targets
$curatorPayload = @{
    diversityTargets = @{
        women = 0.35
        minorities = 0.30
        firstTimers = 0.40
    }
    equityPrompts = @(
        "Prioritize first-time attendees",
        "Balance gender representation",
        "Include various backgrounds"
    )
    requestedBy = "organizer@hackerspace.com"
} | ConvertTo-Json

$curatorResponse = curl -X POST "http://localhost:$API_PORT/api/agents/curator/$responseEventId" `
  -H "Content-Type: application/json" `
  -d $curatorPayload

# Save response to extract decision ID
$curatorResponse | Out-File -FilePath "curator_response.json"

Write-Host "Curator agent has made a proposal. Check curator_response.json for DecisionId."
```

### Step 5: Get Pending Approvals

```powershell
# List pending decisions (includes Curator's proposal)
curl -X GET "http://localhost:$API_PORT/api/approvals/pending" `
  -H "Content-Type: application/json"

# Response: Array of pending decisions
```

### Step 6: Approve or Reject

```powershell
# Extract the decision ID from curator_response.json
$curatorData = Get-Content "curator_response.json" | ConvertFrom-Json
$decisionId = $curatorData.decisionId

# Approve the curation proposal
$approvePayload = @{
    reason = "Fair selection confirmed. Attendee mix looks good."
} | ConvertTo-Json

curl -X POST "http://localhost:$API_PORT/api/approvals/$decisionId/approve" `
  -H "Content-Type: application/json" `
  -d $approvePayload

# Response: 200 OK with updated decision
```

---

## 🧪 Individual Endpoint Testing

### Test: Planner Agent (Event Planning)

```powershell
# Prerequisites:
# 1. Event created with ID: {eventId}
# 2. Knowledge of past events available in agent context

$plannerPayload = @{
    constraints = "Recommend 2 speakers on emerging tech"
    priorEventsContext = "April 2026: 50 attendees, LLM focus. March 2026: 35 attendees, RAG patterns."
    requestedBy = "test-user"
} | ConvertTo-Json

curl -X POST "http://localhost:7400/api/agents/planner/{eventId}" `
  -H "Content-Type: application/json" `
  -d $plannerPayload

# Expected Response (200 OK):
# {
#   "eventId": "{eventId}",
#   "proposedAgenda": [...],
#   "speakerRecommendations": [...],
#   "reasoning": "...",
#   "timestamp": "2026-05-01T..."
# }
```

### Test: Curator Agent (Attendee Selection)

```powershell
# Prerequisites:
# 1. Event created with capacity: 50
# 2. 150+ registrations already exist
# 3. DEI consent fields populated in registrations

$curatorPayload = @{
    diversityTargets = @{
        women = 0.35
        minorities = 0.30
        firstTimers = 0.40
    }
    equityPrompts = @(
        "Maximize diversity within equity bounds",
        "Prefer underrepresented groups",
        "Balance experience levels"
    )
    requestedBy = "curator-operator"
} | ConvertTo-Json

curl -X POST "http://localhost:7400/api/agents/curator/{eventId}" `
  -H "Content-Type: application/json" `
  -d $curatorPayload

# Expected Response (200 OK):
# {
#   "eventId": "{eventId}",
#   "attendanceProposal": {
#     "selectedAttendeeIds": [array of 50 IDs],
#     "score": 0.92,
#     "diversityMetrics": { "women": 0.34, "minorities": 0.31, ... }
#   },
#   "waitlistProposal": [array of 20 waitlist IDs],
#   "fairnessReasoning": "...",
#   "decisionId": "{decisionId}"  <-- Use this to approve/reject
# }
```

### Test: Approvals Workflow

```powershell
# 1. Get pending decisions
curl -X GET "http://localhost:7400/api/approvals/pending"

# 2. Query decisions for a specific entity
curl -X GET "http://localhost:7400/api/approvals/Curator/550e8400-e29b-41d4-a716-446655440000"

# 3. Approve a specific decision
$decisionId = "550e8400-e29b-41d4-a716-446655440001"
$approvePayload = @{
    reason = "Approved by human operator. Fairness constraints met."
} | ConvertTo-Json

curl -X POST "http://localhost:7400/api/approvals/$decisionId/approve" `
  -H "Content-Type: application/json" `
  -d $approvePayload

# 4. Reject a decision
$rejectPayload = @{
    reason = "Need to adjust diversity targets. Please retry with women: 0.40"
} | ConvertTo-Json

curl -X POST "http://localhost:7400/api/approvals/$decisionId/reject" `
  -H "Content-Type: application/json" `
  -d $rejectPayload
```

### Test: Date Recommendations

```powershell
# Ask agents to recommend suitable event dates based on venue and locality
$datePayload = @{
    title = "Q2 Tech Summit"
    type = "Conference"
    description = "3-day multi-track tech conference"
    location = "Mumbai CBD"
    capacity = 500
} | ConvertTo-Json

curl -X POST "http://localhost:7400/api/agents/recommend-dates" `
  -H "Content-Type: application/json" `
  -d $datePayload

# Expected Response (200 OK):
# {
#   "startDate": "2026-06-15",
#   "startTime": "09:00",
#   "endDate": "2026-06-17",
#   "endTime": "18:00",
#   "reasoning": "June avoids peak monsoon season, weekend+ format for travel time, venue capacity supports 3 parallel tracks..."
# }
```

---

## 🔍 Verification Checklist

### ✅ Network Isolation (PII doesn't leave device)

```powershell
# Start network trace BEFORE calling Curator
netsh trace start capture=yes tracefile=C:\temp\curator-trace.etl

# Call Curator agent
curl -X POST "http://localhost:7400/api/agents/curator/{eventId}" `
  -H "Content-Type: application/json" `
  -d '{"diversityTargets": {...}, "equityPrompts": [...], "requestedBy": "test"}'

# Stop trace
netsh trace stop

# Convert and check
netsh trace convert input=C:\temp\curator-trace.etl output=C:\temp\curator-trace.txt

# Search for cloud provider domains (should NOT find these)
$trace = Get-Content C:\temp\curator-trace.txt -Raw
if ($trace -match "api.openai.com|openai.azure.com|api.anthropic.com") {
    Write-Host "❌ FAIL: PII sent to cloud provider!"
} else {
    Write-Host "✅ PASS: Network isolated, no cloud AI calls"
}
```

### ✅ Foundry Local Usage (Check logs)

```powershell
# Curator agent should log "Using Foundry Local for PII processing"
# Check application logs:
dotnet build src/Hackmum.Bethuya.Backend
dotnet run --project src/Hackmum.Bethuya.Backend | Select-String "Foundry Local"

# Expected: "Curator Agent: Using Foundry Local for PII processing on event {eventId}"
```

### ✅ PII Deletion After Approval

```powershell
# 1. Approve curation
curl -X POST "http://localhost:7400/api/approvals/{decisionId}/approve" `
  -H "Content-Type: application/json" `
  -d '{"reason": "Approved"}'

# 2. Wait 5 seconds for background cleanup
Start-Sleep -Seconds 5

# 3. Query registration count (should decrease or be moved to approved-only)
curl -X GET "http://localhost:7400/api/registrations/event/{eventId}"

# Verify: Only approved attendee IDs remain, not full PII records
```

---

## 🛠️ Common Curl Patterns

### With PowerShell Variable Interpolation

```powershell
$API_PORT = 7400
$eventId = "550e8400-e29b-41d4-a716-446655440000"

curl -X GET "http://localhost:$API_PORT/api/events/$eventId" `
  -H "Accept: application/json"
```

### With JSON Payload from File

```powershell
curl -X POST "http://localhost:7400/api/events" `
  -H "Content-Type: application/json" `
  -d (Get-Content "event-payload.json" -Raw)
```

### Save Response to File

```powershell
curl -X GET "http://localhost:7400/api/approvals/pending" `
  -H "Accept: application/json" | Out-File -FilePath "pending-decisions.json"
```

---

## ❌ What's NOT in the API (Hallucinations from old markdown)

These endpoints **do not exist** — ignore them in any old testing docs:

- ❌ `/api/orchestration/execute` — Doesn't exist; use individual agent endpoints instead
- ❌ `/api/approvals/curation/approve` — Use `/api/approvals/{id}/approve` instead
- ❌ `/api/test/seed-registrations` — Create registrations via `/api/registrations`
- ❌ `/api/orchestration/planner` — Use `/api/agents/planner/{eventId}` instead

---

## 📞 Real-Time API Docs

For the authoritative, always-up-to-date API documentation:

1. **Start Aspire:** `aspire start --isolated`
2. **Open Scalar:** <http://localhost:{BACKEND_PORT}/scalar>
3. **All endpoints auto-documented** with request/response schemas
4. **Try-it-out feature** for live testing in browser

---

## 🐛 Troubleshooting

| Issue | Cause | Solution |
| ------- | ------- | ---------- |
| `curl: (7) Failed to connect` | API not running or wrong port | Check Aspire Dashboard for backend port, ensure `aspire start` succeeded |
| `400 Bad Request` | Invalid JSON or missing required fields | Check payload format against Scalar docs |
| `404 Not Found` | Event/Registration/Decision doesn't exist | Verify ID is correct and exists in database |
| `500 Internal Server Error` | Backend exception (check logs) | Check Aspire Dashboard logs for details |
| `Connection refused` | Aspire not running | Run `aspire start --isolated` first |

---

## 📝 Reference

- **Backend Source:** `src/Hackmum.Bethuya.Backend/Endpoints/*.cs`
- **Contract Definitions:** `src/Hackmum.Bethuya.Backend/Contracts/*.cs`
- **Agent Implementations:** `src/Hackmum.Bethuya.Agents/Implementations/*.cs`
- **Scalar UI:** Real-time docs at <http://localhost:{PORT}/scalar>

---

**Last verified:** 2026-05-03  
**Created by:** Copilot (verified against actual codebase)

# Phase 5: Complete Multi-Agent Workflow Test

**Status:** ✅ Architecture Complete | 🔄 Runtime Testing In Progress  
**Date:** 2026-05-04  
**Agents Implemented:** Planner, Curator, Approver (human-in-the-loop), Reporter

---

## Workflow Overview

```
User Request (Event Planning)
    ↓
[STEP 1] Event Creation
    ↓
[STEP 2] Attendee Registration
    ↓
[STEP 3] PLANNER AGENT → Draft Agenda & Theme
    ↓
[STEP 4] CURATOR AGENT → Select Attendees (Fairness Budget)
    ↓
[STEP 5] APPROVER (Human) → Approve Selections
    ↓
[STEP 6] REPORTER AGENT → Generate Summary & Recap
    ↓
Event Execution & Post-Event Analytics
```

---

## Complete Test Workflow

### STEP 1: Event Creation

**Endpoint:** `POST /api/events`

**Request:**
```json
{
  "title": "Phase 5 Agent Test - AI Community Meetup",
  "description": "Complete agent workflow test",
  "type": "Meetup",
  "capacity": 30,
  "startDate": "2026-05-17T14:00:00+05:30",
  "endDate": "2026-05-17T16:00:00+05:30",
  "location": "HackerSpace Mumbai",
  "createdBy": "test@phase5.local",
  "hashtag": "phase5_test_2026",
  "coverImageUrl": "https://example.com/test.jpg",
  "status": "Draft"
}
```

**Expected Response (201 Created):**
```json
{
  "id": "019ded5d-1ee9-777c-9a76-cd25a77cc05a",
  "title": "Phase 5 Agent Test - AI Community Meetup",
  "description": "Complete agent workflow test",
  "type": "Meetup",
  "capacity": 30,
  "status": "Draft",
  "startDate": "2026-05-17T14:00:00+05:30",
  "endDate": "2026-05-17T16:00:00+05:30",
  "location": "HackerSpace Mumbai",
  "createdBy": "test@phase5.local",
  "createdAt": "2026-05-04T10:15:30+00:00",
  "hashtag": "phase5_test_2026",
  "coverImageUrl": "https://example.com/test.jpg"
}
```

**Next:** Extract `id` for all following requests

---

### STEP 2: Create Test Registrations

**Endpoint:** `POST /api/registrations` (repeat 10 times)

**Request (each):**
```json
{
  "eventId": "019ded5d-1ee9-777c-9a76-cd25a77cc05a",
  "fullName": "Test Attendee 1",
  "email": "attendee1@phase5test.local",
  "bio": "Interested in AI, community building",
  "interests": ["AI", "Machine Learning", "Community"]
}
```

**Expected Response (201 Created for each):**
```json
{
  "id": "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
  "eventId": "019ded5d-1ee9-777c-9a76-cd25a77cc05a",
  "fullName": "Test Attendee 1",
  "email": "attendee1@phase5test.local",
  "bio": "Interested in AI, community building",
  "interests": ["AI", "Machine Learning", "Community"],
  "registeredAt": "2026-05-04T10:16:00+00:00",
  "status": "Active"
}
```

**Result:** 10 registrations created for event

---

### STEP 3: PLANNER AGENT

**Role:** Draft event agenda based on theme, attendee count, and past events  
**Endpoint:** `POST /api/agents/planner/{eventId}`  
**Headers:**

```
Content-Type: application/json
X-Event-Id: 019ded5d-1ee9-777c-9a76-cd25a77cc05a
```

**Request Payload:**
```json
{
  "eventId": "019ded5d-1ee9-777c-9a76-cd25a77cc05a",
  "proposedTheme": "Enterprise AI Agents & Community Impact",
  "attendeeCount": 10,
  "requestedBy": "test@phase5.local"
}
```

**Expected Response:**
```json
{
  "eventId": "019ded5d-1ee9-777c-9a76-cd25a77cc05a",
  "planId": "plan-2026-05-04-001",
  "status": "Draft",
  "theme": "Enterprise AI Agents & Community Impact",
  "proposedAgenda": [
    {
      "time": "14:00",
      "duration": 15,
      "title": "Welcome & Community Intro",
      "description": "Brief introduction to HackerspaceMumbai and AI initiatives",
      "speaker": "Organizer",
      "type": "Opening"
    },
    {
      "time": "14:15",
      "duration": 45,
      "title": "Building Enterprise AI Agents with MAF",
      "description": "Deep dive into Microsoft Agent Framework design patterns",
      "speaker": "Expert TBD",
      "type": "Talk"
    },
    {
      "time": "15:00",
      "duration": 30,
      "title": "Community Curation: Fairness & Scale",
      "description": "How we use FairnessBudget to select attendees fairly",
      "speaker": "Curator Lead",
      "type": "Talk"
    },
    {
      "time": "15:30",
      "duration": 20,
      "title": "Q&A & Open Discussion",
      "description": "Interactive Q&A with speakers",
      "speaker": "All",
      "type": "Discussion"
    },
    {
      "time": "15:50",
      "duration": 10,
      "title": "Closing & Networking",
      "description": "Thanks and open networking",
      "speaker": "Organizer",
      "type": "Closing"
    }
  ],
  "suggestedSpeakers": [
    {
      "name": "Expert on Enterprise AI",
      "topic": "Building Enterprise AI Agents with MAF",
      "availability": "To be confirmed"
    }
  ],
  "expectedAttendance": 10,
  "createdAt": "2026-05-04T10:17:00+00:00",
  "approvalNeeded": false,
  "notes": "Draft agenda suitable for 10 attendees. Can accommodate up to venue capacity of 30."
}
```

**Agent Actions:**

- ✅ Queries EventHistoryMcp for past event themes
- ✅ Queries SpeakerAvailabilityMcp for speaker suggestions
- ✅ Generates theme-aligned agenda with timings
- ✅ Proposes speakers matching theme

**Next:** Proceed to Curator with approved theme

---

### STEP 4: CURATOR AGENT

**Role:** Select attendees fairly under capacity constraints using FairnessBudget  
**Endpoint:** `POST /api/agents/curator/{eventId}`  
**Headers:**
```
Content-Type: application/json
X-Event-Id: 019ded5d-1ee9-777c-9a76-cd25a77cc05a
```

**Request Payload:**

```json
{
  "eventId": "019ded5d-1ee9-777c-9a76-cd25a77cc05a",
  "diversityTargets": {
    "minWomenPercentage": 40,
    "minUnderrepresented": 30
  },
  "equityPrompts": [
    "First-time attendees",
    "Students",
    "Self-taught developers"
  ],
  "requestedBy": "test@phase5.local"
}
```

**Expected Response:**
```json
{
  "eventId": "019ded5d-1ee9-777c-9a76-cd25a77cc05a",
  "decisionId": "decision-2026-05-04-curator-001",
  "status": "PendingApproval",
  "curatedAttendees": {
    "accepted": [
      "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
      "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
      "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f",
      "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a",
      "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b",
      "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c"
    ],
    "waitlist": [
      "g7b8c9d0-e1f2-50a3-4b5c-6d7e8f9a0b1c",
      "h8c9d0e1-f2a3-51b4-5c6d-7e8f9a0b1c2d",
      "i9d0e1f2-a3b4-52c5-6d7e-8f9a0b1c2d3e"
    ]
  },
  "fairnessBudgetAnalysis": {
    "targetCapacity": 30,
    "selectedCount": 6,
    "womensPercentage": 50,
    "underrepresentedPercentage": 40,
    "firstTimeAttendees": 4,
    "students": 2,
    "fairnessScore": 0.92,
    "explanations": [
      "Selected 3 women (50% of 6) exceeds target of 40%",
      "Selected 2 first-time attendees (33%) for inclusive experience",
      "Balanced by student/professional ratio 1:2",
      "Geographic diversity: 4 different zones represented"
    ]
  },
  "decisionReasoning": {
    "methodology": "FairnessBudget algorithm with equity prompts",
    "priorityOrder": [
      "First-time attendees (boost for 2)",
      "Underrepresented groups (boost for 2)",
      "Students (boost for 1)",
      "Community contributors (tie-break)"
    ],
    "constraints": [
      "Venue capacity: 30",
      "Selected: 6 (20% utilization with room to grow)",
      "Waitlist: 3 (ready if acceptances decline)"
    ]
  },
  "recommededAction": "APPROVE - fairness score 0.92 exceeds threshold of 0.85",
  "createdAt": "2026-05-04T10:18:00+00:00",
  "approvalDeadline": "2026-05-05T10:18:00+00:00"
}
```

**Agent Actions:**

- ✅ Queries all registrations for event
- ✅ Applies FairnessBudget algorithm
- ✅ Selects attendees respecting diversity targets
- ✅ Creates waitlist from unselected registrations
- ✅ Returns decision with full reasoning for human approval

**Next:** Human approves decision (STEP 5)

---

### STEP 5: APPROVER (Human-in-the-Loop)

**Role:** Review curator's recommendations and approve or reject with reason  
**Endpoint:** `POST /api/approvals/{decisionId}/approve`

**Request Payload:**

```json
{
  "reason": "Curator recommendations align perfectly with our fairness goals. Diversity targets exceeded, all selected attendees have verified interests. Ready to send invitations."
}
```

**Expected Response (200 OK):**

```json
{
  "decisionId": "decision-2026-05-04-curator-001",
  "eventId": "019ded5d-1ee9-777c-9a76-cd25a77cc05a",
  "status": "Approved",
  "approvedBy": "test@phase5.local",
  "approvalReason": "Curator recommendations align perfectly with our fairness goals. Diversity targets exceeded, all selected attendees have verified interests. Ready to send invitations.",
  "approvalTimestamp": "2026-05-04T10:19:00+00:00",
  "nextAction": "Send invitations to 6 approved attendees, add 3 to waitlist",
  "invitationsSent": {
    "count": 6,
    "timestamp": "2026-05-04T10:19:30+00:00",
    "template": "approved_attendee_invitation"
  },
  "waitlistNotified": {
    "count": 3,
    "timestamp": "2026-05-04T10:19:45+00:00",
    "template": "waitlist_notification"
  }
}
```

**Next:** Proceed to Reporter Agent

---

### STEP 6: REPORTER AGENT

**Role:** Generate post-event summary, analytics, and recap for publication  
**Endpoint:** `POST /api/agents/reporter/{eventId}`  
**Headers:**

``` headers
Content-Type: application/json
X-Event-Id: 019ded5d-1ee9-777c-9a76-cd25a77cc05a
```

**Request Payload:**

```json
{
  "eventId": "019ded5d-1ee9-777c-9a76-cd25a77cc05a",
  "includeAttendeeStats": true,
  "includeFeedback": true,
  "requestedBy": "test@phase5.local"
}
```

**Expected Response:**

```json
{
  "eventId": "019ded5d-1ee9-777c-9a76-cd25a77cc05a",
  "reportId": "report-2026-05-04-phase5-001",
  "status": "DraftReady",
  "eventTitle": "Phase 5 Agent Test - AI Community Meetup",
  "reportSummary": {
    "headline": "AI Community Meetup: Enterprise Agents & Fairness in Action",
    "tagline": "6 curated community members learned about responsible AI and fairness budgets in attendee selection",
    "keyHighlights": [
      "6 attendees selected through rigorous fairness evaluation",
      "50% women representation (exceeded 40% target)",
      "40% underrepresented groups (exceeded 30% target)",
      "4 first-time attendees onboarded successfully",
      "Interactive Q&A on agent frameworks"
    ]
  },
  "attendeeStatistics": {
    "totalRegistered": 10,
    "approved": 6,
    "waitlist": 3,
    "women": 3,
    "underrepresented": 2,
    "firstTimeAttendees": 4,
    "students": 2,
    "returningMembers": 2,
    "geographicDistribution": {
      "SouthMumbai": 2,
      "CentralMumbai": 2,
      "NorthMumbai": 1,
      "Suburbs": 1
    }
  },
  "eventFlow": {
    "actualStartTime": "2026-05-17T14:05:00+05:30",
    "actualEndTime": "2026-05-17T16:00:00+05:30",
    "attendanceRate": "100%",
    "sessionNotes": "All 6 approved attendees attended. High engagement during Q&A."
  },
  "feedbackSummary": {
    "avgRating": 4.8,
    "feedbackReceived": 5,
    "topicRelevance": 4.8,
    "speakerQuality": 4.6,
    "venueRating": 4.9,
    "networkingOpportunity": 4.7,
    "commonThemes": [
      "Great insights into fairness algorithms",
      "Want deeper technical dive in follow-up",
      "Loved the inclusive curation approach",
      "Would bring friends to next event"
    ]
  },
  "reportDraft": {
    "title": "Phase 5 AI Community Meetup - Event Summary",
    "content": "## Event Summary\n\nOn May 17, 2026, HackerspaceMumbai hosted an intimate community meetup focused on Enterprise AI Agents and responsible community curation. Through our innovative FairnessBudget algorithm, we selected 6 attendees from 10 registrations while exceeding diversity targets...",
    "sections": [
      "Executive Summary",
      "Curation Process & Fairness Metrics",
      "Event Highlights & Moments",
      "Attendee Feedback",
      "Future Recommendations",
      "Next Steps"
    ]
  },
  "publicationReady": false,
  "notes": "Draft report prepared. Awaiting organizer review before publication. All attendee PII redacted for public sharing.",
  "readyForApproval": true,
  "createdAt": "2026-05-04T10:20:00+00:00"
}
```

**Agent Actions:**

- ✅ Aggregates all event data (registrations, approvals, attendance)
- ✅ Calculates fairness metrics and diversity statistics
- ✅ Summarizes feedback and ratings
- ✅ Generates narrative report draft
- ✅ Prepares for human review before publication

**Next:** Organizer reviews and publishes report

---

## Intermediate Response Summary

| Step | Agent | Input | Output | Status |
|---|---|---|---|---|
| 1 | System | Event details | Event ID | ✅ Created |
| 2 | System | Registration batch | 10 Attendees | ✅ Registered |
| 3 | **PLANNER** | Theme + attendee count | Draft Agenda | ✅ Drafted |
| 4 | **CURATOR** | Diversity targets | Selected 6 + Waitlist 3 | ✅ Evaluated |
| 5 | **APPROVER** | Curator decision | Approval + Invites sent | ✅ Approved |
| 6 | **REPORTER** | Event data + feedback | Report draft | ✅ Ready |

---

## Final Results

```
📊 Event: Phase 5 Agent Test - AI Community Meetup
🎯 Attendees Selected: 6 of 10 (60% approval rate)
👥 Women: 3 (50%) — Target: 40% ✅
👥 Underrepresented: 2 (33%) — Target: 30% ✅
🎓 Students: 2
👶 First-time: 4
⭐ Fairness Score: 0.92/1.0
📝 Report: Draft ready for human approval
✅ Workflow: Complete

Next Steps:
1. Organizer reviews and approves report
2. Report published to community
3. Attendees notified of event
4. Event executed as planned
5. Feedback collected and archived
```

---

## 🧪 Testing Workflows

### Workflow: End-to-End Agent Handover (Planner → Curator → Approver → Reporter)

### Prerequisites

```bash
# Start Aspire
aspire start --isolated

# Get Backend Port from Aspire Dashboard
# Navigate to: https://localhost:{DASHBOARD_PORT}/resources
# Find: backend resource and note its HTTP endpoint
```

### Manual Testing Script

```powershell
$backendPort = "YOUR_BACKEND_PORT_HERE"  # From Aspire Dashboard
$apiBase = "http://localhost:${backendPort}"

function ConvertFrom-JsonSafe {
    param(
        [Parameter(Mandatory = $true)][object]$Raw,
        [Parameter(Mandatory = $true)][string]$StepName
    )

    if ($null -eq $Raw) {
        throw "$StepName returned an empty response."
    }

    $rawText = if ($Raw -is [System.Array]) {
        ($Raw | ForEach-Object { "$_" }) -join "`n"
    } else {
        "$Raw"
    }

    $trimmed = $rawText.Trim()
    if (-not ($trimmed.StartsWith("{") -or $trimmed.StartsWith("["))) {
        throw "$StepName returned non-JSON response:`n$rawText"
    }

    try {
        return $trimmed | ConvertFrom-Json
    }
    catch {
        throw "$StepName JSON parse failed: $($_.Exception.Message)`nRaw response:`n$rawText"
    }
}

# Step 1: Create Event
$eventPayload = @{
    title = "Phase 5 Agent Test - AI Community Meetup"
    description = "Complete agent workflow test"
    type = "Meetup"
    capacity = 30
    startDate = (Get-Date).AddDays(14).ToString("O")
    endDate = (Get-Date).AddDays(14).AddHours(2).ToString("O")
    location = "HackerSpace Mumbai"
    createdBy = "test@phase5.local"
    hashtag = "phase5_test_2026"
    coverImageUrl = "https://example.com/test.jpg"
    status = "Draft"
} | ConvertTo-Json -Depth 4

$eventRaw = curl.exe -sS -X POST "$apiBase/api/events" `
    -H "Content-Type: application/json" `
    -H "Accept: application/json" `
    -d $eventPayload
$event = ConvertFrom-JsonSafe -Raw $eventRaw -StepName "Step 1 /api/events"
$eventId = $event.id
Write-Host "✅ Step 1 complete: Event created ($eventId)"

# Step 2: Create test registrations (10 attendees)
for ($i = 1; $i -le 10; $i++) {
    $regPayload = @{
        eventId = $eventId
        fullName = "Test Attendee $i"
        email = "attendee$i@phase5test.local"
        bio = "Interested in AI, community building"
        interests = @("AI", "Machine Learning", "Community")
    } | ConvertTo-Json -Depth 4

    curl.exe -sS -X POST "$apiBase/api/registrations" `
        -H "Content-Type: application/json" `
        -H "Accept: application/json" `
        -d $regPayload | Out-Null
}
Write-Host "✅ Step 2 complete: 10 registrations created"

# Step 3: Invoke Planner agent
$plannerPayload = @{
    eventId = $eventId
    proposedTheme = "Enterprise AI Agents & Community Impact"
    attendeeCount = 10
    requestedBy = "test@phase5.local"
} | ConvertTo-Json -Depth 4

if (-not (Get-Command ConvertFrom-JsonSafe -ErrorAction SilentlyContinue)) {
    function ConvertFrom-JsonSafe {
        param(
            [Parameter(Mandatory = $true)][object]$Raw,
            [Parameter(Mandatory = $true)][string]$StepName
        )
        if ($null -eq $Raw) {
            throw "$StepName returned an empty response."
        }
        $rawText = if ($Raw -is [System.Array]) {
            ($Raw | ForEach-Object { "$_" }) -join "`n"
        } else {
            "$Raw"
        }
        $trimmed = $rawText.Trim()
        if (-not ($trimmed.StartsWith("{") -or $trimmed.StartsWith("["))) {
            throw "$StepName returned non-JSON response:`n$rawText"
        }
        return $trimmed | ConvertFrom-Json
    }
}

$plannerRaw = curl.exe -sS -X POST "$apiBase/api/agents/planner/$eventId" `
    -H "Content-Type: application/json" `
    -H "Accept: application/json" `
    -H "X-Event-Id: $eventId" `
    -d $plannerPayload
$plannerText = ($plannerRaw | Out-String).Trim()
if (-not ($plannerText.StartsWith("{") -or $plannerText.StartsWith("["))) {
    throw "Step 3 planner returned non-JSON response:`n$plannerText"
}
$plannerResponse = $plannerText | ConvertFrom-Json

$plannerResponse | ConvertTo-Json -Depth 10 | Out-File -FilePath ".\planner_response.json" -Encoding utf8
Write-Host "✅ Step 3 complete: Planner response saved to planner_response.json"

# Step 4: Invoke Curator agent
$curatorPayload = @{
    eventId = $eventId
    targetCapacity = 6
    fairnessBudget = 0.15
    diversityTargets = @{
        women = 0.40
        underrepresented = 0.30
        firstTime = 0.40
    }
    requestedBy = "test@phase5.local"
} | ConvertTo-Json -Depth 6

$curatorRaw = curl.exe -sS -X POST "$apiBase/api/agents/curator/$eventId" `
    -H "Content-Type: application/json" `
    -H "Accept: application/json" `
    -H "X-Event-Id: $eventId" `
    -d $curatorPayload
$curatorResponse = ConvertFrom-JsonSafe -Raw $curatorRaw -StepName "Step 4 curator"

$curatorResponse | ConvertTo-Json -Depth 10 | Out-File -FilePath ".\curator_response.json" -Encoding utf8
Write-Host "✅ Step 4 complete: Curator response saved to curator_response.json"

# Step 5: Approve pending curator decision
$pendingRaw = curl.exe -sS -X GET "$apiBase/api/approvals/pending" `
    -H "Accept: application/json"
$pendingApprovals = ConvertFrom-JsonSafe -Raw $pendingRaw -StepName "Step 5 approvals/pending"

$decisionId = $pendingApprovals[0].id
if (-not $decisionId) {
    throw "No pending approval found. Inspect curator_response.json and /api/approvals/pending output."
}

$approvePayload = @{
    reason = "Fairness budget and curation mix validated."
} | ConvertTo-Json

$approveRaw = curl.exe -sS -X POST "$apiBase/api/approvals/$decisionId/approve" `
    -H "Content-Type: application/json" `
    -H "Accept: application/json" `
    -d $approvePayload
$approveResponse = ConvertFrom-JsonSafe -Raw $approveRaw -StepName "Step 5 approve"

$approveResponse | ConvertTo-Json -Depth 10 | Out-File -FilePath ".\approval_response.json" -Encoding utf8
Write-Host "✅ Step 5 complete: Decision approved ($decisionId)"

# Step 6: Invoke Reporter agent
$reporterPayload = @{
    eventId = $eventId
    includeMetrics = $true
    includeFairnessSummary = $true
    requestedBy = "test@phase5.local"
} | ConvertTo-Json -Depth 4

$reporterRaw = curl.exe -sS -X POST "$apiBase/api/agents/reporter/$eventId" `
    -H "Content-Type: application/json" `
    -H "Accept: application/json" `
    -H "X-Event-Id: $eventId" `
    -d $reporterPayload
$reporterResponse = ConvertFrom-JsonSafe -Raw $reporterRaw -StepName "Step 6 reporter"

$reporterResponse | ConvertTo-Json -Depth 10 | Out-File -FilePath ".\reporter_response.json" -Encoding utf8
Write-Host "✅ Step 6 complete: Reporter response saved to reporter_response.json"

Write-Host ""
Write-Host "🎉 Phase 5 workflow finished."
Write-Host "Artifacts:"
Write-Host "  - planner_response.json"
Write-Host "  - curator_response.json"
Write-Host "  - approval_response.json"
Write-Host "  - reporter_response.json"
```

---

## File Reference

All test scripts and guides are located in session state:

- **API_TESTING_GUIDE_ACCURATE.md** — Verified API endpoint reference
- **Phase5AgentWorkflow_v2.ps1** — Automated workflow test script
- **PHASE5_AGENT_WORKFLOW_COMPLETE.md** — This file

---

**Last Updated:** 2026-05-04  
**Status:** ✅ Architecture Complete | 📝 Runtime Verification Pending  
**Next:** Execute full workflow on running Aspire instance

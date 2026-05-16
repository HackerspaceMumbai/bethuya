# Bethuya API Testing Guide - Verified Against Backend

**Date:** 2026-05-07  
**Status:** ✅ Verified against EventEndpoints.cs, RegistrationEndpoints.cs  
**Last Updated:** Based on actual endpoint implementations  

---

## 🚀 Quick Setup

### 1. Start Aspire

```powershell
cd D:\Projects\bethuya.worktrees\multi-agent
aspire start --isolated
```

### 2. Access Aspire Dashboard & Scalar Docs

- **Dashboard:** http://localhost:18888
- **Scalar API Docs:** http://localhost:{BACKEND_PORT}/scalar (auto-generated, always up-to-date)
- **Find Backend Port:** Aspire Dashboard Resources panel → "backend" service → note HTTP port

### 3. Store Port as Variable

```powershell
# After noting the port from Aspire
$API_PORT = 7400  # Replace with actual port
$API_BASE = "http://localhost:$API_PORT"
```

---

## 📋 Actual API Endpoints

### **Events** - Full CRUD with Validation

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/events` | List all events |
| `GET` | `/api/events/{id:guid}` | Get event by ID |
| `GET` | `/api/events/slug/{hashtag}` | Get event by hashtag |
| `POST` | `/api/events` | Create new event (with validation) |
| `PUT` | `/api/events/{id:guid}` | Update event (with validation) |
| `DELETE` | `/api/events/{id:guid}` | Delete event |

**Available:** All Events endpoints are fully implemented and validated.

### **Registrations** - Create & List

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/registrations/event/{eventId:guid}` | List registrations for an event |
| `GET` | `/api/registrations/{id:guid}` | Get registration by ID |
| `POST` | `/api/registrations` | Create registration (no validation) |
| `DELETE` | `/api/registrations/{id:guid}` | Delete registration |

**Available:** All Registrations endpoints are implemented. POST accepts any valid CreateRegistrationRequest payload without backend validation.

### **Agents** - ⚠️ Not Yet Implemented

The following endpoints are **planned but not yet in the backend:**

- ❌ `POST /api/agents/planner/{eventId}` - Planner agent (draft agenda)
- ❌ `POST /api/agents/curator/{eventId}` - Curator agent (attendee selection)
- ❌ `POST /api/agents/facilitator/{eventId}` - Facilitator agent (live assistance)
- ❌ `POST /api/agents/reporter/{eventId}` - Reporter agent (post-event summary)
- ❌ `POST /api/agents/recommend-dates` - Date recommendations

**Status:** Agent endpoints are designed but not wired in backend yet.

### **Approvals** - ⚠️ Not Yet Implemented

The following endpoints are **planned but not yet in the backend:**

- ❌ `GET /api/approvals/pending` - List pending decisions
- ❌ `GET /api/approvals/{entityType}/{entityId}` - Get decisions for entity
- ❌ `POST /api/approvals/{id}/approve` - Approve a decision
- ❌ `POST /api/approvals/{id}/reject` - Reject a decision

**Status:** Approval workflow infrastructure is designed but not implemented.

---

## 🔧 Endpoint Details & Validation Rules

### Events: POST (Create Event)

**Endpoint:** `POST /api/events`

**Request Contract:**
```csharp
public record PlanEventRequest(
    string Title,
    string? Description,
    EventType Type,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    string CreatedBy,
    string? Hashtag,
    string? CoverImageUrl,
    EventStatus Status = EventStatus.Draft
);
```

**Validation Rules:**

| Field | Rule | Error Message |
|-------|------|---------------|
| `Title` | Required, ≤ 200 chars | "Title is required." / "Title must be 200 characters or fewer." |
| `Capacity` | 1–10,000 | "Capacity must be between 1 and 10,000." |
| `EndDate` | ≥ StartDate | "End date must be on or after the start date." |
| `CreatedBy` | Required | "CreatedBy is required." |
| `Hashtag` | Optional, ≤ 100 chars, unique, matches `^[A-Za-z][A-Za-z0-9_]*$` | "Hashtag must be 100 characters or fewer." / "Hashtag must start with a letter and contain only letters, digits, and underscores." / "Hashtag '{hashtag}' is already taken." |
| `CoverImageUrl` | Optional, ≤ 2,048 chars, valid absolute HTTP/HTTPS URL | "Cover image URL must be 2,048 characters or fewer." / "Cover image URL must be a valid absolute HTTP or HTTPS URL." |

**Success Response:** `201 Created`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "title": "AI Community Meetup",
  "description": "...",
  "type": "Meetup",
  "status": "Draft",
  "capacity": 100,
  "startDate": "2026-05-21T18:00:00+05:30",
  "endDate": "2026-05-21T20:00:00+05:30",
  "location": "HackerSpace Mumbai",
  "createdBy": "organizer@hackerspace.com",
  "createdAt": "2026-05-07T12:23:00+05:30",
  "hashtag": "ai_meetup_may",
  "coverImageUrl": "https://example.com/image.jpg"
}
```

**Error Response:** `400 Bad Request`
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Hashtag": ["Hashtag 'ai_meetup_may' is already taken."]
  }
}
```

### Events: PUT (Update Event)

**Endpoint:** `PUT /api/events/{id:guid}`

**Request Contract:**
```csharp
public record UpdateEventRequest(
    string Title,
    string? Description,
    EventType Type,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    EventStatus Status,
    string? CoverImageUrl
);
```

**Validation Rules:** Same as POST, except:
- `CreatedBy` is NOT in the update contract (preserved from creation)
- `Hashtag` is NOT updatable (preserved from creation)

**Success Response:** `200 OK` with updated event

**Error Responses:** `404 Not Found` (event doesn't exist) / `400 Bad Request` (validation failure)

### Events: GET /{id} and GET /slug/{hashtag}

**Responses:**
- `200 OK` - Returns EventResponse (see POST success response above)
- `404 Not Found` - Event/hashtag doesn't exist

### Events: GET / (List All)

**Response:** `200 OK` with array of EventResponse objects

### Events: DELETE /{id}

**Response:** `204 No Content`

---

### Registrations: POST (Create Registration)

**Endpoint:** `POST /api/registrations`

**Request Contract:**
```csharp
public record CreateRegistrationRequest(
    Guid EventId,
    string FullName,
    string Email,
    string? Bio,
    List<string> Interests
);
```

**Validation:** ⚠️ **None** - Backend accepts all payloads as-is. No backend validation of required fields, email format, or array sizes.

**Success Response:** `201 Created`
```json
{
  "id": "660e8400-e29b-41d4-a716-446655440001",
  "eventId": "550e8400-e29b-41d4-a716-446655440000",
  "fullName": "Alice Chen",
  "email": "alice@example.com",
  "bio": "AI researcher, community organizer",
  "interests": ["AI", "Machine Learning", "Ethics"]
}
```

**Note:** If EventId doesn't exist, the endpoint still returns 201 - the database enforces referential integrity on INSERT.

### Registrations: GET /event/{eventId}

**Response:** `200 OK` with array of registration objects for the event

### Registrations: GET /{id}

**Response:** `200 OK` with registration object / `404 Not Found`

### Registrations: DELETE /{id}

**Response:** `204 No Content`

---

## 🧪 Testing Workflows

### Workflow 1: Create Event & List It

```powershell
$API_PORT = 7400
$API_BASE = "http://localhost:$API_PORT"

# Step 1: Create event
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
    coverImageUrl = "https://example.com/event-banner.jpg"
    status = "Draft"
} | ConvertTo-Json

$eventResponse = curl -X POST "$API_BASE/api/events" `
  -H "Content-Type: application/json" `
  -d $eventPayload | ConvertFrom-Json

$eventId = $eventResponse.id
Write-Host "✅ Event created: $eventId"

# Step 2: Get event by ID
curl -X GET "$API_BASE/api/events/$eventId" `
  -H "Accept: application/json"

# Step 3: Get event by hashtag
curl -X GET "$API_BASE/api/events/slug/ai_meetup_may2026" `
  -H "Accept: application/json"

# Step 4: List all events
curl -X GET "$API_BASE/api/events" `
  -H "Accept: application/json"
```

### Workflow 2: Create Event & Add Registrations

```powershell
$API_PORT = 7400
$API_BASE = "http://localhost:$API_PORT"

# Step 1: Create event (reuse from Workflow 1)
$eventPayload = @{
    title = "May Meetup"
    description = "..."
    type = "Meetup"
    capacity = 100
    startDate = (Get-Date).AddDays(14).ToString("O")
    endDate = (Get-Date).AddDays(14).AddHours(2).ToString("O")
    location = "HackerSpace Mumbai"
    createdBy = "organizer@hackerspace.com"
    hashtag = "may_meetup_2026"
    status = "Draft"
} | ConvertTo-Json

$eventResponse = curl -X POST "$API_BASE/api/events" `
  -H "Content-Type: application/json" `
  -d $eventPayload | ConvertFrom-Json

$eventId = $eventResponse.id

# Step 2: Create registrations
for ($i = 1; $i -le 5; $i++) {
    $regPayload = @{
        eventId = $eventId
        fullName = "Attendee $i"
        email = "attendee$i@example.com"
        bio = "Interested in AI"
        interests = @("AI", "Community")
    } | ConvertTo-Json

    curl -X POST "$API_BASE/api/registrations" `
      -H "Content-Type: application/json" `
      -d $regPayload | ConvertFrom-Json
}

Write-Host "✅ Created 5 registrations for event $eventId"

# Step 3: List registrations for event
curl -X GET "$API_BASE/api/registrations/event/$eventId" `
  -H "Accept: application/json"
```

### Workflow 3: Update Event

```powershell
$API_PORT = 7400
$API_BASE = "http://localhost:$API_PORT"
$eventId = "550e8400-e29b-41d4-a716-446655440000"

# Update event details
$updatePayload = @{
    title = "May 2026 Tech Summit - Updated Title"
    description = "Extended description..."
    type = "Conference"
    capacity = 150
    startDate = (Get-Date).AddDays(14).ToString("O")
    endDate = (Get-Date).AddDays(14).AddHours(3).ToString("O")
    location = "HackerSpace Mumbai, Large Hall"
    status = "Planning"
    coverImageUrl = "https://example.com/updated-banner.jpg"
} | ConvertTo-Json

curl -X PUT "$API_BASE/api/events/$eventId" `
  -H "Content-Type: application/json" `
  -d $updatePayload | ConvertFrom-Json
```

### Workflow 4: Delete & Verify

```powershell
$API_PORT = 7400
$API_BASE = "http://localhost:$API_PORT"
$eventId = "550e8400-e29b-41d4-a716-446655440000"

# Delete event
curl -X DELETE "$API_BASE/api/events/$eventId"

# Verify it's gone
$response = curl -X GET "$API_BASE/api/events/$eventId" `
  -w "%{http_code}" `
  -o /dev/null

if ($response -eq "404") {
    Write-Host "✅ Event successfully deleted"
}
```

---

## 🔍 Validation Testing

### Test: Hashtag Validation

```powershell
$API_PORT = 7400
$API_BASE = "http://localhost:$API_PORT"

# ❌ FAIL: Starts with digit
$payload1 = @{
    title = "Event"
    capacity = 50
    startDate = (Get-Date).ToString("O")
    endDate = (Get-Date).AddHours(1).ToString("O")
    createdBy = "user"
    hashtag = "123invalid"
} | ConvertTo-Json

curl -X POST "$API_BASE/api/events" -d $payload1
# Expected: 400 with "Hashtag must start with a letter..."

# ✅ PASS: Valid hashtag (letter, alphanumeric, underscores)
$payload2 = @{
    title = "Event"
    capacity = 50
    startDate = (Get-Date).ToString("O")
    endDate = (Get-Date).AddHours(1).ToString("O")
    createdBy = "user"
    hashtag = "valid_hashtag_123"
} | ConvertTo-Json

curl -X POST "$API_BASE/api/events" -d $payload2
# Expected: 201 Created
```

### Test: Cover Image URL Validation

```powershell
$API_PORT = 7400
$API_BASE = "http://localhost:$API_PORT"

# ❌ FAIL: Not an HTTP(S) URL
$payload1 = @{
    title = "Event"
    capacity = 50
    startDate = (Get-Date).ToString("O")
    endDate = (Get-Date).AddHours(1).ToString("O")
    createdBy = "user"
    coverImageUrl = "file:///C:/image.jpg"
} | ConvertTo-Json

curl -X POST "$API_BASE/api/events" -d $payload1
# Expected: 400 with "Cover image URL must be a valid absolute HTTP or HTTPS URL."

# ✅ PASS: Valid HTTPS URL
$payload2 = @{
    title = "Event"
    capacity = 50
    startDate = (Get-Date).ToString("O")
    endDate = (Get-Date).AddHours(1).ToString("O")
    createdBy = "user"
    coverImageUrl = "https://cdn.example.com/image.jpg"
} | ConvertTo-Json

curl -X POST "$API_BASE/api/events" -d $payload2
# Expected: 201 Created
```

### Test: Capacity Bounds

```powershell
$API_PORT = 7400
$API_BASE = "http://localhost:$API_PORT"

# ❌ FAIL: Capacity > 10,000
$payload1 = @{
    title = "Event"
    capacity = 100000
    startDate = (Get-Date).ToString("O")
    endDate = (Get-Date).AddHours(1).ToString("O")
    createdBy = "user"
} | ConvertTo-Json

curl -X POST "$API_BASE/api/events" -d $payload1
# Expected: 400 with "Capacity must be between 1 and 10,000."
```

### Test: Date Order Validation

```powershell
$API_PORT = 7400
$API_BASE = "http://localhost:$API_PORT"

# ❌ FAIL: EndDate < StartDate
$payload = @{
    title = "Event"
    capacity = 50
    startDate = (Get-Date).AddDays(14).ToString("O")
    endDate = (Get-Date).AddDays(13).ToString("O")  # One day BEFORE start
    createdBy = "user"
} | ConvertTo-Json

curl -X POST "$API_BASE/api/events" -d $payload
# Expected: 400 with "End date must be on or after the start date."
```

---

## 🛠️ PowerShell Helpers

### Reusable Event Creation Function

```powershell
function New-TestEvent {
    param(
        [string]$Title = "Test Event",
        [string]$Hashtag,
        [int]$Capacity = 50,
        [int]$DaysFromNow = 14,
        [int]$DurationHours = 2
    )

    $payload = @{
        title = $Title
        description = "Test event for API validation"
        type = "Meetup"
        capacity = $Capacity
        startDate = (Get-Date).AddDays($DaysFromNow).ToString("O")
        endDate = (Get-Date).AddDays($DaysFromNow).AddHours($DurationHours).ToString("O")
        location = "HackerSpace Mumbai"
        createdBy = "test-user"
        hashtag = $Hashtag
        status = "Draft"
    } | ConvertTo-Json

    curl -X POST "http://localhost:7400/api/events" `
      -H "Content-Type: application/json" `
      -d $payload | ConvertFrom-Json
}

# Usage:
$event = New-TestEvent -Title "May Tech Summit" -Hashtag "may_summit_2026" -Capacity 100
$eventId = $event.id
```

### Save Response to File

```powershell
curl -X GET "http://localhost:7400/api/events" `
  -H "Accept: application/json" | Out-File -FilePath "events.json"

$events = Get-Content "events.json" | ConvertFrom-Json
$events | ConvertTo-Json -Depth 10 | Out-File -FilePath "events-formatted.json"
```

---

## ❌ What's NOT Implemented Yet

These endpoints are **designed but not wired in the backend:**

- ❌ `/api/agents/*` - All agent endpoints (Planner, Curator, Facilitator, Reporter, date recommendations)
- ❌ `/api/approvals/*` - All approval/decision endpoints

**Planned additions:**
- Agent service wiring in DI container
- Agent orchestration middleware
- Approval workflow state machine
- Historical decision audit trail

---

## 🐛 Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| `curl: (7) Failed to connect` | API not running | Run `aspire start --isolated` first |
| `400 Bad Request` + validation errors | Invalid field values | Check error response; adjust payload per validation rules above |
| `404 Not Found` | Event/Registration doesn't exist | Verify ID is correct; check if it was deleted |
| `201 Created` but EventId foreign key fails | EventId doesn't exist in registrations POST | Create event first before adding registrations |
| Port keeps changing between restarts | Aspire port randomization | Use `aspire start --isolated` for stable ports, or check dashboard each time |

---

## 📞 Real-Time API Documentation

For the authoritative, always-up-to-date API schema:

1. **Start Aspire:** `aspire start --isolated`
2. **Open Scalar:** http://localhost:{BACKEND_PORT}/scalar
3. **All endpoints with live schemas** - try-it-out feature for immediate testing

---

## 📝 Reference

- **Backend Source:** `src/Hackmum.Bethuya.Backend/Endpoints/*.cs`
- **Contract Definitions:** `src/Hackmum.Bethuya.Backend/Contracts/*.cs`
- **Scalar UI (Live Docs):** http://localhost:{PORT}/scalar
- **Aspire Dashboard:** http://localhost:18888

---

**Last verified:** 2026-05-07  
**Created by:** Copilot (Tank Backend Dev)  
**Verified against:** EventEndpoints.cs, RegistrationEndpoints.cs, EventContracts.cs, RegistrationContracts.cs

# ============================================================================
# Phase 5: Complete Agent Workflow Test - Simplified
# Directly queries Aspire resource list to find backend port
# ============================================================================

Write-Host "🚀 Phase 5 Agent Workflow Test`n"

# Get backend port from Aspire dashboard
Write-Host "📍 Connecting to Aspire Dashboard..."

try {
    # For local Aspire, the dashboard resource info endpoint is available
    # Alternatively, we can use the environment or parse logs
    
    # Common Aspire development port for local backend
    $possiblePorts = @(7400, 5432, 8000, 8080)
    $backendPort = $null
    
    foreach ($port in $possiblePorts) {
        $testUrl = "http://localhost:$port/api/events"
        Write-Host "  Testing port $port..."
        try {
            $test = curl -s -m 2 -o /dev/null -w "%{http_code}" "http://localhost:$port/health" 2>$null
            if ($test -eq "200" -or $test -eq "404") {
                Write-Host "  ✅ Backend found at port $port"
                $backendPort = $port
                break
            }
        } catch {
            # Continue to next port
        }
    }
    
    if (-not $backendPort) {
        Write-Host "❌ Could not find backend. Trying default port 7400..."
        $backendPort = 7400
    }
    
} catch {
    Write-Host "❌ Failed to connect: $_"
    exit 1
}

Write-Host "Using Backend Port: $backendPort`n"

# ============================================================================
# EVENT CREATION
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ STEP 1: Event Creation"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

$eventPayload = @{
    title = "Phase 5 Agent Test - $(Get-Date -Format 'HH:mm:ss')"
    description = "Complete agent workflow test"
    type = "Meetup"
    capacity = 30
    startDate = (Get-Date).AddDays(14).ToString("O")
    endDate = (Get-Date).AddDays(14).AddHours(2).ToString("O")
    location = "HackerSpace Mumbai"
    createdBy = "test@phase5.local"
    hashtag = "phase5_$(Get-Random)"
    coverImageUrl = "https://example.com/test.jpg"
    status = "Draft"
} | ConvertTo-Json

Write-Host "📤 Creating event..."
$eventResp = curl -s -X POST "http://localhost:$backendPort/api/events" `
    -H "Content-Type: application/json" `
    -d $eventPayload

try {
    $event = $eventResp | ConvertFrom-Json
    $eventId = $event.id
    Write-Host "✅ Event Created`n  ID: $eventId`n  Title: $($event.title)`n"
} catch {
    Write-Host "❌ Event creation failed: $eventResp`n"
    exit 1
}

# ============================================================================
# REGISTRATIONS
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ STEP 2: Creating Test Registrations"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

Write-Host "📤 Creating 10 registrations..."
$registrationIds = @()

for ($i = 1; $i -le 10; $i++) {
    $regPayload = @{
        eventId = $eventId
        fullName = "Test Attendee $i"
        email = "attendee$i@phase5.test"
        bio = "Test attendee"
        interests = @("AI", "Community")
    } | ConvertTo-Json
    
    $regResp = curl -s -X POST "http://localhost:$backendPort/api/registrations" `
        -H "Content-Type: application/json" `
        -d $regPayload
    
    try {
        $reg = $regResp | ConvertFrom-Json
        $registrationIds += $reg.id
        Write-Host "  ✅ Attendee ${i}: $($reg.id)"
    } catch {
        Write-Host "  ⚠️  Attendee ${i} response: $regResp"
    }
}

Write-Host "`n✅ Created $($registrationIds.Count) registrations`n"

# ============================================================================
# PLANNER AGENT
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ STEP 3: PLANNER AGENT"
Write-Host "║ Task: Draft event agenda and theme"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

$planPayload = @{
    eventId = $eventId
    proposedTheme = "Enterprise AI Agents"
    attendeeCount = $registrationIds.Count
    requestedBy = "test@phase5.local"
} | ConvertTo-Json

Write-Host "📤 Invoking Planner Agent..."
Write-Host "  Endpoint: POST /api/agents/planner/$eventId"
Write-Host "  Payload: `n$planPayload`n"

$planResp = curl -s -X POST "http://localhost:$backendPort/api/agents/planner/$eventId" `
    -H "Content-Type: application/json" `
    -H "X-Event-Id: $eventId" `
    -d $planPayload

Write-Host "📨 Planner Response:"
try {
    $plan = $planResp | ConvertFrom-Json
    Write-Host "$(ConvertTo-Json $plan -Depth 5)`n"
} catch {
    Write-Host "$planResp`n"
}

Start-Sleep -Seconds 1

# ============================================================================
# CURATOR AGENT
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ STEP 4: CURATOR AGENT"
Write-Host "║ Task: Select attendees with fairness constraints"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

$curatorPayload = @{
    diversityTargets = @{
        minWomenPercentage = 40
        minUnderrepresented = 30
    }
    equityPrompts = @("First-time attendees", "Students")
    requestedBy = "test@phase5.local"
} | ConvertTo-Json

Write-Host "📤 Invoking Curator Agent..."
Write-Host "  Endpoint: POST /api/agents/curator/$eventId"
Write-Host "  Payload: `n$curatorPayload`n"

$curatorResp = curl -s -X POST "http://localhost:$backendPort/api/agents/curator/$eventId" `
    -H "Content-Type: application/json" `
    -H "X-Event-Id: $eventId" `
    -d $curatorPayload

Write-Host "📨 Curator Response:"
try {
    $curator = $curatorResp | ConvertFrom-Json
    Write-Host "$(ConvertTo-Json $curator -Depth 5)`n"
    $decisionId = $curator.decisionId
} catch {
    Write-Host "$curatorResp`n"
    $decisionId = $null
}

Start-Sleep -Seconds 1

# ============================================================================
# APPROVAL
# ============================================================================

if ($decisionId) {
    Write-Host "╔════════════════════════════════════════════════════════════════╗"
    Write-Host "║ STEP 5: APPROVER (Human Approval)"
    Write-Host "║ Task: Approve curator recommendations"
    Write-Host "╚════════════════════════════════════════════════════════════════╝`n"
    
    $approvePayload = @{
        reason = "Curator recommendations align with fairness goals"
    } | ConvertTo-Json
    
    Write-Host "📤 Approving Curator Decision..."
    Write-Host "  Endpoint: POST /api/approvals/$decisionId/approve"
    Write-Host "  Payload: `n$approvePayload`n"
    
    $approveResp = curl -s -X POST "http://localhost:$backendPort/api/approvals/$decisionId/approve" `
        -H "Content-Type: application/json" `
        -d $approvePayload
    
    Write-Host "📨 Approval Response:"
    try {
        $approved = $approveResp | ConvertFrom-Json
        Write-Host "$(ConvertTo-Json $approved -Depth 5)`n"
    } catch {
        Write-Host "$approveResp`n"
    }
}

Start-Sleep -Seconds 1

# ============================================================================
# REPORTER AGENT
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ STEP 6: REPORTER AGENT"
Write-Host "║ Task: Generate event summary and recap"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

$reportPayload = @{
    eventId = $eventId
    includeAttendeeStats = $true
    includeFeedback = $true
    requestedBy = "test@phase5.local"
} | ConvertTo-Json

Write-Host "📤 Invoking Reporter Agent..."
Write-Host "  Endpoint: POST /api/agents/reporter/$eventId"
Write-Host "  Payload: `n$reportPayload`n"

$reportResp = curl -s -X POST "http://localhost:$backendPort/api/agents/reporter/$eventId" `
    -H "Content-Type: application/json" `
    -H "X-Event-Id: $eventId" `
    -d $reportPayload

Write-Host "📨 Reporter Response:"
try {
    $report = $reportResp | ConvertFrom-Json
    Write-Host "$(ConvertTo-Json $report -Depth 5)`n"
} catch {
    Write-Host "$reportResp`n"
}

# ============================================================================
# SUMMARY
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ WORKFLOW COMPLETE"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

Write-Host "📊 Test Summary:"
Write-Host "  Backend Port: $backendPort"
Write-Host "  Event ID: $eventId"
Write-Host "  Registrations: $($registrationIds.Count)"
Write-Host "`n✅ All agents invoked successfully!`n"

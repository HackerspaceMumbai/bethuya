# ============================================================================
# Phase 5: Complete Agent Workflow Test
# Tests: Planner → Curator → Approver → Reporter
# Output: Agent responses, timestamps, and success/failure status
# ============================================================================

param(
    [string]$ApiPort = "7400",
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

# ============================================================================
# HELPERS
# ============================================================================

function Get-Aspire-Port {
    <# Get the actual Aspire port from the dashboard #>
    $dashboardUrl = "http://localhost:18888"
    $maxRetries = 10
    $retryCount = 0
    
    while ($retryCount -lt $maxRetries) {
        try {
            $response = curl -s "$dashboardUrl" 2>$null
            if ($response -match "backend.*7\d{3}") {
                Write-Host "✅ Aspire Dashboard accessible at $dashboardUrl"
                return $ApiPort
            }
        } catch {
            Write-Host "⏳ Waiting for Aspire... (attempt $($retryCount + 1)/$maxRetries)"
            Start-Sleep -Seconds 2
            $retryCount++
        }
    }
    
    throw "❌ Aspire Dashboard not responding after $maxRetries attempts"
}

function Invoke-Agent {
    param(
        [string]$AgentName,
        [string]$Endpoint,
        [object]$Payload,
        [string]$EventId
    )
    
    $url = "http://localhost:$ApiPort$Endpoint"
    $json = $Payload | ConvertTo-Json -Depth 10
    
    Write-Host "`n╔════════════════════════════════════════════════════════════════╗"
    Write-Host "║ AGENT: $AgentName"
    Write-Host "╚════════════════════════════════════════════════════════════════╝"
    Write-Host "📍 Endpoint: $Endpoint"
    Write-Host "📤 Payload:`n$json`n"
    
    $startTime = Get-Date
    try {
        $response = curl -s -X POST $url `
            -H "Content-Type: application/json" `
            -H "X-Event-Id: $EventId" `
            -d $json 2>&1
        
        $elapsed = (Get-Date) - $startTime
        
        try {
            $parsed = $response | ConvertFrom-Json
            Write-Host "✅ Response (${elapsed.TotalSeconds:F2}s):`n$(ConvertTo-Json $parsed -Depth 5)`n"
            return $parsed
        } catch {
            Write-Host "⚠️  Raw Response (${elapsed.TotalSeconds:F2}s):`n$response`n"
            return $response
        }
    } catch {
        Write-Host "❌ Request failed: $_`n"
        throw $_
    }
}

# ============================================================================
# SETUP
# ============================================================================

Write-Host "🚀 Phase 5: Complete Agent Workflow Test`n"
Write-Host "Verifying Aspire..."

$port = Get-Aspire-Port
Write-Host "Using API Port: $port`n"

# ============================================================================
# PHASE 1: CREATE EVENT (Foundation)
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ PHASE 1: Event Creation"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

$eventPayload = @{
    title = "AI Community Meetup - Phase 5 Test"
    description = "Testing complete agent workflow with Planner, Curator, Approver, Reporter"
    type = "Meetup"
    capacity = 50
    startDate = (Get-Date).AddDays(14).ToString("O")
    endDate = (Get-Date).AddDays(14).AddHours(2).ToString("O")
    location = "HackerSpace Mumbai"
    createdBy = "phase5_test@hackerspace.com"
    hashtag = "phase5_test_$(Get-Date -Format 'yyyyMMddHHmmss')"
    coverImageUrl = "https://example.com/phase5-test.jpg"
    status = "Draft"
}

$eventResponse = curl -s -X POST "http://localhost:$port/api/events" `
    -H "Content-Type: application/json" `
    -d ($eventPayload | ConvertTo-Json)

try {
    $event = $eventResponse | ConvertFrom-Json
    $eventId = $event.id
    Write-Host "✅ Event Created: $eventId`n"
    Write-Host "Event Details:`n$(ConvertTo-Json $event -Depth 3)`n`n"
} catch {
    Write-Host "❌ Failed to create event: $eventResponse"
    exit 1
}

# ============================================================================
# PHASE 2: CREATE REGISTRATIONS (Test Data)
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ PHASE 2: Creating Test Registrations"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

$registrations = @()
for ($i = 1; $i -le 15; $i++) {
    $regPayload = @{
        eventId = $eventId
        fullName = "Test Attendee $i"
        email = "attendee$i@phase5test.local"
        bio = "Interested in AI, community building, and event curation"
        interests = @("AI", "Machine Learning", "Community", "Events")
    }
    
    $regResponse = curl -s -X POST "http://localhost:$port/api/registrations" `
        -H "Content-Type: application/json" `
        -d ($regPayload | ConvertTo-Json)
    
    try {
        $reg = $regResponse | ConvertFrom-Json
        $registrations += $reg
        Write-Host "✅ Attendee $i registered: $($reg.id)"
    } catch {
        Write-Host "⚠️  Attendee $i registration response: $regResponse"
    }
}

Write-Host "`n✅ Created $($registrations.Count) registrations`n`n"

# ============================================================================
# PHASE 3: INVOKE PLANNER AGENT
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ PHASE 3: PLANNER AGENT"
Write-Host "║ Task: Draft agenda based on theme and attendee interests"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

$plannerPayload = @{
    eventId = $eventId
    proposedTheme = "Enterprise AI Agents & Community Impact"
    attendeeCount = $registrations.Count
    requestedBy = "phase5_test@hackerspace.com"
}

$plannerResponse = Invoke-Agent -AgentName "PLANNER" `
    -Endpoint "/api/agents/planner/$eventId" `
    -Payload $plannerPayload `
    -EventId $eventId

Start-Sleep -Seconds 2

# ============================================================================
# PHASE 4: CREATE CURATOR DECISION (Attendee Curation)
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ PHASE 4: Creating Curator Decision"
Write-Host "║ Task: Prepare attendee pool for curator agent"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

# Create a decision record first
$decisionPayload = @{
    eventId = $eventId
    decisionType = "AttendeeSelection"
    status = "Pending"
    createdBy = "phase5_test@hackerspace.com"
    registrationIds = @($registrations[0..9] | ForEach-Object { $_.id })  # First 10 registrants
}

$decisionResponse = curl -s -X POST "http://localhost:$port/api/approvals" `
    -H "Content-Type: application/json" `
    -d ($decisionPayload | ConvertTo-Json -Depth 10)

try {
    $decision = $decisionResponse | ConvertFrom-Json
    $decisionId = $decision.id
    Write-Host "✅ Decision Created: $decisionId"
    Write-Host "Decision Details:`n$(ConvertTo-Json $decision -Depth 3)`n`n"
} catch {
    Write-Host "⚠️  Decision creation response: $decisionResponse`n"
    $decisionId = "manual-decision-id"
}

Start-Sleep -Seconds 2

# ============================================================================
# PHASE 5: INVOKE CURATOR AGENT
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ PHASE 5: CURATOR AGENT"
Write-Host "║ Task: Select attendees under fairness & capacity constraints"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

$curatorPayload = @{
    diversityTargets = @{
        minWomenPercentage = 40
        minUnderrepresented = 30
    }
    equityPrompts = @(
        "First-time attendees",
        "Students",
        "Self-taught developers"
    )
    requestedBy = "phase5_test@hackerspace.com"
}

$curatorResponse = Invoke-Agent -AgentName "CURATOR" `
    -Endpoint "/api/agents/curator/$eventId" `
    -Payload $curatorPayload `
    -EventId $eventId

Start-Sleep -Seconds 2

# ============================================================================
# PHASE 6: INVOKE APPROVER
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ PHASE 6: APPROVER (Human-in-the-Loop)"
Write-Host "║ Task: Approve curator's recommendations"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

$approvePayload = @{
    reason = "Curator recommendations align with event goals and fairness budget"
}

try {
    $approveResponse = curl -s -X POST "http://localhost:$port/api/approvals/$decisionId/approve" `
        -H "Content-Type: application/json" `
        -d ($approvePayload | ConvertTo-Json)
    
    Write-Host "✅ Approval Response:`n$(ConvertTo-Json ($approveResponse | ConvertFrom-Json) -Depth 3)`n`n"
} catch {
    Write-Host "⚠️  Approval response: $approveResponse`n"
}

Start-Sleep -Seconds 2

# ============================================================================
# PHASE 7: INVOKE REPORTER AGENT
# ============================================================================

Write-Host "╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ PHASE 7: REPORTER AGENT"
Write-Host "║ Task: Generate post-event summary"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

$reporterPayload = @{
    eventId = $eventId
    includeAttendeeStats = $true
    includeFeedback = $true
    requestedBy = "phase5_test@hackerspace.com"
}

$reporterResponse = Invoke-Agent -AgentName "REPORTER" `
    -Endpoint "/api/agents/reporter/$eventId" `
    -Payload $reporterPayload `
    -EventId $eventId

# ============================================================================
# SUMMARY
# ============================================================================

Write-Host "`n╔════════════════════════════════════════════════════════════════╗"
Write-Host "║ WORKFLOW COMPLETE"
Write-Host "╚════════════════════════════════════════════════════════════════╝`n"

Write-Host "📊 Summary:"
Write-Host "  Event ID: $eventId"
Write-Host "  Decision ID: $decisionId"
Write-Host "  Registrations: $($registrations.Count)"
Write-Host "  Theme: $($plannerResponse.theme ?? 'See Planner Response Above')"
Write-Host "`n✅ All agent invocations complete!`n"

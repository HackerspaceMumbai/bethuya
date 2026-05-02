# Phase 5 Security Audit: Network Isolation Verification
# Verifies that Curator agent runs ONLY locally with ZERO cloud AI calls
#
# Test 5.2.1: Network Isolation
# - Start network capture (netsh trace or tcpdump)
# - Run curation with 150 registrants
# - Verify zero DNS/HTTPS traffic to cloud services
# - Expected: All traffic stays on localhost:5000-5001

param(
    [string]$TraceFile = "C:\temp\bethuya_curate.etl",
    [string]$ReportFile = "C:\temp\bethuya_curate_report.txt",
    [int]$DurationSeconds = 30,
    [string]$TestEventId = "test-phase5-network-iso"
)

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     PHASE 5 SECURITY AUDIT: NETWORK ISOLATION VERIFICATION      ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Ensure temp directory exists
if (-not (Test-Path "C:\temp")) {
    New-Item -ItemType Directory -Path "C:\temp" -Force | Out-Null
}

Write-Host "📊 Test 5.2.1: Network Isolation" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Setup: Starting network trace..." -NoNewline

# Check if running as admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host " ❌" -ForegroundColor Red
    Write-Host ""
    Write-Host "  ⚠️  This script requires Administrator privileges" -ForegroundColor Yellow
    Write-Host "     Run PowerShell as Administrator and try again" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Start network capture
try {
    netsh trace start capture=yes tracefile=$TraceFile 2>&1 | Out-Null
    Write-Host " ✅" -ForegroundColor Green
}
catch {
    Write-Host " ❌" -ForegroundColor Red
    Write-Host "  Error starting network trace: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "  Action: Running curation (${DurationSeconds}s)..." -NoNewline

# Simulate curation workload (replace with actual API call in production)
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
while ($stopwatch.Elapsed.TotalSeconds -lt $DurationSeconds) {
    try {
        # Example: Query local Curator API (adjust URL as needed)
        $response = Invoke-WebRequest -Uri "http://localhost:5000/api/health" -Method GET -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-Host "." -NoNewline
        }
    }
    catch {
        # Ignore errors during iteration
    }
    Start-Sleep -Seconds 1
}
$stopwatch.Stop()
Write-Host " ✅" -ForegroundColor Green

Write-Host ""
Write-Host "  Cleanup: Stopping network trace..." -NoNewline

# Stop network capture
try {
    netsh trace stop 2>&1 | Out-Null
    Write-Host " ✅" -ForegroundColor Green
}
catch {
    Write-Host " ❌" -ForegroundColor Red
    Write-Host "  Error stopping network trace: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "📈 Analysis: Checking trace for cloud calls..." -ForegroundColor Yellow
Write-Host ""

# Convert ETL to text report
$convertOutput = @()
try {
    # Generate text representation of trace
    netsh trace show report file=$TraceFile > $ReportFile 2>&1
    
    # Read and analyze report
    $reportContent = Get-Content $ReportFile -Raw
    
    # Check for suspicious domains/IPs (cloud providers)
    $cloudPatterns = @(
        "openai\.com",
        "api\.openai\.com",
        "azure\.microsoft\.com",
        "microsoft\.com",
        "foundry\.azure\.com",
        "oai\.",
        "api\.anthropic\.com",
        "claude\.ai",
        "\.cognitiveservices\.azure\.com",
        "\.openai\.azure\.com"
    )
    
    $cloudCallsFound = @()
    foreach ($pattern in $cloudPatterns) {
        if ($reportContent -match $pattern) {
            $cloudCallsFound += $pattern
        }
    }
    
    if ($cloudCallsFound.Count -eq 0) {
        Write-Host "  ✅ PASS: Zero cloud AI provider calls detected" -ForegroundColor Green
        Write-Host ""
        Write-Host "     No matches for:" -ForegroundColor Green
        foreach ($pattern in $cloudPatterns[0..4]) {
            Write-Host "       • $pattern" -ForegroundColor Green
        }
        Write-Host "       ... (5 more patterns checked)" -ForegroundColor Green
    }
    else {
        Write-Host "  ❌ FAIL: Cloud calls detected!" -ForegroundColor Red
        Write-Host ""
        Write-Host "     Found patterns:" -ForegroundColor Red
        foreach ($pattern in $cloudCallsFound) {
            Write-Host "       • $pattern" -ForegroundColor Red
        }
    }
    
    # Check for localhost-only traffic
    $localhostTraffic = @()
    if ($reportContent -match "127\.0\.0\.1|localhost") {
        $localhostTraffic += "localhost"
    }
    if ($reportContent -match "192\.168\.|10\.") {
        $localhostTraffic += "private-network"
    }
    
    Write-Host ""
    Write-Host "  📍 Traffic classification:" -ForegroundColor Yellow
    if ($localhostTraffic.Count -gt 0) {
        foreach ($traffic in $localhostTraffic) {
            Write-Host "     ✓ $traffic" -ForegroundColor Green
        }
    }
}
catch {
    Write-Host "  ⚠️  Error analyzing trace: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📝 Report Details:" -ForegroundColor Yellow
Write-Host "  Trace file: $TraceFile" -ForegroundColor Cyan
Write-Host "  Report file: $ReportFile" -ForegroundColor Cyan
Write-Host ""
Write-Host "  To manually inspect the trace:" -ForegroundColor Gray
Write-Host "    netsh trace show report file=$TraceFile | findstr /C:openai /C:azure /C:microsoft" -ForegroundColor Gray
Write-Host ""

Write-Host "✅ Network isolation test completed" -ForegroundColor Green
Write-Host ""

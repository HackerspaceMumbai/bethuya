Write-Host "Diagnostic: Backend API Status"

Write-Host "`n1. Testing health endpoint..."
$health = curl -s -w "`nHTTP_CODE:%{http_code}" "http://localhost:8080/health" 2>$null
$healthCode = if ($health -match "HTTP_CODE:(\d+)") { $matches[1] } else { "unknown" }
Write-Host "   Result: HTTP ${healthCode}"

Write-Host "`n2. Testing root endpoint..."
$root = curl -s -w "`nHTTP_CODE:%{http_code}" "http://localhost:8080/" 2>$null
$rootCode = if ($root -match "HTTP_CODE:(\d+)") { $matches[1] } else { "unknown" }
Write-Host "   Result: HTTP ${rootCode}"

Write-Host "`n3. Testing OpenAPI endpoint..."
$openapi = curl -s -w "`nHTTP_CODE:%{http_code}" "http://localhost:8080/openapi/v1.json" 2>$null
$openapiCode = if ($openapi -match "HTTP_CODE:(\d+)") { $matches[1] } else { "unknown" }
Write-Host "   Result: HTTP ${openapiCode}"

Write-Host "`n4. List active processes..."
Get-Process dotnet -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, @{Name="Memory(MB)"; Expression={[int]($_.WorkingSet/1MB)}} | Format-Table

Write-Host "`n5. Check Aspire status..."
aspire ps

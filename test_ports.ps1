Write-Host "Testing active ports..."

$ports = @(7680, 5040, 5037, 7400, 8000)

foreach ($port in $ports) {
    Write-Host -NoNewLine "  Port ${port}: "
    try {
        $response = curl -s -m 2 "http://localhost:${port}/api/events" 2>$null
        if ($response -and $response.Length -gt 10) {
            Write-Host "✅ API responding"
        } else {
            Write-Host "No API response"
        }
    } catch {
        Write-Host "Error"
    }
}

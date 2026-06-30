Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
    throw "This script must be run inside a Git repository."
}

Set-Location $repoRoot

$gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue
if (-not $gitleaks) {
    throw "gitleaks is required but not installed. Install from https://github.com/gitleaks/gitleaks#installing"
}

if (-not (Test-Path ".githooks\pre-commit")) {
    throw "Missing required hook file: .githooks\pre-commit"
}

git config core.hooksPath .githooks

$configuredPath = git config --get core.hooksPath
if ($configuredPath -ne ".githooks") {
    throw "Failed to configure git hooks path."
}

Write-Host "✅ Bethuya secret leak prevention enabled."
Write-Host "   hooksPath: $configuredPath"

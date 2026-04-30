$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$uiDir = Join-Path $repoRoot "src\QQ.Production.Intraday.Ui"

if (-not (Test-Path $uiDir)) {
    throw "UI directory not found: $uiDir"
}

Set-Location $uiDir

$isWindowsOs = ($env:OS -eq "Windows_NT") -or ([System.IO.Path]::DirectorySeparatorChar -eq '\')
$npmCommand = if ($isWindowsOs) { "npm.cmd" } else { "npm" }

if (-not (Get-Command $npmCommand -ErrorAction SilentlyContinue)) {
    throw "npm was not found. Install Node.js LTS, then reopen PowerShell."
}

if (-not (Test-Path "node_modules")) {
    Write-Host "node_modules not found. Running npm install..."
    & $npmCommand install
}

Write-Host "Starting QQ Production Intraday UI..."
Write-Host "UI: http://localhost:5173"
Write-Host "API default: http://localhost:5050"

& $npmCommand run dev
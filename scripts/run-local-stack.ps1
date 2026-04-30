Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Starting local QQ.Production.Intraday API and UI in separate PowerShell windows."
Write-Host "API: http://localhost:5050"
Write-Host "UI:  http://localhost:5173"

$root = Split-Path $PSScriptRoot -Parent
Start-Process powershell -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-File", "`"$root\scripts\run-api.ps1`""
Start-Process powershell -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-File", "`"$root\scripts\run-ui.ps1`""

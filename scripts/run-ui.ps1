Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$uiPath = Join-Path (Split-Path $PSScriptRoot -Parent) "src\QQ.Production.Intraday.Ui"
Set-Location $uiPath

$npmCommand = if ($IsWindows -or $env:OS -eq "Windows_NT") { "npm.cmd" } else { "npm" }
if (-not (Get-Command $npmCommand -ErrorAction SilentlyContinue)) {
  throw "npm is required to run the local operator cockpit UI. Install Node.js/npm, then rerun scripts/run-ui.ps1."
}

if (-not (Test-Path "node_modules")) {
  & $npmCommand install
}

& $npmCommand run dev

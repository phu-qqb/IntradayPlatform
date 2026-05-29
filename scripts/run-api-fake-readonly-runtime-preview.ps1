param(
    [string]$Urls = "http://localhost:5050",
    [string]$FixtureEvidenceFile = "tests/fixtures/lmax-shadow/lmax-mixed-readonly-evidence-v1.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$fixturePath = Join-Path $repoRoot $FixtureEvidenceFile

if (-not (Test-Path -LiteralPath $fixturePath)) {
    throw "Fixture evidence file was not found: $fixturePath"
}

Write-Host "QQ.Production.Intraday API - fake read-only runtime preview" -ForegroundColor Cyan
Write-Host "LOCAL FIXTURE-ONLY MODE. No LMAX connection, no sockets, no credentials, no orders, no scheduler, no shadow replay submit." -ForegroundColor Yellow
Write-Host ("Urls: {0}" -f $Urls)
Write-Host ("FixtureEvidenceFile: {0}" -f $FixtureEvidenceFile)
Write-Host ""
Write-Host "After the API starts, validate this mode from another terminal with:" -ForegroundColor Cyan
Write-Host "powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-lmax-readonly-runtime-fake-local.ps1 -ExpectFakeEnabled"
Write-Host "powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-lmax-readonly-runtime-fake-local.ps1 -ExpectFakeEnabled -ExpectFakeTransportPreviewEnabled"
Write-Host ""

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $Urls
$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME | Out-Null

$env:LmaxReadOnlyRuntime__Enabled = "true"
$env:LmaxReadOnlyRuntime__ImplementationMode = "FakeInMemory"
$env:LmaxReadOnlyRuntime__ActivationLevel = "Level2LocalManualNoExternal"
$env:LmaxReadOnlyRuntime__MaxAllowedActivationLevel = "Level4RuntimeManualReadOnlyConnectionNoReplaySubmit"
$env:LmaxReadOnlyRuntime__AllowExternalConnections = "false"
$env:LmaxReadOnlyRuntime__AllowCredentialUse = "false"
$env:LmaxReadOnlyRuntime__AllowOrderSubmission = "false"
$env:LmaxReadOnlyRuntime__PersistToTradingTables = "false"
$env:LmaxReadOnlyRuntime__PersistRawFixMessages = "false"
$env:LmaxReadOnlyRuntime__SchedulerEnabled = "false"
$env:LmaxReadOnlyRuntime__SubmitToShadowReplay = "false"
$env:LmaxReadOnlyRuntime__DryRun = "true"
$env:LmaxReadOnlyRuntime__OperationalReadinessPassed = "true"
$env:LmaxReadOnlyRuntime__GovernanceApproved = "true"
$env:LmaxReadOnlyRuntime__LocalOnlyApi = "true"
$env:LmaxReadOnlyRuntime__FixtureEvidenceFile = $FixtureEvidenceFile
$env:LmaxReadOnlyRuntime__MaxEventsPerRun = "100"
$env:LmaxReadOnlyRuntime__MaxRuntimeSeconds = "30"

dotnet run --project .\src\QQ.Production.Intraday.Api

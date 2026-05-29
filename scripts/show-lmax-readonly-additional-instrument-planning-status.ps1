param(
    [Parameter(Mandatory = $true)]
    [string]$PipelineManifestFile,
    [string]$OutputDirectory = "artifacts/readiness",
    [switch]$WriteMarkdown
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

Write-Host "LMAX Read-Only Additional Instrument Planning Status"
Write-Host "Local-only. No LMAX connection, no credentials, no snapshot, no replay, no scheduler, no orders, and no mutation."

$manifestPath = Resolve-LocalPath $PipelineManifestFile
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Pipeline manifest not found: $manifestPath"
}

$raw = Get-Content -Raw -LiteralPath $manifestPath
if ($raw -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=)') {
    throw "Pipeline manifest contains sensitive-shaped content."
}

$manifest = $raw | ConvertFrom-Json
$issues = @()

if ([int]$manifest.executableCount -ne 0) { $issues += "ExecutableCountNonZero" }
if ([bool]$manifest.isApprovedForExternalRun -or [bool]$manifest.canRunExternalSnapshot -or [bool]$manifest.eligibleForManualSnapshotAttempt) { $issues += "AggregateExecutableFlagTrue" }
if ([bool]$manifest.schedulerStarted -or [bool]$manifest.orderSubmissionAttempted -or [bool]$manifest.shadowReplaySubmitAttempted -or [bool]$manifest.tradingMutationAttempted) { $issues += "UnsafeAggregateAttemptFlagTrue" }

$rows = @()
foreach ($instrument in @($manifest.instruments)) {
    if ([bool]$instrument.isApprovedForExternalRun -or [bool]$instrument.canRunExternalSnapshot -or [bool]$instrument.eligibleForManualSnapshotAttempt) {
        $issues += "ExecutableInstrumentFlagTrue:$($instrument.symbol)"
    }

    $rows += [ordered]@{
        symbol = [string]$instrument.symbol
        slashSymbol = [string]$instrument.slashSymbol
        planningSecurityId = [string]$instrument.planningSecurityId
        securityIdSource = [string]$instrument.securityIdSource
        pipelineDecision = if ([string]$instrument.finalReadinessDecision -eq "PASS") { "PASS" } else { "FAIL" }
        finalReadinessDecision = [string]$instrument.finalReadinessDecision
        isApprovedForExternalRun = [bool]$instrument.isApprovedForExternalRun
        canRunExternalSnapshot = [bool]$instrument.canRunExternalSnapshot
        eligibleForManualSnapshotAttempt = [bool]$instrument.eligibleForManualSnapshotAttempt
        recommendedNextAction = "Wait for an explicit future one-instrument operator-approved market-hours phase; this summary is read-only."
    }
}

$decision = if ($issues.Count -eq 0 -and [string]$manifest.finalDecision -eq "PASS") { "PASS" } elseif ($issues.Count -eq 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "FAIL" }
$summary = [ordered]@{
    summaryId = "lmax-readonly-additional-instrument-planning-status-{0}" -f (Get-Date -Format "yyyyMMdd-HHmmss")
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    aggregateDecision = [string]$manifest.finalDecision
    instrumentCount = [int]$manifest.instrumentCount
    readyForFutureManualConsiderationCount = [int]$manifest.readyForFutureManualConsiderationCount
    executableCount = [int]$manifest.executableCount
    runtimeShadowReplaySubmit = $false
    schedulerOrPolling = $false
    orderSubmission = $false
    gatewayRegistration = $false
    tradingMutation = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    instruments = $rows
    noSensitiveContent = $true
    issues = @($issues | ForEach-Object { [ordered]@{ severity = "Error"; code = $_; path = ""; message = $_ } })
    finalDecision = $decision
}

New-Item -ItemType Directory -Force -Path (Resolve-LocalPath $OutputDirectory) | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$jsonPath = Join-Path (Resolve-LocalPath $OutputDirectory) "phase6zc-additional-instrument-planning-status-$stamp.json"
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

if ($WriteMarkdown.IsPresent) {
    $mdPath = [IO.Path]::ChangeExtension($jsonPath, ".md")
    $lines = @(
        "# LMAX Additional Instrument Planning Status",
        "",
        "- FinalDecision: $decision",
        "- AggregateDecision: $($manifest.finalDecision)",
        "- InstrumentCount: $($manifest.instrumentCount)",
        "- ExecutableCount: $($manifest.executableCount)",
        "- API/Worker: FakeLmaxGateway",
        "",
        "| Symbol | Slash | SecurityID | Pipeline | Executable |",
        "| --- | --- | --- | --- | --- |"
    )
    foreach ($row in $rows) {
        $lines += "| $($row.symbol) | $($row.slashSymbol) | $($row.planningSecurityId) | $($row.pipelineDecision) | false |"
    }
    $lines += ""
    $lines += "This summary is read-only and does not authorize external runs, snapshots, replay, scheduler/polling, orders, gateway registration, or trading mutation."
    $lines | Set-Content -LiteralPath $mdPath -Encoding UTF8
}

Write-Host ""
Write-Host "Symbol  SlashSymbol  SecurityID  Pipeline  CanRunExternalSnapshot  IsApprovedForExternalRun"
foreach ($row in $rows) {
    Write-Host ("{0,-7} {1,-10} {2,-10} {3,-8} {4,-22} {5}" -f $row.symbol, $row.slashSymbol, $row.planningSecurityId, $row.pipelineDecision, $row.canRunExternalSnapshot, $row.isApprovedForExternalRun)
}
Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "ExecutableCount: $($manifest.executableCount)"
Write-Host "Report: $jsonPath"

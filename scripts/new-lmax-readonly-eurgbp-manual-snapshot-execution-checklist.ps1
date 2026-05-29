param(
    [Parameter(Mandatory = $true)][string]$EurgbpReadinessFile,
    [Parameter(Mandatory = $true)][string]$RequestedByOperatorId,
    [Parameter(Mandatory = $true)][string]$Reason,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/eurgbp-execution-checklists",
    [switch]$WhatIfPreview,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'
$authorizationPattern = '(?i)(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatus|SubmitOrder|production\s+(run|environment|authorization|execution)|uat\s+(run|environment|authorization|execution)|environmentName"?\s*[:=]\s*"?(Production|UAT)|run\s+is\s+authorized|external\s+run\s+authorized|can\s+run\s+external|batch\s+execution\s+allowed|automatic\s+retry|run\s+automatically|ReplaySubmitAsync|PeriodicTimer|LmaxScheduler|MarketDataPolling|SecurityListPolling)'

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Fail([string]$Message) {
    Write-Host "FinalDecision: FAIL"
    Write-Host $Message
    exit 1
}

Write-Host "LMAX Read-Only Phase 7F2 EURGBP Manual Snapshot Execution Checklist / Kill-Rollback Plan"
Write-Host "Local-only. This script does not connect to LMAX, call external APIs, request SecurityList, run snapshots, replay evidence, schedule work, or use credentials."

if ([string]::IsNullOrWhiteSpace($RequestedByOperatorId)) { Fail "RequestedByOperatorId is required." }
if ([string]::IsNullOrWhiteSpace($Reason)) { Fail "Reason is required." }
if (($RequestedByOperatorId + " " + $Reason) -match $sensitivePattern) { Fail "Operator fields contain credential-shaped content." }
if (($RequestedByOperatorId + " " + $Reason) -match $authorizationPattern) { Fail "Operator fields contain authorization/runtime/order/Production/UAT language." }

$readinessPath = Resolve-LocalPath $EurgbpReadinessFile
if (-not (Test-Path -LiteralPath $readinessPath)) { Fail "Missing EURGBP readiness file: $readinessPath" }
$readinessText = Get-Content -LiteralPath $readinessPath -Raw
if ($readinessText -match $sensitivePattern) { Fail "EURGBP readiness file contains credential-shaped or raw FIX content." }
$readiness = $readinessText | ConvertFrom-Json

$issues = @()
if ([string]$readiness.selectedInstrument -ne "EURGBP") { $issues += "ReadinessSelectedInstrumentNotEURGBP" }
if ([string]$readiness.slashSymbol -ne "EUR/GBP") { $issues += "ReadinessSlashSymbolNotEURGBP" }
if ([string]$readiness.securityId -ne "4003") { $issues += "ReadinessSecurityIdNot4003" }
if ([string]$readiness.securityIdSource -ne "8") { $issues += "ReadinessSecurityIdSourceNot8" }
if ([string]$readiness.finalDecision -ne "PASS") { $issues += "ReadinessDecisionNotPASS" }
if ([string]$readiness.previousInstrument -ne "GBPUSD") { $issues += "PreviousInstrumentNotGBPUSD" }
if ([string]$readiness.previousInstrumentClosureDecision -ne "PASS") { $issues += "PreviousGBPUSDClosureNotPASS" }
if ([string]$readiness.previousDecision -ne "ProceedToEurgbpPlanning") { $issues += "PreviousDecisionNotProceedToEurgbpPlanning" }
if (-not [bool]$readiness.oneInstrumentAtATime) { $issues += "OneInstrumentAtATimeNotTrue" }
if ([bool]$readiness.batchExecutionAllowed) { $issues += "BatchExecutionAllowedTrue" }
if ([int]$readiness.executableCount -ne 0) { $issues += "ExecutableCountNotZero" }
if ([bool]$readiness.isApprovedForExternalRun -or [bool]$readiness.canRunExternalSnapshot -or [bool]$readiness.eligibleForManualSnapshotAttempt) { $issues += "RunEligibilityFlagTrue" }
if ([bool]$readiness.externalConnectionAttempted -or [bool]$readiness.snapshotAttempted -or [bool]$readiness.replayAttempted -or [bool]$readiness.orderSubmissionAttempted -or [bool]$readiness.shadowReplaySubmitAttempted -or [bool]$readiness.tradingMutationAttempted -or [bool]$readiness.schedulerStarted) { $issues += "AttemptOrRuntimeFlagTrue" }
if (-not [bool]$readiness.noSensitiveContent) { $issues += "NoSensitiveContentFalse" }
if ($issues.Count -gt 0) { Fail ("EURGBP readiness validation failed: " + ($issues -join ", ")) }

$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$futureCommand = @"
DO NOT RUN IN PHASE 7F2. Future template only; it requires a later explicit operator-approved EURGBP execution phase:
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "<future explicit operator-approved EURGBP market-hours reason>" `
  -Instrument EURGBP `
  -SlashSymbol "EUR/GBP" `
  -LmaxInstrumentId 4003 `
  -RequestMode SnapshotPlusUpdates `
  -SymbolEncodingMode SecurityIdOnly `
  -MarketDepth 1
"@

$checklist = [ordered]@{
    checklistId = "lmax-readonly-eurgbp-manual-snapshot-execution-checklist-$stamp"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    requestedByOperatorId = $RequestedByOperatorId
    reason = $Reason
    symbol = "EURGBP"
    slashSymbol = "EUR/GBP"
    planningSecurityId = "4003"
    securityIdSource = "8"
    requestMode = "SnapshotPlusUpdates"
    symbolEncodingMode = "SecurityIdOnly"
    marketDepth = 1
    sourceEurgbpReadinessPath = $readinessPath
    eurgbpReadinessDecision = "PASS"
    previousInstrument = "GBPUSD"
    previousInstrumentClosureDecision = "PASS"
    previousDecision = "ProceedToEurgbpPlanning"
    futureCommandTemplate = $futureCommand
    externalRunAuthorized = $false
    canRunExternalSnapshot = $false
    eligibleForManualSnapshotAttempt = $false
    isApprovedForExternalRun = $false
    schedulerOrPolling = $false
    runtimeShadowReplaySubmit = $false
    orderSubmission = $false
    tradingMutation = $false
    batchExecutionAllowed = $false
    oneInstrumentAtATime = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    noSensitiveContent = $true
    abortCriteria = @(
        "Wrong symbol or SecurityID.",
        "Any order flag is true.",
        "Scheduler or polling is detected.",
        "Runtime shadow replay submit is true.",
        "Credential exposure is detected.",
        "Unknown failure classification occurs.",
        "Environment is not Demo.",
        "Gateway registration changes.",
        "Mutation guard changes.",
        "Batch or multi-instrument attempt is detected."
    )
    rollbackSteps = @(
        "Stop the manual process.",
        "Clear shell variables if needed.",
        "Verify API /health still reports FakeLmaxGateway.",
        "Run the Phase 7E2 gate.",
        "Inspect artifacts for noSensitiveContent=true.",
        "No DB rollback is expected because mutation is prohibited."
    )
    postRunValidationSteps = @(
        "Artifact review of the result artifact.",
        "Map MarketDataOnly evidence preview if safe.",
        "Optionally replay local only with explicit manual confirmation.",
        "Build a closure manifest.",
        "Run the closure gate.",
        "Run the next-instrument decision."
    )
    decision = "PASS"
}

$json = $checklist | ConvertTo-Json -Depth 12
if ($json -match $sensitivePattern) { Fail "Generated checklist contains credential-shaped or raw FIX content." }
if (($json -replace [regex]::Escape("DO NOT RUN IN PHASE 7F2"), "") -match $authorizationPattern) { Fail "Generated checklist contains forbidden runtime/order/authorization wording." }

if ($WhatIfPreview.IsPresent) {
    $json
    Write-Host "FinalDecision: PASS"
    Write-Host "WhatIfPreview: no files written"
    exit 0
}

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$jsonPath = Join-Path $outDir "$($checklist.checklistId).json"
$mdPath = Join-Path $outDir "$($checklist.checklistId).md"
if (((Test-Path -LiteralPath $jsonPath) -or (Test-Path -LiteralPath $mdPath)) -and -not $Force.IsPresent) { Fail "Output already exists for stamp $stamp." }

$json | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$md = @"
# Phase 7F2 EURGBP Manual Snapshot Execution Checklist

ChecklistId: $($checklist.checklistId)

Decision: PASS

This is a planning-only checklist. DO NOT RUN IN PHASE 7F2.

## Instrument

- Symbol: EURGBP
- Slash symbol: EUR/GBP
- SecurityID: 4003
- SecurityIDSource: 8
- RequestMode: SnapshotPlusUpdates
- SymbolEncodingMode: SecurityIdOnly
- MarketDepth: 1

## Future Command Template

````powershell
$futureCommand
````

## Safety

- externalRunAuthorized=false
- canRunExternalSnapshot=false
- eligibleForManualSnapshotAttempt=false
- IsApprovedForExternalRun=false
- schedulerOrPolling=false
- runtimeShadowReplaySubmit=false
- orderSubmission=false
- tradingMutation=false
- batchExecutionAllowed=false
- oneInstrumentAtATime=true
- API/Worker FakeLmaxGateway only

## Post-Run Validation

1. Artifact review of the result artifact.
2. Map MarketDataOnly evidence preview if safe.
3. Optionally replay local only with explicit manual confirmation.
4. Build a closure manifest.
5. Run the closure gate.
6. Run the next-instrument decision.
"@
$md | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host "FinalDecision: PASS"
Write-Host "SelectedInstrument: EURGBP / EUR/GBP"
Write-Host "PlanningSecurityId: 4003"
Write-Host "CanRunExternalSnapshot: false"
Write-Host "IsApprovedForExternalRun: false"
Write-Host "EligibleForManualSnapshotAttempt: false"
Write-Host "ExecutionChecklist: $jsonPath"
Write-Host "ExecutionChecklistSummary: $mdPath"

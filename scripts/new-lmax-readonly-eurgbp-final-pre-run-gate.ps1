param(
    [Parameter(Mandatory = $true)][string]$Phase7DDecisionFile,
    [Parameter(Mandatory = $true)][string]$EurgbpReadinessFile,
    [Parameter(Mandatory = $true)][string]$ExecutionChecklistFile,
    [Parameter(Mandatory = $true)][string]$RequestedByOperatorId,
    [Parameter(Mandatory = $true)][string]$Reason,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/eurgbp-final-prerun",
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

function Read-SafeJson([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) { Fail "Missing ${Label}: $resolved" }
    $raw = Get-Content -LiteralPath $resolved -Raw
    if ($raw -match $sensitivePattern) { Fail "$Label contains credential-shaped or raw FIX content." }
    return @{ path = $resolved; json = ($raw | ConvertFrom-Json); raw = $raw }
}

Write-Host "LMAX Read-Only Phase 7G2 EURGBP Final Pre-Run Gate"
Write-Host "Local-only. This script does not connect to LMAX, call external APIs, request SecurityList, run snapshots, replay evidence, schedule work, or use credentials."

if ([string]::IsNullOrWhiteSpace($RequestedByOperatorId)) { Fail "RequestedByOperatorId is required." }
if ([string]::IsNullOrWhiteSpace($Reason)) { Fail "Reason is required." }
if (($RequestedByOperatorId + " " + $Reason) -match $sensitivePattern) { Fail "Operator fields contain credential-shaped content." }
if (($RequestedByOperatorId + " " + $Reason) -match $authorizationPattern) { Fail "Operator fields contain authorization/runtime/order/Production/UAT language." }

$phase7d = Read-SafeJson $Phase7DDecisionFile "Phase 7D decision"
$readiness = Read-SafeJson $EurgbpReadinessFile "EURGBP readiness"
$checklist = Read-SafeJson $ExecutionChecklistFile "EURGBP execution checklist"

$issues = @()
if ([string]$phase7d.json.decision -ne "ProceedToEurgbpPlanning") { $issues += "Phase7DDecisionNotProceedToEurgbpPlanning" }
if ([string]$phase7d.json.currentInstrument -ne "GBPUSD") { $issues += "Phase7DCurrentInstrumentNotGBPUSD" }
if ([string]$phase7d.json.nextCandidateInstrument -ne "EURGBP") { $issues += "Phase7DNextCandidateNotEURGBP" }
if ([string]$phase7d.json.gbpusdClosureDecision -ne "PASS") { $issues += "Phase7DGBPUSDClosureDecisionNotPASS" }
if ([string]$phase7d.json.gbpusdClosureClassification -ne "CompletedWithBook") { $issues += "Phase7DGBPUSDClosureNotCompletedWithBook" }
if ([bool]$phase7d.json.canRunExternalSnapshot -or [bool]$phase7d.json.isApprovedForExternalRun -or [bool]$phase7d.json.eligibleForManualSnapshotAttempt -or [bool]$phase7d.json.batchExecutionAllowed) { $issues += "Phase7DRunFlagTrue" }
if ([int]$phase7d.json.executableCount -ne 0) { $issues += "Phase7DExecutableCountNotZero" }

if ([string]$readiness.json.selectedInstrument -ne "EURGBP") { $issues += "ReadinessSelectedInstrumentNotEURGBP" }
if ([string]$readiness.json.slashSymbol -ne "EUR/GBP") { $issues += "ReadinessSlashSymbolNotEURGBP" }
if ([string]$readiness.json.securityId -ne "4003") { $issues += "ReadinessSecurityIdNot4003" }
if ([string]$readiness.json.securityIdSource -ne "8") { $issues += "ReadinessSecurityIdSourceNot8" }
if ([string]$readiness.json.environmentName -ne "Demo") { $issues += "ReadinessEnvironmentNotDemo" }
if ([string]$readiness.json.venueProfileName -ne "DemoLondon") { $issues += "ReadinessVenueNotDemoLondon" }
if ([string]$readiness.json.requestMode -ne "SnapshotPlusUpdates") { $issues += "ReadinessRequestModeInvalid" }
if ([string]$readiness.json.symbolEncodingMode -ne "SecurityIdOnly") { $issues += "ReadinessEncodingInvalid" }
if ([int]$readiness.json.marketDepth -ne 1) { $issues += "ReadinessMarketDepthInvalid" }
if ([string]$readiness.json.finalDecision -ne "PASS") { $issues += "ReadinessDecisionNotPASS" }
if ([string]$readiness.json.previousInstrument -ne "GBPUSD" -or [string]$readiness.json.previousInstrumentClosureDecision -ne "PASS" -or [string]$readiness.json.previousDecision -ne "ProceedToEurgbpPlanning") { $issues += "ReadinessPreviousChainInvalid" }
if (-not [bool]$readiness.json.oneInstrumentAtATime -or [bool]$readiness.json.batchExecutionAllowed) { $issues += "ReadinessSingleInstrumentRuleInvalid" }
if ([bool]$readiness.json.canRunExternalSnapshot -or [bool]$readiness.json.isApprovedForExternalRun -or [bool]$readiness.json.eligibleForManualSnapshotAttempt) { $issues += "ReadinessRunFlagTrue" }

if ([string]$checklist.json.symbol -ne "EURGBP") { $issues += "ChecklistSymbolNotEURGBP" }
if ([string]$checklist.json.slashSymbol -ne "EUR/GBP") { $issues += "ChecklistSlashSymbolNotEURGBP" }
if ([string]$checklist.json.planningSecurityId -ne "4003") { $issues += "ChecklistSecurityIdNot4003" }
if ([string]$checklist.json.securityIdSource -ne "8") { $issues += "ChecklistSecurityIdSourceNot8" }
if ([string]$checklist.json.requestMode -ne "SnapshotPlusUpdates") { $issues += "ChecklistRequestModeInvalid" }
if ([string]$checklist.json.symbolEncodingMode -ne "SecurityIdOnly") { $issues += "ChecklistEncodingInvalid" }
if ([int]$checklist.json.marketDepth -ne 1) { $issues += "ChecklistMarketDepthInvalid" }
if ([string]$checklist.json.decision -ne "PASS") { $issues += "ChecklistDecisionNotPASS" }
if ([string]$checklist.json.eurgbpReadinessDecision -ne "PASS") { $issues += "ChecklistReadinessDecisionNotPASS" }
if ([string]$checklist.json.previousInstrument -ne "GBPUSD" -or [string]$checklist.json.previousInstrumentClosureDecision -ne "PASS" -or [string]$checklist.json.previousDecision -ne "ProceedToEurgbpPlanning") { $issues += "ChecklistPreviousChainInvalid" }
if (-not [bool]$checklist.json.oneInstrumentAtATime -or [bool]$checklist.json.batchExecutionAllowed) { $issues += "ChecklistSingleInstrumentRuleInvalid" }
if ([bool]$checklist.json.externalRunAuthorized -or [bool]$checklist.json.canRunExternalSnapshot -or [bool]$checklist.json.eligibleForManualSnapshotAttempt -or [bool]$checklist.json.isApprovedForExternalRun) { $issues += "ChecklistRunFlagTrue" }
if ([bool]$checklist.json.schedulerOrPolling -or [bool]$checklist.json.runtimeShadowReplaySubmit -or [bool]$checklist.json.orderSubmission -or [bool]$checklist.json.tradingMutation) { $issues += "ChecklistRuntimeFlagTrue" }

if ($issues.Count -gt 0) { Fail ("EURGBP final pre-run source validation failed: " + ($issues -join ", ")) }

$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$gate = [ordered]@{
    gateId = "lmax-readonly-eurgbp-final-prerun-gate-$stamp"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    requestedByOperatorId = $RequestedByOperatorId
    reason = $Reason
    symbol = "EURGBP"
    slashSymbol = "EUR/GBP"
    planningSecurityId = "4003"
    securityIdSource = "8"
    environmentName = "Demo"
    venueProfileName = "DemoLondon"
    requestMode = "SnapshotPlusUpdates"
    symbolEncodingMode = "SecurityIdOnly"
    marketDepth = 1
    sourcePhase7DDecisionPath = $phase7d.path
    sourceEurgbpReadinessPath = $readiness.path
    sourceExecutionChecklistPath = $checklist.path
    sourceEurgbpReadinessDecision = "PASS"
    sourceExecutionChecklistDecision = "PASS"
    previousInstrument = "GBPUSD"
    previousInstrumentClosureDecision = "PASS"
    previousDecision = "ProceedToEurgbpPlanning"
    oneInstrumentAtATime = $true
    batchExecutionAllowed = $false
    externalRunAuthorized = $false
    canRunExternalSnapshot = $false
    eligibleForManualSnapshotAttempt = $false
    isApprovedForExternalRun = $false
    schedulerOrPolling = $false
    runtimeShadowReplaySubmit = $false
    orderSubmission = $false
    tradingMutation = $false
    gatewayRegistration = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    noSensitiveContent = $true
    finalDecision = "PASS"
}

$json = $gate | ConvertTo-Json -Depth 12
if ($json -match $sensitivePattern) { Fail "Generated final pre-run gate contains credential-shaped or raw FIX content." }
if ($json -match $authorizationPattern) { Fail "Generated final pre-run gate contains forbidden runtime/order/authorization wording." }

if ($WhatIfPreview.IsPresent) {
    $json
    Write-Host "FinalDecision: PASS"
    Write-Host "WhatIfPreview: no files written"
    exit 0
}

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$jsonPath = Join-Path $outDir "$($gate.gateId).json"
$mdPath = Join-Path $outDir "$($gate.gateId).md"
if (((Test-Path -LiteralPath $jsonPath) -or (Test-Path -LiteralPath $mdPath)) -and -not $Force.IsPresent) { Fail "Output already exists for stamp $stamp." }

$json | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$md = @"
# Phase 7G2 EURGBP Final Pre-Run Gate

GateId: $($gate.gateId)

Decision: PASS

This is a final pre-run consistency gate only. It does not authorize execution and does not run EURGBP.

## Instrument

- Symbol: EURGBP
- Slash symbol: EUR/GBP
- SecurityID: 4003
- SecurityIDSource: 8
- Environment: Demo
- VenueProfile: DemoLondon
- RequestMode: SnapshotPlusUpdates
- SymbolEncodingMode: SecurityIdOnly
- MarketDepth: 1

## Source Chain

- Phase 7D decision: ProceedToEurgbpPlanning
- Previous instrument: GBPUSD
- Previous GBPUSD closure: PASS
- EURGBP readiness: PASS
- EURGBP execution checklist: PASS

## Safety

- externalRunAuthorized=false
- canRunExternalSnapshot=false
- eligibleForManualSnapshotAttempt=false
- IsApprovedForExternalRun=false
- schedulerOrPolling=false
- runtimeShadowReplaySubmit=false
- orderSubmission=false
- tradingMutation=false
- gatewayRegistration=false
- batchExecutionAllowed=false
- oneInstrumentAtATime=true
- API/Worker FakeLmaxGateway only
"@
$md | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host "FinalDecision: PASS"
Write-Host "SelectedInstrument: EURGBP / EUR/GBP"
Write-Host "PlanningSecurityId: 4003"
Write-Host "CanRunExternalSnapshot: false"
Write-Host "IsApprovedForExternalRun: false"
Write-Host "EligibleForManualSnapshotAttempt: false"
Write-Host "FinalPreRunGate: $jsonPath"
Write-Host "FinalPreRunGateSummary: $mdPath"

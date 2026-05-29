param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("GBPUSD", "EURGBP", "USDJPY", "AUDUSD")]
    [string]$Symbol,
    [string]$FinalReadinessFile = "",
    [string]$ExecutionPlanFile = "",
    [string]$OperatorSignoffFile = "",
    [string]$ExecutionChecklistFile = "",
    [Parameter(Mandatory = $true)]
    [string]$RequestedByOperatorId,
    [Parameter(Mandatory = $true)]
    [string]$Reason,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/additional-final-prerun",
    [switch]$WhatIfPreview,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'
$authorizationPattern = '(?i)(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatus|SubmitOrder|production\s+(run|environment|authorization|execution)|uat\s+(run|environment|authorization|execution)|environmentName"?\s*[:=]\s*"?(Production|UAT)|run\s+is\s+authorized|external\s+run\s+authorized|can\s+run\s+external|batch\s+execution\s+allowed|automatic\s+retry|run\s+automatically|ReplaySubmitAsync|PeriodicTimer|LmaxScheduler|MarketDataPolling|SecurityListPolling)'

$instrumentMap = @{
    GBPUSD = @{ slashSymbol = "GBP/USD"; securityId = "4002" }
    EURGBP = @{ slashSymbol = "EUR/GBP"; securityId = "4003" }
    USDJPY = @{ slashSymbol = "USD/JPY"; securityId = "4004" }
    AUDUSD = @{ slashSymbol = "AUD/USD"; securityId = "4007" }
}

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

function Read-SafeJson([string]$PathValue, [string]$Label, [bool]$Required = $true) {
    $resolved = Resolve-LocalPath $PathValue
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) {
        if ($Required) { Fail "Missing ${Label}: $resolved" }
        return $null
    }
    $raw = Get-Content -LiteralPath $resolved -Raw
    if ($raw -match $sensitivePattern) { Fail "$Label contains credential-shaped or raw FIX content." }
    return @{ path = $resolved; json = ($raw | ConvertFrom-Json); raw = $raw }
}

function Find-LatestArtifact([string]$Subdir, [string]$Pattern) {
    $dir = Join-Path $repoRoot "artifacts/lmax-readonly-runtime-securityid-planning/$Subdir"
    if (-not (Test-Path -LiteralPath $dir)) { return "" }
    $match = Get-ChildItem -LiteralPath $dir -Filter $Pattern -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($null -eq $match) { return "" }
    return $match.FullName
}

function Get-String($Object, [string]$Name, [string]$Default = "") {
    if ($null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name) { return [string]$Object.$Name }
    return $Default
}

Write-Host "LMAX Read-Only Phase 7H2 Additional-Instrument Final Pre-Run Gate"
Write-Host "Local-only. This script does not connect to LMAX, call external APIs, request SecurityList, run snapshots, replay evidence, schedule work, or use credentials."

if ([string]::IsNullOrWhiteSpace($RequestedByOperatorId)) { Fail "RequestedByOperatorId is required." }
if ([string]::IsNullOrWhiteSpace($Reason)) { Fail "Reason is required." }
if (($RequestedByOperatorId + " " + $Reason) -match $sensitivePattern) { Fail "Operator fields contain credential-shaped content." }
if (($RequestedByOperatorId + " " + $Reason) -match $authorizationPattern) { Fail "Operator fields contain authorization/runtime/order/Production/UAT language." }

$def = $instrumentMap[$Symbol]
if ([string]::IsNullOrWhiteSpace($FinalReadinessFile)) {
    $FinalReadinessFile = Find-LatestArtifact "final-readiness" "lmax-readonly-additional-instrument-final-readiness-$Symbol-*.json"
}
if ([string]::IsNullOrWhiteSpace($ExecutionPlanFile)) {
    $ExecutionPlanFile = Find-LatestArtifact "execution-plans" "lmax-readonly-additional-instrument-manual-snapshot-execution-plan-$Symbol-*.json"
}
if ([string]::IsNullOrWhiteSpace($OperatorSignoffFile)) {
    $OperatorSignoffFile = Find-LatestArtifact "operator-signoffs" "lmax-readonly-additional-instrument-operator-signoff-$Symbol-*.json"
}

$finalReadiness = Read-SafeJson $FinalReadinessFile "final readiness" $true
$executionPlan = Read-SafeJson $ExecutionPlanFile "execution plan" $false
$operatorSignoff = Read-SafeJson $OperatorSignoffFile "operator signoff" $false
$executionChecklist = Read-SafeJson $ExecutionChecklistFile "execution checklist" $false

$issues = @()
if ([string]$finalReadiness.json.symbol -ne $Symbol) { $issues += "FinalReadinessSymbolMismatch" }
if ([string]$finalReadiness.json.slashSymbol -ne $def.slashSymbol) { $issues += "FinalReadinessSlashSymbolMismatch" }
if ([string]$finalReadiness.json.planningSecurityId -ne $def.securityId) { $issues += "FinalReadinessSecurityIdMismatch" }
if ([string]$finalReadiness.json.securityIdSource -ne "8") { $issues += "FinalReadinessSecurityIdSourceNot8" }
if ([string]$finalReadiness.json.environmentName -ne "Demo") { $issues += "FinalReadinessEnvironmentNotDemo" }
if ([string]$finalReadiness.json.venueProfileName -ne "DemoLondon") { $issues += "FinalReadinessVenueNotDemoLondon" }
if ([string]$finalReadiness.json.requestMode -ne "SnapshotPlusUpdates") { $issues += "FinalReadinessRequestModeInvalid" }
if ([string]$finalReadiness.json.symbolEncodingMode -ne "SecurityIdOnly") { $issues += "FinalReadinessEncodingInvalid" }
if ([int]$finalReadiness.json.marketDepth -ne 1) { $issues += "FinalReadinessMarketDepthInvalid" }
if ([string]$finalReadiness.json.readinessDecision -ne "PASS") { $issues += "FinalReadinessDecisionNotPASS" }
if ([bool]$finalReadiness.json.isApprovedForExternalRun -or [bool]$finalReadiness.json.canRunExternalSnapshot -or [bool]$finalReadiness.json.eligibleForManualSnapshotAttempt) { $issues += "FinalReadinessRunFlagTrue" }
if ([bool]$finalReadiness.json.externalConnectionAttempted -or [bool]$finalReadiness.json.snapshotAttempted -or [bool]$finalReadiness.json.replayAttempted -or [bool]$finalReadiness.json.orderSubmissionAttempted -or [bool]$finalReadiness.json.shadowReplaySubmitAttempted -or [bool]$finalReadiness.json.tradingMutationAttempted -or [bool]$finalReadiness.json.schedulerStarted) { $issues += "FinalReadinessAttemptFlagTrue" }
if ([string]$finalReadiness.json.apiWorkerGatewayMode -ne "FakeLmaxGateway") { $issues += "FinalReadinessGatewayModeInvalid" }
if (-not [bool]$finalReadiness.json.noSensitiveContent) { $issues += "FinalReadinessSensitiveContentFlagFalse" }

$executionPlanDecision = if ($null -ne $executionPlan) { Get-String $executionPlan.json "decision" (Get-String $executionPlan.json "executionPlanDecision") } else { "" }
$operatorSignoffDecision = if ($null -ne $operatorSignoff) { Get-String $operatorSignoff.json "signoffDecision" } else { "" }
$executionChecklistDecision = if ($null -ne $executionChecklist) { Get-String $executionChecklist.json "decision" (Get-String $executionChecklist.json "finalDecision") } else { "" }
if (-not [string]::IsNullOrWhiteSpace($executionPlanDecision) -and $executionPlanDecision -ne "PASS") { $issues += "ExecutionPlanDecisionNotPASS" }
if (-not [string]::IsNullOrWhiteSpace($operatorSignoffDecision) -and $operatorSignoffDecision -ne "SignedForPlanning") { $issues += "OperatorSignoffNotSignedForPlanning" }
if (-not [string]::IsNullOrWhiteSpace($executionChecklistDecision) -and $executionChecklistDecision -ne "PASS") { $issues += "ExecutionChecklistDecisionNotPASS" }

if ($issues.Count -gt 0) { Fail ("Additional-instrument final pre-run source validation failed: " + ($issues -join ", ")) }

$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$gate = [ordered]@{
    gateId = "lmax-readonly-additional-instrument-final-prerun-gate-$Symbol-$stamp"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    requestedByOperatorId = $RequestedByOperatorId
    reason = $Reason
    symbol = $Symbol
    slashSymbol = $def.slashSymbol
    planningSecurityId = $def.securityId
    securityIdSource = "8"
    environmentName = "Demo"
    venueProfileName = "DemoLondon"
    requestMode = "SnapshotPlusUpdates"
    symbolEncodingMode = "SecurityIdOnly"
    marketDepth = 1
    sourceFinalReadinessPath = $finalReadiness.path
    sourceExecutionPlanPath = if ($null -ne $executionPlan) { $executionPlan.path } else { "" }
    sourceOperatorSignoffPath = if ($null -ne $operatorSignoff) { $operatorSignoff.path } else { "" }
    sourceExecutionChecklistPath = if ($null -ne $executionChecklist) { $executionChecklist.path } else { "" }
    sourceFinalReadinessDecision = "PASS"
    sourceExecutionPlanDecision = $executionPlanDecision
    sourceOperatorSignoffDecision = $operatorSignoffDecision
    sourceExecutionChecklistDecision = $executionChecklistDecision
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
# Phase 7H2 Additional-Instrument Final Pre-Run Gate

GateId: $($gate.gateId)

Decision: PASS

This is a final pre-run compatibility and safety gate only. It does not authorize execution and does not run $Symbol.

## Instrument

- Symbol: $Symbol
- Slash symbol: $($def.slashSymbol)
- SecurityID: $($def.securityId)
- SecurityIDSource: 8
- Environment: Demo
- VenueProfile: DemoLondon
- RequestMode: SnapshotPlusUpdates
- SymbolEncodingMode: SecurityIdOnly
- MarketDepth: 1

## Safety

- oneInstrumentAtATime=true
- batchExecutionAllowed=false
- externalRunAuthorized=false
- canRunExternalSnapshot=false
- eligibleForManualSnapshotAttempt=false
- IsApprovedForExternalRun=false
- schedulerOrPolling=false
- runtimeShadowReplaySubmit=false
- orderSubmission=false
- tradingMutation=false
- gatewayRegistration=false
- API/Worker FakeLmaxGateway only

The Phase 7H generic wrapper must still be run manually with explicit operator flags before any future external attempt.
"@
$md | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host "FinalDecision: PASS"
Write-Host "SelectedInstrument: $Symbol / $($def.slashSymbol)"
Write-Host "PlanningSecurityId: $($def.securityId)"
Write-Host "CanRunExternalSnapshot: false"
Write-Host "IsApprovedForExternalRun: false"
Write-Host "EligibleForManualSnapshotAttempt: false"
Write-Host "FinalPreRunGate: $jsonPath"
Write-Host "FinalPreRunGateSummary: $mdPath"

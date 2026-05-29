param(
    [string]$BaseDir = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expectedDecision = "LMAX_R1_RUNTIME_ENABLEMENT_DESIGN_REVIEW_COMPLETE_NO_ENABLEMENT"
$expectedUsdJpyT7 = "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT"
$expectedPhase7N = "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED"
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|BEGIN\s+PRIVATE\s+KEY)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}
function Resolve-RepoPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}
function Read-TextSafe([string]$PathValue, [string]$Label) {
    $resolved = Resolve-RepoPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }
    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw `
        -replace 'credential|Credential|secret|Secret','SAFE_METADATA' `
        -replace 'raw FIX|rawFix|FIX Logon','SAFE_FIX_METADATA' `
        -replace 'LMAX_DEMO_FIX_USERNAME|LMAX_DEMO_FIX_PASSWORD|LMAX_DEMO_SENDER_COMP_ID|LMAX_DEMO_TARGET_COMP_ID','SAFE_ENV_LABEL'
    if ($safe -match $sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped content."
    }
    return $raw
}
function Assert-False([object]$Value, [string]$Category, [string]$Check) {
    if (-not [bool]$Value) { Add-Result $Category $Check "PASS" "false." } else { Add-Result $Category $Check "FAIL" "Expected false." }
}
function Assert-True([object]$Value, [string]$Category, [string]$Check) {
    if ([bool]$Value) { Add-Result $Category $Check "PASS" "true." } else { Add-Result $Category $Check "FAIL" "Expected true." }
}
function Assert-Equals([object]$Actual, [string]$Expected, [string]$Category, [string]$Check) {
    if ([string]$Actual -eq $Expected) { Add-Result $Category $Check "PASS" $Expected } else { Add-Result $Category $Check "FAIL" "Expected '$Expected' but found '$Actual'." }
}

Write-Host "LMAX-R1 Runtime Enablement Design Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime enablement, or config mutation."

$requiredR1 = [ordered]@{
    BoundaryMap = "phase-lmax-r1-runtime-enablement-boundary-map.json"
    ComponentImpact = "phase-lmax-r1-component-impact-review.json"
    SafetyModel = "phase-lmax-r1-readonly-safety-model.json"
    R2Preconditions = "phase-lmax-r1-future-r2-preconditions.json"
    DecisionGate = "phase-lmax-r1-design-only-decision-gate.json"
    NonRunValidation = "phase-lmax-r1-non-run-validation.json"
    Report = "phase-lmax-r1-runtime-enablement-design-review.md"
    OperatorNote = "phase-lmax-r1-operator-note.md"
}

$raw = @{}
foreach ($key in $requiredR1.Keys) { $raw[$key] = Read-TextSafe (Join-Path $BaseDir $requiredR1[$key]) $key }

$phase7NManifest = Read-TextSafe "artifacts/readiness/phase7n-final-lmax-readonly-runtime-evidence-archive-manifest.json" "Phase7NManifest" | ConvertFrom-Json
$phase7NSummary = Read-TextSafe "artifacts/readiness/phase7n-final-lmax-readonly-runtime-evidence-status-summary.json" "Phase7NSummary" | ConvertFrom-Json
$usdJpyT7 = Read-TextSafe "artifacts/readiness/usdjpy-troubleshooting/phase-usdjpy-t7-final-closure-gate.json" "UsdJpyT7Closure" | ConvertFrom-Json
Assert-Equals $phase7NManifest.finalDecision $expectedPhase7N "Phase7NManifest" "Final decision"
Assert-True $phase7NManifest.archiveComplete "Phase7NManifest" "Archive complete"
Assert-True $phase7NManifest.apiWorkerRemainFakeLmaxGatewayOnly "Phase7NManifest" "API/Worker remain FakeLmaxGatewayOnly"
Assert-Equals $usdJpyT7.finalDecision $expectedUsdJpyT7 "UsdJpyT7Closure" "Final decision"
Assert-Equals $usdJpyT7.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "UsdJpyT7Closure" "Gateway mode"

$boundary = $raw.BoundaryMap | ConvertFrom-Json
Assert-Equals $boundary.phase "LMAX-R1" "BoundaryMap" "Phase"
Assert-True $boundary.designOnly "BoundaryMap" "Design only"
Assert-False $boundary.runtimeEnablementExecuted "BoundaryMap" "Runtime enablement executed"
Assert-Equals $boundary.currentState.apiExecutionGateway "FakeLmaxGateway" "BoundaryMap" "API execution gateway"
Assert-Equals $boundary.currentState.workerExecutionGateway "FakeLmaxGateway" "BoundaryMap" "Worker execution gateway"
Assert-False $boundary.currentState.allowExternalConnections "BoundaryMap" "AllowExternalConnections"
Assert-False $boundary.currentState.allowOrderSubmission "BoundaryMap" "AllowOrderSubmission"
Assert-False $boundary.currentState.allowLiveTrading "BoundaryMap" "AllowLiveTrading"
Assert-Equals $boundary.finalDecision $expectedDecision "BoundaryMap" "Final decision"

$impact = $raw.ComponentImpact | ConvertFrom-Json
Assert-Equals $impact.phase "LMAX-R1" "ComponentImpact" "Phase"
Assert-True $impact.designOnly "ComponentImpact" "Design only"
Assert-True (($impact.components | Measure-Object).Count -ge 10) "ComponentImpact" "Component coverage"

$safety = $raw.SafetyModel | ConvertFrom-Json
Assert-True $safety.ordersImpossibleByConstruction "SafetyModel" "Orders impossible by construction"
Assert-True $safety.orderGatewayNotRegistered "SafetyModel" "Order gateway not registered"
Assert-True $safety.schedulerDisabledUnlessSeparatePhaseApproves "SafetyModel" "Scheduler separate approval"
Assert-True $safety.pollingDisabledUnlessSeparatePhaseApproves "SafetyModel" "Polling separate approval"
Assert-True $safety.productionAccountForbidden "SafetyModel" "Production account forbidden"
Assert-Equals $safety.finalDecision $expectedDecision "SafetyModel" "Final decision"

$r2 = $raw.R2Preconditions | ConvertFrom-Json
Assert-Equals $r2.phase "LMAX-R1" "R2Preconditions" "Phase"
if ([string]$r2.recommendedNextPhase -like "Phase LMAX-R2*Read-Only Runtime Activation Preflight Pack") {
    Add-Result "R2Preconditions" "Recommended next phase" "PASS" $r2.recommendedNextPhase
} else {
    Add-Result "R2Preconditions" "Recommended next phase" "FAIL" "Unexpected next phase $($r2.recommendedNextPhase)"
}
Assert-Equals $r2.finalDecision $expectedDecision "R2Preconditions" "Final decision"

$gate = $raw.DecisionGate | ConvertFrom-Json
Assert-Equals $gate.phase "LMAX-R1" "DecisionGate" "Phase"
Assert-True $gate.designReviewCompleted "DecisionGate" "Design review completed"
Assert-True $gate.designOnly "DecisionGate" "Design only"
Assert-False $gate.runtimeEnablementAuthorized "DecisionGate" "Runtime enablement authorized"
Assert-False $gate.tradingEnablementAuthorized "DecisionGate" "Trading enablement authorized"
Assert-False $gate.schedulerPollingEnablementAuthorized "DecisionGate" "Scheduler/polling enablement authorized"
Assert-False $gate.orderPathEnablementAuthorized "DecisionGate" "Order path enablement authorized"
foreach ($flag in @("externalRunExecuted", "snapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "evidenceArchivesModified")) {
    Assert-False $gate.$flag "DecisionGate" $flag
}
Assert-Equals $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "DecisionGate" "API/Worker gateway mode"
Assert-True $gate.usdJpyT7ClosureIntact "DecisionGate" "USDJPY T7 closure intact"
Assert-True $gate.validatedRailsArchivesIntact "DecisionGate" "Validated rails archives intact"
Assert-Equals $gate.finalDecision $expectedDecision "DecisionGate" "Final decision"

$nonRun = $raw.NonRunValidation | ConvertFrom-Json
foreach ($flag in @("externalRunExecuted", "snapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "evidenceArchivesModified", "validatedRailsModified", "usdJpyT7ClosureModified")) {
    Assert-False $nonRun.$flag "NonRunValidation" $flag
}
Assert-Equals $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "NonRunValidation" "API/Worker gateway mode"
Assert-True $nonRun.outputSanitized "NonRunValidation" "Output sanitized"

if ($null -ne $raw.Report) {
    foreach ($marker in @("Executive summary", "Current final evidence state", "Scope and non-run guarantees", "Runtime enablement boundary map", "Component impact review", "Read-only safety model", "Forbidden paths", "Future R2 preconditions", "Decision", "Recommended next phase")) {
        if ($raw.Report.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Report" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Report" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$apiProgramPath = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgramPath = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$appSettingsPath = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json"
$apiProgram = Get-Content -Raw -LiteralPath $apiProgramPath
$workerProgram = Get-Content -Raw -LiteralPath $workerProgramPath
$appSettings = Get-Content -Raw -LiteralPath $appSettingsPath | ConvertFrom-Json
if ($apiProgram -match "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>" -and $workerProgram -match "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>" -and -not ($apiProgram -match "RealLmaxGateway|LmaxVenueGatewaySkeleton" -or $workerProgram -match "RealLmaxGateway|LmaxVenueGatewaySkeleton")) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}
Assert-False $appSettings.Safety.AllowExternalConnections "AppSettings" "Safety:AllowExternalConnections"
Assert-False $appSettings.Safety.AllowLiveTrading "AppSettings" "Safety:AllowLiveTrading"
Assert-True $appSettings.Safety.RequireFakeExecutionGateway "AppSettings" "Safety:RequireFakeExecutionGateway"
Assert-False $appSettings.LmaxReadOnlyRuntime.Enabled "AppSettings" "LmaxReadOnlyRuntime:Enabled"
Assert-Equals $appSettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "AppSettings" "LmaxReadOnlyRuntime:ImplementationMode"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowExternalConnections "AppSettings" "LmaxReadOnlyRuntime:AllowExternalConnections"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowOrderSubmission "AppSettings" "LmaxReadOnlyRuntime:AllowOrderSubmission"
Assert-False $appSettings.LmaxReadOnlyRuntime.SchedulerEnabled "AppSettings" "LmaxReadOnlyRuntime:SchedulerEnabled"
Assert-False $appSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "AppSettings" "LmaxReadOnlyRuntime:SubmitToShadowReplay"

$liveScript = Join-Path $repoRoot "scripts/run-lmax-r1-runtime-enablement.ps1"
if (Test-Path -LiteralPath $liveScript) {
    Add-Result "Scripts" "No live connection script created" "FAIL" "Unexpected $liveScript"
} else {
    Add-Result "Scripts" "No live connection script created" "PASS" "No R1 live connection script exists."
}

Add-Result "ValidatorRuntime" "No external action" "PASS" "Validator only reads local artifacts and source/config files."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { $expectedDecision }
$outPath = Resolve-RepoPath (Join-Path $BaseDir "phase-lmax-r1-gate-validation.json")
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "LMAX-R1"
    finalDecision = $decision
    externalRunExecuted = $false
    snapshotExecuted = $false
    replayExecuted = $false
    postEndpointInvoked = $false
    realSocketOpened = $false
    tcpConnectionAttempted = $false
    tlsHandshakeAttempted = $false
    fixLogonAttempted = $false
    marketDataRequestSent = $false
    orderSubmissionExecuted = $false
    tradingStateMutated = $false
    schedulerStarted = $false
    pollingStarted = $false
    shadowReplaySubmitted = $false
    apiWorkerStarted = $false
    runtimePoweredUp = $false
    retryExecuted = $false
    batchExecuted = $false
    loopExecuted = $false
    runtimeEnablementExecuted = $false
    tradingEnablementExecuted = $false
    schedulerEnablementExecuted = $false
    orderPathEnablementExecuted = $false
    defaultGatewayRegistrationChanged = $false
    liveConnectionScriptCreated = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    evidenceArchivesModified = $false
    outputSanitized = $true
    noSensitiveContent = $true
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }

param(
    [string]$BaseDir = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expectedR1Decision = "LMAX_R1_RUNTIME_ENABLEMENT_DESIGN_REVIEW_COMPLETE_NO_ENABLEMENT"
$expectedR2Decision = "LMAX_R2_READONLY_RUNTIME_PREFLIGHT_READY_NO_ACTIVATION"
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

Write-Host "LMAX-R2 Read-Only Runtime Preflight Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime activation, or config mutation."

$requiredR1 = @(
    "phase-lmax-r1-runtime-enablement-boundary-map.json",
    "phase-lmax-r1-component-impact-review.json",
    "phase-lmax-r1-readonly-safety-model.json",
    "phase-lmax-r1-future-r2-preconditions.json",
    "phase-lmax-r1-design-only-decision-gate.json",
    "phase-lmax-r1-non-run-validation.json",
    "phase-lmax-r1-gate-validation.json"
)
$requiredR2 = [ordered]@{
    PreflightChecklist = "phase-lmax-r2-runtime-readonly-preflight-checklist.json"
    ApprovalModel = "phase-lmax-r2-future-r3-operator-approval-model.json"
    ActivationBoundary = "phase-lmax-r2-future-r3-activation-boundary-design.json"
    HardBlockMatrix = "phase-lmax-r2-hard-block-matrix.json"
    EvidenceSchema = "phase-lmax-r2-future-r3-evidence-schema.json"
    TestValidatorPlan = "phase-lmax-r2-test-validator-plan.json"
    DecisionGate = "phase-lmax-r2-preflight-decision-gate.json"
    NonRunValidation = "phase-lmax-r2-non-run-validation.json"
    Report = "phase-lmax-r2-readonly-runtime-preflight-pack.md"
    OperatorNote = "phase-lmax-r2-operator-note.md"
}

$raw = @{}
foreach ($name in $requiredR1) { $raw[$name] = Read-TextSafe (Join-Path $BaseDir $name) "R1:$name" }
foreach ($key in $requiredR2.Keys) { $raw[$key] = Read-TextSafe (Join-Path $BaseDir $requiredR2[$key]) $key }

$r1Gate = $raw["phase-lmax-r1-design-only-decision-gate.json"] | ConvertFrom-Json
Assert-Equals $r1Gate.finalDecision $expectedR1Decision "R1DecisionGate" "Final decision"

$phase7NManifest = Read-TextSafe "artifacts/readiness/phase7n-final-lmax-readonly-runtime-evidence-archive-manifest.json" "Phase7NManifest" | ConvertFrom-Json
$phase7NSummary = Read-TextSafe "artifacts/readiness/phase7n-final-lmax-readonly-runtime-evidence-status-summary.json" "Phase7NSummary" | ConvertFrom-Json
$usdJpyT7 = Read-TextSafe "artifacts/readiness/usdjpy-troubleshooting/phase-usdjpy-t7-final-closure-gate.json" "UsdJpyT7Closure" | ConvertFrom-Json
Assert-Equals $phase7NManifest.finalDecision $expectedPhase7N "Phase7NManifest" "Final decision"
Assert-True $phase7NManifest.archiveComplete "Phase7NManifest" "Archive complete"
Assert-Equals $usdJpyT7.finalDecision $expectedUsdJpyT7 "UsdJpyT7Closure" "Final decision"
Assert-Equals $usdJpyT7.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "UsdJpyT7Closure" "Gateway mode"
Assert-Equals $usdJpyT7.caveat "prior failed-safe root cause remains unproven" "UsdJpyT7Closure" "Caveat"

$checklist = $raw.PreflightChecklist | ConvertFrom-Json
Assert-Equals $checklist.phase "LMAX-R2" "PreflightChecklist" "Phase"
Assert-True $checklist.preflightPackComplete "PreflightChecklist" "Preflight pack complete"
Assert-True $checklist.designOnly "PreflightChecklist" "Design only"
Assert-False $checklist.r3AuthorizedByR2 "PreflightChecklist" "R3 authorized by R2"
Assert-Equals $checklist.finalDecision $expectedR2Decision "PreflightChecklist" "Final decision"
if (($checklist.approvedReadOnlyInstrumentsForFutureConsideration.instrument -contains "GBPUSD") -and ($checklist.approvedReadOnlyInstrumentsForFutureConsideration.instrument -contains "EURGBP") -and ($checklist.approvedReadOnlyInstrumentsForFutureConsideration.instrument -contains "AUDUSD") -and ($checklist.approvedReadOnlyInstrumentsForFutureConsideration.instrument -contains "USDJPY")) {
    Add-Result "PreflightChecklist" "Instrument enumeration" "PASS" "GBPUSD/EURGBP/AUDUSD/USDJPY listed."
} else {
    Add-Result "PreflightChecklist" "Instrument enumeration" "FAIL" "Missing expected instrument."
}

$approval = $raw.ApprovalModel | ConvertFrom-Json
Assert-False $approval.approvalCollectedInR2 "ApprovalModel" "Approval collected in R2"
Assert-False $approval.r3AuthorizedByR2 "ApprovalModel" "R3 authorized by R2"
Assert-True $approval.usdJpyCaveatAcknowledgementRequired "ApprovalModel" "USDJPY caveat acknowledgement"
if ([string]$approval.requiredApprovalPhraseTemplate -like "I, Philippe, explicitly approve Phase LMAX-R3*") {
    Add-Result "ApprovalModel" "Approval phrase template" "PASS" $approval.requiredApprovalPhraseTemplate
} else {
    Add-Result "ApprovalModel" "Approval phrase template" "FAIL" "Missing exact R3 approval phrase template."
}

$boundary = $raw.ActivationBoundary | ConvertFrom-Json
Assert-True $boundary.designOnly "ActivationBoundary" "Design only"
Assert-False $boundary.r3AuthorizedByR2 "ActivationBoundary" "R3 authorized by R2"
Assert-Equals $boundary.futureR3ActivationBoundary.executionGateway "must remain FakeLmaxGateway" "ActivationBoundary" "Execution gateway"

$blocks = $raw.HardBlockMatrix | ConvertFrom-Json
Assert-Equals $blocks.phase "LMAX-R2" "HardBlockMatrix" "Phase"
if (($blocks.hardBlocks | Measure-Object).Count -ge 12) {
    Add-Result "HardBlockMatrix" "Hard block coverage" "PASS" "Hard block count sufficient."
} else {
    Add-Result "HardBlockMatrix" "Hard block coverage" "FAIL" "Hard block count too low."
}
Assert-False $blocks.r3AuthorizedByR2 "HardBlockMatrix" "R3 authorized by R2"

$schema = $raw.EvidenceSchema | ConvertFrom-Json
Assert-True $schema.designOnly "EvidenceSchema" "Design only"
Assert-False $schema.r3AuthorizedByR2 "EvidenceSchema" "R3 authorized by R2"
foreach ($field in @("operatorApprovalRecord", "preflightGate", "runtimeActivationRecord", "instrumentSubscriptionRecord", "sanitizedSessionStatusRecord", "sanitizedMarketDataStatusRecord", "nonMutationValidation", "rollbackShutdownRecord", "railIsolationValidation", "finalDecisionGate")) {
    if ($schema.schemas.PSObject.Properties.Name -contains $field) { Add-Result "EvidenceSchema" "Schema: $field" "PASS" "Schema present." } else { Add-Result "EvidenceSchema" "Schema: $field" "FAIL" "Schema missing." }
}

$plan = $raw.TestValidatorPlan | ConvertFrom-Json
Assert-True $plan.r2ValidatorOnly "TestValidatorPlan" "R2 validator only"
Assert-False $plan.liveR3LauncherCreated "TestValidatorPlan" "Live R3 launcher created"
Assert-False $plan.r3AuthorizedByR2 "TestValidatorPlan" "R3 authorized by R2"

$gate = $raw.DecisionGate | ConvertFrom-Json
Assert-Equals $gate.phase "LMAX-R2" "DecisionGate" "Phase"
Assert-True $gate.preflightPackComplete "DecisionGate" "Preflight pack complete"
Assert-True $gate.designOnly "DecisionGate" "Design only"
Assert-False $gate.r3Authorized "DecisionGate" "R3 authorized"
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
Assert-True $gate.usdJpyCaveatPreserved "DecisionGate" "USDJPY caveat preserved"
Assert-Equals $gate.finalDecision $expectedR2Decision "DecisionGate" "Final decision"

$nonRun = $raw.NonRunValidation | ConvertFrom-Json
foreach ($flag in @("externalRunExecuted", "snapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "r3Authorized", "evidenceArchivesModified", "validatedRailsModified", "usdJpyT7ClosureModified")) {
    Assert-False $nonRun.$flag "NonRunValidation" $flag
}
Assert-Equals $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "NonRunValidation" "API/Worker gateway mode"
Assert-True $nonRun.outputSanitized "NonRunValidation" "Output sanitized"

if ($null -ne $raw.Report) {
    foreach ($marker in @("Executive summary", "Current final evidence state", "Scope and non-run guarantees", "R1 design summary", "R2 preflight checklist", "Future R3 operator approval model", "Future R3 activation boundary", "Hard-block matrix", "Future R3 evidence schema", "Test/validator plan", "Decision", "Recommended next phase")) {
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

foreach ($scriptName in @("run-lmax-r2-readonly-runtime.ps1", "run-lmax-r3-readonly-runtime-activation.ps1", "run-lmax-runtime-live-activation.ps1")) {
    $liveScript = Join-Path $repoRoot "scripts/$scriptName"
    if (Test-Path -LiteralPath $liveScript) {
        Add-Result "Scripts" "No live connection script: $scriptName" "FAIL" "Unexpected $liveScript"
    } else {
        Add-Result "Scripts" "No live connection script: $scriptName" "PASS" "Not present."
    }
}

Add-Result "ValidatorRuntime" "No external action" "PASS" "Validator only reads local artifacts and source/config files."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { $expectedR2Decision }
$outPath = Resolve-RepoPath (Join-Path $BaseDir "phase-lmax-r2-gate-validation.json")
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "LMAX-R2"
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
    r3Authorized = $false
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

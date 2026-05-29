param(
    [string]$BaseDir = "artifacts/readiness/usdjpy-troubleshooting"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expectedT1Decision = "USDJPY_REMAINS_PARKED_LOCAL_ONLY_DIAGNOSTIC_COMPLETE"
$expectedT2Decision = "USDJPY_REMAINS_PARKED_RETRY_PRECONDITIONS_DOCUMENTED"
$expectedT3Decision = "USDJPY_T4_MANUAL_RETRY_DESIGN_READY_BUT_NOT_AUTHORIZED"
$expectedT4Decision = "USDJPY_T4_PASS_SNAPSHOT_RECEIVED_SANITIZED"
$expectedT5Decision = "USDJPY_T5_SUCCESS_REVIEW_READY_FOR_PROMOTION_GATE"
$expectedCaveat = "prior failed-safe root cause remains unproven"
$allowedEligibility = @(
    "USDJPY_ELIGIBLE_FOR_CONTROLLED_PROMOTION_WITH_CAVEAT",
    "USDJPY_ELIGIBLE_FOR_ARCHIVE_ONLY_NOT_PROMOTION",
    "USDJPY_NOT_ELIGIBLE_ROOT_CAUSE_BLOCKER",
    "USDJPY_REQUIRES_ADDITIONAL_REVIEW_BEFORE_PROMOTION"
)
$allowedDecisions = @(
    "USDJPY_T6_CONTROLLED_PROMOTION_GATE_PASSED_WITH_CAVEAT",
    "USDJPY_T6_ARCHIVE_ONLY_NO_PROMOTION",
    "USDJPY_T6_NOT_ELIGIBLE_ROOT_CAUSE_BLOCKER",
    "USDJPY_T6_REQUIRES_ADDITIONAL_REVIEW"
)
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

Write-Host "USDJPY-T6 Controlled Success Promotion Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, runtime enablement, or promotion action."

$requiredPrior = @(
    "phase-usdjpy-t1-decision-gate.json",
    "phase-usdjpy-t2-decision-gate.json",
    "phase-usdjpy-t3-decision-gate.json",
    "phase-usdjpy-t4-decision-gate.json",
    "phase-usdjpy-t5-decision-gate.json",
    "phase-usdjpy-t5-gate-validation.json"
)
$requiredT6 = [ordered]@{
    ArchiveManifest = "phase-usdjpy-t6-controlled-success-archive-manifest.json"
    EligibilityReview = "phase-usdjpy-t6-promotion-eligibility-review.json"
    PromotionGate = "phase-usdjpy-t6-controlled-promotion-gate.json"
    RailTransitionPlan = "phase-usdjpy-t6-rail-transition-plan.json"
    NonRunValidation = "phase-usdjpy-t6-non-run-validation.json"
    Report = "phase-usdjpy-t6-controlled-success-archive-report.md"
    OperatorNote = "phase-usdjpy-t6-operator-note.md"
}

$raw = @{}
foreach ($name in $requiredPrior) { $raw[$name] = Read-TextSafe (Join-Path $BaseDir $name) "Prior:$name" }
foreach ($key in $requiredT6.Keys) { $raw[$key] = Read-TextSafe (Join-Path $BaseDir $requiredT6[$key]) $key }

$t1Gate = $raw["phase-usdjpy-t1-decision-gate.json"] | ConvertFrom-Json
$t2Gate = $raw["phase-usdjpy-t2-decision-gate.json"] | ConvertFrom-Json
$t3Gate = $raw["phase-usdjpy-t3-decision-gate.json"] | ConvertFrom-Json
$t4Gate = $raw["phase-usdjpy-t4-decision-gate.json"] | ConvertFrom-Json
$t5Gate = $raw["phase-usdjpy-t5-decision-gate.json"] | ConvertFrom-Json
Assert-Equals $t1Gate.finalDecision $expectedT1Decision "T1DecisionGate" "Final decision"
Assert-Equals $t2Gate.finalDecision $expectedT2Decision "T2DecisionGate" "Final decision"
Assert-Equals $t3Gate.finalDecision $expectedT3Decision "T3DecisionGate" "Final decision"
Assert-Equals $t4Gate.finalDecision $expectedT4Decision "T4DecisionGate" "Final decision"
Assert-Equals $t5Gate.finalDecision $expectedT5Decision "T5DecisionGate" "Final decision"

$runtimePath = Resolve-RepoPath "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260512-122928.json"
if (Test-Path -LiteralPath $runtimePath) {
    Add-Result "T4RuntimeArtifact" "File exists" "PASS" $runtimePath
} else {
    Add-Result "T4RuntimeArtifact" "File exists" "FAIL" "Missing $runtimePath"
}

$manifest = $raw.ArchiveManifest | ConvertFrom-Json
Assert-Equals $manifest.phase "USDJPY-T6" "ArchiveManifest" "Phase"
Assert-True $manifest.archiveComplete "ArchiveManifest" "Archive complete"
Assert-True $manifest.t4SuccessCanonicalForCurrentUsdJpyDemoReadOnlyMarketDataViability "ArchiveManifest" "T4 success canonical"
Assert-True $manifest.priorFailedSafeRootCauseRemainsUnproven "ArchiveManifest" "Prior failed-safe root cause unproven"
Assert-True $manifest.historicalFailureEvidencePreserved "ArchiveManifest" "Historical failure evidence preserved"
Assert-True $manifest.validatedRailsUntouched "ArchiveManifest" "Validated rails untouched"
Assert-Equals $manifest.historicalFailedSafeCaveat $expectedCaveat "ArchiveManifest" "Caveat"

$review = $raw.EligibilityReview | ConvertFrom-Json
Assert-Equals $review.phase "USDJPY-T6" "EligibilityReview" "Phase"
if ($allowedEligibility -contains [string]$review.eligibilityStatus) {
    Add-Result "EligibilityReview" "Allowed eligibility status" "PASS" $review.eligibilityStatus
} else {
    Add-Result "EligibilityReview" "Allowed eligibility status" "FAIL" "Unexpected eligibility $($review.eligibilityStatus)"
}
Assert-False $review.runtimeEnablement "EligibilityReview" "Runtime enablement"
Assert-False $review.tradingEnablement "EligibilityReview" "Trading enablement"
Assert-False $review.schedulerEnablement "EligibilityReview" "Scheduler enablement"
Assert-False $review.promotionExecuted "EligibilityReview" "Promotion executed"
Assert-Equals $review.caveat $expectedCaveat "EligibilityReview" "Caveat"

$gate = $raw.PromotionGate | ConvertFrom-Json
Assert-Equals $gate.phase "USDJPY-T6" "PromotionGate" "Phase"
Assert-True $gate.controlledPromotionGateCompleted "PromotionGate" "Gate completed"
Assert-Equals $gate.promotionType "documentation_readiness_promotion_only" "PromotionGate" "Promotion type"
Assert-Equals $gate.caveat $expectedCaveat "PromotionGate" "Caveat"
Assert-False $gate.runtimeEnablement "PromotionGate" "Runtime enablement"
Assert-False $gate.tradingEnablement "PromotionGate" "Trading enablement"
Assert-False $gate.schedulerEnablement "PromotionGate" "Scheduler enablement"
Assert-False $gate.externalActionExecuted "PromotionGate" "External action executed"
Assert-True $gate.validatedRailsUntouched "PromotionGate" "Validated rails untouched"
Assert-False $gate.runtimeBehaviorChangeAuthorized "PromotionGate" "Runtime behavior change authorized"
Assert-False $gate.usdJpyRuntimeEnablementAuthorized "PromotionGate" "USDJPY runtime enablement authorized"
Assert-False $gate.usdJpySchedulerEnablementAuthorized "PromotionGate" "USDJPY scheduler enablement authorized"
Assert-False $gate.usdJpyTradingEnablementAuthorized "PromotionGate" "USDJPY trading enablement authorized"
foreach ($flag in @("newSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "promotionExecuted", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "phaseT3ArtifactsModified", "phaseT4ArtifactsModified", "phaseT5ArtifactsModified", "nonUsdJpyExternalActionExecuted")) {
    Assert-False $gate.$flag "PromotionGate" $flag
}
Assert-Equals $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "PromotionGate" "API/Worker gateway mode"
Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "PromotionGate" "API/Worker remain FakeLmaxGatewayOnly"
if ($allowedEligibility -contains [string]$gate.promotionEligibilityStatus) {
    Add-Result "PromotionGate" "Allowed eligibility status" "PASS" $gate.promotionEligibilityStatus
} else {
    Add-Result "PromotionGate" "Allowed eligibility status" "FAIL" "Unexpected eligibility $($gate.promotionEligibilityStatus)"
}
if ($allowedDecisions -contains [string]$gate.finalDecision) {
    Add-Result "PromotionGate" "Allowed final decision" "PASS" $gate.finalDecision
} else {
    Add-Result "PromotionGate" "Allowed final decision" "FAIL" "Unexpected finalDecision $($gate.finalDecision)"
}
Assert-True $gate.noSensitiveContent "PromotionGate" "No sensitive content"

$plan = $raw.RailTransitionPlan | ConvertFrom-Json
Assert-Equals $plan.previousState "ParkedSeparateTroubleshootingRail" "RailTransitionPlan" "Previous state"
Assert-Equals $plan.newProposedState "ValidatedReadinessArchiveWithCaveat" "RailTransitionPlan" "New proposed state"
Assert-False $plan.runtimeEnablement "RailTransitionPlan" "Runtime enablement"
Assert-False $plan.tradingEnablement "RailTransitionPlan" "Trading enablement"
Assert-False $plan.schedulerEnablement "RailTransitionPlan" "Scheduler enablement"

$nonRun = $raw.NonRunValidation | ConvertFrom-Json
foreach ($flag in @("externalRunExecuted", "newSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "promotionExecuted", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "phaseT3ArtifactsModified", "phaseT4ArtifactsModified", "phaseT5ArtifactsModified", "nonUsdJpyExternalActionExecuted")) {
    Assert-False $nonRun.$flag "NonRunValidation" $flag
}
Assert-Equals $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "NonRunValidation" "API/Worker gateway mode"
Assert-True $nonRun.outputSanitized "NonRunValidation" "Output sanitized"

if ($null -ne $raw.Report) {
    foreach ($marker in @("Executive summary", "Scope and non-run guarantees", "Evidence chain T1 through T5", "T4/T5 success basis", "Historical failed-safe caveat", "Controlled success archive manifest summary", "Promotion eligibility review", "Controlled promotion gate", "Rail transition plan", "What is allowed after T6", "What remains forbidden after T6", "Decision", "Recommended next phase")) {
        if ($raw.Report.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Report" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Report" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupText = (@($apiProgram, $workerProgram) | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($startupText.Contains("FakeLmaxGateway") -and -not ($startupText.Contains("RealLmaxGateway") -or $startupText.Contains("LmaxVenueGatewaySkeleton"))) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}

Add-Result "ValidatorRuntime" "No external action" "PASS" "Validator only reads local artifacts."
Add-Result "Promotion" "No runtime promotion action" "PASS" "T6 is documentation/readiness promotion only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { [string]$gate.finalDecision }
$outPath = Resolve-RepoPath (Join-Path $BaseDir "phase-usdjpy-t6-gate-validation.json")
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "USDJPY-T6"
    finalDecision = $decision
    promotionEligibilityStatus = [string]$gate.promotionEligibilityStatus
    caveat = [string]$gate.caveat
    externalRunExecuted = $false
    newSnapshotExecuted = $false
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
    promotionExecuted = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    validatedRailsModified = $false
    phase7ArchiveModified = $false
    phaseT1ArtifactsModified = $false
    phaseT2ArtifactsModified = $false
    phaseT3ArtifactsModified = $false
    phaseT4ArtifactsModified = $false
    phaseT5ArtifactsModified = $false
    nonUsdJpyExternalActionExecuted = $false
    outputSanitized = $true
    noSensitiveContent = $true
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }

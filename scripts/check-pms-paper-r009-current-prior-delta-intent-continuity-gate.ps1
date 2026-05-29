$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo "artifacts/readiness/pms-paper"

function Read-Json($name) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R009 file: $name"
    }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Assert-True($value, $message) {
    if ($value -ne $true) {
        throw $message
    }
}

function Assert-False($value, $message) {
    if ($value -ne $false) {
        throw $message
    }
}

$summaryPath = Join-Path $root "phase-pms-paper-r009-summary.md"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Missing required R009 summary."
}

$r008Reference = Read-Json "phase-pms-paper-r009-r008-output-reference.json"
$state = Read-Json "phase-pms-paper-r009-current-prior-state-review.json"
$delta = Read-Json "phase-pms-paper-r009-target-current-delta-continuity.json"
$intent = Read-Json "phase-pms-paper-r009-non-executable-oms-intent-continuity.json"
$boundary = Read-Json "phase-pms-paper-r009-ems-fix-lmax-boundary-audit.json"
$audit = Read-Json "phase-pms-paper-r009-no-order-no-route-no-fill-audit.json"
$evidence = Read-Json "phase-pms-paper-r009-build-test-validator-evidence.json"
$next = Read-Json "phase-pms-paper-r009-next-gate-plan.json"

if (-not (Test-Path -LiteralPath (Join-Path $root "phase-pms-paper-r008-manual-no-external-command.json"))) {
    throw "R008 reference is missing."
}
if ($r008Reference.R008CommandStatus -ne "ExecutedOnce") {
    throw "R008 command status must be ExecutedOnce."
}
if ($r008Reference.R008ManualNoExternalResult -ne "CompletedNoExternal") {
    throw "R008 result must be CompletedNoExternal."
}
Assert-True $r008Reference.R008ValidatorPassed "R008 validator must be recorded as passed."
Assert-True $r008Reference.ReadOnlyReview "R009 must be a read-only review."
Assert-False $r008Reference.RerunOccurred "R009 must not rerun ManualPaperCycle."

if ($state.Status -ne "ReviewedCurrentPriorPaperState") {
    throw "Current/prior state review status is invalid."
}
Assert-False $state.QubesZeroOnlyMarkedPmsApproved "Qubes ZeroOnly must not be PMS-approved."
Assert-False $state.SyntheticFixtureMislabeledAsQubesModelOutput "Synthetic fixture must not be mislabeled as Qubes model output."

if ($delta.Status -ne "IncompleteDeltaFields") {
    throw "R009 should classify missing delta fields explicitly."
}
Assert-True $delta.NotionalContinuityDerivable "Notional continuity should be derivable."
Assert-False $delta.WeightLevelContinuityValidated "Weight-level continuity must not be marked validated without fields."

if ($intent.Status -ne "ValidatedNonExecutableOmsIntentContinuity") {
    throw "Non-executable OMS intent continuity must be validated."
}
if ($intent.PreviewLineCount -ne 3) {
    throw "R009 expected exactly three R008 preview lines."
}
Assert-True $intent.AllNotAnOrder "All preview lines must be NotAnOrder."
Assert-True $intent.AllNotSubmitted "All preview lines must be NotSubmitted."
Assert-True $intent.AllNoBrokerRoute "All preview lines must be NoBrokerRoute."
Assert-True $intent.AllNoExecutableSchedule "All preview lines must be NoExecutableSchedule."
Assert-True $intent.AllNoFill "All preview lines must be NoFill."
Assert-True $intent.AllNoRoute "All preview lines must be NoRoute."
Assert-True $intent.AllNoSubmission "All preview lines must be NoSubmission."
Assert-False $intent.ExecutionAllowed "OMS intent preview must not allow execution."
Assert-False $intent.ExecutableOrderIdPresent "Executable order id must not be present."
Assert-False $intent.BrokerRouteIdPresent "Broker route id must not be present."
Assert-False $intent.FixMessagePresent "FIX message must not be present."
Assert-False $intent.FillIdPresent "Fill id must not be present."
Assert-False $intent.ExecutionReportPresent "Execution report must not be present."

Assert-False $boundary.EmsAllowed "EMS must remain blocked."
Assert-False $boundary.FixSessionAllowed "FIX session must remain blocked."
Assert-False $boundary.LmaxLiveAllowed "LMAX live must remain blocked."
Assert-False $boundary.RouteGenerationAllowed "Route generation must remain blocked."
Assert-False $boundary.ExecutionAllowed "Execution must remain blocked."
Assert-False $boundary.BrokerSubmissionAllowed "Broker submission must remain blocked."

Assert-True $audit.NoOrdersCreated "No orders may be created."
Assert-True $audit.NoOrderIntentsExecutable "No order intents may be executable."
Assert-True $audit.NoFillsCreated "No fills may be created."
Assert-True $audit.NoRoutesCreated "No routes may be created."
Assert-True $audit.NoBrokerSubmission "No broker submission may occur."
Assert-True $audit.NoSchedulesCreated "No schedules may be created."
Assert-True $audit.NoLiveTradingStateMutation "No live state mutation may occur."
Assert-True $audit.NoFixSession "No FIX session may be opened."
Assert-True $audit.NoLmaxCall "No LMAX call may occur."
Assert-True $audit.NoQubesExecutableRun "No Qubes executable may run."
Assert-True $audit.NoNettingRun "No netting may run."
Assert-True $audit.NoNettedUsdWeightsProduced "No NettedUsdWeights may be produced."
Assert-True $audit.NoExecAlgoGate "R009 must not be an Exec Algo gate."
Assert-False $audit.QubesZeroOnlyMarkedPmsApproved "Qubes ZeroOnly must not be PMS-approved."
Assert-False $audit.SyntheticFixtureMislabeledAsQubesModelOutput "Synthetic fixture must not be mislabeled as Qubes model output."

$executionSim = Join-Path $repo "artifacts/readiness/execution-sim"
if (Test-Path -LiteralPath $executionSim) {
    $r009ExecutionSim = Get-ChildItem -LiteralPath $executionSim -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*pms-paper-r009*" -or $_.Name -like "*phase-pms-paper-r009*" -or $_.FullName -like "*pms-paper-r009*" }
    if ($r009ExecutionSim) {
        throw "R009 artifacts were found under execution-sim."
    }
}

if ($next.RecommendedNextGate -notlike "PMS-PAPER-R010*") {
    throw "Next gate must be an R010 PMS paper gate."
}

"PMS-PAPER-R009 validator passed."

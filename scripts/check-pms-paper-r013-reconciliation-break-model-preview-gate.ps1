$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo "artifacts/readiness/pms-paper"

function Read-Json($name) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R013 file: $name"
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

$summaryPath = Join-Path $root "phase-pms-paper-r013-summary.md"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Missing required R013 summary."
}
if (-not (Test-Path -LiteralPath (Join-Path $root "phase-pms-paper-r012-oms-state-preview-validation.json"))) {
    throw "R012 reference is missing."
}

$reference = Read-Json "phase-pms-paper-r013-r012-output-reference.json"
$contract = Read-Json "phase-pms-paper-r013-reconciliation-break-model-contract.json"
$current = Read-Json "phase-pms-paper-r013-current-gate-reconciliation-preview.json"
$future = Read-Json "phase-pms-paper-r013-future-exec-algo-fill-reconciliation-contract.json"
$pnl = Read-Json "phase-pms-paper-r013-pnl-readiness-boundary.json"
$boundary = Read-Json "phase-pms-paper-r013-exec-algo-handoff-boundary.json"
$audit = Read-Json "phase-pms-paper-r013-no-order-no-route-no-fill-audit.json"
$evidence = Read-Json "phase-pms-paper-r013-build-test-validator-evidence.json"
$next = Read-Json "phase-pms-paper-r013-next-gate-plan.json"
$r012State = Get-Content -LiteralPath (Join-Path $root "phase-pms-paper-r012-oms-state-preview-validation.json") -Raw | ConvertFrom-Json

Assert-True $reference.ReadOnlyReview "R013 should review R012 output read-only."
Assert-False $reference.ManualPaperCycleRerun "R013 must not rerun ManualPaperCycle."
if ($reference.R012ValidatorReference.Result -ne "Passed") {
    throw "R012 validator must have passed."
}

if ($r012State.Status -ne "ValidatedOmsStatePreview") {
    throw "R012 OMS state preview must be valid."
}
foreach ($state in $r012State.States) {
    if ($state.State -ne "BlockedBeforeSubmission") {
        throw "Every R012 OMS state must be BlockedBeforeSubmission."
    }
    Assert-False $state.ExecutionAllowed "R012 state must not allow execution."
    Assert-False $state.EmsRouteAllowed "R012 state must not allow EMS route."
    Assert-False $state.DirectLmaxCallFromPmsPaperGateAllowed "R012 state must not allow direct LMAX."
    Assert-False $state.CrossRailExecAlgoHandoffAllowedNow "R012 state must not allow cross-rail handoff now."
}

if ($contract.Status -ne "ReconciliationBreakModelContractDefined") {
    throw "Reconciliation break model contract must be defined."
}
if ($contract.BreakModelStatus -ne "ReconciliationBreakModelContractDefined") {
    throw "BreakModelStatus must be ReconciliationBreakModelContractDefined."
}
foreach ($field in @("ExpectedOrderCount", "ActualOrderCount", "ExpectedRouteCount", "ActualRouteCount", "ExpectedFillCount", "ActualFillCount", "ExpectedExecutionReportCount", "ActualExecutionReportCount", "CurrentGateBreakCount")) {
    if (-not $contract.PSObject.Properties[$field]) {
        throw "Contract missing count field $field."
    }
}
foreach ($category in @("MissingOrder", "UnexpectedOrder", "MissingRoute", "UnexpectedRoute", "MissingFill", "UnexpectedFill", "PartialFillMismatch", "DuplicateFill", "RejectedOrder", "CancelledOrder", "StaleOrder", "QuantityMismatch", "SymbolMismatch", "VenueMismatch", "PriceOutOfBand", "ExecutionReportMissing", "ExecutionReportDuplicate", "BrokerStateMismatch", "UnknownBreak")) {
    if ($contract.BreakCategories -notcontains $category) {
        throw "Missing break category $category."
    }
}
foreach ($key in @("OmsPreviewStateId", "IntentPreviewId", "FutureOrderId", "FutureRouteId", "FutureFillId", "FutureExecutionReportId", "Symbol", "Quantity", "Side", "Price", "Timestamp", "Venue")) {
    if ($contract.RequiredFutureCorrelationKeys -notcontains $key) {
        throw "Missing future correlation key $key."
    }
}
if ($contract.CurrentGateBreakCount -ne 0) {
    throw "CurrentGateBreakCount must be zero while no executable lifecycle exists."
}
Assert-True $contract.FutureBreakDetectionReady "Future break detection must be ready."
Assert-False $contract.CurrentGateExecutionAllowed "Current gate execution must not be allowed."
Assert-False $contract.ReconciliationExecutionAllowed "Reconciliation execution must not be allowed."
Assert-True $contract.CrossRailExecAlgoInputsRequired "Cross-rail Exec Algo inputs must be required for future reconciliation."

if ($current.Status -ne "ValidatedCurrentGateNoBreaks") {
    throw "Current gate reconciliation preview must validate no breaks."
}
foreach ($field in @("ExpectedOrderCount", "ActualOrderCount", "ExpectedRouteCount", "ActualRouteCount", "ExpectedFillCount", "ActualFillCount", "ExpectedExecutionReportCount", "ActualExecutionReportCount", "CurrentGateBreakCount")) {
    if (-not $current.PSObject.Properties[$field]) {
        throw "Current preview missing count field $field."
    }
    if ($current.$field -ne 0) {
        throw "$field must be zero."
    }
}
if ($current.CurrentGateBreaks.Count -ne 0) {
    throw "CurrentGateBreaks must be empty."
}
if ($current.Reason -ne "BlockedBeforeSubmissionNoExecutableLifecycle") {
    throw "Current gate reason must be BlockedBeforeSubmissionNoExecutableLifecycle."
}
Assert-True $current.AllSourceOmsStatesBlockedBeforeSubmission "All source OMS states must be blocked before submission."
Assert-False $current.ReconciliationExecutionAllowed "Reconciliation execution must be blocked."

if ($future.Status -ne "DraftInactive") {
    throw "Future Exec Algo fill reconciliation contract must be draft inactive."
}
Assert-False $future.CrossRailExecAlgoHandoffAllowedNow "Cross-rail handoff must not be allowed now."
Assert-False $future.DirectLmaxCallFromPmsPaperGateAllowed "PMS-PAPER must not directly call LMAX."
Assert-False $future.FixSessionOpenedByThisGate "This gate must not open FIX."
Assert-False $future.LmaxCallMadeByThisGate "This gate must not call LMAX."
Assert-False $future.ExecutionAllowedByThisGate "This gate must not allow execution."
foreach ($key in @("OmsPreviewStateId", "IntentPreviewId", "FutureOrderId", "FutureRouteId", "FutureFillId", "FutureExecutionReportId", "Symbol", "Quantity", "Side", "Price", "Timestamp", "Venue")) {
    if ($future.RequiredFutureInputs -notcontains $key) {
        throw "Future contract missing required input $key."
    }
}

if ($pnl.Status -ne "PnlReadinessBoundaryDefined") {
    throw "PnL readiness boundary must be defined."
}
Assert-False $pnl.PnlCalculationAllowed "PnL calculation must not be allowed."
Assert-False $pnl.FillBasedPnlAvailable "Fill-based PnL must not be available."
Assert-False $pnl.RealizedPnlAvailable "Realized PnL must not be available."
Assert-False $pnl.ProductionPnlAvailable "Production PnL must not be available."
Assert-False $pnl.CostAttributionAvailable "Cost attribution must not be available."

if ($boundary.Status -ne "ExecAlgoHandoffBoundaryPreserved") {
    throw "Exec Algo handoff boundary must be preserved."
}
Assert-False $boundary.DirectLmaxCallFromPmsPaperGateAllowed "Direct LMAX from PMS-PAPER must be blocked."
Assert-True $boundary.SandboxLmaxAvailableInExecAlgoRail "Sandbox LMAX availability belongs to Exec Algo rail."
Assert-False $boundary.CrossRailExecAlgoHandoffAllowedNow "Cross-rail handoff must not be allowed now."
Assert-True $boundary.FutureCrossRailGateRequired "Future cross-rail gate must be required."
Assert-False $boundary.OrdersSubmittedByThisGate "This gate must not submit orders."
Assert-False $boundary.FixSessionOpenedByThisGate "This gate must not open FIX."
Assert-False $boundary.LmaxCallMadeByThisGate "This gate must not call LMAX."
Assert-False $boundary.BrokerRouteCreatedByThisGate "This gate must not create broker routes."
Assert-False $boundary.ExecutionAllowedByThisGate "This gate must not allow execution."

Assert-True $audit.NoOrdersCreated "No orders may be created."
Assert-True $audit.NoOrderIntentsExecutable "No order intents may be executable."
Assert-True $audit.NoFillsCreated "No fills may be created."
Assert-True $audit.NoRoutesCreated "No routes may be created."
Assert-True $audit.NoBrokerSubmission "No broker submission may occur."
Assert-True $audit.NoSchedulesCreated "No schedules may be created."
Assert-True $audit.NoLiveTradingStateMutation "No live state mutation may occur."
Assert-True $audit.NoFixSession "No FIX session may be opened."
Assert-True $audit.NoLmaxCallFromThisGate "This gate must not call LMAX."
Assert-True $audit.NoQubesExecutableRun "No Qubes executable may run."
Assert-True $audit.NoNettingRun "No netting may run."
Assert-True $audit.NoNettedUsdWeightsProduced "No NettedUsdWeights may be produced."
Assert-True $audit.NoExecAlgoGateExecuted "No Exec Algo gate may execute in R013."

if ($evidence.R012Review.ReadOnlyReview -ne $true) {
    throw "Evidence must record read-only R012 review."
}
if ($evidence.R012Review.ManualPaperCycleRerun -ne $false) {
    throw "Evidence must record no ManualPaperCycle rerun."
}

$executionSim = Join-Path $repo "artifacts/readiness/execution-sim"
if (Test-Path -LiteralPath $executionSim) {
    $r013ExecutionSim = Get-ChildItem -LiteralPath $executionSim -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*pms-paper-r013*" -or $_.Name -like "*phase-pms-paper-r013*" -or $_.FullName -like "*pms-paper-r013*" }
    if ($r013ExecutionSim) {
        throw "R013 artifacts were found under execution-sim."
    }
}

if ($next.RecommendedNextGate -notlike "PMS-PAPER-R014*") {
    throw "Next gate must be an R014 PMS paper gate."
}

"PMS-PAPER-R013 validator passed."

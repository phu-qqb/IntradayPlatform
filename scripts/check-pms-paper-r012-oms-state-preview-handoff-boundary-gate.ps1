$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo "artifacts/readiness/pms-paper"

function Read-Json($name) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R012 file: $name"
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

$summaryPath = Join-Path $root "phase-pms-paper-r012-summary.md"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Missing required R012 summary."
}
if (-not (Test-Path -LiteralPath (Join-Path $root "phase-pms-paper-r011-oms-intent-preview-validation.json"))) {
    throw "R011 reference is missing."
}

$contract = Read-Json "phase-pms-paper-r012-oms-state-preview-contract.json"
$reference = Read-Json "phase-pms-paper-r012-r011-output-reference.json"
$state = Read-Json "phase-pms-paper-r012-oms-state-preview-validation.json"
$boundary = Read-Json "phase-pms-paper-r012-exec-algo-handoff-boundary.json"
$draft = Read-Json "phase-pms-paper-r012-exec-algo-handoff-contract-draft.json"
$recon = Read-Json "phase-pms-paper-r012-reconciliation-readiness-hooks.json"
$pnl = Read-Json "phase-pms-paper-r012-pnl-readiness-hooks.json"
$audit = Read-Json "phase-pms-paper-r012-no-order-no-route-no-fill-audit.json"
$evidence = Read-Json "phase-pms-paper-r012-build-test-validator-evidence.json"
$next = Read-Json "phase-pms-paper-r012-next-gate-plan.json"

if ($contract.Status -ne "OmsStatePreviewContractDefined") {
    throw "OMS state preview contract must be defined."
}
Assert-False $contract.ExecutionAllowed "Contract must not allow execution."
Assert-False $contract.RouteCreationAllowed "Contract must not allow routes."
Assert-False $contract.FillCreationAllowed "Contract must not allow fills."
Assert-False $contract.DirectLmaxCallFromPmsPaperGateAllowed "PMS-PAPER must not directly call LMAX."
Assert-False $contract.CrossRailExecAlgoHandoffAllowedNow "Cross-rail handoff must not be allowed now."

foreach ($field in @("OmsPreviewStateId", "IntentPreviewId", "CycleRunId", "Symbol", "SourceClassification", "PreviewLineReason", "TargetWeight", "CurrentWeight", "DeltaWeight", "TargetNotional", "CurrentNotional", "DeltaNotional", "Direction", "ArithmeticValid", "State", "BlockReason", "NotAnOrder", "NotSubmitted", "NoBrokerRoute", "NoFixMessage", "NoExecutableSchedule", "NoFill", "NoRoute", "NoSubmission", "ExecutionAllowed", "EmsRouteAllowed", "DirectLmaxCallFromPmsPaperGateAllowed", "SandboxLmaxAvailableInExecAlgoRail", "CrossRailExecAlgoHandoffAllowedNow", "FutureCrossRailGateRequired")) {
    if ($contract.RequiredFields -notcontains $field) {
        throw "Contract is missing required field $field."
    }
}
foreach ($forbidden in @("Submitted", "Routed", "Filled", "PartiallyFilled", "CancelledLive", "RejectedLive")) {
    if ($contract.ForbiddenStates -notcontains $forbidden) {
        throw "Contract is missing forbidden state $forbidden."
    }
}

Assert-True $reference.ReadOnlyReview "R012 should review R011 output read-only."
Assert-False $reference.ManualPaperCycleRerun "R012 must not rerun ManualPaperCycle."
if ($reference.R011ValidatorReference.Result -ne "Passed") {
    throw "R011 validator must have passed."
}

if ($state.Status -ne "ValidatedOmsStatePreview") {
    throw "OMS state preview must be validated."
}
if ($state.PreviewStateCount -ne 3) {
    throw "Expected exactly three OMS preview states."
}
Assert-True $state.AllBlockedBeforeSubmission "Every OMS preview state must be blocked before submission."
Assert-True $state.AllNonExecutable "Every OMS preview state must be non-executable."
Assert-True $state.AllArithmeticValid "Every OMS preview state must be arithmetic-valid."
Assert-False $state.ExecutableOrderIdsPresent "Executable order IDs must not be present."
Assert-False $state.RouteIdsPresent "Route IDs must not be present."
Assert-False $state.FillIdsPresent "Fill IDs must not be present."
Assert-False $state.ExecutionReportIdsPresent "Execution report IDs must not be present."

$requiredStateFields = @("OmsPreviewStateId", "IntentPreviewId", "CycleRunId", "Symbol", "SourceClassification", "PreviewLineReason", "TargetWeight", "CurrentWeight", "DeltaWeight", "TargetNotional", "CurrentNotional", "DeltaNotional", "Direction", "ArithmeticValid", "State", "BlockReason", "NotAnOrder", "NotSubmitted", "NoBrokerRoute", "NoFixMessage", "NoExecutableSchedule", "NoFill", "NoRoute", "NoSubmission", "ExecutionAllowed", "EmsRouteAllowed", "DirectLmaxCallFromPmsPaperGateAllowed", "SandboxLmaxAvailableInExecAlgoRail", "CrossRailExecAlgoHandoffAllowedNow", "FutureCrossRailGateRequired")
$symbols = @()
foreach ($line in $state.States) {
    foreach ($field in $requiredStateFields) {
        if (-not $line.PSObject.Properties[$field]) {
            throw "OMS preview state missing field $field."
        }
    }
    $symbols += $line.Symbol
    if ($line.State -ne "BlockedBeforeSubmission" -and $line.State -ne "PreviewCreated") {
        throw "Invalid OMS preview state $($line.State)."
    }
    if ($line.BlockReason -ne "PaperOnlyNoExecution") {
        throw "OMS preview state must be blocked for PaperOnlyNoExecution."
    }
    Assert-True $line.ArithmeticValid "Each OMS preview state must be arithmetic-valid."
    Assert-True $line.NotAnOrder "Each OMS preview state must be NotAnOrder."
    Assert-True $line.NotSubmitted "Each OMS preview state must be NotSubmitted."
    Assert-True $line.NoBrokerRoute "Each OMS preview state must be NoBrokerRoute."
    Assert-True $line.NoFixMessage "Each OMS preview state must be NoFixMessage."
    Assert-True $line.NoExecutableSchedule "Each OMS preview state must be NoExecutableSchedule."
    Assert-True $line.NoFill "Each OMS preview state must be NoFill."
    Assert-True $line.NoRoute "Each OMS preview state must be NoRoute."
    Assert-True $line.NoSubmission "Each OMS preview state must be NoSubmission."
    Assert-False $line.ExecutionAllowed "Each OMS preview state must not allow execution."
    Assert-False $line.EmsRouteAllowed "Each OMS preview state must not allow EMS route."
    Assert-False $line.DirectLmaxCallFromPmsPaperGateAllowed "PMS-PAPER state must not allow direct LMAX."
    Assert-True $line.SandboxLmaxAvailableInExecAlgoRail "Sandbox LMAX availability must be scoped to Exec Algo rail."
    Assert-False $line.CrossRailExecAlgoHandoffAllowedNow "Cross-rail handoff must not be allowed now."
    Assert-True $line.FutureCrossRailGateRequired "Future cross-rail gate must be required."
}
foreach ($expected in @("AUDUSD", "EURUSD", "GBPUSD")) {
    if ($symbols -notcontains $expected) {
        throw "Missing expected symbol $expected."
    }
}

if ($boundary.Status -ne "ExecAlgoHandoffBoundaryDefined") {
    throw "Exec Algo handoff boundary must be defined."
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

if ($draft.Status -ne "DraftInactive") {
    throw "Cross-rail handoff contract must be draft inactive."
}
if ($draft.SourceRail -ne "PMS-PAPER") {
    throw "Draft source rail must be PMS-PAPER."
}
if ($draft.TargetRail -ne "ExecAlgoSandbox") {
    throw "Draft target rail must be ExecAlgoSandbox."
}
Assert-True $draft.SourceIsNonExecutable "Draft source must be non-executable."
Assert-True $draft.SourceContainsNoOrders "Draft source must contain no orders."
Assert-True $draft.SourceContainsNoRoutes "Draft source must contain no routes."
Assert-True $draft.SourceContainsNoFills "Draft source must contain no fills."
Assert-True $draft.SourceContainsNoFixMessages "Draft source must contain no FIX messages."
Assert-False $draft.DirectExecutionAllowedNow "Draft must not allow direct execution now."
Assert-False $draft.DirectLmaxCallFromPmsPaperGateAllowed "Draft must not allow direct LMAX from PMS-PAPER."
Assert-False $draft.CrossRailExecAlgoHandoffAllowedNow "Draft must not allow cross-rail handoff now."

if ($recon.Status -ne "ReconciliationReadinessHooksDefined") {
    throw "Reconciliation readiness hooks must be defined."
}
if ($recon.ExpectedOrderCount -ne 0 -or $recon.ExpectedRouteCount -ne 0 -or $recon.ExpectedFillCount -ne 0 -or $recon.ExpectedExecutionReportCount -ne 0) {
    throw "Reconciliation expected counts must all be zero."
}
Assert-True $recon.BreakDetectionReadyForFuture "Break detection should be ready for a future gate."
Assert-False $recon.ReconciliationExecutionAllowed "Reconciliation execution must not be allowed."

if ($pnl.Status -ne "PnlReadinessHooksDefined") {
    throw "PnL readiness hooks must be defined."
}
Assert-False $pnl.PnlCalculationAllowed "PnL calculation must not be allowed."
Assert-False $pnl.FillBasedPnlAvailable "Fill-based PnL must not be available."
Assert-False $pnl.RealizedPnlAvailable "Realized PnL must not be available."
Assert-False $pnl.ProductionPnlAvailable "Production PnL must not be available."
Assert-False $pnl.CostAttributionAvailable "Cost attribution must not be available."

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
Assert-True $audit.NoExecAlgoGateExecuted "No Exec Algo gate may execute in R012."

if ($evidence.R011Review.ReadOnlyReview -ne $true) {
    throw "Evidence must record read-only R011 review."
}
if ($evidence.R011Review.ManualPaperCycleRerun -ne $false) {
    throw "Evidence must record no ManualPaperCycle rerun."
}

$executionSim = Join-Path $repo "artifacts/readiness/execution-sim"
if (Test-Path -LiteralPath $executionSim) {
    $r012ExecutionSim = Get-ChildItem -LiteralPath $executionSim -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*pms-paper-r012*" -or $_.Name -like "*phase-pms-paper-r012*" -or $_.FullName -like "*pms-paper-r012*" }
    if ($r012ExecutionSim) {
        throw "R012 artifacts were found under execution-sim."
    }
}

if ($next.RecommendedNextGate -notlike "PMS-PAPER-R013*") {
    throw "Next gate must be an R013 PMS paper gate."
}
if ($next.SeparateFutureCrossRailGate -ne "ExecAlgoSandboxHandoffGate") {
    throw "Separate future cross-rail gate must be ExecAlgoSandboxHandoffGate."
}

"PMS-PAPER-R012 validator passed."

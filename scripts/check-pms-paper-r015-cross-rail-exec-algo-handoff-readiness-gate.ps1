$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo "artifacts/readiness/pms-paper"

function Read-Json($name) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R015 file: $name"
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

$summaryPath = Join-Path $root "phase-pms-paper-r015-summary.md"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Missing required R015 summary."
}
if (-not (Test-Path -LiteralPath (Join-Path $root "phase-pms-paper-r014-pnl-readiness-model-contract.json"))) {
    throw "R014 reference is missing."
}

$reference = Read-Json "phase-pms-paper-r015-r014-output-reference.json"
$contract = Read-Json "phase-pms-paper-r015-cross-rail-handoff-contract.json"
$assessment = Read-Json "phase-pms-paper-r015-handoff-readiness-assessment.json"
$inputs = Read-Json "phase-pms-paper-r015-required-exec-algo-inputs.json"
$boundary = Read-Json "phase-pms-paper-r015-sandbox-safety-boundary.json"
$recon = Read-Json "phase-pms-paper-r015-reconciliation-correlation-contract.json"
$pnl = Read-Json "phase-pms-paper-r015-pnl-correlation-contract.json"
$audit = Read-Json "phase-pms-paper-r015-no-order-no-route-no-fill-audit.json"
$evidence = Read-Json "phase-pms-paper-r015-build-test-validator-evidence.json"
$next = Read-Json "phase-pms-paper-r015-next-gate-plan.json"
$r012State = Get-Content -LiteralPath (Join-Path $root "phase-pms-paper-r012-oms-state-preview-validation.json") -Raw | ConvertFrom-Json
$r011Intent = Get-Content -LiteralPath (Join-Path $root "phase-pms-paper-r011-oms-intent-preview-validation.json") -Raw | ConvertFrom-Json
$r013Contract = Get-Content -LiteralPath (Join-Path $root "phase-pms-paper-r013-reconciliation-break-model-contract.json") -Raw | ConvertFrom-Json
$r014Contract = Get-Content -LiteralPath (Join-Path $root "phase-pms-paper-r014-pnl-readiness-model-contract.json") -Raw | ConvertFrom-Json

Assert-True $reference.ReadOnlyReview "R015 should review R014 output read-only."
Assert-False $reference.ManualPaperCycleRerun "R015 must not rerun ManualPaperCycle."
if ($reference.R014ValidatorReference.Result -ne "Passed") {
    throw "R014 validator must have passed."
}

if ($r014Contract.Status -ne "PnlReadinessModelContractDefined") {
    throw "R014 PnL readiness model contract must exist."
}
if ($r013Contract.Status -ne "ReconciliationBreakModelContractDefined") {
    throw "R013 reconciliation break model contract must exist."
}
if ($r012State.Status -ne "ValidatedOmsStatePreview") {
    throw "R012 OMS state preview must exist."
}
if ($r011Intent.Status -ne "ValidatedOmsIntentPreviewContract") {
    throw "R011 OMS intent preview contract must exist."
}
foreach ($state in $r012State.States) {
    if ($state.State -ne "BlockedBeforeSubmission") {
        throw "Every OMS state must remain blocked before submission."
    }
    Assert-True $state.NotAnOrder "OMS state must be NotAnOrder."
    Assert-True $state.NotSubmitted "OMS state must be NotSubmitted."
    Assert-True $state.NoBrokerRoute "OMS state must be NoBrokerRoute."
    Assert-True $state.NoFixMessage "OMS state must have no FIX messages."
    Assert-True $state.NoFill "OMS state must have no fill."
    Assert-True $state.NoRoute "OMS state must have no route."
    Assert-False $state.ExecutionAllowed "OMS state must be non-executable."
    Assert-False $state.EmsRouteAllowed "OMS state must not allow EMS route."
    Assert-False $state.DirectLmaxCallFromPmsPaperGateAllowed "OMS state must not allow direct LMAX."
    Assert-False $state.CrossRailExecAlgoHandoffAllowedNow "OMS state must not allow cross-rail handoff now."
}
Assert-False $r012State.ExecutableOrderIdsPresent "Source must contain no executable order ids."
Assert-False $r012State.RouteIdsPresent "Source must contain no route ids."
Assert-False $r012State.FillIdsPresent "Source must contain no fill ids."
Assert-False $r012State.ExecutionReportIdsPresent "Source must contain no execution report ids."

if ($contract.Status -ne "CrossRailHandoffContractDefined") {
    throw "Cross-rail handoff contract must be defined."
}
if ($contract.HandoffContractStatus -ne "DraftInactive") {
    throw "Handoff contract must be draft inactive."
}
if ($contract.SourceRail -ne "PMS-PAPER") {
    throw "SourceRail must be PMS-PAPER."
}
if ($contract.TargetRail -ne "ExecAlgoSandbox") {
    throw "TargetRail must be ExecAlgoSandbox."
}
if ($contract.SourceArtifactType -ne "OmsStatePreview") {
    throw "SourceArtifactType must be OmsStatePreview."
}
foreach ($field in @("SourceCycleRunId", "SourceIntentPreviewIds", "SourceOmsPreviewStateIds", "SourceSymbols", "SourceTargetWeights", "SourceCurrentWeights", "SourceDeltaWeights", "SourceTargetNotionals", "SourceCurrentNotionals", "SourceDeltaNotionals", "SourceDirections")) {
    if (-not $contract.PSObject.Properties[$field]) {
        throw "Handoff contract missing source field $field."
    }
}
if ($contract.SourceIntentPreviewIds.Count -ne 3 -or $contract.SourceOmsPreviewStateIds.Count -ne 3 -or $contract.SourceSymbols.Count -ne 3) {
    throw "Handoff source arrays must each contain three entries."
}
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    if ($contract.SourceSymbols -notcontains $symbol) {
        throw "Missing source symbol $symbol."
    }
}
Assert-True $contract.SourceIsNonExecutable "Source must be non-executable."
Assert-True $contract.SourceContainsNoOrders "Source must contain no orders."
Assert-True $contract.SourceContainsNoRoutes "Source must contain no routes."
Assert-True $contract.SourceContainsNoFills "Source must contain no fills."
Assert-True $contract.SourceContainsNoFixMessages "Source must contain no FIX messages."
Assert-False $contract.DirectExecutionAllowedNow "Direct execution must not be allowed now."
Assert-False $contract.DirectLmaxCallFromPmsPaperGateAllowed "PMS-PAPER must not directly call LMAX."
Assert-True $contract.SandboxLmaxAvailableInExecAlgoRail "Sandbox LMAX belongs to Exec Algo rail."
Assert-False $contract.CrossRailExecAlgoHandoffAllowedNow "Cross-rail handoff must not be allowed now."
Assert-True $contract.FutureCrossRailGateRequired "Future cross-rail gate must be required."

if (@("HandoffDraftReady", "HandoffContractDraftOnly", "BlockedMissingHandoffFields", "Invalid") -notcontains $assessment.Status) {
    throw "Invalid handoff readiness status."
}
if ($assessment.Status -ne "HandoffContractDraftOnly") {
    throw "Expected HandoffContractDraftOnly for R015."
}
Assert-True $assessment.SourceFieldsComplete "Source fields should be complete."
if ($assessment.MissingSourceFields.Count -ne 0) {
    throw "MissingSourceFields should be empty."
}
if ($assessment.MissingFutureExecAlgoFields.Count -lt 1) {
    throw "Missing future Exec Algo fields should be listed."
}
Assert-False $assessment.ReadyForExecAlgoSandboxGate "R015 must not mark ready for Exec Algo sandbox gate."
Assert-False $assessment.CrossRailExecAlgoHandoffAllowedNow "Cross-rail handoff must not be allowed now."
Assert-False $assessment.DirectLmaxCallFromPmsPaperGateAllowed "PMS-PAPER must not directly call LMAX."

if ($inputs.Status -ne "RequiredFutureInputsDefined") {
    throw "Required future inputs must be defined."
}
foreach ($required in @("OmsPreviewStateId", "IntentPreviewId", "Symbol", "Direction", "DeltaWeight", "DeltaNotional", "Account or paper account id", "Quantity model or sizing rule", "Instrument mapping to broker symbol", "Side derivation rule", "Order type / time in force / algo parameters", "Sandbox mode flag", "No-live flag", "Correlation id", "Future route id", "Future order id", "Future execution report id", "Future fill id", "Reconciliation link back to PMS-PAPER ids")) {
    if ($inputs.RequiredFutureInputs -notcontains $required) {
        throw "Missing required future input $required."
    }
}
if ($inputs.MissingInputs.Count -lt 1) {
    throw "Missing future inputs must be listed."
}
if ($inputs.RequiredFutureGate -ne "ExecAlgoSandboxHandoffGate") {
    throw "Required future gate must be ExecAlgoSandboxHandoffGate."
}

if ($boundary.Status -ne "SandboxSafetyBoundaryDefined") {
    throw "Sandbox safety boundary must be defined."
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

if ($recon.Status -ne "ReconciliationCorrelationContractDrafted") {
    throw "Reconciliation correlation contract must be drafted."
}
foreach ($key in @("OmsPreviewStateId", "IntentPreviewId", "FutureOrderId", "FutureRouteId", "FutureExecutionReportId", "FutureFillId")) {
    if ($recon.RequiredFutureKeys -notcontains $key) {
        throw "Missing reconciliation future key $key."
    }
}
if ($recon.CurrentGateBreaks.Count -ne 0) {
    throw "CurrentGateBreaks must be empty."
}
Assert-False $recon.ReconciliationExecutionAllowed "Reconciliation execution must not be allowed."
Assert-False $recon.CrossRailExecAlgoHandoffAllowedNow "Reconciliation contract must not allow cross-rail handoff now."

if ($pnl.Status -ne "PnlCorrelationContractDrafted") {
    throw "PnL correlation contract must be drafted."
}
foreach ($key in @("FutureFillId", "FutureExecutionReportId", "FillPrice", "FillQuantity", "FillTimestamp", "MarkPrice", "MarkTimestamp", "CostAmount", "CostCurrency", "FxConversionRate", "PositionId", "OmsPreviewStateId", "IntentPreviewId")) {
    if ($pnl.RequiredFutureKeys -notcontains $key) {
        throw "Missing PnL future key $key."
    }
}
Assert-False $pnl.ProductionPnlAllowedNow "Production PnL must not be allowed now."
Assert-False $pnl.FillBasedPnlAllowedNow "Fill-based PnL must not be allowed now."
Assert-False $pnl.RealizedPnlAllowedNow "Realized PnL must not be allowed now."
Assert-False $pnl.CrossRailExecAlgoHandoffAllowedNow "PnL contract must not allow cross-rail handoff now."

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
Assert-True $audit.NoExecAlgoGateExecuted "No Exec Algo gate may execute in R015."

if ($evidence.R014Review.ReadOnlyReview -ne $true) {
    throw "Evidence must record read-only R014 review."
}
if ($evidence.R014Review.ManualPaperCycleRerun -ne $false) {
    throw "Evidence must record no ManualPaperCycle rerun."
}

$executionSim = Join-Path $repo "artifacts/readiness/execution-sim"
if (Test-Path -LiteralPath $executionSim) {
    $r015ExecutionSim = Get-ChildItem -LiteralPath $executionSim -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*pms-paper-r015*" -or $_.Name -like "*phase-pms-paper-r015*" -or $_.FullName -like "*pms-paper-r015*" }
    if ($r015ExecutionSim) {
        throw "R015 artifacts were found under execution-sim."
    }
}

if ($next.RecommendedNextGate -notlike "Cross-rail next gate*") {
    throw "Next gate should be the cross-rail Exec Algo sandbox handoff gate."
}

"PMS-PAPER-R015 validator passed."

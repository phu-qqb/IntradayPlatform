$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo "artifacts/readiness/pms-paper"

function Read-Json($name) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R014 file: $name"
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

$summaryPath = Join-Path $root "phase-pms-paper-r014-summary.md"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Missing required R014 summary."
}
if (-not (Test-Path -LiteralPath (Join-Path $root "phase-pms-paper-r013-current-gate-reconciliation-preview.json"))) {
    throw "R013 reference is missing."
}
if (-not (Test-Path -LiteralPath (Join-Path $root "phase-pms-paper-r012-oms-state-preview-validation.json"))) {
    throw "R012 OMS state preview reference is missing."
}

$reference = Read-Json "phase-pms-paper-r014-r013-output-reference.json"
$contract = Read-Json "phase-pms-paper-r014-pnl-readiness-model-contract.json"
$capability = Read-Json "phase-pms-paper-r014-current-gate-pnl-capability.json"
$exposure = Read-Json "phase-pms-paper-r014-theoretical-exposure-preview.json"
$fillContract = Read-Json "phase-pms-paper-r014-future-fill-based-pnl-contract.json"
$mtmContract = Read-Json "phase-pms-paper-r014-future-mtm-pnl-contract.json"
$boundary = Read-Json "phase-pms-paper-r014-exec-algo-handoff-boundary.json"
$audit = Read-Json "phase-pms-paper-r014-no-order-no-route-no-fill-audit.json"
$evidence = Read-Json "phase-pms-paper-r014-build-test-validator-evidence.json"
$next = Read-Json "phase-pms-paper-r014-next-gate-plan.json"
$r013Current = Get-Content -LiteralPath (Join-Path $root "phase-pms-paper-r013-current-gate-reconciliation-preview.json") -Raw | ConvertFrom-Json
$r012State = Get-Content -LiteralPath (Join-Path $root "phase-pms-paper-r012-oms-state-preview-validation.json") -Raw | ConvertFrom-Json

Assert-True $reference.ReadOnlyReview "R014 should review R013 output read-only."
Assert-False $reference.ManualPaperCycleRerun "R014 must not rerun ManualPaperCycle."
if ($reference.R013ValidatorReference.Result -ne "Passed") {
    throw "R013 validator must have passed."
}

foreach ($field in @("ExpectedOrderCount", "ActualOrderCount", "ExpectedRouteCount", "ActualRouteCount", "ExpectedFillCount", "ActualFillCount", "ExpectedExecutionReportCount", "ActualExecutionReportCount", "CurrentGateBreakCount")) {
    if (-not $r013Current.PSObject.Properties[$field]) {
        throw "R013 current reconciliation preview missing $field."
    }
    if ($r013Current.$field -ne 0) {
        throw "R013 $field must be zero."
    }
}
if ($r012State.Status -ne "ValidatedOmsStatePreview") {
    throw "R012 OMS state preview must be valid."
}
foreach ($state in $r012State.States) {
    if ($state.State -ne "BlockedBeforeSubmission") {
        throw "Every R012 OMS state must be blocked before submission."
    }
}

if ($contract.Status -ne "PnlReadinessModelContractDefined") {
    throw "PnL readiness model contract must be defined."
}
if ($contract.PnlReadinessStatus -ne "PnlReadinessModelContractDefined") {
    throw "PnlReadinessStatus must be PnlReadinessModelContractDefined."
}
foreach ($field in @("ExpectedOrderCount", "ExpectedFillCount", "CurrentGateHasFills", "CurrentGateHasExecutionReports", "CurrentGateHasBrokerPositions", "CurrentGateHasLiveMarks", "CurrentGateCanComputeProductionPnl", "CurrentGateCanComputeRealizedPnl", "CurrentGateCanComputeFillBasedPnl", "CurrentGateCanComputeUnrealizedPnl", "CurrentGateCanComputeTheoreticalExposurePreview", "PnlExecutionAllowed", "DirectLmaxCallFromPmsPaperGateAllowed", "CrossRailExecAlgoHandoffAllowedNow")) {
    if (-not $contract.PSObject.Properties[$field]) {
        throw "PnL contract missing field $field."
    }
}
if ($contract.ExpectedOrderCount -ne 0 -or $contract.ExpectedFillCount -ne 0) {
    throw "Expected order/fill counts must be zero."
}
Assert-False $contract.CurrentGateHasFills "Current gate must have no fills."
Assert-False $contract.CurrentGateHasExecutionReports "Current gate must have no execution reports."
Assert-False $contract.CurrentGateHasBrokerPositions "Current gate must have no broker positions."
Assert-False $contract.CurrentGateHasLiveMarks "Current gate must have no live marks."
Assert-False $contract.CurrentGateCanComputeProductionPnl "Current gate must not compute production PnL."
Assert-False $contract.CurrentGateCanComputeRealizedPnl "Current gate must not compute realized PnL."
Assert-False $contract.CurrentGateCanComputeFillBasedPnl "Current gate must not compute fill-based PnL."
Assert-False $contract.CurrentGateCanComputeUnrealizedPnl "Current gate must not compute unrealized PnL."
Assert-True $contract.CurrentGateCanComputeTheoreticalExposurePreview "Current gate should provide theoretical exposure preview."
Assert-False $contract.CurrentGatePnlAllowed "Current gate PnL must not be allowed."
Assert-False $contract.FillBasedPnlAllowed "Fill-based PnL must not be allowed."
Assert-False $contract.RealizedPnlAllowed "Realized PnL must not be allowed."
Assert-False $contract.ProductionPnlAllowed "Production PnL must not be allowed."
Assert-False $contract.PnlExecutionAllowed "PnL execution must not be allowed."
Assert-False $contract.DirectLmaxCallFromPmsPaperGateAllowed "Direct LMAX from PMS-PAPER must be blocked."
Assert-False $contract.CrossRailExecAlgoHandoffAllowedNow "Cross-rail handoff must not be allowed now."
Assert-False $contract.TheoreticalExposurePreviewIsPnl "Theoretical exposure preview must not be labeled PnL."
foreach ($category in @("target notional preview", "pre-trade theoretical exposure", "fill-based realized PnL", "mark-to-market unrealized PnL", "transaction cost / spread / slippage attribution", "FX conversion")) {
    if ($contract.PnlCategories -notcontains $category) {
        throw "Missing PnL category $category."
    }
}
foreach ($input in @("fills from Exec Algo sandbox or later live rail", "execution reports", "order/route correlation IDs", "fill timestamps", "fill prices", "fill quantities", "side", "symbol", "venue", "current positions", "prior positions", "realized cost basis", "mark prices", "FX conversion rates", "transaction costs / spread / commissions", "slippage model", "broker/account/currency context")) {
    if ($contract.RequiredFutureInputs -notcontains $input) {
        throw "Missing future PnL input $input."
    }
}

if ($capability.Status -ne "CurrentGatePnlBlocked") {
    throw "Current gate PnL capability must be blocked."
}
Assert-False $capability.CurrentGateHasOrders "Current gate must have no orders."
Assert-False $capability.CurrentGateHasRoutes "Current gate must have no routes."
Assert-False $capability.CurrentGateHasFills "Current gate must have no fills."
Assert-False $capability.CurrentGateHasExecutionReports "Current gate must have no execution reports."
Assert-False $capability.CurrentGateHasBrokerPositions "Current gate must have no broker positions."
Assert-False $capability.CurrentGateHasLiveMarks "Current gate must have no live marks."
Assert-False $capability.ProductionPnlAvailable "Production PnL must not be available."
Assert-False $capability.FillBasedPnlAvailable "Fill-based PnL must not be available."
Assert-False $capability.RealizedPnlAvailable "Realized PnL must not be available."
Assert-False $capability.UnrealizedPnlAvailable "Unrealized PnL must not be available."
Assert-False $capability.CurrentGatePnlAllowed "Current gate PnL must not be allowed."

if ($exposure.Status -ne "PreviewOnly") {
    throw "Theoretical exposure preview must be PreviewOnly."
}
Assert-True $exposure.NotPnl "Theoretical exposure preview must be NotPnl."
Assert-True $exposure.NoFills "Theoretical exposure preview must have no fills."
Assert-True $exposure.NoMarks "Theoretical exposure preview must have no marks."
Assert-True $exposure.NoBrokerPosition "Theoretical exposure preview must have no broker position."
Assert-False $exposure.ExecutionAllowed "Theoretical exposure preview must not allow execution."
if ($exposure.Rows.Count -ne 3) {
    throw "Expected three theoretical exposure rows."
}
foreach ($row in $exposure.Rows) {
    foreach ($field in @("Symbol", "TargetNotional", "CurrentNotional", "DeltaNotional")) {
        if (-not $row.PSObject.Properties[$field]) {
            throw "Theoretical exposure row missing $field."
        }
    }
}

if ($fillContract.Status -ne "DraftInactive") {
    throw "Future fill-based PnL contract must be draft inactive."
}
Assert-False $fillContract.FillBasedPnlAllowedNow "Fill-based PnL must not be allowed now."
Assert-False $fillContract.DirectLmaxCallFromPmsPaperGateAllowed "Fill PnL contract must not allow direct LMAX from PMS-PAPER."
Assert-False $fillContract.CrossRailExecAlgoHandoffAllowedNow "Fill PnL contract must not allow cross-rail handoff now."

if ($mtmContract.Status -ne "DraftInactive") {
    throw "Future MTM PnL contract must be draft inactive."
}
Assert-False $mtmContract.MtmPnlAllowedNow "MTM PnL must not be allowed now."
Assert-False $mtmContract.ProductionPnlAllowedNow "Production PnL must not be allowed now."
Assert-False $mtmContract.DirectLmaxCallFromPmsPaperGateAllowed "MTM PnL contract must not allow direct LMAX from PMS-PAPER."
Assert-False $mtmContract.CrossRailExecAlgoHandoffAllowedNow "MTM PnL contract must not allow cross-rail handoff now."

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
Assert-True $audit.NoExecAlgoGateExecuted "No Exec Algo gate may execute in R014."

if ($evidence.R013Review.ReadOnlyReview -ne $true) {
    throw "Evidence must record read-only R013 review."
}
if ($evidence.R013Review.ManualPaperCycleRerun -ne $false) {
    throw "Evidence must record no ManualPaperCycle rerun."
}

$executionSim = Join-Path $repo "artifacts/readiness/execution-sim"
if (Test-Path -LiteralPath $executionSim) {
    $r014ExecutionSim = Get-ChildItem -LiteralPath $executionSim -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*pms-paper-r014*" -or $_.Name -like "*phase-pms-paper-r014*" -or $_.FullName -like "*pms-paper-r014*" }
    if ($r014ExecutionSim) {
        throw "R014 artifacts were found under execution-sim."
    }
}

if ($next.RecommendedNextGate -notlike "PMS-PAPER-R015*") {
    throw "Next gate must be an R015 PMS paper gate."
}

"PMS-PAPER-R014 validator passed."

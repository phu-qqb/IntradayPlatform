$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo "artifacts/readiness/pms-paper"

function Read-Json($name) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R011 file: $name"
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

$summaryPath = Join-Path $root "phase-pms-paper-r011-summary.md"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Missing required R011 summary."
}
if (-not (Test-Path -LiteralPath (Join-Path $root "phase-pms-paper-r010-target-current-delta-continuity.json"))) {
    throw "R010 reference is missing."
}
if (-not (Test-Path -LiteralPath (Join-Path $root "r010-manual-noexternal-delta-fields/phase-pms-ems-oms-manual-noexternal-paper-execution-plan-lines.json"))) {
    throw "R010 output lines reference is missing."
}

$contract = Read-Json "phase-pms-paper-r011-oms-intent-contract.json"
$reference = Read-Json "phase-pms-paper-r011-r010-output-reference.json"
$preview = Read-Json "phase-pms-paper-r011-oms-intent-preview-validation.json"
$identity = Read-Json "phase-pms-paper-r011-oms-intent-identity-continuity.json"
$boundary = Read-Json "phase-pms-paper-r011-ems-fix-lmax-boundary-audit.json"
$audit = Read-Json "phase-pms-paper-r011-no-order-no-route-no-fill-audit.json"
$evidence = Read-Json "phase-pms-paper-r011-build-test-validator-evidence.json"
$next = Read-Json "phase-pms-paper-r011-next-gate-plan.json"

if ($contract.Status -ne "OmsIntentPreviewContractDefined") {
    throw "OMS intent preview contract must be defined."
}
Assert-True $contract.NotQubesEconomicOutputRequired "NotQubesEconomicOutput must be required."
Assert-False $contract.ExecutionAllowed "Contract must not allow execution."
Assert-False $contract.OrderCreationAllowed "Contract must not allow order creation."
Assert-False $contract.RouteCreationAllowed "Contract must not allow route creation."
Assert-False $contract.FillCreationAllowed "Contract must not allow fill creation."

foreach ($field in @("IntentPreviewId", "CycleRunId", "SourceLineId", "SourcePreviewLineId", "SourceClassification", "PreviewLineReason", "Symbol", "TargetWeight", "CurrentWeight", "DeltaWeight", "TargetNotional", "CurrentNotional", "DeltaNotional", "Direction", "ArithmeticValid", "NotAnOrder", "NotSubmitted", "NoBrokerRoute", "NoFixMessage", "NoExecutableSchedule", "NoFill", "NoRoute", "NoSubmission", "ExecutionAllowed", "OmsIntentExecutable", "EmsRouteAllowed", "LmaxAllowed")) {
    if ($contract.RequiredFields -notcontains $field) {
        throw "Contract is missing required field $field."
    }
}

Assert-True $reference.ReadOnlyReview "R011 should review R010 output read-only."
Assert-False $reference.ManualPaperCycleRerun "R011 must not rerun ManualPaperCycle."
if ($reference.R010CommandStatus -ne "CompletedNoExternal") {
    throw "R010 command status must be CompletedNoExternal."
}
if ($reference.R010ManualNoExternalStatus -ne "ExecutedOnce") {
    throw "R010 ManualNoExternal status must be ExecutedOnce."
}

if ($preview.Status -ne "ValidatedOmsIntentPreviewContract") {
    throw "OMS intent preview contract must be validated."
}
if ($preview.PreviewLineCount -ne 3) {
    throw "R011 expected exactly three preview lines."
}
Assert-True $preview.AllNonExecutable "All intent previews must be non-executable."
Assert-True $preview.AllArithmeticValid "All intent previews must be arithmetically valid."
Assert-True $preview.AllNotAnOrder "All intent previews must be NotAnOrder."
Assert-True $preview.AllNotSubmitted "All intent previews must be NotSubmitted."
Assert-True $preview.AllNoBrokerRoute "All intent previews must be NoBrokerRoute."
Assert-True $preview.AllNoFixMessage "All intent previews must be NoFixMessage."
Assert-True $preview.AllNoExecutableSchedule "All intent previews must be NoExecutableSchedule."
Assert-True $preview.AllNoFill "All intent previews must be NoFill."
Assert-True $preview.AllNoRoute "All intent previews must be NoRoute."
Assert-True $preview.AllNoSubmission "All intent previews must be NoSubmission."
Assert-False $preview.ExecutionAllowed "Preview must not allow execution."
Assert-False $preview.QubesZeroOnlyMarkedPmsApproved "Qubes ZeroOnly must not be PMS-approved."
Assert-True $preview.SyntheticFixtureNotQubesEconomicOutput "Synthetic fixture must remain not Qubes economic output."

$requiredLineFields = @("IntentPreviewId", "CycleRunId", "SourceLineId", "SourcePreviewLineId", "SourceClassification", "PreviewLineReason", "Symbol", "TargetWeight", "CurrentWeight", "DeltaWeight", "TargetNotional", "CurrentNotional", "DeltaNotional", "Direction", "ArithmeticValid", "NotAnOrder", "NotSubmitted", "NoBrokerRoute", "NoFixMessage", "NoExecutableSchedule", "NoFill", "NoRoute", "NoSubmission", "ExecutionAllowed", "OmsIntentExecutable", "EmsRouteAllowed", "LmaxAllowed")
foreach ($line in $preview.Lines) {
    foreach ($field in $requiredLineFields) {
        if (-not $line.PSObject.Properties[$field]) {
            throw "Intent preview line missing field $field."
        }
    }
    Assert-True $line.ArithmeticValid "Each intent preview must be arithmetic-valid."
    Assert-True $line.NotAnOrder "Each intent preview must be NotAnOrder."
    Assert-True $line.NotSubmitted "Each intent preview must be NotSubmitted."
    Assert-True $line.NoBrokerRoute "Each intent preview must be NoBrokerRoute."
    Assert-True $line.NoFixMessage "Each intent preview must be NoFixMessage."
    Assert-True $line.NoExecutableSchedule "Each intent preview must be NoExecutableSchedule."
    Assert-True $line.NoFill "Each intent preview must be NoFill."
    Assert-True $line.NoRoute "Each intent preview must be NoRoute."
    Assert-True $line.NoSubmission "Each intent preview must be NoSubmission."
    Assert-False $line.ExecutionAllowed "Each intent preview must not allow execution."
    Assert-False $line.OmsIntentExecutable "Each intent preview must not be executable."
    Assert-False $line.EmsRouteAllowed "Each intent preview must not allow EMS route."
    Assert-False $line.LmaxAllowed "Each intent preview must not allow LMAX."
    if (@("Increase", "Decrease", "NoChange") -notcontains $line.Direction) {
        throw "Invalid direction $($line.Direction)."
    }
}

if ($identity.Status -ne "ValidatedIntentIdentityContinuity") {
    throw "Intent identity continuity must be validated."
}
Assert-False $identity.DuplicateIntentPreviewIds "IntentPreviewIds must be unique."
$ids = @($identity.IntentPreviewIds)
if ($ids.Count -ne 3) {
    throw "Expected exactly three IntentPreviewIds."
}
if (($ids | Select-Object -Unique).Count -ne $ids.Count) {
    throw "Duplicate IntentPreviewIds found."
}

Assert-False $boundary.EmsAllowed "EMS must remain blocked."
Assert-False $boundary.FixSessionAllowed "FIX session must remain blocked."
Assert-False $boundary.LmaxLiveAllowed "LMAX live must remain blocked."
Assert-False $boundary.RouteGenerationAllowed "Route generation must remain blocked."
Assert-False $boundary.ExecutionAllowed "Execution must remain blocked."
Assert-False $boundary.BrokerSubmissionAllowed "Broker submission must remain blocked."
Assert-False $boundary.OmsIntentExecutable "OMS intent must remain non-executable."
Assert-False $boundary.EmsRouteAllowed "EMS route must remain blocked."
Assert-True $boundary.NoFixMessage "No FIX message may be produced."

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
Assert-True $audit.NoExecAlgoGate "R011 must not be an Exec Algo gate."
Assert-False $audit.QubesZeroOnlyMarkedPmsApproved "Qubes ZeroOnly must not be PMS-approved."
Assert-False $audit.SyntheticFixtureMislabeledAsQubesModelOutput "Synthetic fixture must not be mislabeled as Qubes model output."

if ($evidence.R010Review.ReadOnlyReview -ne $true) {
    throw "Evidence must record read-only R010 review."
}
if ($evidence.R010Review.ManualPaperCycleRerun -ne $false) {
    throw "Evidence must record no ManualPaperCycle rerun."
}

$executionSim = Join-Path $repo "artifacts/readiness/execution-sim"
if (Test-Path -LiteralPath $executionSim) {
    $r011ExecutionSim = Get-ChildItem -LiteralPath $executionSim -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*pms-paper-r011*" -or $_.Name -like "*phase-pms-paper-r011*" -or $_.FullName -like "*pms-paper-r011*" }
    if ($r011ExecutionSim) {
        throw "R011 artifacts were found under execution-sim."
    }
}

if ($next.RecommendedNextGate -notlike "PMS-PAPER-R012*") {
    throw "Next gate must be an R012 PMS paper gate."
}

"PMS-PAPER-R011 validator passed."

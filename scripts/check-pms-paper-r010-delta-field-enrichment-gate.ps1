$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo "artifacts/readiness/pms-paper"

function Read-Json($name) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R010 file: $name"
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

$summaryPath = Join-Path $root "phase-pms-paper-r010-summary.md"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Missing required R010 summary."
}
if (-not (Test-Path -LiteralPath (Join-Path $root "phase-pms-paper-r009-target-current-delta-continuity.json"))) {
    throw "R009 reference is missing."
}

$contract = Read-Json "phase-pms-paper-r010-schema-enrichment-contract.json"
$command = Read-Json "phase-pms-paper-r010-manual-no-external-command.json"
$inventory = Read-Json "phase-pms-paper-r010-enriched-paper-cycle-output-inventory.json"
$delta = Read-Json "phase-pms-paper-r010-target-current-delta-continuity.json"
$preview = Read-Json "phase-pms-paper-r010-non-executable-oms-preview-validation.json"
$boundary = Read-Json "phase-pms-paper-r010-ems-fix-lmax-boundary-audit.json"
$audit = Read-Json "phase-pms-paper-r010-no-order-no-route-no-fill-audit.json"
$evidence = Read-Json "phase-pms-paper-r010-build-test-validator-evidence.json"
$next = Read-Json "phase-pms-paper-r010-next-gate-plan.json"

if ($contract.Status -ne "SchemaEnrichmentContractDefined") {
    throw "Schema enrichment contract must be defined."
}
Assert-True $contract.NotQubesEconomicOutputRequired "NotQubesEconomicOutput must be required."
Assert-False $contract.ExecutionAllowed "Schema contract must not allow execution."

if ($command.Status -ne "ExecutedOnce") {
    throw "R010 command must be executed once."
}
if ($command.ExitCode -ne 0) {
    throw "R010 command exit code must be zero."
}
Assert-True $command.RanAtMostOnce "ManualNoExternal command must run at most once."
Assert-True $command.NoExternal "Command must be no-external."
Assert-True $command.NoOrder "Command must include no-order."
Assert-True $command.NoRoute "Command must include no-route."
Assert-True $command.NoFill "Command must include no-fill."
Assert-True $command.NoBroker "Command must include no-broker."
Assert-True $command.NoFix "Command must include no-FIX."
Assert-True $command.NoExecutableSchedule "Command must include no executable schedule."
Assert-True $command.NoLiveStateMutation "Command must include no live state mutation."
Assert-True $command.NoLedgerCommit "Command must include no ledger commit."

Assert-True $inventory.ReadinessOnly "Output inventory must be readiness-only."
Assert-False $inventory.ExecutableArtifactsPresent "Executable artifacts must not be present."
if ($inventory.Files.Count -lt 1) {
    throw "Expected R010 output inventory files."
}

if ($delta.Status -ne "ValidatedDeltaContinuity") {
    throw "Delta continuity must be validated."
}
Assert-True $delta.AllArithmeticValid "All arithmetic must be valid."
if ($delta.MissingFields.Count -ne 0) {
    throw "Delta continuity cannot be valid while missing required fields."
}
foreach ($row in $delta.Rows) {
    foreach ($field in @("TargetWeight", "CurrentWeight", "DeltaWeight", "TargetNotional", "CurrentNotional", "DeltaNotional", "PreviewLineReason")) {
        if (-not $row.PSObject.Properties[$field]) {
            throw "Delta row missing field $field."
        }
    }
    Assert-True $row.ArithmeticValid "Each delta row must be arithmetically valid."
}

if ($preview.Status -ne "ValidatedNonExecutableOmsPreview") {
    throw "OMS preview must be validated."
}
if ($preview.PreviewLineCount -ne 3) {
    throw "R010 expected exactly three preview lines."
}
Assert-True $preview.AllNotAnOrder "All preview lines must be NotAnOrder."
Assert-True $preview.AllNotSubmitted "All preview lines must be NotSubmitted."
Assert-True $preview.AllNoBrokerRoute "All preview lines must be NoBrokerRoute."
Assert-True $preview.AllNoExecutableSchedule "All preview lines must be NoExecutableSchedule."
Assert-True $preview.AllNoFill "All preview lines must be NoFill."
Assert-True $preview.AllNoRoute "All preview lines must be NoRoute."
Assert-True $preview.AllNoSubmission "All preview lines must be NoSubmission."
Assert-True $preview.AllNoFixMessage "All preview lines must be NoFixMessage."
Assert-False $preview.ExecutionAllowed "OMS preview must not allow execution."

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
Assert-True $audit.NoExecAlgoGate "R010 must not be an Exec Algo gate."
Assert-False $audit.QubesZeroOnlyMarkedPmsApproved "Qubes ZeroOnly must not be PMS-approved."
Assert-False $audit.SyntheticFixtureMislabeledAsQubesModelOutput "Synthetic fixture must not be mislabeled as Qubes model output."

$executionSim = Join-Path $repo "artifacts/readiness/execution-sim"
if (Test-Path -LiteralPath $executionSim) {
    $r010ExecutionSim = Get-ChildItem -LiteralPath $executionSim -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*pms-paper-r010*" -or $_.Name -like "*phase-pms-paper-r010*" -or $_.FullName -like "*pms-paper-r010*" }
    if ($r010ExecutionSim) {
        throw "R010 artifacts were found under execution-sim."
    }
}

if ($next.RecommendedNextGate -notlike "PMS-PAPER-R011*") {
    throw "Next gate must be an R011 PMS paper gate."
}

"PMS-PAPER-R010 validator passed."

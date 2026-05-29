$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo "artifacts/readiness/pms-paper"
$expectedFixtureHash = "294987DE89DCA26A56FD70278FCA2B77D085BE08D7F460626CB362163C69B5A2"
$fixture = "C:\Users\phili\AppData\Local\Temp\q5aj\polygon-richer-eurusd-h1-202512220000-202601092100\10_validation\synthetic-pms-fixture\synthetic-pms-weights-v0.txt"

function Read-Json($name) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R008 file: $name"
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

if (-not (Test-Path -LiteralPath $fixture)) {
    throw "Core synthetic fixture is missing."
}
$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fixture).Hash
if ($actualHash -ne $expectedFixtureHash) {
    throw "Fixture hash mismatch: $actualHash"
}

$contract = Read-Json "phase-pms-paper-r008-synthetic-fixture-adapter-contract.json"
$validation = Read-Json "phase-pms-paper-r008-synthetic-fixture-adapter-validation.json"
$command = Read-Json "phase-pms-paper-r008-manual-no-external-command.json"
$inventory = Read-Json "phase-pms-paper-r008-paper-cycle-output-inventory.json"
$preview = Read-Json "phase-pms-paper-r008-pms-oms-paper-preview-result.json"
$boundary = Read-Json "phase-pms-paper-r008-ems-fix-lmax-boundary-audit.json"
$audit = Read-Json "phase-pms-paper-r008-no-order-no-route-no-fill-audit.json"
$evidence = Read-Json "phase-pms-paper-r008-build-test-validator-evidence.json"
$next = Read-Json "phase-pms-paper-r008-next-gate-plan.json"

$summaryPath = Join-Path $root "phase-pms-paper-r008-summary.md"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Missing required R008 summary."
}

if ($contract.Status -ne "AdapterContractDefined") {
    throw "Adapter contract must be defined."
}
if ($contract.InputFormat -ne "CanonicalModelSymbol;WeightDecimalF8") {
    throw "Adapter input format mismatch."
}
Assert-True $contract.NotQubesEconomicOutput "Adapter contract must remain NotQubesEconomicOutput."
Assert-True $contract.PaperOnly "Adapter contract must remain PaperOnly."
Assert-True $contract.NonExecutable "Adapter contract must remain NonExecutable."
Assert-False $contract.QubesZeroOnlyAcceptedAsPmsInput "Qubes ZeroOnly must not be accepted as PMS input."
Assert-False $contract.ExecutionAllowed "Adapter contract must not allow execution."

if ($validation.Status -ne "AdapterValidated") {
    throw "Adapter validation must pass."
}
if ($validation.FixtureSha256 -ne "sha256:$expectedFixtureHash") {
    throw "Adapter validation fixture hash mismatch."
}
Assert-True $validation.NotQubesEconomicOutput "Adapter validation must remain NotQubesEconomicOutput."
if ($validation.ValidationFailures.Count -ne 0) {
    throw "Adapter validation has failures."
}

if ($command.Status -ne "ExecutedOnce") {
    throw "R008 command must be executed once."
}
if ($command.ExitCode -ne 0) {
    throw "R008 command exit code must be zero."
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
    throw "Expected R008 output inventory files."
}

if ($preview.Status -ne "PaperPreviewValidated") {
    throw "PMS to OMS preview must be validated."
}
Assert-True $preview.NonExecutableIntentPreviewAvailable "Non-executable preview must be available."
Assert-False $preview.ExecutionAllowed "PMS/OMS preview must not allow execution."
Assert-True $preview.NotAnOrder "PMS/OMS preview must preserve NotAnOrder."
Assert-True $preview.NotSubmitted "PMS/OMS preview must preserve NotSubmitted."
Assert-True $preview.NoBrokerRoute "PMS/OMS preview must preserve NoBrokerRoute."
Assert-True $preview.NoFixMessage "PMS/OMS preview must preserve NoFixMessage."

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
Assert-True $audit.NoExecAlgoGate "R008 must not be an Exec Algo gate."
Assert-False $audit.QubesZeroOnlyMarkedPmsApproved "Qubes ZeroOnly must not be PMS-approved."
Assert-False $audit.SyntheticFixtureMislabeledAsQubesModelOutput "Synthetic fixture must not be mislabeled as Qubes model output."

$executionSim = Join-Path $repo "artifacts/readiness/execution-sim"
if (Test-Path -LiteralPath $executionSim) {
    $r008ExecutionSim = Get-ChildItem -LiteralPath $executionSim -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*pms-paper-r008*" -or $_.Name -like "*phase-pms-paper-r008*" -or $_.FullName -like "*pms-paper-r008*" }
    if ($r008ExecutionSim) {
        throw "R008 artifacts were found under execution-sim."
    }
}

if ($next.RecommendedNextGate -notlike "PMS-PAPER-R009*") {
    throw "Next gate must be an R009 PMS paper gate."
}

"PMS-PAPER-R008 validator passed."

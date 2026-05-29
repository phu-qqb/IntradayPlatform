$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo "artifacts/readiness/pms-paper"
$expectedFixtureHash = "294987DE89DCA26A56FD70278FCA2B77D085BE08D7F460626CB362163C69B5A2"
$fixture = "C:\Users\phili\AppData\Local\Temp\q5aj\polygon-richer-eurusd-h1-202512220000-202601092100\10_validation\synthetic-pms-fixture\synthetic-pms-weights-v0.txt"

function Read-Json($name) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R007 file: $name"
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

$reference = Read-Json "phase-pms-paper-r007-core-synthetic-fixture-reference.json"
$command = Read-Json "phase-pms-paper-r007-manual-no-external-command.json"
$inventory = Read-Json "phase-pms-paper-r007-paper-cycle-output-inventory.json"
$preview = Read-Json "phase-pms-paper-r007-pms-oms-paper-preview-result.json"
$boundary = Read-Json "phase-pms-paper-r007-ems-fix-lmax-boundary-audit.json"
$audit = Read-Json "phase-pms-paper-r007-no-order-no-route-no-fill-audit.json"
$evidence = Read-Json "phase-pms-paper-r007-build-test-validator-evidence.json"
$next = Read-Json "phase-pms-paper-r007-next-gate-plan.json"

$summaryPath = Join-Path $root "phase-pms-paper-r007-summary.md"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Missing required R007 summary."
}

Assert-True $reference.NotQubesEconomicOutput "Core fixture must remain NotQubesEconomicOutput."
Assert-True $reference.PaperOnly "Core fixture must remain PaperOnly."
Assert-True $reference.NonExecutable "Core fixture must remain NonExecutable."
Assert-False $reference.QubesZeroOnlyMarkedPmsApproved "Qubes ZeroOnly must not be PMS-approved."

if ($command.Status -ne "ExecutedOnce") {
    throw "R007 command status must record the single attempted execution."
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

Assert-False $inventory.ExecutableArtifactsPresent "Executable artifacts must not be present."
Assert-False $preview.ExecutionAllowed "PMS/OMS preview must not allow execution."
Assert-True $preview.NotAnOrder "PMS/OMS preview boundary must preserve NotAnOrder."
Assert-True $preview.NotSubmitted "PMS/OMS preview boundary must preserve NotSubmitted."
Assert-True $preview.NoBrokerRoute "PMS/OMS preview boundary must preserve NoBrokerRoute."
Assert-True $preview.NoFixMessage "PMS/OMS preview boundary must preserve NoFixMessage."

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
Assert-True $audit.NoExecAlgoGate "R007 must not be an Exec Algo gate."
Assert-False $audit.QubesZeroOnlyMarkedPmsApproved "Qubes ZeroOnly must not be PMS-approved."
Assert-False $audit.SyntheticFixtureMislabeledAsQubesModelOutput "Synthetic fixture must not be mislabeled as Qubes model output."

$executionSim = Join-Path $repo "artifacts/readiness/execution-sim"
if (Test-Path -LiteralPath $executionSim) {
    $r007ExecutionSim = Get-ChildItem -LiteralPath $executionSim -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*pms-paper-r007*" -or $_.Name -like "*phase-pms-paper-r007*" -or $_.FullName -like "*pms-paper-r007*" }
    if ($r007ExecutionSim) {
        throw "R007 artifacts were found under execution-sim."
    }
}

if ($next.RecommendedNextGate -notlike "PMS-PAPER-R008*") {
    throw "Next gate must be an R008 PMS paper gate."
}

"PMS-PAPER-R007 validator passed."

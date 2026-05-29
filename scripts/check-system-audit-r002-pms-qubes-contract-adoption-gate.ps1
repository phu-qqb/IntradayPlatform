$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo "artifacts/readiness/pms-qubes-system-audit"

function Read-Json($name) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R016 file: $name"
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

$summaryPath = Join-Path $root "system-audit-r002-adoption-summary.md"
if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "Missing required R016 summary."
}

$index = Read-Json "system-audit-r002-contract-index.json"
$qubes = Read-Json "qubes-output-v1-adoption.json"
$pms = Read-Json "pms-handoff-v1-adoption.json"
$timing = Read-Json "canonical-timing-v1-adoption.json"
$risk = Read-Json "risk-control-v1-adoption.json"
$unsupported = Read-Json "unsupported-instrument-diagnostics.json"
$side = Read-Json "exec-algo-side-derivation-diagnostics.json"
$usd = Read-Json "usd-pair-execution-handoff-constraints.json"
$direct = Read-Json "no-direct-cross-execution-audit.json"
$audit = Read-Json "no-external-no-execution-audit.json"
$evidence = Read-Json "build-test-validator-evidence.json"
$next = Read-Json "next-gate-plan.json"

$requiredContracts = @(
    "qubes-output.v1",
    "pms-handoff.v1",
    "canonical-timing.v1",
    "risk-control.v1",
    "execution-intent.v1",
    "marketdata-readiness.v1",
    "lmax-marketdata-db.v1",
    "r009-sandbox-execution.v1",
    "oms-sandbox-state-model.v1",
    "paper-ledger-separation.v1",
    "environment-secret.v1"
)
foreach ($contractId in $requiredContracts) {
    if (-not ($index.ContractIds | Where-Object { $_.ContractId -eq $contractId })) {
        throw "Missing contract id $contractId."
    }
}

if ($qubes.ContractId -ne "qubes-output.v1") {
    throw "qubes-output contract id mismatch."
}
if ($qubes.Status -ne "AdoptedWithWarnings" -and $qubes.Status -ne "Adopted" -and $qubes.Status -ne "BlockedMissingEvidence") {
    throw "Invalid qubes-output adoption status."
}
Assert-True $qubes.DirectCrossSignalOnly "Qubes direct cross signal-only flag must be true."
Assert-True $qubes.RequiresNetting "Qubes requires netting."
Assert-False $qubes.CurrentQubesBranchHandoffEligible "Current Qubes ZeroOnly branch must not be handoff eligible."
Assert-False $qubes.NonZeroQubesModelBehaviorValidated "Non-zero Qubes model behavior must not be validated."
Assert-False $qubes.EconomicNettingBehaviorValidated "Economic netting behavior must not be validated."
if ($qubes.ManagerWeightsProfile -ne "ZeroOnly") {
    throw "ManagerWeightsProfile must be ZeroOnly."
}

if ($pms.ContractId -ne "pms-handoff.v1") {
    throw "pms-handoff contract id mismatch."
}
if ($pms.HandoffReadinessClassification -ne "HandoffContractDraftOnly") {
    throw "PMS-PAPER-R015 handoff readiness must be HandoffContractDraftOnly."
}
Assert-True $pms.PmsPaperSourceFieldsComplete "PMS-PAPER source fields must be complete."
Assert-True $pms.SyntheticFixtureNotQubesEconomicOutput "Synthetic fixture must remain not Qubes economic output."
Assert-True $pms.QubesZeroOnlyNotApproved "Qubes ZeroOnly must not be PMS-approved."
if ($pms.ExecAlgoSideDerivationStatus -ne "MissingExecAlgoSide") {
    throw "Exec Algo side must be MissingExecAlgoSide unless evidence is available."
}
if ($pms.PmsAccountId -ne $null -or $pms.PortfolioId -ne $null -or $pms.RiskReviewId -ne $null -or $pms.OperatorApprovalId -ne $null) {
    throw "Account/portfolio/risk/operator IDs must not be invented."
}

if ($timing.ContractId -ne "canonical-timing.v1") {
    throw "canonical timing contract id mismatch."
}
if ($timing.CanonicalQuarterHourCloses.Count -ne 4) {
    throw "Canonical quarter-hour closes must contain four entries."
}
foreach ($minute in @(":00", ":15", ":30", ":45")) {
    if ($timing.CanonicalQuarterHourCloses -notcontains $minute) {
        throw "Missing canonical minute $minute."
    }
}
foreach ($legacy in @(":06", ":21", ":36", ":51")) {
    if ($timing.LegacyOffsetsCompatibilityOnly -notcontains $legacy) {
        throw "Missing legacy compatibility offset $legacy."
    }
}
Assert-False $timing.LegacyFutureCanonicalTimingUsed "Legacy :06/:21/:36/:51 must not be future canonical timing."
if ($timing.CanonicalTargetCloseUtc -notmatch ":(00|15|30|45):00Z$") {
    throw "Canonical target close must be a quarter-hour UTC close."
}

if ($risk.ContractId -ne "risk-control.v1") {
    throw "risk-control contract id mismatch."
}
$expectedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
if ($risk.SupportedExecutionSymbols.Count -ne $expectedSymbols.Count) {
    throw "Supported execution symbols count mismatch."
}
foreach ($symbol in $expectedSymbols) {
    if ($risk.SupportedExecutionSymbols -notcontains $symbol) {
        throw "Missing supported execution symbol $symbol."
    }
}
Assert-False $risk.DirectCrossExecutionAllowed "Direct-cross execution must be disabled."
Assert-True $risk.DirectCrossSignalOnly "Direct crosses must be signal-only."
Assert-True $risk.RequiresNetting "Direct crosses must require netting."
Assert-True $risk.UsdPairOnlyExecutionHandoff "USD-pair-only handoff must be true."
Assert-False $risk.ProductionLiveAllowed "Production/live must be blocked."
if (-not $risk.UsdjpyCaveat -or $risk.UsdjpyCaveat.NormalizedPortfolioSymbol -ne "JPYUSD" -or $risk.UsdjpyCaveat.ExecutionTradableSymbol -ne "USDJPY" -or $risk.UsdjpyCaveat.RequiresInversion -ne $true -or $risk.UsdjpyCaveat.SecurityID -ne 4004 -or $risk.UsdjpyCaveat.SecurityIDSource -ne 8) {
    throw "USDJPY caveat is missing or incomplete."
}

Assert-False $unsupported.UnsupportedInstrumentsMarkedExecutable "Unsupported instruments must not be executable."
Assert-False $direct.DirectCrossExecutionAllowed "Direct-cross execution must be blocked."
Assert-False $direct.DirectCrossesSubmitted "Direct crosses must not be submitted."
Assert-True $direct.UnsupportedDirectCrossesHeldOrRequireNetting "Unsupported direct crosses must be held or require netting."
Assert-True $direct.NoOrderCreated "No direct-cross order may be created."
Assert-True $direct.NoRouteCreated "No direct-cross route may be created."
Assert-True $direct.NoFillCreated "No direct-cross fill may be created."

if ($side.Status -ne "MissingExecAlgoSide" -and $side.Status -ne "Derived" -and $side.Status -ne "NotApplicable") {
    throw "Invalid side derivation diagnostic status."
}
Assert-True $side.DoNotInventSide "Side must not be invented."
if ($side.Status -eq "MissingExecAlgoSide" -and $side.MissingFields.Count -lt 1) {
    throw "MissingExecAlgoSide must list missing fields."
}

Assert-True $usd.UsdPairOnly "USD-pair-only constraint must be true."
Assert-False $usd.DirectCrossExecutionAllowed "USD-pair handoff must block direct crosses."
Assert-True $usd.R009Selected "R009 must be selected as reference."
Assert-True $usd.LmaxSandboxLifecycleValidated "LMAX sandbox lifecycle must be reference-validated."
Assert-False $usd.ProductionLiveAllowed "Production/live must remain blocked."
if (-not $usd.UsdjpyCaveat -or $usd.UsdjpyCaveat.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usd.UsdjpyCaveat.ExecutionTradableSymbol -ne "USDJPY") {
    throw "USDJPY caveat missing from USD-pair constraints."
}

if ($audit.Status -ne "Passed") {
    throw "No-external/no-execution audit must pass."
}
Assert-True $audit.NoLmaxCall "No LMAX call may occur."
Assert-True $audit.NoFixSession "No FIX session may occur."
Assert-True $audit.NoPolygonMassiveCall "No Polygon/Massive call may occur."
Assert-True $audit.NoSqlMutation "No SQL mutation may occur."
Assert-True $audit.NoOrdersCreated "No orders may be created."
Assert-True $audit.NoFillsCreated "No fills may be created."
Assert-True $audit.NoRoutesCreated "No routes may be created."
Assert-True $audit.NoSchedulesCreated "No schedules may be created."
Assert-True $audit.NoBrokerSubmission "No broker submission may occur."
Assert-True $audit.NoLiveTradingStateMutation "No live trading state mutation may occur."
Assert-True $audit.NoQubesExecutableRun "No Qubes executable may run."
Assert-True $audit.NoNettedUsdWeightsProduced "No NettedUsdWeights may be produced."
Assert-True $audit.NoProductionPromotion "No production/live promotion may occur."

if ($next.RecommendedNextGate -notlike "Cross-rail ExecAlgoSandboxHandoffGate*") {
    throw "Unexpected next gate."
}

"PMS-QUBES-R016 validator passed."

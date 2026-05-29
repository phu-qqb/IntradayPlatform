param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R001 validation failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required file: $Path"
    }

    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Assert-False($Value, [string]$Name) {
    if ($true -eq $Value) {
        Fail "$Name must be false"
    }
}

function Assert-True($Value, [string]$Name) {
    if ($true -ne $Value) {
        Fail "$Name must be true"
    }
}

$artifactRoot = Join-Path $RepoRoot "artifacts\readiness\cross-rail-sandbox-handoff"
$requiredFiles = @(
    "cross-rail-r001-summary.md",
    "cross-rail-r001-pms-paper-source-reference.json",
    "cross-rail-r001-system-audit-contract-reference.json",
    "cross-rail-r001-exec-r009-sandbox-reference.json",
    "cross-rail-r001-field-completion-assessment.json",
    "cross-rail-r001-side-derivation-rules.json",
    "cross-rail-r001-broker-symbol-mapping.json",
    "cross-rail-r001-sandbox-quantity-model.json",
    "cross-rail-r001-r009-algo-parameter-plan.json",
    "cross-rail-r001-sandbox-order-candidate-plan.json",
    "cross-rail-r001-idempotency-correlation-plan.json",
    "cross-rail-r001-reconciliation-correlation-plan.json",
    "cross-rail-r001-pnl-correlation-plan.json",
    "cross-rail-r001-direct-cross-and-usdjpy-policy-audit.json",
    "cross-rail-r001-no-execution-safety-audit.json",
    "cross-rail-r001-build-test-validator-evidence.json",
    "cross-rail-r001-next-gate-plan.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $artifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required R001 artifact: $file"
    }
}

$pms = Read-Json (Join-Path $artifactRoot "cross-rail-r001-pms-paper-source-reference.json")
$system = Read-Json (Join-Path $artifactRoot "cross-rail-r001-system-audit-contract-reference.json")
$exec = Read-Json (Join-Path $artifactRoot "cross-rail-r001-exec-r009-sandbox-reference.json")
$field = Read-Json (Join-Path $artifactRoot "cross-rail-r001-field-completion-assessment.json")
$side = Read-Json (Join-Path $artifactRoot "cross-rail-r001-side-derivation-rules.json")
$mapping = Read-Json (Join-Path $artifactRoot "cross-rail-r001-broker-symbol-mapping.json")
$quantity = Read-Json (Join-Path $artifactRoot "cross-rail-r001-sandbox-quantity-model.json")
$algo = Read-Json (Join-Path $artifactRoot "cross-rail-r001-r009-algo-parameter-plan.json")
$candidatePlan = Read-Json (Join-Path $artifactRoot "cross-rail-r001-sandbox-order-candidate-plan.json")
$idempotency = Read-Json (Join-Path $artifactRoot "cross-rail-r001-idempotency-correlation-plan.json")
$recon = Read-Json (Join-Path $artifactRoot "cross-rail-r001-reconciliation-correlation-plan.json")
$pnl = Read-Json (Join-Path $artifactRoot "cross-rail-r001-pnl-correlation-plan.json")
$policy = Read-Json (Join-Path $artifactRoot "cross-rail-r001-direct-cross-and-usdjpy-policy-audit.json")
$safety = Read-Json (Join-Path $artifactRoot "cross-rail-r001-no-execution-safety-audit.json")

$supported = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
$actualSupported = @($exec.SupportedExecutionSymbols)
if (($actualSupported -join "|") -ne ($supported -join "|")) {
    Fail "Supported execution symbols must match the frozen USD-pair list exactly"
}

Assert-True $pms.PmsPaperR015Passed "PMS-PAPER-R015 reference"
if ($pms.HandoffReadinessClassification -ne "HandoffContractDraftOnly") {
    Fail "PMS-PAPER-R015 handoff readiness must be HandoffContractDraftOnly"
}
Assert-True $pms.SourceNonExecutable "PMS-PAPER source non-executable"
Assert-True $pms.NotQubesEconomicOutput "Synthetic fixture NotQubesEconomicOutput"
Assert-False $pms.CrossRailExecAlgoHandoffAllowedNow "CrossRailExecAlgoHandoffAllowedNow"

if ($system.MissingExecAlgoSideFromR016 -ne "MissingExecAlgoSide") {
    Fail "R016 MissingExecAlgoSide warning must be referenced"
}
Assert-True $system.QubesZeroOnlyNotPmsApproved "Qubes ZeroOnly not PMS-approved"
Assert-True $system.SyntheticFixtureNotQubesEconomicOutput "Synthetic fixture NotQubesEconomicOutput from system audit"
Assert-False $system.DirectCrossExecutionAllowed "DirectCrossExecutionAllowed"
Assert-False $system.LegacyFutureCanonicalTimingUsed "LegacyFutureCanonicalTimingUsed"

$targetClose = [datetimeoffset]::Parse($algo.CanonicalTargetClose)
if (@(0, 15, 30, 45) -notcontains $targetClose.Minute) {
    Fail "Canonical target close must use :00/:15/:30/:45"
}
if (@(6, 21, 36, 51) -contains $targetClose.Minute) {
    Fail "Legacy :06/:21/:36/:51 used as future canonical timing"
}
Assert-False $algo.TimingPolicy.LegacyFutureCanonicalTimingUsed "Algo plan legacy future canonical timing"

Assert-True $exec.R009Selected "R009Selected"
if ($exec.PrimaryAlgo -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") {
    Fail "Unexpected primary R009 algo"
}
if ($exec.SecondaryAlgo -ne "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0") {
    Fail "Unexpected secondary R009 algo"
}
if ($exec.ResidualModule -ne "ControlledResidualCross_BalancedResidualCross_v0") {
    Fail "Unexpected R009 residual module"
}
Assert-True $exec.SandboxLifecycleValidated "SandboxLifecycleValidated"
Assert-False $exec.ProductionLiveAllowed "ProductionLiveAllowed"
Assert-True $exec.CredentialValuesRedacted "CredentialValuesRedacted"

if ($field.Status -notin @("SandboxHandoffPreflightReady", "HandoffFieldCompletionIncomplete", "Blocked")) {
    Fail "Invalid field-completion status"
}
if ($field.Status -eq "SandboxHandoffPreflightReady" -and @($field.MissingFields).Count -gt 0) {
    Fail "Preflight cannot be ready while MissingFields are present"
}
foreach ($requiredMissing in @("MissingPaperAccount", "MissingRiskApprovalId", "MissingOperatorApprovalId")) {
    if (@($field.MissingFields) -notcontains $requiredMissing) {
        Fail "Expected missing field not recorded: $requiredMissing"
    }
}

if ($side.Status -ne "SideDerivationRulesDefined") {
    Fail "Side derivation rules were not defined"
}
Assert-True $side.DoNotInventSide "DoNotInventSide"
$derived = @{}
foreach ($row in @($side.PerSymbolDerivedSide)) {
    $derived[[string]$row.Symbol] = [string]$row.DerivedSide
    if ($true -eq $row.DirectCross -and $row.DerivedSide -ne "RequiresNettingOrHold") {
        Fail "Direct-cross row must be RequiresNettingOrHold"
    }
}
if ($derived["AUDUSD"] -ne "SELL" -or $derived["EURUSD"] -ne "SELL" -or $derived["GBPUSD"] -ne "BUY") {
    Fail "Unexpected derived sides for AUDUSD/EURUSD/GBPUSD"
}

if ($mapping.Status -ne "BrokerSymbolMappingDefined") {
    Fail "Broker symbol mapping was not defined"
}
Assert-False $mapping.DirectCrossExecutionAllowed "Broker mapping direct-cross execution"
foreach ($symbol in $supported) {
    if ($mapping.SupportedMappings.$symbol -ne $symbol) {
        Fail "Missing or invalid broker mapping for $symbol"
    }
}
if (-not $mapping.UsdjpyCaveat -or $true -ne $mapping.UsdjpyCaveat.RequiresInversion -or $mapping.UsdjpyCaveat.SecurityID -ne 4004) {
    Fail "USDJPY caveat missing or incomplete"
}

if ($quantity.Status -ne "SandboxQuantityModelDefined" -or [decimal]$quantity.Quantity -ne 0.1) {
    Fail "Sandbox quantity model must define quantity 0.1"
}
Assert-True $quantity.NotProductionSizing "NotProductionSizing"
Assert-True $quantity.NotRiskBasedSizing "NotRiskBasedSizing"

if ($candidatePlan.Status -ne "CandidatePlanOnly") {
    Fail "Sandbox order candidate plan must be CandidatePlanOnly"
}
Assert-False $candidatePlan.OrdersSubmittedNow "OrdersSubmittedNow"
Assert-False $candidatePlan.RoutesCreatedNow "RoutesCreatedNow"
Assert-False $candidatePlan.FillsCreatedNow "FillsCreatedNow"
Assert-False $candidatePlan.ExecutionAllowedNow "ExecutionAllowedNow"
Assert-True $candidatePlan.CandidatePlanIsNotAnOrder "CandidatePlanIsNotAnOrder"

foreach ($candidate in @($candidatePlan.Candidates)) {
    if (@($supported) -notcontains [string]$candidate.Symbol) {
        Fail "Unsupported symbol included as candidate: $($candidate.Symbol)"
    }
    Assert-False $candidate.ExecutionAllowedNow "Candidate ExecutionAllowedNow for $($candidate.Symbol)"
    Assert-False $candidate.OrdersSubmittedNow "Candidate OrdersSubmittedNow for $($candidate.Symbol)"
    Assert-True $candidate.SandboxOnly "Candidate SandboxOnly for $($candidate.Symbol)"
    Assert-True $candidate.NoLive "Candidate NoLive for $($candidate.Symbol)"
    Assert-True $candidate.NoActualOrderIdCreated "Candidate NoActualOrderIdCreated for $($candidate.Symbol)"
}

Assert-True $idempotency.NoActualOrderIdsCreated "NoActualOrderIdsCreated"
Assert-True $idempotency.FutureOrderIdRequired "FutureOrderIdRequired"
Assert-True $recon.NoFillsNow "NoFillsNow"
Assert-False $recon.ReconciliationExecutionAllowedNow "ReconciliationExecutionAllowedNow"
Assert-True $pnl.NoPnlNow "NoPnlNow"
Assert-True $pnl.NotPnl "NotPnl"
Assert-False $pnl.ProductionPnlAllowedNow "ProductionPnlAllowedNow"
Assert-False $pnl.FillBasedPnlAllowedNow "FillBasedPnlAllowedNow"
Assert-False $pnl.RealizedPnlAllowedNow "RealizedPnlAllowedNow"

Assert-False $policy.DirectCrossExecutionAllowed "Policy DirectCrossExecutionAllowed"
Assert-True $policy.UnsupportedDirectCrossesHeldOrRequireNetting "UnsupportedDirectCrossesHeldOrRequireNetting"
Assert-True $policy.PolicyPassed "PolicyPassed"
if (-not $policy.UsdjpyCaveat -or $true -ne $policy.UsdjpyCaveat.RequiresInversion -or $policy.UsdjpyCaveat.SecurityID -ne 4004) {
    Fail "Policy USDJPY caveat missing or incomplete"
}

if ($safety.Status -ne "Passed") {
    Fail "No-execution safety audit must pass"
}
foreach ($name in @(
    "NoLmaxCall",
    "NoFixSession",
    "NoOrdersSubmitted",
    "NoRoutesCreated",
    "NoFillsCreated",
    "NoSchedulesCreated",
    "NoBrokerSubmission",
    "NoLiveTradingStateMutation",
    "NoProductionCredentialUse",
    "CredentialValuesRedacted",
    "NoQubesExecutableRun",
    "NoNettingRun",
    "NoNettedUsdWeightsProduced",
    "NoPolygonMassiveCall",
    "NoSqlMutation",
    "NoProductionPromotion"
)) {
    Assert-True $safety.$name $name
}

$r001Text = Get-ChildItem -LiteralPath $artifactRoot -File | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }
if ($r001Text -match '"CredentialValue"\s*:' -or
    $r001Text -match '"CredentialValues"\s*:' -or
    $r001Text -match '"PasswordValue"\s*:' -or
    $r001Text -match '"SecretValue"\s*:') {
    Fail "Potential credential value persisted in R001 artifacts"
}

$executionSimR001 = Get-ChildItem -Path (Join-Path $RepoRoot "artifacts\readiness\execution-sim") -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "*cross-rail-r001*" }
if ($executionSimR001) {
    Fail "R001 artifacts must not be written under execution-sim"
}

Write-Host "CROSS-RAIL-R001 validator passed."

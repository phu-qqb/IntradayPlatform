param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $RepoRoot "artifacts/readiness/pms-qubes-sandbox-target-notional-r007"
$requiredFiles = @(
    "phase-pms-qubes-sandbox-target-notional-r007-summary.md",
    "phase-pms-qubes-sandbox-target-notional-r007-target-notional-policy.json",
    "phase-pms-qubes-sandbox-target-notional-r007-r005-r006-intake.json",
    "phase-pms-qubes-sandbox-target-notional-r007-price-basis-evidence.json",
    "phase-pms-qubes-sandbox-target-notional-r007-quantity-transformation-policy.json",
    "phase-pms-qubes-sandbox-target-notional-r007-direct-cross-execution-validation.json",
    "phase-pms-qubes-sandbox-target-notional-r007-pms-rebalance-intent-candidate.json",
    "phase-pms-qubes-sandbox-target-notional-r007-execution-candidate-readiness.json",
    "phase-pms-qubes-sandbox-target-notional-r007-active-sandbox-handoff-manifest.json",
    "phase-pms-qubes-sandbox-target-notional-r007-test-evidence.json",
    "phase-pms-qubes-sandbox-target-notional-r007-contract-status-update.json",
    "phase-pms-qubes-sandbox-target-notional-r007-boundary-safety-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $artifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R007 artifact: $file"
    }
}

function Read-JsonArtifact {
    param([string]$Name)
    $path = Join-Path $artifactRoot $Name
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$allText = Get-ChildItem -LiteralPath $artifactRoot -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }

$secretPatterns = @(
    "Password\s*=",
    "Pwd\s*=",
    "User\s+ID\s*=",
    "ApiKey\s*=",
    "Secret\s*=",
    "Bearer\s+[A-Za-z0-9_\-\.]+"
)

foreach ($pattern in $secretPatterns) {
    if ($allText -match $pattern) {
        throw "Potential credential value found in R007 artifacts: $pattern"
    }
}

$forbiddenClaims = @(
    "productionLiveReady[`"']?\s*:\s*true",
    "accountingPnlReady[`"']?\s*:\s*true",
    "netPnlReady[`"']?\s*:\s*true",
    "productionPnlReady[`"']?\s*:\s*true",
    "ledgerCommitReady[`"']?\s*:\s*true",
    "ledgerCommitClaimed[`"']?\s*:\s*true",
    "dbMutationClaimed[`"']?\s*:\s*true",
    "migrationClaimed[`"']?\s*:\s*true",
    "schemaCreationClaimed[`"']?\s*:\s*true",
    "seedClaimed[`"']?\s*:\s*true",
    "fabricatedLiveMarketDataClaimed[`"']?\s*:\s*true",
    "inventedMarksClaimed[`"']?\s*:\s*true",
    "inventedFxRatesClaimed[`"']?\s*:\s*true",
    "inventedTargetNotionalBeyondOperatorProvidedValue[`"']?\s*:\s*true",
    "inventedQuantities[`"']?\s*:\s*true",
    "inventedPrices[`"']?\s*:\s*true",
    "inventedAccountId[`"']?\s*:\s*true",
    "inventedPortfolioId[`"']?\s*:\s*true",
    "inventedStrategyId[`"']?\s*:\s*true",
    "inventedSourceExecutionIntentId[`"']?\s*:\s*true",
    "inventedAccountCurrency[`"']?\s*:\s*true",
    "crossRailR014RetroactivelyRelabelledAsQubesDriven[`"']?\s*:\s*true"
)

foreach ($pattern in $forbiddenClaims) {
    if ($allText -match $pattern) {
        throw "Forbidden R007 claim found: $pattern"
    }
}

$policy = Read-JsonArtifact "phase-pms-qubes-sandbox-target-notional-r007-target-notional-policy.json"
if ($policy.classification -ne "SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED") {
    throw "Target-notional policy must be operator-provided ready."
}
if ([decimal]$policy.targetNotionalAmount -ne [decimal]6000000 -or $policy.targetNotionalCurrency -ne "USD" -or $policy.targetNotionalScope -ne "SandboxPreviewSizingOnly") {
    throw "Target notional must equal operator-provided USD 6000000 sandbox preview sizing only."
}
if ($policy.sandboxOnly -ne $true -or $policy.notProduction -ne $true -or $policy.notAccounting -ne $true -or $policy.notAccountCurrency -ne $true -or $policy.notAumAccounting -ne $true -or $policy.notNav -ne $true -or $policy.notLedgerCapital -ne $true) {
    throw "Target-notional boundary flags are invalid."
}

$intake = Read-JsonArtifact "phase-pms-qubes-sandbox-target-notional-r007-r005-r006-intake.json"
if ($intake.classification -notin @(
    "R005_R006_INTAKE_READY_FOR_TARGET_NOTIONAL_SIZING",
    "R005_R006_INTAKE_PRESENT_BUT_INCOMPLETE",
    "R005_R006_INTAKE_MISSING",
    "R005_R006_INTAKE_CONTRADICTORY"
)) {
    throw "Invalid R005/R006 intake classification."
}

$price = Read-JsonArtifact "phase-pms-qubes-sandbox-target-notional-r007-price-basis-evidence.json"
if ($price.classification -notin @(
    "SIZING_PRICE_BASIS_READY_FROM_MARKETDATA_SNAPSHOT",
    "SIZING_PRICE_BASIS_READY_FROM_STATIC_LOCAL_PRICE_SNAPSHOT",
    "SIZING_PRICE_BASIS_READY_FROM_EXPLICIT_SANDBOX_PREVIEW_FILL_PRICE_POLICY",
    "SIZING_PRICE_BASIS_NOT_REQUIRED_BY_EXPLICIT_QUANTITY_POLICY",
    "SIZING_PRICE_BASIS_BLOCKED_MARKETDATA_MISSING",
    "SIZING_PRICE_BASIS_BLOCKED_POLICY_MISSING",
    "SIZING_PRICE_BASIS_CONTRADICTORY"
)) {
    throw "Invalid price basis classification."
}
if ($price.lmaxInstrumentsCsvUsedForPrices -eq $true -or $price.fillPricesUsedForSizing -eq $true -or $price.liveOrExternalMarketDataUsed -eq $true) {
    throw "Invalid price basis source used."
}

$quantity = Read-JsonArtifact "phase-pms-qubes-sandbox-target-notional-r007-quantity-transformation-policy.json"
if ($quantity.classification -notin @(
    "QUANTITY_TRANSFORMATION_READY_WITH_OPERATOR_TARGET_NOTIONAL_AND_PRICE_BASIS",
    "QUANTITY_TRANSFORMATION_READY_WITH_EXPLICIT_QUANTITY_POLICY_NO_PRICE_REQUIRED",
    "QUANTITY_TRANSFORMATION_BLOCKED_MISSING_PRICE_OR_MARK_SOURCE",
    "QUANTITY_TRANSFORMATION_BLOCKED_MISSING_INSTRUMENT_METADATA",
    "QUANTITY_TRANSFORMATION_BLOCKED_ROUNDING_POLICY_MISSING",
    "QUANTITY_TRANSFORMATION_CONTRADICTORY"
)) {
    throw "Invalid quantity transformation classification."
}
if ($quantity.quantitiesDerived -eq $true -and $price.classification -like "SIZING_PRICE_BASIS_BLOCKED*") {
    throw "Quantities cannot be derived while price basis is blocked."
}

$direct = Read-JsonArtifact "phase-pms-qubes-sandbox-target-notional-r007-direct-cross-execution-validation.json"
if ($direct.classification -notin @(
    "DIRECT_CROSS_POLICY_PRESERVED_EXECUTION_UNIVERSE_READY",
    "DIRECT_CROSS_POLICY_PRESERVED_BUT_PRICE_BASIS_BLOCKED",
    "DIRECT_CROSS_POLICY_PRESERVED_BUT_QUANTITIES_BLOCKED",
    "DIRECT_CROSS_POLICY_NOT_APPLICABLE",
    "DIRECT_CROSS_POLICY_CONTRADICTORY",
    "DIRECT_CROSS_EXECUTION_LEAKAGE_FOUND"
)) {
    throw "Invalid direct-cross validation classification."
}
if ($direct.directCrossExecutionLeakageFound -eq $true) {
    throw "Direct-cross execution leakage found."
}

$candidate = Read-JsonArtifact "phase-pms-qubes-sandbox-target-notional-r007-pms-rebalance-intent-candidate.json"
if ($candidate.candidateStatus -notin @(
    "PMS_REBALANCE_INTENT_CANDIDATE_READY_WITH_TARGET_NOTIONAL_QUANTITIES",
    "PMS_REBALANCE_INTENT_CANDIDATE_PREVIEW_ONLY_PRICE_BASIS_MISSING",
    "PMS_REBALANCE_INTENT_CANDIDATE_PREVIEW_ONLY_QUANTITY_POLICY_INCOMPLETE",
    "PMS_REBALANCE_INTENT_CANDIDATE_NOT_CREATED",
    "PMS_REBALANCE_INTENT_CANDIDATE_CONTRADICTORY"
)) {
    throw "Invalid PMS candidate status."
}
if ([decimal]$candidate.targetNotionalAmount -ne [decimal]6000000 -or $candidate.targetNotionalCurrency -ne "USD" -or $candidate.targetNotionalScope -ne "SandboxPreviewSizingOnly") {
    throw "PMS candidate target-notional fields are invalid."
}
if ($candidate.sandboxOnly -ne $true -or $candidate.notProduction -ne $true -or $candidate.notAccounting -ne $true -or $candidate.notExecuted -ne $true -or $candidate.notLedgerCommit -ne $true) {
    throw "PMS candidate safety flags are invalid."
}
if ($candidate.executionReadyPreview -eq $true -and $null -eq $candidate.quantities) {
    throw "PMS candidate cannot be execution-ready without quantities."
}
if ($candidate.executionReadyPreview -ne $true -and $null -ne $candidate.quantities) {
    throw "Preview-only PMS candidate must not contain quantities."
}
if ($null -ne $candidate.accountId -or $null -ne $candidate.portfolioId -or $null -ne $candidate.strategyId -or $null -ne $candidate.sourceExecutionIntentId -or $null -ne $candidate.accountCurrency) {
    throw "R007 candidate invented identity or account currency fields."
}

$decision = Read-JsonArtifact "phase-pms-qubes-sandbox-target-notional-r007-execution-candidate-readiness.json"
if ($decision.classification -notin @(
    "SANDBOX_QUBES_PMS_EXECUTION_CANDIDATE_READY_WITH_TARGET_NOTIONAL_QUANTITIES",
    "SANDBOX_QUBES_PMS_PREVIEW_ONLY_PRICE_BASIS_BLOCKED",
    "SANDBOX_QUBES_PMS_PREVIEW_ONLY_QUANTITY_POLICY_INCOMPLETE",
    "SANDBOX_QUBES_PMS_CANDIDATE_BLOCKED_CONTRADICTORY"
)) {
    throw "Invalid execution-candidate readiness classification."
}

$manifest = Read-JsonArtifact "phase-pms-qubes-sandbox-target-notional-r007-active-sandbox-handoff-manifest.json"
if ($manifest.handoffType -notin @(
    "SANDBOX_QUBES_PROTOTYPE_TO_PMS_EXECUTION_CANDIDATE_WITH_TARGET_NOTIONAL",
    "SANDBOX_QUBES_PROTOTYPE_TO_PMS_PREVIEW_ONLY_PRICE_BASIS_BLOCKED",
    "SANDBOX_QUBES_PROTOTYPE_TO_PMS_PREVIEW_ONLY_QUANTITY_POLICY_INCOMPLETE",
    "BLOCKED_UNSAFE_OR_CONTRADICTORY"
)) {
    throw "Invalid handoff type."
}
if ($manifest.sandboxOnly -ne $true -or $manifest.notProduction -ne $true -or $manifest.notAccounting -ne $true -or $manifest.notExecuted -ne $true -or $manifest.notLedgerCommit -ne $true) {
    throw "Handoff manifest safety flags are invalid."
}

$contracts = Read-JsonArtifact "phase-pms-qubes-sandbox-target-notional-r007-contract-status-update.json"
$statusByContract = @{}
foreach ($entry in $contracts.contractStatuses) {
    $statusByContract[$entry.contract] = $entry.status
}
if ($statusByContract["pms-target-notional-policy.v1"] -ne "YES") {
    throw "Target-notional policy contract must be YES for sandbox preview scope."
}
if ($statusByContract["production-readiness.v1"] -ne "BLOCKED") {
    throw "Production readiness must remain BLOCKED."
}
if ($statusByContract["accounting-attribution.v1"] -ne "BLOCKED") {
    throw "Accounting attribution must remain BLOCKED."
}
if ($statusByContract["pms-execution-candidate.v1"] -eq "YES" -and $candidate.executionReadyPreview -ne $true) {
    throw "Execution candidate contract cannot be YES while candidate is not execution-ready."
}
if ($statusByContract["pms-quantity-policy.v1"] -eq "YES" -and $candidate.executionReadyPreview -ne $true) {
    throw "Quantity policy cannot be YES while price basis blocks quantities."
}

Write-Host "PMS-QUBES-SANDBOX-TARGET-NOTIONAL-R007 gate passed."

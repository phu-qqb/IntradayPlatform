param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $RepoRoot "artifacts/readiness/pms-qubes-sandbox-sizing-r006"
$requiredFiles = @(
    "phase-pms-qubes-sandbox-sizing-r006-summary.md",
    "phase-pms-qubes-sandbox-sizing-r006-r005-intake.json",
    "phase-pms-qubes-sandbox-sizing-r006-sizing-policy-discovery.json",
    "phase-pms-qubes-sandbox-sizing-r006-quantity-transformation-policy.json",
    "phase-pms-qubes-sandbox-sizing-r006-direct-cross-execution-validation.json",
    "phase-pms-qubes-sandbox-sizing-r006-pms-rebalance-intent-candidate.json",
    "phase-pms-qubes-sandbox-sizing-r006-execution-candidate-readiness.json",
    "phase-pms-qubes-sandbox-sizing-r006-active-sandbox-handoff-manifest.json",
    "phase-pms-qubes-sandbox-sizing-r006-test-evidence.json",
    "phase-pms-qubes-sandbox-sizing-r006-contract-status-update.json",
    "phase-pms-qubes-sandbox-sizing-r006-boundary-safety-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $artifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R006 artifact: $file"
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
        throw "Potential credential value found in R006 artifacts: $pattern"
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
    "inventedTargetNotional[`"']?\s*:\s*true",
    "inventedQuantities[`"']?\s*:\s*true",
    "inventedAccountId[`"']?\s*:\s*true",
    "inventedPortfolioId[`"']?\s*:\s*true",
    "inventedStrategyId[`"']?\s*:\s*true",
    "inventedSourceExecutionIntentId[`"']?\s*:\s*true",
    "inventedAccountCurrency[`"']?\s*:\s*true",
    "crossRailR014RetroactivelyRelabelledAsQubesDriven[`"']?\s*:\s*true"
)

foreach ($pattern in $forbiddenClaims) {
    if ($allText -match $pattern) {
        throw "Forbidden R006 claim found: $pattern"
    }
}

$intake = Read-JsonArtifact "phase-pms-qubes-sandbox-sizing-r006-r005-intake.json"
if ($intake.classification -notin @(
    "R005_PROTOTYPE_OUTPUT_READY_FOR_SIZING",
    "R005_PROTOTYPE_OUTPUT_PRESENT_BUT_INCOMPLETE",
    "R005_OUTPUT_MISSING",
    "R005_OUTPUT_CONTRADICTORY"
)) {
    throw "Invalid R005 intake classification."
}

$sizing = Read-JsonArtifact "phase-pms-qubes-sandbox-sizing-r006-sizing-policy-discovery.json"
if ($sizing.classification -notin @(
    "SANDBOX_TARGET_NOTIONAL_POLICY_READY",
    "SANDBOX_QUANTITY_POLICY_READY_FROM_EXPLICIT_CONFIG",
    "SANDBOX_QUANTITY_POLICY_SOURCE_ONLY_NOT_EXECUTION_READY",
    "SANDBOX_QUANTITY_POLICY_BLOCKED_MISSING_TARGET_NOTIONAL",
    "SANDBOX_QUANTITY_POLICY_BLOCKED_MISSING_INSTRUMENT_METADATA",
    "SANDBOX_QUANTITY_POLICY_BLOCKED_ACCOUNT_OR_CURRENCY_REQUIRED",
    "SANDBOX_QUANTITY_POLICY_CONTRADICTORY"
)) {
    throw "Invalid sizing policy classification."
}
if ($sizing.explicitSandboxTargetNotionalFound -ne $true -and $null -ne $sizing.targetNotional) {
    throw "Target notional appears without explicit policy."
}

$quantity = Read-JsonArtifact "phase-pms-qubes-sandbox-sizing-r006-quantity-transformation-policy.json"
if ($quantity.classification -notin @(
    "QUANTITY_TRANSFORMATION_READY_WITH_EXPLICIT_TARGET_NOTIONAL_AND_METADATA",
    "QUANTITY_TRANSFORMATION_READY_WITH_EXPLICIT_QUANTITY_CONFIG",
    "QUANTITY_TRANSFORMATION_BLOCKED_MISSING_TARGET_NOTIONAL",
    "QUANTITY_TRANSFORMATION_BLOCKED_MISSING_PRICE_OR_MARK_SOURCE",
    "QUANTITY_TRANSFORMATION_BLOCKED_MISSING_INSTRUMENT_METADATA",
    "QUANTITY_TRANSFORMATION_BLOCKED_ACCOUNT_CURRENCY_REQUIRED",
    "QUANTITY_TRANSFORMATION_SOURCE_ONLY_NOT_EXECUTION_READY",
    "QUANTITY_TRANSFORMATION_CONTRADICTORY"
)) {
    throw "Invalid quantity transformation classification."
}

$direct = Read-JsonArtifact "phase-pms-qubes-sandbox-sizing-r006-direct-cross-execution-validation.json"
if ($direct.classification -notin @(
    "DIRECT_CROSS_POLICY_PRESERVED_EXECUTION_UNIVERSE_READY",
    "DIRECT_CROSS_POLICY_PRESERVED_BUT_SIZING_BLOCKED",
    "DIRECT_CROSS_POLICY_NOT_APPLICABLE",
    "DIRECT_CROSS_POLICY_CONTRADICTORY",
    "DIRECT_CROSS_EXECUTION_LEAKAGE_FOUND"
)) {
    throw "Invalid direct-cross validation classification."
}
if ($direct.directCrossExecutionLeakageFound -eq $true) {
    throw "Direct-cross execution leakage found."
}

$candidate = Read-JsonArtifact "phase-pms-qubes-sandbox-sizing-r006-pms-rebalance-intent-candidate.json"
if ($candidate.candidateStatus -notin @(
    "PMS_REBALANCE_INTENT_CANDIDATE_READY_WITH_QUANTITIES",
    "PMS_REBALANCE_INTENT_CANDIDATE_PREVIEW_ONLY_QUANTITIES_MISSING",
    "PMS_REBALANCE_INTENT_CANDIDATE_PREVIEW_ONLY_PRICE_SOURCE_MISSING",
    "PMS_REBALANCE_INTENT_CANDIDATE_PREVIEW_ONLY_ACCOUNT_OR_CURRENCY_REQUIRED",
    "PMS_REBALANCE_INTENT_CANDIDATE_NOT_CREATED",
    "PMS_REBALANCE_INTENT_CANDIDATE_CONTRADICTORY"
)) {
    throw "Invalid PMS candidate status."
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
    throw "R006 candidate invented identity or account currency fields."
}

$decision = Read-JsonArtifact "phase-pms-qubes-sandbox-sizing-r006-execution-candidate-readiness.json"
if ($decision.classification -notin @(
    "SANDBOX_QUBES_PMS_EXECUTION_CANDIDATE_READY_WITH_QUANTITIES",
    "SANDBOX_QUBES_PMS_PREVIEW_ONLY_QUANTITIES_BLOCKED",
    "SANDBOX_QUBES_PMS_PREVIEW_ONLY_PRICE_SOURCE_BLOCKED",
    "SANDBOX_QUBES_PMS_PREVIEW_ONLY_ACCOUNT_OR_CURRENCY_BLOCKED",
    "SANDBOX_QUBES_PMS_CANDIDATE_BLOCKED_CONTRADICTORY"
)) {
    throw "Invalid execution-candidate readiness classification."
}

$manifest = Read-JsonArtifact "phase-pms-qubes-sandbox-sizing-r006-active-sandbox-handoff-manifest.json"
if ($manifest.handoffType -notin @(
    "SANDBOX_QUBES_PROTOTYPE_TO_PMS_EXECUTION_CANDIDATE",
    "SANDBOX_QUBES_PROTOTYPE_TO_PMS_PREVIEW_ONLY_QUANTITIES_BLOCKED",
    "SANDBOX_QUBES_PROTOTYPE_TO_PMS_PREVIEW_ONLY_PRICE_SOURCE_BLOCKED",
    "SANDBOX_QUBES_PROTOTYPE_TO_PMS_PREVIEW_ONLY_ACCOUNT_OR_CURRENCY_BLOCKED",
    "BLOCKED_UNSAFE_OR_CONTRADICTORY"
)) {
    throw "Invalid handoff type."
}
if ($manifest.sandboxOnly -ne $true -or $manifest.notProduction -ne $true -or $manifest.notAccounting -ne $true -or $manifest.notExecuted -ne $true -or $manifest.notLedgerCommit -ne $true) {
    throw "Handoff manifest safety flags are invalid."
}

$contracts = Read-JsonArtifact "phase-pms-qubes-sandbox-sizing-r006-contract-status-update.json"
$statusByContract = @{}
foreach ($entry in $contracts.contractStatuses) {
    $statusByContract[$entry.contract] = $entry.status
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
if ($statusByContract["pms-quantity-policy.v1"] -eq "YES" -and $sizing.explicitSandboxTargetNotionalFound -ne $true -and $sizing.explicitSandboxQuantityConfigFound -ne $true) {
    throw "Quantity policy cannot be YES without explicit policy."
}

Write-Host "PMS-QUBES-SANDBOX-SIZING-R006 gate passed."

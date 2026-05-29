param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $RepoRoot "artifacts/readiness/qubes-operationalization-r005"
$requiredFiles = @(
    "phase-qubes-operationalization-r005-summary.md",
    "phase-qubes-operationalization-r005-product-architecture-decision.json",
    "phase-qubes-operationalization-r005-input-snapshot-contract.json",
    "phase-qubes-operationalization-r005-runner-adapter-evidence.json",
    "phase-qubes-operationalization-r005-qubes-output.json",
    "phase-qubes-operationalization-r005-direct-cross-execution-transform.json",
    "phase-qubes-operationalization-r005-sizing-policy-evidence.json",
    "phase-qubes-operationalization-r005-pms-rebalance-intent-candidate.json",
    "phase-qubes-operationalization-r005-active-sandbox-handoff-manifest.json",
    "phase-qubes-operationalization-r005-test-evidence.json",
    "phase-qubes-operationalization-r005-contract-status-update.json",
    "phase-qubes-operationalization-r005-boundary-safety-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $artifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R005 artifact: $file"
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
        throw "Potential credential value found in R005 artifacts: $pattern"
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
    "inferredAccountCurrencyClaimed[`"']?\s*:\s*true",
    "inventedAccountId[`"']?\s*:\s*true",
    "inventedPortfolioId[`"']?\s*:\s*true",
    "inventedStrategyId[`"']?\s*:\s*true",
    "inventedSourceExecutionIntentId[`"']?\s*:\s*true",
    "crossRailR014RetroactivelyRelabelledAsQubesDriven[`"']?\s*:\s*true"
)

foreach ($pattern in $forbiddenClaims) {
    if ($allText -match $pattern) {
        throw "Forbidden R005 claim found: $pattern"
    }
}

$decision = Read-JsonArtifact "phase-qubes-operationalization-r005-product-architecture-decision.json"
if ($decision.operationalizationRoute -notin @(
    "ROUTE_A_EXISTING_QUBES_ENGINE_IN_REPO",
    "ROUTE_B_EXTERNAL_QUBES_ENGINE_INTEGRATION_REQUIRED",
    "ROUTE_C_SANDBOX_QUBES_PROTOTYPE_CREATED",
    "ROUTE_D_QUBES_OPERATIONALIZATION_BLOCKED",
    "ROUTE_E_CONTRADICTORY_OR_UNSAFE"
)) {
    throw "Invalid R005 architecture route."
}

if ($decision.operationalizationRoute -eq "ROUTE_C_SANDBOX_QUBES_PROTOTYPE_CREATED" -and $decision.prototypeName -ne "SandboxQubesPrototype") {
    throw "Prototype route must name SandboxQubesPrototype."
}

$input = Read-JsonArtifact "phase-qubes-operationalization-r005-input-snapshot-contract.json"
if ($input.snapshotType -notin @(
    "LOCAL_SANDBOX_MARKETDATA_SNAPSHOT",
    "LOCAL_SANDBOX_SIGNAL_SNAPSHOT",
    "LOCAL_SANDBOX_RISK_INPUT_SNAPSHOT",
    "LOCAL_FIXTURE_INPUT_SNAPSHOT",
    "PROTOTYPE_DETERMINISTIC_INPUT_SNAPSHOT",
    "EXTERNAL_QUBES_INPUT_CONTRACT_ONLY"
)) {
    throw "Invalid input snapshot type."
}
if ($input.sandboxOnly -ne $true -or $input.notProduction -ne $true) {
    throw "Input snapshot must be sandbox-only and not production."
}

$runner = Read-JsonArtifact "phase-qubes-operationalization-r005-runner-adapter-evidence.json"
if ([string]::IsNullOrWhiteSpace($runner.runnerStatus)) {
    throw "Runner/adapter status missing."
}
if ($runner.readsExternalApi -eq $true -or $runner.readsLiveMarketData -eq $true -or $runner.mutatesDb -eq $true) {
    throw "Runner evidence crosses R005 boundaries."
}

$output = Read-JsonArtifact "phase-qubes-operationalization-r005-qubes-output.json"
if ([string]::IsNullOrWhiteSpace($output.outputStatus)) {
    throw "Qubes output status missing."
}
if ($output.sandboxOnly -ne $true -or $output.notProduction -ne $true -or $output.notAccounting -ne $true -or $output.notExecuted -ne $true -or $output.notLedgerCommit -ne $true) {
    throw "Qubes output safety flags are invalid."
}
if ([string]::IsNullOrWhiteSpace($output.sandboxQubesRunId) -or [string]::IsNullOrWhiteSpace($output.qubesOutputId)) {
    throw "Qubes output identifiers missing."
}

$transform = Read-JsonArtifact "phase-qubes-operationalization-r005-direct-cross-execution-transform.json"
if ($transform.classification -notin @(
    "DIRECT_CROSS_POLICY_PRESERVED_EXECUTION_UNIVERSE_READY",
    "DIRECT_CROSS_POLICY_PRESERVED_BUT_SIZING_MISSING",
    "DIRECT_CROSS_POLICY_NOT_APPLICABLE",
    "DIRECT_CROSS_POLICY_CONTRADICTORY",
    "DIRECT_CROSS_EXECUTION_LEAKAGE_FOUND"
)) {
    throw "Invalid direct-cross transformation classification."
}
if ($transform.directCrossExecutionLeakageFound -eq $true) {
    throw "Direct-cross execution leakage found."
}

$sizing = Read-JsonArtifact "phase-qubes-operationalization-r005-sizing-policy-evidence.json"
if ($sizing.classification -notin @(
    "SANDBOX_TARGET_NOTIONAL_POLICY_READY",
    "SANDBOX_QUANTITY_POLICY_READY_FROM_EXPLICIT_CONFIG",
    "SANDBOX_PREVIEW_SYMBOL_SIDE_ONLY_QUANTITIES_BLOCKED",
    "SANDBOX_QUANTITY_POLICY_BLOCKED_MISSING_TARGET_NOTIONAL",
    "SANDBOX_QUANTITY_POLICY_CONTRADICTORY"
)) {
    throw "Invalid sizing policy classification."
}
if ($sizing.explicitSandboxTargetNotionalFound -ne $true -and $null -ne $sizing.targetNotional) {
    throw "Target notional must not be invented."
}

$candidate = Read-JsonArtifact "phase-qubes-operationalization-r005-pms-rebalance-intent-candidate.json"
if ($candidate.candidateStatus -notin @(
    "PMS_REBALANCE_INTENT_CANDIDATE_READY_WITH_QUANTITIES",
    "PMS_REBALANCE_INTENT_CANDIDATE_PREVIEW_ONLY_QUANTITIES_MISSING",
    "PMS_REBALANCE_INTENT_CANDIDATE_CONTRACT_ONLY_EXTERNAL_QUBES_REQUIRED",
    "PMS_REBALANCE_INTENT_CANDIDATE_NOT_CREATED",
    "PMS_REBALANCE_INTENT_CANDIDATE_CONTRADICTORY"
)) {
    throw "Invalid PMS candidate status."
}
if ($candidate.sandboxOnly -ne $true -or $candidate.notProduction -ne $true -or $candidate.notAccounting -ne $true -or $candidate.notExecuted -ne $true -or $candidate.notLedgerCommit -ne $true) {
    throw "PMS candidate safety flags are invalid."
}

$manifest = Read-JsonArtifact "phase-qubes-operationalization-r005-active-sandbox-handoff-manifest.json"
if ($manifest.handoffType -notin @(
    "TRUE_QUBES_ENGINE_TO_PMS_SANDBOX_HANDOFF",
    "SANDBOX_QUBES_PROTOTYPE_TO_PMS_HANDOFF",
    "EXTERNAL_QUBES_ENGINE_CONTRACT_ONLY",
    "QUBES_OPERATIONALIZATION_BLOCKED",
    "BLOCKED_UNSAFE_OR_CONTRADICTORY"
)) {
    throw "Invalid handoff type."
}
if ($manifest.sandboxOnly -ne $true -or $manifest.notProduction -ne $true -or $manifest.notAccounting -ne $true -or $manifest.notExecuted -ne $true -or $manifest.notLedgerCommit -ne $true) {
    throw "Handoff manifest safety flags are invalid."
}

$contracts = Read-JsonArtifact "phase-qubes-operationalization-r005-contract-status-update.json"
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
if ($statusByContract["pms-quantity-policy.v1"] -ne "BLOCKED" -and $sizing.quantitiesDerivable -ne $true) {
    throw "Quantity policy status is inconsistent with sizing evidence."
}
if ($statusByContract["pms-qubes-handoff.v1"] -eq "YES" -and $candidate.executionReady -ne $true) {
    throw "PMS-Qubes handoff cannot be YES when candidate is not execution-ready."
}

Write-Host "QUBES-OPERATIONALIZATION-R005 gate passed."

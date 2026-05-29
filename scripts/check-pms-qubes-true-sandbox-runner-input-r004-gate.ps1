param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PMS_QUBES_TRUE_SANDBOX_RUNNER_INPUT_R004_VALIDATOR_FAIL: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }
    try {
        return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    } catch {
        Fail "Invalid JSON artifact: $Path :: $($_.Exception.Message)"
    }
}

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\pms-qubes-true-sandbox-runner-input-r004"
$required = @(
    "phase-pms-qubes-true-sandbox-runner-input-r004-summary.md",
    "phase-pms-qubes-true-sandbox-runner-input-r004-qubes-runner-evidence.json",
    "phase-pms-qubes-true-sandbox-runner-input-r004-input-snapshot-evidence.json",
    "phase-pms-qubes-true-sandbox-runner-input-r004-marketdata-input-binding.json",
    "phase-pms-qubes-true-sandbox-runner-input-r004-quantity-policy-evidence.json",
    "phase-pms-qubes-true-sandbox-runner-input-r004-qubes-output-evidence.json",
    "phase-pms-qubes-true-sandbox-runner-input-r004-direct-cross-execution-universe-transform.json",
    "phase-pms-qubes-true-sandbox-runner-input-r004-pms-rebalance-intent-candidate.json",
    "phase-pms-qubes-true-sandbox-runner-input-r004-active-handoff-manifest.json",
    "phase-pms-qubes-true-sandbox-runner-input-r004-product-decision.json",
    "phase-pms-qubes-true-sandbox-runner-input-r004-contract-status-update.json",
    "phase-pms-qubes-true-sandbox-runner-input-r004-boundary-safety-evidence.json"
)

foreach ($name in $required) {
    $path = Join-Path $artifactDir $name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact: $name"
    }
}

$allArtifactText = ($required | ForEach-Object { Get-Content -Raw -LiteralPath (Join-Path $artifactDir $_) }) -join "`n"

$secretPatterns = @(
    '(?i)Password\s*=',
    '(?i)Pwd\s*=',
    '(?i)User\s+ID\s*=',
    '(?i)Uid\s*=',
    '(?i)AccessToken',
    '(?i)ApiKey',
    '(?i)Secret\s*=',
    '(?i)Bearer\s+[A-Za-z0-9_\-\.]+'
)
foreach ($pattern in $secretPatterns) {
    if ($allArtifactText -match $pattern) {
        Fail "Potential credential value or secret-like token appears in artifacts."
    }
}

$forbiddenPositiveClaims = @(
    'productionLiveReady"\s*:\s*true',
    'ledgerCommitReady"\s*:\s*true',
    'netPnlReady"\s*:\s*true',
    'accountingPnlReady"\s*:\s*true',
    'productionPnlReady"\s*:\s*true',
    'externalMarketDataCall"\s*:\s*true',
    'lmaxLiveCall"\s*:\s*true',
    'lmaxSandboxOrderFillReport"\s*:\s*true',
    'productionOrder"\s*:\s*true',
    'productionFillOrReport"\s*:\s*true',
    'r009OrderSubmission"\s*:\s*true',
    'dbMutation"\s*:\s*true',
    'migration"\s*:\s*true',
    'schemaCreation"\s*:\s*true',
    'seed"\s*:\s*true',
    'fabricatedMarketData"\s*:\s*true',
    'inventedMarks"\s*:\s*true',
    'inventedFxRates"\s*:\s*true',
    'inferredAccountCurrency"\s*:\s*true',
    'newQubesOptimizationRun"\s*:\s*true',
    'manualNoExternalRun"\s*:\s*true',
    'crossRailR014RetroactivelyRelabelledAsQubesDriven"\s*:\s*true'
)
foreach ($pattern in $forbiddenPositiveClaims) {
    if ($allArtifactText -match $pattern) {
        Fail "Forbidden positive readiness/action claim matched: $pattern"
    }
}

$runner = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-qubes-runner-evidence.json")
$allowedRunner = @(
    "QUBES_LOCAL_DRY_RUNNER_READY",
    "QUBES_LOCAL_RUNNER_FOUND_INPUT_REQUIRED",
    "QUBES_LOCAL_RUNNER_FOUND_BUT_UNSAFE_EXTERNAL_OR_STATEFUL",
    "QUBES_TEST_RUNNER_ONLY",
    "QUBES_OUTPUT_NORMALIZER_ONLY_NO_ENGINE",
    "QUBES_ENGINE_MISSING",
    "QUBES_RUNNER_CONTRADICTORY"
)
if ($allowedRunner -notcontains $runner.runnerStatus) {
    Fail "Qubes runner status is not explicitly classified."
}
if ($runner.safeLocalDryRunnerExecuted -ne $false) {
    Fail "No safe dry-runner should have been executed for R004 blocked classification."
}

$input = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-input-snapshot-evidence.json")
$allowedInput = @(
    "LOCAL_SANDBOX_QUBES_INPUT_SNAPSHOT_READY",
    "LOCAL_SANDBOX_MARKETDATA_SNAPSHOT_READY",
    "LOCAL_FIXTURE_INPUT_SNAPSHOT_ONLY",
    "MARKETDATA_DB_REFERENCE_PRESENT_BUT_UNQUERYABLE",
    "OUTPUT_ONLY_WEIGHTS_NO_INPUT_SNAPSHOT",
    "INPUT_SOURCE_EXTERNAL_OR_UNSAFE",
    "INPUT_SOURCE_MISSING",
    "INPUT_SOURCE_CONTRADICTORY"
)
if ($allowedInput -notcontains $input.inputStatus) {
    Fail "Input snapshot status is not explicitly classified."
}
if ($null -ne $input.qubesInputSnapshotId -or $null -ne $input.sandboxMarketDataSnapshotId) {
    Fail "Input/MarketData snapshot IDs must not be fabricated."
}

$binding = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-marketdata-input-binding.json")
$allowedBinding = @(
    "QUBES_INPUT_BOUND_TO_CANONICAL_MARKETDATA_SOURCE",
    "QUBES_INPUT_BOUND_TO_LOCAL_SANDBOX_MARKETDATA_SNAPSHOT",
    "QUBES_INPUT_FIXTURE_ONLY_NOT_CANONICAL",
    "QUBES_INPUT_SOURCE_PRESENT_MARKETDATA_DB_UNQUERYABLE",
    "QUBES_OUTPUT_ONLY_NO_MARKETDATA_BINDING",
    "NO_QUBES_INPUT_OR_MARKETDATA_BINDING",
    "MARKETDATA_BINDING_CONTRADICTORY"
)
if ($allowedBinding -notcontains $binding.bindingClassification) {
    Fail "MarketData/input binding is not explicitly classified."
}
if ($binding.sameSourceProven -eq $true -or $binding.reconciledSourceProven -eq $true) {
    Fail "Same/reconciled source must not be claimed."
}

$quantity = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-quantity-policy-evidence.json")
$allowedQuantity = @(
    "QUANTITY_POLICY_READY_WITH_TARGET_NOTIONAL",
    "QUANTITY_POLICY_READY_WITH_EXISTING_PMS_SIZING",
    "QUANTITY_POLICY_SOURCE_ONLY_NOT_EXECUTION_READY",
    "QUANTITY_POLICY_MISSING_TARGET_NOTIONAL",
    "QUANTITY_POLICY_MISSING_INSTRUMENT_METADATA",
    "QUANTITY_POLICY_OUTPUT_ONLY_SIDES_NO_QUANTITIES",
    "QUANTITY_POLICY_CONTRADICTORY"
)
if ($allowedQuantity -notcontains $quantity.quantityPolicyClassification) {
    Fail "Quantity policy is not explicitly classified."
}
if ($quantity.quantitiesDerivable -ne $false -or $quantity.quantitiesInvented -ne $false) {
    Fail "Quantities must not be derivable or invented."
}

$output = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-qubes-output-evidence.json")
$allowedOutput = @(
    "TRUE_QUBES_DRYRUN_OUTPUT_CREATED",
    "TRUE_QUBES_RUNNER_AVAILABLE_BUT_NOT_EXECUTED_INPUT_MISSING",
    "TRUE_QUBES_RUNNER_AVAILABLE_BUT_NOT_EXECUTED_UNSAFE",
    "LOCAL_INPUT_FOUND_BUT_NO_RUNNER",
    "OUTPUT_ONLY_R003_FIXTURE_REUSED_FOR_COMPARISON_ONLY",
    "NO_USABLE_QUBES_OUTPUT",
    "OUTPUT_CONTRADICTORY"
)
if ($allowedOutput -notcontains $output.outputStatus) {
    Fail "Qubes output status is not explicitly classified."
}
if ($output.trueDryRunExecuted -ne $false -or $output.newQubesRunIdCreated -ne $false) {
    Fail "True dry-run output/NewQubesRunId must not be claimed."
}
if ($output.r003FixtureOutputComparison.countsAsTrueQubesProgress -ne $false) {
    Fail "R003 fixture output must not count as true Qubes progress."
}

$transform = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-direct-cross-execution-universe-transform.json")
$allowedTransform = @(
    "TRUE_QUBES_OUTPUT_TRANSFORMED_TO_EXECUTION_UNIVERSE",
    "DIRECT_CROSS_POLICY_PRESERVED_EXECUTION_UNIVERSE_READY",
    "DIRECT_CROSS_POLICY_PRESERVED_BUT_QUANTITIES_MISSING",
    "OUTPUT_ONLY_COMPARISON_NO_EXECUTION_READINESS",
    "DIRECT_CROSS_POLICY_NOT_APPLICABLE",
    "DIRECT_CROSS_POLICY_CONTRADICTORY",
    "DIRECT_CROSS_EXECUTION_LEAKAGE_FOUND",
    "TRANSFORMATION_NOT_AVAILABLE_NO_TRUE_QUBES_OUTPUT"
)
if ($allowedTransform -notcontains $transform.transformationClassification) {
    Fail "Direct-cross/execution-universe transformation is not explicitly classified."
}
if ($transform.directCrossPolicy.executionLeakageFound -ne $false -or $transform.executionReady -ne $false) {
    Fail "Transformation must not be execution-ready or leak direct crosses."
}

$intent = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-pms-rebalance-intent-candidate.json")
$allowedCandidate = @(
    "PMS_REBALANCE_INTENT_CANDIDATE_READY_WITH_QUANTITIES",
    "PMS_REBALANCE_INTENT_CANDIDATE_PREVIEW_ONLY_QUANTITIES_MISSING",
    "PMS_REBALANCE_INTENT_CANDIDATE_PREVIEW_ONLY_SOURCE_UNPROVEN",
    "PMS_REBALANCE_INTENT_CANDIDATE_NOT_CREATED_NO_TRUE_QUBES_OUTPUT",
    "PMS_REBALANCE_INTENT_CANDIDATE_CONTRADICTORY"
)
if ($allowedCandidate -notcontains $intent.candidateClassification) {
    Fail "PMS rebalance intent candidate is not explicitly classified."
}
if ($intent.candidateCreated -ne $false -or $intent.ExecutionReady -ne $false) {
    Fail "R004 PMS candidate must not be created or execution-ready."
}
foreach ($flag in @("SandboxOnly", "NotAccounting", "NotProduction", "NotExecuted", "NotLedgerCommit")) {
    if ($intent.$flag -ne $true) {
        Fail "PMS candidate boundary flag not preserved: $flag"
    }
}

$manifest = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-active-handoff-manifest.json")
$allowedHandoff = @(
    "TRUE_QUBES_DRYRUN_TO_PMS_SANDBOX_HANDOFF",
    "TRUE_QUBES_INPUT_FOUND_BUT_NO_RUNNER_HANDOFF_BLOCKED",
    "TRUE_QUBES_RUNNER_FOUND_BUT_INPUT_MISSING_HANDOFF_BLOCKED",
    "QUBES_OUTPUT_ONLY_COMPARISON_NOT_HANDOFF",
    "HANDOFF_CONTRACT_ONLY_NOT_EXECUTION_READY",
    "BLOCKED_NO_SAFE_QUBES_RUNNER_OR_INPUT",
    "BLOCKED_UNSAFE_OR_CONTRADICTORY"
)
if ($allowedHandoff -notcontains $manifest.HandoffType) {
    Fail "Active handoff manifest type is not explicitly classified."
}
if ($manifest.HandoffType -ne "BLOCKED_NO_SAFE_QUBES_RUNNER_OR_INPUT" -or $manifest.ExecutionReady -ne $false) {
    Fail "Manifest must block on missing safe runner/input and not be execution-ready."
}

$decision = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-product-decision.json")
$allowedDecision = @(
    "TRUE_QUBES_SANDBOX_HANDOFF_CREATED_EXECUTION_READY",
    "TRUE_QUBES_SANDBOX_HANDOFF_CREATED_PREVIEW_ONLY",
    "QUBES_INPUT_FOUND_BUT_RUNNER_MISSING",
    "QUBES_RUNNER_FOUND_BUT_INPUT_MISSING",
    "QUBES_RUNNER_AND_INPUT_FOUND_BUT_QUANTITY_POLICY_MISSING",
    "QUBES_NOT_OPERATIONAL_FOR_SANDBOX_ECONOMIC_RAIL",
    "CONTRADICTORY_OR_UNSAFE_QUBES_HANDOFF_BLOCKED"
)
if ($allowedDecision -notcontains $decision.productDecisionClassification) {
    Fail "Product decision classification is not an allowed value."
}
if ($decision.productDecisionClassification -ne "QUBES_NOT_OPERATIONAL_FOR_SANDBOX_ECONOMIC_RAIL") {
    Fail "Expected QUBES_NOT_OPERATIONAL_FOR_SANDBOX_ECONOMIC_RAIL."
}
if ($decision.existingCrossRailR014Changed -ne $false -or $decision.existingCrossRailR014StillPmsIntentDriven -ne $true) {
    Fail "Existing CROSS-RAIL-R014 must remain PMS-intent-driven and unchanged."
}
foreach ($field in @("unblocksTheoreticalPnl", "unblocksNetPnl", "unblocksAccountingPnl", "unblocksProductionPnl", "unblocksLedgerCommit")) {
    if ($decision.$field -ne $false) {
        Fail "Package must not unblock $field"
    }
}

$contracts = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-contract-status-update.json")
$contractMap = @{}
foreach ($status in $contracts.statuses) {
    $contractMap[$status.contractId] = $status.status
}
foreach ($blocked in @("pms-qubes-runner.v1", "pms-qubes-input-snapshot.v1", "pms-qubes-handoff.v1", "pms-quantity-policy.v1", "accounting-attribution.v1", "production-readiness.v1")) {
    if ($contractMap[$blocked] -ne "BLOCKED") {
        Fail "$blocked must be BLOCKED."
    }
}
if ($contractMap["pnl-preview.v1"] -ne "YES") {
    Fail "pnl-preview.v1 must remain YES only for accepted gross sandbox PnL V0."
}

$boundary = Read-Json (Join-Path $artifactDir "phase-pms-qubes-true-sandbox-runner-input-r004-boundary-safety-evidence.json")
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency")) {
    if ($null -ne $boundary.fieldsNotInvented.$field) {
        Fail "Missing identity/economic field was invented: $field"
    }
}

Write-Host "PMS_QUBES_TRUE_SANDBOX_RUNNER_INPUT_R004_VALIDATOR_PASS"

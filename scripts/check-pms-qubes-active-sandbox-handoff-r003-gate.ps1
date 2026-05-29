param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PMS_QUBES_ACTIVE_SANDBOX_HANDOFF_R003_VALIDATOR_FAIL: $Message"
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

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\pms-qubes-active-sandbox-handoff-r003"
$required = @(
    "phase-pms-qubes-active-sandbox-handoff-r003-summary.md",
    "phase-pms-qubes-active-sandbox-handoff-r003-qubes-runner-availability.json",
    "phase-pms-qubes-active-sandbox-handoff-r003-input-snapshot-discovery.json",
    "phase-pms-qubes-active-sandbox-handoff-r003-qubes-output.json",
    "phase-pms-qubes-active-sandbox-handoff-r003-direct-cross-execution-universe-transform.json",
    "phase-pms-qubes-active-sandbox-handoff-r003-pms-rebalance-intent-candidate.json",
    "phase-pms-qubes-active-sandbox-handoff-r003-active-handoff-manifest.json",
    "phase-pms-qubes-active-sandbox-handoff-r003-product-decision.json",
    "phase-pms-qubes-active-sandbox-handoff-r003-contract-status-update.json",
    "phase-pms-qubes-active-sandbox-handoff-r003-boundary-safety-evidence.json"
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
    'productionOrder"\s*:\s*true',
    'productionFillOrReport"\s*:\s*true',
    'r009OrderSubmission"\s*:\s*true',
    'newLmaxSandboxOrder"\s*:\s*true',
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

$runner = Read-Json (Join-Path $artifactDir "phase-pms-qubes-active-sandbox-handoff-r003-qubes-runner-availability.json")
$allowedRunner = @(
    "QUBES_LOCAL_DRY_RUNNER_AVAILABLE",
    "QUBES_LOCAL_RUNNER_EXISTS_BUT_INPUT_MISSING",
    "QUBES_OUTPUT_ONLY_AVAILABLE_NO_RUNNER",
    "QUBES_LEGACY_FIXTURE_ONLY",
    "QUBES_RUNNER_UNSAFE_OR_EXTERNAL_DEPENDENT",
    "QUBES_RUNNER_MISSING"
)
if ($allowedRunner -notcontains $runner.runnerStatus) {
    Fail "Qubes runner availability is not explicitly classified."
}
if ($runner.safeLocalDryRunnerExecuted -ne $false) {
    Fail "No Qubes dry-run should have been executed for fixture-derived R003."
}

$input = Read-Json (Join-Path $artifactDir "phase-pms-qubes-active-sandbox-handoff-r003-input-snapshot-discovery.json")
$allowedInput = @(
    "LOCAL_SANDBOX_MARKETDATA_SNAPSHOT_FOUND",
    "LOCAL_SANDBOX_QUBES_INPUT_SNAPSHOT_FOUND",
    "LOCAL_FIXTURE_INPUT_ONLY",
    "QUBES_OUTPUT_ONLY_NO_INPUT_SNAPSHOT",
    "MARKETDATA_SOURCE_REFERENCE_PRESENT_BUT_UNQUERYABLE",
    "INPUT_SOURCE_EXTERNAL_OR_UNSAFE",
    "INPUT_SOURCE_MISSING"
)
if ($allowedInput -notcontains $input.inputStatus) {
    Fail "Input snapshot status is not explicitly classified."
}
if ($null -ne $input.marketDataSnapshotId -or $null -ne $input.qubesInputSnapshotId) {
    Fail "Input or MarketData snapshot ID must not be fabricated."
}

$output = Read-Json (Join-Path $artifactDir "phase-pms-qubes-active-sandbox-handoff-r003-qubes-output.json")
if ($output.qubesOutputStatus -ne "LEGACY_FIXTURE_DERIVED_OUTPUT_ONLY") {
    Fail "Qubes output status must be explicit fixture-derived output-only."
}
if ([string]::IsNullOrWhiteSpace([string]$output.qubesOutputId)) {
    Fail "QubesOutputId missing."
}

$transform = Read-Json (Join-Path $artifactDir "phase-pms-qubes-active-sandbox-handoff-r003-direct-cross-execution-universe-transform.json")
$allowedTransform = @(
    "DIRECT_CROSS_POLICY_PRESERVED_EXECUTION_UNIVERSE_READY",
    "DIRECT_CROSS_POLICY_PRESERVED_BUT_QUANTITIES_MISSING",
    "OUTPUT_ONLY_SYMBOL_SIDE_MATCH_NO_QUANTITIES",
    "DIRECT_CROSS_POLICY_NOT_APPLICABLE",
    "DIRECT_CROSS_POLICY_CONTRADICTORY",
    "DIRECT_CROSS_EXECUTION_LEAKAGE_FOUND"
)
if ($allowedTransform -notcontains $transform.transformationClassification) {
    Fail "Direct-cross/execution-universe transformation is not explicitly classified."
}
if ($transform.directCrossPolicy.executionLeakageFound -ne $false) {
    Fail "Direct-cross execution leakage must not be present."
}
if ($transform.quantitiesDerivable -ne $false) {
    Fail "Quantities must not be treated as derivable."
}

$intent = Read-Json (Join-Path $artifactDir "phase-pms-qubes-active-sandbox-handoff-r003-pms-rebalance-intent-candidate.json")
foreach ($flag in @("SandboxOnly", "NotAccounting", "NotProduction", "NotExecuted", "NotLedgerCommit", "NotR014Relabel")) {
    if ($intent.$flag -ne $true) {
        Fail "PMS intent candidate flag not preserved: $flag"
    }
}
if ($intent.ExecutionReady -ne $false) {
    Fail "R003 PMS intent candidate must not be execution-ready."
}

$manifest = Read-Json (Join-Path $artifactDir "phase-pms-qubes-active-sandbox-handoff-r003-active-handoff-manifest.json")
$allowedHandoffType = @(
    "ACTIVE_QUBES_DRYRUN_TO_PMS_SANDBOX_HANDOFF",
    "QUBES_OUTPUT_ONLY_TO_PMS_SANDBOX_HANDOFF",
    "LEGACY_FIXTURE_OUTPUT_TO_PMS_SANDBOX_HANDOFF",
    "HANDOFF_CONTRACT_ONLY_NO_USABLE_QUBES_OUTPUT",
    "BLOCKED_UNSAFE_OR_CONTRADICTORY"
)
if ($allowedHandoffType -notcontains $manifest.HandoffType) {
    Fail "Active handoff manifest HandoffType is not allowed."
}
if ($manifest.HandoffType -ne "LEGACY_FIXTURE_OUTPUT_TO_PMS_SANDBOX_HANDOFF") {
    Fail "Expected fixture-derived handoff type."
}
foreach ($flag in @("SandboxOnly", "NotAccounting", "NotProduction", "NotExecuted", "NotLedgerCommit")) {
    if ($manifest.$flag -ne $true) {
        Fail "Manifest flag not preserved: $flag"
    }
}
if ($manifest.HonestyChecks.NoR014Relabel -ne $true -or $manifest.HonestyChecks.NoQuantitiesInvented -ne $true) {
    Fail "Manifest honesty checks missing."
}

$decision = Read-Json (Join-Path $artifactDir "phase-pms-qubes-active-sandbox-handoff-r003-product-decision.json")
$allowedDecision = @(
    "ACTIVE_QUBES_SANDBOX_HANDOFF_CREATED",
    "QUBES_OUTPUT_ONLY_SANDBOX_HANDOFF_CREATED",
    "LEGACY_FIXTURE_SANDBOX_HANDOFF_CREATED_WITH_WARNINGS",
    "HANDOFF_CONTRACT_CREATED_BUT_NOT_USABLE_FOR_PMS_EXECUTION",
    "ACTIVE_QUBES_HANDOFF_BLOCKED",
    "CONTRADICTORY_OR_UNSAFE_HANDOFF_BLOCKED"
)
if ($allowedDecision -notcontains $decision.productDecisionClassification) {
    Fail "Product decision classification is not an allowed value."
}
if ($decision.productDecisionClassification -ne "LEGACY_FIXTURE_SANDBOX_HANDOFF_CREATED_WITH_WARNINGS") {
    Fail "Expected fixture-derived product decision."
}
if ($decision.existingCrossRailR014Changed -ne $false -or $decision.existingCrossRailR014StillPmsIntentDriven -ne $true) {
    Fail "Existing CROSS-RAIL-R014 must remain PMS-intent-driven and unchanged."
}
foreach ($field in @("unblocksTheoreticalPnl", "unblocksNetPnl", "unblocksAccountingPnl", "unblocksProductionPnl", "unblocksLedgerCommit")) {
    if ($decision.$field -ne $false) {
        Fail "Package must not unblock $field"
    }
}

$contracts = Read-Json (Join-Path $artifactDir "phase-pms-qubes-active-sandbox-handoff-r003-contract-status-update.json")
$contractMap = @{}
foreach ($status in $contracts.statuses) {
    $contractMap[$status.contractId] = $status.status
}
if ($contractMap["pms-qubes-handoff.v1"] -ne "WITH_WARNINGS") {
    Fail "pms-qubes-handoff.v1 must be WITH_WARNINGS for fixture-derived handoff."
}
if ($contractMap["qubes-marketdata-lineage.v1"] -ne "WITH_WARNINGS") {
    Fail "qubes-marketdata-lineage.v1 must remain WITH_WARNINGS."
}
if ($contractMap["accounting-attribution.v1"] -ne "BLOCKED") {
    Fail "accounting-attribution.v1 must remain BLOCKED."
}
if ($contractMap["production-readiness.v1"] -ne "BLOCKED") {
    Fail "production-readiness.v1 must remain BLOCKED."
}

$boundary = Read-Json (Join-Path $artifactDir "phase-pms-qubes-active-sandbox-handoff-r003-boundary-safety-evidence.json")
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency")) {
    if ($null -ne $boundary.fieldsNotInvented.$field) {
        Fail "Missing identity/economic field was invented: $field"
    }
}

Write-Host "PMS_QUBES_ACTIVE_SANDBOX_HANDOFF_R003_VALIDATOR_PASS"

param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PMS_QUBES_CANONICAL_HANDOFF_R002_VALIDATOR_FAIL: $Message"
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

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\pms-qubes-canonical-handoff-r002"
$required = @(
    "phase-pms-qubes-canonical-handoff-r002-summary.md",
    "phase-pms-qubes-canonical-handoff-r002-active-handoff-discovery.json",
    "phase-pms-qubes-canonical-handoff-r002-qubes-output-normalized.json",
    "phase-pms-qubes-canonical-handoff-r002-marketdata-source-binding.json",
    "phase-pms-qubes-canonical-handoff-r002-pms-intent-reconciliation.json",
    "phase-pms-qubes-canonical-handoff-r002-sandbox-handoff-manifest.json",
    "phase-pms-qubes-canonical-handoff-r002-product-decision.json",
    "phase-pms-qubes-canonical-handoff-r002-contract-status-update.json",
    "phase-pms-qubes-canonical-handoff-r002-boundary-safety-evidence.json"
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
    'dbMutation"\s*:\s*true',
    'migration"\s*:\s*true',
    'schemaCreation"\s*:\s*true',
    'seed"\s*:\s*true',
    'fabricatedMarketData"\s*:\s*true',
    'inventedMarks"\s*:\s*true',
    'inventedFxRates"\s*:\s*true',
    'inferredAccountCurrency"\s*:\s*true',
    'newQubesOptimizationRun"\s*:\s*true',
    'manualNoExternalRun"\s*:\s*true'
)
foreach ($pattern in $forbiddenPositiveClaims) {
    if ($allArtifactText -match $pattern) {
        Fail "Forbidden positive readiness/action claim matched: $pattern"
    }
}

$discovery = Read-Json (Join-Path $artifactDir "phase-pms-qubes-canonical-handoff-r002-active-handoff-discovery.json")
$allowedHandoff = @(
    "ACTIVE_PMS_APPROVED_QUBES_HANDOFF_FOUND",
    "ACTIVE_QUBES_OUTPUT_ONLY_HANDOFF_FOUND",
    "LEGACY_OR_FIXTURE_QUBES_OUTPUT_ONLY",
    "PMS_INTENT_DRIVEN_NO_ACTIVE_QUBES_HANDOFF",
    "HANDOFF_CONTRADICTORY",
    "HANDOFF_MISSING"
)
if ($allowedHandoff -notcontains $discovery.activeHandoffStatus) {
    Fail "Active Qubes handoff status is not explicitly classified."
}
if ($discovery.activePmsApprovedQubesHandoff.found -ne $false) {
    Fail "Active PMS-approved Qubes handoff must not be claimed."
}

$normalized = Read-Json (Join-Path $artifactDir "phase-pms-qubes-canonical-handoff-r002-qubes-output-normalized.json")
if (-not $normalized.candidatesNormalized -or $normalized.candidatesNormalized.Count -lt 1) {
    Fail "Qubes output candidates must be normalized or explicitly missing."
}
if ($normalized.normalizedOutputConclusion.outputOnlyCanProveQubesInputDataSource -ne $false) {
    Fail "Output-only weights must not prove Qubes input data source."
}

$marketData = Read-Json (Join-Path $artifactDir "phase-pms-qubes-canonical-handoff-r002-marketdata-source-binding.json")
$allowedMarketData = @(
    "MARKETDATA_SNAPSHOT_BOUND_TO_QUBES_INPUT",
    "MARKETDATA_SNAPSHOT_BOUND_TO_QUBES_OUTPUT_ONLY",
    "MARKETDATA_SOURCE_REFERENCE_PRESENT_BUT_UNQUERYABLE",
    "NO_MARKETDATA_SOURCE_BOUND_TO_QUBES",
    "MARKETDATA_BINDING_CONTRADICTORY"
)
if ($allowedMarketData -notcontains $marketData.sourceBindingClassification) {
    Fail "MarketData source binding is not explicitly classified."
}
if ($marketData.sameSourceProven -eq $true -or $marketData.reconciledSourceProven -eq $true) {
    Fail "Same/reconciled MarketData source must not be claimed."
}

$recon = Read-Json (Join-Path $artifactDir "phase-pms-qubes-canonical-handoff-r002-pms-intent-reconciliation.json")
$allowedRecon = @(
    "QUBES_OUTPUT_MATCHES_ACTIVE_PMS_INTENT",
    "QUBES_OUTPUT_PARTIALLY_MATCHES_ACTIVE_PMS_INTENT",
    "QUBES_OUTPUT_ONLY_CANNOT_PROVE_PMS_INTENT_SOURCE",
    "PMS_INTENT_HAS_NO_QUBES_OUTPUT_MATCH",
    "PMS_INTENT_CONTRADICTS_QUBES_OUTPUT"
)
if ($allowedRecon -notcontains $recon.reconciliationClassification) {
    Fail "PMS intent reconciliation is not explicitly classified."
}
if ($recon.contradictionFound -ne $false) {
    Fail "Contradiction must not be present for this package classification."
}

$manifest = Read-Json (Join-Path $artifactDir "phase-pms-qubes-canonical-handoff-r002-sandbox-handoff-manifest.json")
if ($manifest.SandboxOnly -ne $true -or $manifest.NotAccounting -ne $true -or $manifest.NotProduction -ne $true) {
    Fail "Sandbox handoff manifest must preserve sandbox-only, not-accounting, and not-production flags."
}
if ($manifest.ActiveSandboxRailClassification -ne "PMS_INTENT_DRIVEN_SANDBOX_PREVIEW_NOT_QUBES_ECONOMIC") {
    Fail "Sandbox handoff manifest must honestly classify the active rail."
}
if ($manifest.HonestyChecks.DoesNotPretendActiveQubesHandoffExists -ne $true) {
    Fail "Manifest honesty check missing for active Qubes handoff."
}

$decision = Read-Json (Join-Path $artifactDir "phase-pms-qubes-canonical-handoff-r002-product-decision.json")
$allowedDecision = @(
    "QUBES_ECONOMIC_HANDOFF_ACTIVE_FOR_SANDBOX_PREVIEW",
    "QUBES_OUTPUT_ONLY_HANDOFF_FOR_SANDBOX_PREVIEW",
    "PMS_INTENT_DRIVEN_SANDBOX_PREVIEW_NOT_QUBES_ECONOMIC",
    "LEGACY_FIXTURE_QUBES_ONLY_NOT_ACTIVE_RAIL",
    "CONTRADICTORY_QUBES_PMS_HANDOFF"
)
if ($allowedDecision -notcontains $decision.productDecisionClassification) {
    Fail "Product decision classification is not an allowed value."
}
if ($decision.productDecisionClassification -ne "PMS_INTENT_DRIVEN_SANDBOX_PREVIEW_NOT_QUBES_ECONOMIC") {
    Fail "Expected PMS-intent-driven product decision for current evidence."
}
if ($decision.grossSandboxPnlV0Changed -ne $false) {
    Fail "Gross sandbox PnL V0 must remain unchanged."
}
foreach ($field in @("unblocksTheoreticalPnl", "unblocksNetPnl", "unblocksAccountingPnl", "unblocksProductionPnl", "unblocksLedgerCommit")) {
    if ($decision.$field -ne $false) {
        Fail "Package must not unblock $field"
    }
}

$contracts = Read-Json (Join-Path $artifactDir "phase-pms-qubes-canonical-handoff-r002-contract-status-update.json")
$contractMap = @{}
foreach ($status in $contracts.statuses) {
    $contractMap[$status.contractId] = $status.status
}
if ($contractMap["pms-qubes-handoff.v1"] -ne "BLOCKED") {
    Fail "pms-qubes-handoff.v1 must be BLOCKED when no active Qubes handoff is found."
}
if ($contractMap["qubes-marketdata-lineage.v1"] -ne "WITH_WARNINGS") {
    Fail "qubes-marketdata-lineage.v1 must remain WITH_WARNINGS."
}
if ($contractMap["marketdata-readiness.v1"] -ne "WITH_WARNINGS") {
    Fail "marketdata-readiness.v1 must remain WITH_WARNINGS."
}
if ($contractMap["accounting-attribution.v1"] -ne "BLOCKED") {
    Fail "accounting-attribution.v1 must remain BLOCKED."
}

$boundary = Read-Json (Join-Path $artifactDir "phase-pms-qubes-canonical-handoff-r002-boundary-safety-evidence.json")
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "PMSApprovedQubesRunId")) {
    if ($null -ne $boundary.fieldsNotInvented.$field) {
        Fail "Missing identity/economic field was invented: $field"
    }
}

Write-Host "PMS_QUBES_CANONICAL_HANDOFF_R002_VALIDATOR_PASS"

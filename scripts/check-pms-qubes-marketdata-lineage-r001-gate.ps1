param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PMS_QUBES_MARKETDATA_LINEAGE_R001_VALIDATOR_FAIL: $Message"
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

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\pms-qubes-marketdata-lineage-r001"
$required = @(
    "phase-pms-qubes-marketdata-lineage-r001-summary.md",
    "phase-pms-qubes-marketdata-lineage-r001-qubes-data-source-inventory.json",
    "phase-pms-qubes-marketdata-lineage-r001-qubesrunid-pms-linkage.json",
    "phase-pms-qubes-marketdata-lineage-r001-qubes-universe-direct-cross-policy.json",
    "phase-pms-qubes-marketdata-lineage-r001-marketdata-source-comparison.json",
    "phase-pms-qubes-marketdata-lineage-r001-fills-qubes-marketdata-separation.json",
    "phase-pms-qubes-marketdata-lineage-r001-pnl-implication.json",
    "phase-pms-qubes-marketdata-lineage-r001-contract-status-update.json",
    "phase-pms-qubes-marketdata-lineage-r001-boundary-safety-evidence.json"
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
    'inferredAccountCurrency"\s*:\s*true'
)
foreach ($pattern in $forbiddenPositiveClaims) {
    if ($allArtifactText -match $pattern) {
        Fail "Forbidden positive readiness/action claim matched: $pattern"
    }
}

$inventory = Read-Json (Join-Path $artifactDir "phase-pms-qubes-marketdata-lineage-r001-qubes-data-source-inventory.json")
if (-not $inventory.dataSourceCandidates -or $inventory.dataSourceCandidates.Count -lt 1) {
    Fail "Qubes data-source inventory must be non-empty or explicitly classified missing."
}
if ($inventory.summary.marketDataSourceUsedByQubesSameAsCanonicalSourceProven -ne $false) {
    Fail "Qubes/MarketData same-source proof must not be claimed."
}

$linkage = Read-Json (Join-Path $artifactDir "phase-pms-qubes-marketdata-lineage-r001-qubesrunid-pms-linkage.json")
if ($linkage.qubesRunIdStatus -ne "PRESENT_WITH_WARNINGS_NOT_PMS_APPROVED_ECONOMIC_OUTPUT") {
    Fail "QubesRunId status must remain explicitly warning-only unless PMS-approved evidence exists."
}
if ($null -ne $linkage.pmsApprovedQubesRunId) {
    Fail "PMS-approved QubesRunId must not be invented."
}
if ($null -ne $linkage.activePmsCrossRailLinkage.sourceExecutionIntentId) {
    Fail "SourceExecutionIntentId must not be synthesized."
}

$direct = Read-Json (Join-Path $artifactDir "phase-pms-qubes-marketdata-lineage-r001-qubes-universe-direct-cross-policy.json")
if ($direct.classification -ne "DIRECT_CROSS_POLICY_PRESERVED") {
    Fail "Direct-cross policy must be explicitly classified as preserved for this evidence set."
}
if ($direct.directCrossExecutionLeakageFound -ne $false) {
    Fail "Direct-cross execution leakage must not be present."
}

$sourceComparison = Read-Json (Join-Path $artifactDir "phase-pms-qubes-marketdata-lineage-r001-marketdata-source-comparison.json")
$allowedRelationships = @(
    "SAME_SOURCE_PROVEN",
    "RECONCILED_SOURCE_PROVEN",
    "DIFFERENT_SOURCE_BUT_EXPLAINED",
    "QUBES_SOURCE_PROVEN_MARKETDATA_SOURCE_UNQUERYABLE",
    "QUBES_SOURCE_UNPROVEN_MARKETDATA_SOURCE_KNOWN",
    "BOTH_SOURCES_UNPROVEN",
    "CONTRADICTORY_SOURCE_EVIDENCE"
)
if ($allowedRelationships -notcontains $sourceComparison.relationshipClassification) {
    Fail "Qubes vs MarketData source relationship is not explicitly classified."
}
if ($sourceComparison.sameSourceProven -eq $true -or $sourceComparison.reconciledSourceProven -eq $true) {
    Fail "Same/reconciled source must not be claimed for R001 evidence."
}

$separation = Read-Json (Join-Path $artifactDir "phase-pms-qubes-marketdata-lineage-r001-fills-qubes-marketdata-separation.json")
foreach ($requiredClass in @("EXECUTION_AUDIT_SOURCE_CONFIRMED", "PAPER_LEDGER_PREVIEW_MAPPING_CONFIRMED", "FILLS_TO_MARKETDATA_CONFUSION_RISK_DOCUMENTED")) {
    if ($separation.classifications -notcontains $requiredClass) {
        Fail "Missing fills/Qubes/MarketData separation classification: $requiredClass"
    }
}
if ($separation.marketDataDbMarksReferencePrices.marksInventedFromFills -ne $false) {
    Fail "Marks must not be inferred from fills."
}

$pnl = Read-Json (Join-Path $artifactDir "phase-pms-qubes-marketdata-lineage-r001-pnl-implication.json")
foreach ($requiredClass in @("GROSS_ROUND_TRIP_PNL_V0_UNAFFECTED", "THEORETICAL_PNL_BLOCKED_MARKETDATA_LINEAGE_UNPROVEN", "ACCOUNTING_PNL_BLOCKED_ATTRIBUTION_OR_MARKETDATA_UNPROVEN", "NET_PNL_BLOCKED_COST_MODEL_MISSING", "PRODUCTION_PNL_BLOCKED")) {
    if ($pnl.classifications -notcontains $requiredClass) {
        Fail "Missing PnL implication classification: $requiredClass"
    }
}

$contracts = Read-Json (Join-Path $artifactDir "phase-pms-qubes-marketdata-lineage-r001-contract-status-update.json")
$contractMap = @{}
foreach ($status in $contracts.statuses) {
    $contractMap[$status.contractId] = $status.status
}
if ($contractMap["qubes-marketdata-lineage.v1"] -ne "WITH_WARNINGS") {
    Fail "qubes-marketdata-lineage.v1 must remain WITH_WARNINGS."
}
if ($contractMap["marketdata-readiness.v1"] -ne "WITH_WARNINGS") {
    Fail "marketdata-readiness.v1 must remain WITH_WARNINGS."
}
if ($contractMap["lmax-marketdata-db.v1"] -ne "WITH_WARNINGS") {
    Fail "lmax-marketdata-db.v1 must remain WITH_WARNINGS."
}
if ($contractMap["accounting-attribution.v1"] -ne "BLOCKED") {
    Fail "accounting-attribution.v1 must remain BLOCKED."
}

$boundary = Read-Json (Join-Path $artifactDir "phase-pms-qubes-marketdata-lineage-r001-boundary-safety-evidence.json")
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "PMSApprovedQubesRunId")) {
    if ($null -ne $boundary.fieldsNotInvented.$field) {
        Fail "Missing identity/economic field was invented: $field"
    }
}

Write-Host "PMS_QUBES_MARKETDATA_LINEAGE_R001_VALIDATOR_PASS"

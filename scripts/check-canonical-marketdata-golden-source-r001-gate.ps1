param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $RepoRoot "artifacts/readiness/canonical-marketdata-golden-source-r001"
$requiredFiles = @(
    "phase-canonical-marketdata-golden-source-r001-summary.md",
    "phase-canonical-marketdata-golden-source-r001-candidate-inventory.json",
    "phase-canonical-marketdata-golden-source-r001-source-selection.json",
    "phase-canonical-marketdata-golden-source-r001-snapshot-contract.json",
    "phase-canonical-marketdata-golden-source-r001-coverage-evidence.json",
    "phase-canonical-marketdata-golden-source-r001-usage-policy.json",
    "phase-canonical-marketdata-golden-source-r001-consumer-binding-plan.json",
    "phase-canonical-marketdata-golden-source-r001-db-role-decision.json",
    "phase-canonical-marketdata-golden-source-r001-contract-status-update.json",
    "phase-canonical-marketdata-golden-source-r001-test-evidence.json",
    "phase-canonical-marketdata-golden-source-r001-boundary-safety-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $artifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R001 artifact: $file"
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
        throw "Potential credential value found in canonical MarketData artifacts: $pattern"
    }
}

$forbiddenClaims = @(
    "productionLiveReady[`"']?\s*:\s*true",
    "accountingPnlReady[`"']?\s*:\s*true",
    "netPnlReady[`"']?\s*:\s*true",
    "productionPnlReady[`"']?\s*:\s*true",
    "ledgerCommitClaimed[`"']?\s*:\s*true",
    "dbMutationClaimed[`"']?\s*:\s*true",
    "migrationClaimed[`"']?\s*:\s*true",
    "schemaCreationClaimed[`"']?\s*:\s*true",
    "seedClaimed[`"']?\s*:\s*true",
    "fabricatedPricesClaimed[`"']?\s*:\s*true",
    "inventedMarksClaimed[`"']?\s*:\s*true",
    "inventedFxRatesClaimed[`"']?\s*:\s*true",
    "inventedAccountId[`"']?\s*:\s*true",
    "inventedPortfolioId[`"']?\s*:\s*true",
    "inventedStrategyId[`"']?\s*:\s*true",
    "inventedSourceExecutionIntentId[`"']?\s*:\s*true",
    "inventedAccountCurrency[`"']?\s*:\s*true",
    "crossRailR014ChangedOrRelabelled[`"']?\s*:\s*true"
)

foreach ($pattern in $forbiddenClaims) {
    if ($allText -match $pattern) {
        throw "Forbidden canonical MarketData claim found: $pattern"
    }
}

$inventory = Read-JsonArtifact "phase-canonical-marketdata-golden-source-r001-candidate-inventory.json"
if ($inventory.inventoryStatus -ne "NON_EMPTY" -or $inventory.candidates.Count -eq 0) {
    throw "Candidate inventory must be non-empty."
}

$selection = Read-JsonArtifact "phase-canonical-marketdata-golden-source-r001-source-selection.json"
if ($selection.classification -notin @(
    "TRUE_GOLDEN_SOURCE_SELECTED",
    "SANDBOX_GOLDEN_SOURCE_SELECTED_WITH_WARNINGS",
    "QUBES_INPUT_SOURCE_SELECTED_MARKETDATA_COMPLETENESS_WARNINGS",
    "EXTERNAL_GOLDEN_SOURCE_INTEGRATION_REQUIRED",
    "ONLY_FIXTURE_OR_PROTOTYPE_SOURCE_AVAILABLE",
    "NO_USABLE_GOLDEN_SOURCE_FOUND",
    "CONTRADICTORY_OR_UNSAFE_SOURCE_EVIDENCE"
)) {
    throw "Invalid source selection classification."
}

$snapshot = Read-JsonArtifact "phase-canonical-marketdata-golden-source-r001-snapshot-contract.json"
if ($snapshot.snapshotType -notin @(
    "TRUE_MARKETDATA_SNAPSHOT",
    "QUBES_INPUT_MARKETDATA_SNAPSHOT",
    "QUBES_INPUT_SIGNAL_OR_RETURN_SNAPSHOT",
    "SANDBOX_STATIC_MARKETDATA_SNAPSHOT",
    "SANDBOX_PROTOTYPE_INPUT_SNAPSHOT",
    "EXTERNAL_SOURCE_CONTRACT_ONLY"
)) {
    throw "Invalid snapshot type."
}
if ($snapshot.snapshotScope -notin @(
    "SandboxResearchPreview",
    "SandboxSizingOnly",
    "SandboxQubesInputOnly",
    "SandboxMarkPreviewOnly",
    "ExternalContractOnly"
)) {
    throw "Invalid snapshot scope."
}
if ([string]::IsNullOrWhiteSpace($snapshot.marketDataSnapshotId)) {
    throw "MarketDataSnapshotId must exist when a source is selected."
}
if ($snapshot.containsWeights -eq $true) {
    throw "Output weights cannot be promoted as MarketData source."
}
if ($snapshot.instrumentMetadataBinding -match "is price source|as price source|used for prices") {
    throw "Instrument metadata must not be promoted as market price source."
}
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    if ($snapshot.symbols -notcontains $symbol) {
        throw "Snapshot contract missing required symbol $symbol."
    }
}

$coverage = Read-JsonArtifact "phase-canonical-marketdata-golden-source-r001-coverage-evidence.json"
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    $entry = $coverage.coverage | Where-Object { $_.symbol -eq $symbol }
    if ($null -eq $entry) {
        throw "Coverage missing for $symbol."
    }
    if ($entry.classification -notin @(
        "PRICE_AT_CANONICAL_CLOSE_PRESENT",
        "NEAREST_BEFORE_CLOSE_PRICE_PRESENT",
        "BAR_CLOSE_PRICE_PRESENT",
        "QUOTE_MID_PRICE_PRESENT",
        "RETURNS_OR_SIGNALS_ONLY_NO_PRICE",
        "MARK_PRESENT",
        "NO_PRICE_EVIDENCE",
        "SOURCE_UNAVAILABLE"
    )) {
        throw "Invalid coverage classification for $symbol."
    }
}

$usage = Read-JsonArtifact "phase-canonical-marketdata-golden-source-r001-usage-policy.json"
if ($usage.canUseForNetPnl -eq $true -or $usage.canUseForAccountingPnl -eq $true -or $usage.canUseForLedgerCommit -eq $true -or $usage.canUseForProductionLive -eq $true) {
    throw "Usage policy overclaims net/accounting/ledger/production readiness."
}
if ($usage.sizingPriceBasisStatus -notin @("READY", "READY_WITH_WARNINGS", "BLOCKED_MISSING_PRICE", "BLOCKED_POLICY", "NOT_APPLICABLE")) {
    throw "Invalid sizing price basis status."
}
if ($usage.markPolicyStatus -notin @("READY_EXACT_CLOSE", "READY_NEAREST_BEFORE_CLOSE", "READY_BAR_CLOSE", "BLOCKED_NO_MARK_POLICY", "BLOCKED_NO_PRICE", "NOT_APPLICABLE")) {
    throw "Invalid mark policy status."
}
if ($usage.accountingUseStatus -notin @("BLOCKED_NOT_ACCOUNTING_SOURCE", "BLOCKED_MISSING_ACCOUNTING_POLICY", "BLOCKED_MISSING_ACCOUNT_CURRENCY", "READY_WITH_ACCOUNTING_POLICY", "NOT_APPLICABLE")) {
    throw "Invalid accounting use status."
}

$binding = Read-JsonArtifact "phase-canonical-marketdata-golden-source-r001-consumer-binding-plan.json"
if ([string]::IsNullOrWhiteSpace($binding.marketDataSnapshotId) -or $binding.bindings.Count -lt 3) {
    throw "Consumer binding plan is incomplete."
}

$dbRole = Read-JsonArtifact "phase-canonical-marketdata-golden-source-r001-db-role-decision.json"
if ($dbRole.classification -notin @(
    "DB_PROJECTION_LAYER_OVER_GOLDEN_SOURCE",
    "DB_CANONICAL_ONLY_IF_BACKED_BY_SOURCE_MANIFEST",
    "DB_UNAVAILABLE_BUT_NOT_BLOCKING_GOLDEN_SOURCE",
    "DB_REQUIRED_AND_BLOCKING",
    "DB_ROLE_CONTRADICTORY"
)) {
    throw "Invalid DB role classification."
}
if ($dbRole.dbIsGoldenSourceByItself -eq $true) {
    throw "DB must not be treated as the golden source by itself."
}

$contracts = Read-JsonArtifact "phase-canonical-marketdata-golden-source-r001-contract-status-update.json"
$statusByContract = @{}
foreach ($entry in $contracts.contractStatuses) {
    $statusByContract[$entry.contract] = $entry.status
}
if ($statusByContract["accounting-attribution.v1"] -ne "BLOCKED") {
    throw "Accounting attribution must remain BLOCKED."
}
if ($statusByContract["production-readiness.v1"] -ne "BLOCKED") {
    throw "Production readiness must remain BLOCKED."
}
if ($statusByContract["canonical-marketdata-source.v1"] -eq "YES" -and [string]::IsNullOrWhiteSpace($snapshot.marketDataSnapshotId)) {
    throw "Canonical MarketData source cannot be YES without MarketDataSnapshotId."
}
if ($statusByContract["pms-sizing-price-basis.v1"] -eq "YES" -and $usage.sizingPriceBasisStatus -notin @("READY", "READY_WITH_WARNINGS")) {
    throw "Sizing price basis contract inconsistent with usage policy."
}

Write-Host "CANONICAL-MARKETDATA-GOLDEN-SOURCE-R001 gate passed."

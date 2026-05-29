param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) { throw "MARKETDATA-LOCALDB-RECOVERY-R005 gate failed: $Message" }
function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { Fail "missing artifact: $Path" }
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$artifactDir = Join-Path $RepoRoot "artifacts/readiness/marketdata-localdb-recovery-r005"
$required = @(
    "phase-marketdata-localdb-recovery-r005-summary.md",
    "phase-marketdata-localdb-recovery-r005-pre-recovery-diagnosis.json",
    "phase-marketdata-localdb-recovery-r005-recovery-action-log.json",
    "phase-marketdata-localdb-recovery-r005-post-recovery-state.json",
    "phase-marketdata-localdb-recovery-r005-db-provenance-evidence.json",
    "phase-marketdata-localdb-recovery-r005-database-queryability-evidence.json",
    "phase-marketdata-localdb-recovery-r005-schema-table-evidence.json",
    "phase-marketdata-localdb-recovery-r005-rowcount-window-evidence.json",
    "phase-marketdata-localdb-recovery-r005-mark-reference-evidence.json",
    "phase-marketdata-localdb-recovery-r005-mark-policy-evidence.json",
    "phase-marketdata-localdb-recovery-r005-m30-policy-evidence.json",
    "phase-marketdata-localdb-recovery-r005-fx-policy-evidence.json",
    "phase-marketdata-localdb-recovery-r005-contract-status-update.json",
    "phase-marketdata-localdb-recovery-r005-boundary-safety-evidence.json",
    "phase-marketdata-localdb-recovery-r005-build-checks-evidence.json"
)
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) { Fail "required artifact missing: $name" }
}

$joined = (Get-ChildItem -LiteralPath $artifactDir -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$secretPatterns = @("Password\s*=", "Pwd\s*=", "User\s+ID\s*=", "Uid\s*=", "Data\s+Source\s*=", "Server\s*=", "Initial\s+Catalog\s*=")
foreach ($pattern in $secretPatterns) {
    if ($joined -match $pattern) { Fail "possible credential/connection value persisted: $pattern" }
}
$forbiddenTruePatterns = @(
    '"productionLiveReady"\s*:\s*true',
    '"productionPnlReady"\s*:\s*true',
    '"accountingPnlReady"\s*:\s*true',
    '"netPnlReady"\s*:\s*true',
    '"ledgerCommitReady"\s*:\s*true',
    '"accountCurrencyAggregationReady"\s*:\s*true',
    '"externalMarketDataCalled"\s*:\s*true',
    '"liveBrokerDataUsed"\s*:\s*true',
    '"productionCandidateUsed"\s*:\s*true',
    '"productionTargetUsed"\s*:\s*true',
    '"unknownProvenanceUsed"\s*:\s*true',
    '"fixtureStaticDataOverstatedAsMarketDataReady"\s*:\s*true'
)
foreach ($pattern in $forbiddenTruePatterns) {
    if ($joined -match $pattern) { Fail "forbidden claim found: $pattern" }
}

$diagnosis = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-pre-recovery-diagnosis.json")
if ($diagnosis.localDbToolingAvailable -ne $true) { Fail "LocalDB tooling availability not diagnosed" }
if ($diagnosis.existingLocalDbInstances -notcontains "MSSQLLocalDB") { Fail "MSSQLLocalDB not diagnosed" }

$actions = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-recovery-action-log.json")
$allowedExecuted = @("SAFE_LOCAL_ENVIRONMENT_ACTION","SAFE_LOCAL_DEV_INSTANCE_CREATION","SAFE_LOCAL_DEV_ATTACH_OR_RESTORE","SAFE_REPO_LOCAL_BOOTSTRAP")
foreach ($action in $actions.executedActions) {
    if ($action.classification -notin $allowedExecuted) { Fail "executed unsafe/unclassified recovery action: $($action.actionId)" }
    if ($action.localDevSandboxOnly -ne $true) { Fail "executed action not local/dev/sandbox scoped: $($action.actionId)" }
    if ($action.touchesBusinessMarketTradingLedgerData -ne $false) { Fail "executed action touched business/market/trading/ledger data: $($action.actionId)" }
}
if ($actions.unsafeOrDestructiveActionsExecuted -ne $false -or $actions.productionTargetUsed -ne $false) { Fail "unsafe/destructive/production action executed" }

$state = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-post-recovery-state.json")
if ($state.localDbRecovered -eq $true -and $state.qqProductionIntradayOnlineQueryable -ne $true) { Fail "recovered state inconsistent" }

$provenance = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-db-provenance-evidence.json")
if ($provenance.dbProvenanceClassification -notin @("EXISTING_LOCALDB_DATABASE","EXISTING_LOCAL_MDF_ATTACHED","EXISTING_LOCAL_BACKUP_RESTORED","REPO_LOCAL_BOOTSTRAP_SCHEMA_ONLY","REPO_LOCAL_BOOTSTRAP_WITH_STATIC_FIXTURE_DATA","UNKNOWN_PROVENANCE","NOT_QUERYABLE")) { Fail "invalid DB provenance classification" }
if ($provenance.productionCandidateUsed -ne $false) { Fail "production DB candidate used" }
if ($provenance.unknownProvenanceUsed -ne $false) { Fail "unknown provenance used" }

$query = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-database-queryability-evidence.json")
if ($query.nonSelectQueriesExecuted -ne $false -or $query.dbMutationAttempted -ne $false) { Fail "non-SELECT or DB mutation recorded" }
if ($query.credentialValuesPrinted -ne $false -or $query.credentialValuesPersisted -ne $false) { Fail "credential value printed/persisted" }

$schema = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-schema-table-evidence.json")
if ($schema.dbQueryable -eq $true -and $schema.actualDbMetadataCollected -ne $true) { Fail "queryable DB without actual metadata" }
foreach ($layer in $schema.schemaLayers) {
    if ($layer.classification -notin @("DB_PRESENT","DB_PRESENT_BUT_COLUMNS_INCOMPLETE","SOURCE_ONLY","MISSING","NOT_APPLICABLE","UNKNOWN_CONNECTION_UNAVAILABLE","DB_NOT_QUERYABLE","FIXTURE_ONLY")) { Fail "invalid schema classification: $($layer.layer)" }
}

$rows = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-rowcount-window-evidence.json")
foreach ($layer in $rows.countsByLayer) {
    foreach ($row in $layer.rows) {
        if ($row.symbol -notin @("AUDUSD","EURUSD","GBPUSD")) { Fail "unexpected row symbol: $($row.symbol)" }
        if ($rows.dbQueryable -eq $false -and ($null -ne $row.rowCount -or $row.status -ne "DB_NOT_QUERYABLE")) { Fail "row evidence inconsistent with non-queryable DB" }
    }
}

$marks = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-mark-reference-evidence.json")
if ($marks.externalMarketDataCalled -ne $false -or $marks.liveBrokerDataUsed -ne $false -or $marks.marksInvented -ne $false -or $marks.fillsUsedAsMarks -ne $false) { Fail "mark/reference boundary failed" }
foreach ($symbol in $marks.symbolEvidence) {
    if ($symbol.symbol -notin @("AUDUSD","EURUSD","GBPUSD")) { Fail "unexpected mark symbol: $($symbol.symbol)" }
}

$markPolicy = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-mark-policy-evidence.json")
if ($markPolicy.markPolicyClassification -notin @("EXACT_CLOSE_MARK_POLICY_READY","NEAREST_BEFORE_CLOSE_POLICY_READY","BAR_CLOSE_POLICY_READY","M30_MARK_POLICY_READY","NO_ACTIVE_MARK_POLICY_FOUND","POLICY_CONTRADICTORY")) { Fail "mark policy not classified" }

$m30 = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-m30-policy-evidence.json")
if ($m30.m30Classification -notin @("M30_READY_WITH_DB_EVIDENCE","M30_DB_PRESENT_NO_RELEVANT_ROWS","M30_SOURCE_ONLY","M30_UNSUPPORTED_NOT_REQUIRED_FOR_CURRENT_SANDBOX_GROSS_PNL_V0","M30_BLOCKER_FOR_THEORETICAL_OR_ACCOUNTING_PNL","M30_UNKNOWN_DB_NOT_QUERYABLE","M30_POLICY_CONTRADICTORY","M30_FIXTURE_ONLY")) { Fail "M30 not classified" }

$fx = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-fx-policy-evidence.json")
if ($fx.fxAccountCurrencyClassification -notin @("QUOTE_CURRENCY_ONLY_READY_FOR_SANDBOX_GROSS_PNL_V0","ACCOUNT_CURRENCY_CONVERSION_READY_WITH_EXPLICIT_EVIDENCE","ACCOUNT_CURRENCY_CONVERSION_BLOCKED_MISSING_ACCOUNT_CURRENCY","ACCOUNT_CURRENCY_CONVERSION_BLOCKED_MISSING_FX_SOURCE","ACCOUNT_CURRENCY_POLICY_CONTRADICTORY")) { Fail "FX classification invalid" }
if ($fx.accountCurrencyInferred -ne $false -or $fx.fxRatesInvented -ne $false -or $fx.accountCurrencyAggregationReady -ne $false) { Fail "FX/account currency boundary failed" }

$contracts = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-contract-status-update.json")
$map = @{}
foreach ($status in $contracts.statuses) {
    if ($status.status -notin @("YES","WITH_WARNINGS","BLOCKED")) { Fail "invalid contract status for $($status.contractId)" }
    $map[$status.contractId] = $status.status
}
if ($map["canonical-timing.v1"] -ne "YES") { Fail "canonical timing not YES" }
if ($map["environment-secret.v1"] -ne "YES") { Fail "environment secret not YES" }
if ($map["lmax-marketdata-db.v1"] -eq "YES" -and $schema.actualDbMetadataCollected -ne $true) { Fail "lmax-marketdata-db YES without actual DB metadata" }
if ($map["marketdata-readiness.v1"] -eq "YES" -and ($markPolicy.markPolicyClassification -eq "NO_ACTIVE_MARK_POLICY_FOUND" -or $rows.rowWindowEvidenceCollected -ne $true)) { Fail "marketdata-readiness YES without row/mark policy closure" }
foreach ($flag in @("theoreticalPnlReady","netPnlReady","accountingPnlReady","productionPnlReady","accountCurrencyAggregationReady","ledgerCommitReady","productionLiveReady","fixtureStaticDataOverstatedAsMarketDataReady")) {
    if ($contracts.$flag -ne $false) { Fail "forbidden contract readiness promoted: $flag" }
}

$safety = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-boundary-safety-evidence.json")
if ($safety.status -ne "PASS") { Fail "boundary safety not PASS" }
$safety.PSObject.Properties | Where-Object { $_.Name -like "no*" } | ForEach-Object {
    if ($_.Value -ne $true) { Fail "boundary safety failed: $($_.Name)" }
}

$checks = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-recovery-r005-build-checks-evidence.json")
if ($checks.dotnetBuildNoRestore.status -notin @("Passed","PassedWithWarnings")) { Fail "build evidence missing" }
if ($checks.focusedStaticChecks.jsonArtifactsParse -ne "Passed") { Fail "JSON parse check missing" }
if ($checks.focusedStaticChecks.secretScan -ne "Passed") { Fail "secret scan missing" }
if ($checks.focusedStaticChecks.boundaryClaimScan -ne "Passed") { Fail "boundary scan missing" }
if ($checks.validator.status -notin @("Pending","Passed")) { Fail "validator evidence missing" }

Write-Host "MARKETDATA-LOCALDB-RECOVERY-R005 gate passed"

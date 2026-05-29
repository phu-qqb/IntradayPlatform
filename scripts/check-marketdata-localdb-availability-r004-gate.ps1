param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "MARKETDATA-LOCALDB-AVAILABILITY-R004 gate failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { Fail "missing artifact: $Path" }
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$artifactDir = Join-Path $RepoRoot "artifacts/readiness/marketdata-localdb-availability-r004"
$required = @(
    "phase-marketdata-localdb-availability-r004-summary.md",
    "phase-marketdata-localdb-availability-r004-localdb-availability-evidence.json",
    "phase-marketdata-localdb-availability-r004-database-existence-evidence.json",
    "phase-marketdata-localdb-availability-r004-readonly-connection-evidence.json",
    "phase-marketdata-localdb-availability-r004-schema-table-evidence.json",
    "phase-marketdata-localdb-availability-r004-rowcount-window-evidence.json",
    "phase-marketdata-localdb-availability-r004-mark-reference-evidence.json",
    "phase-marketdata-localdb-availability-r004-mark-policy-evidence.json",
    "phase-marketdata-localdb-availability-r004-m30-policy-evidence.json",
    "phase-marketdata-localdb-availability-r004-fx-policy-evidence.json",
    "phase-marketdata-localdb-availability-r004-contract-status-update.json",
    "phase-marketdata-localdb-availability-r004-boundary-safety-evidence.json",
    "phase-marketdata-localdb-availability-r004-build-checks-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) { Fail "required artifact missing: $name" }
}

$joined = (Get-ChildItem -LiteralPath $artifactDir -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$secretPatterns = @("Password\s*=", "Pwd\s*=", "User\s+ID\s*=", "Uid\s*=", "Data\s+Source\s*=", "Server\s*=", "Initial\s+Catalog\s*=")
foreach ($pattern in $secretPatterns) {
    if ($joined -match $pattern) { Fail "possible credential/connection value persisted: $pattern" }
}

$forbiddenReady = @(
    '"productionLiveReady"\s*:\s*true',
    '"productionPnlReady"\s*:\s*true',
    '"accountingPnlReady"\s*:\s*true',
    '"netPnlReady"\s*:\s*true',
    '"ledgerCommitReady"\s*:\s*true',
    '"accountCurrencyAggregationReady"\s*:\s*true'
)
foreach ($pattern in $forbiddenReady) {
    if ($joined -match $pattern) { Fail "forbidden readiness claim found: $pattern" }
}

$localdb = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-localdb-availability-evidence.json")
if ($localdb.localDbCommandAvailable -ne $true) { Fail "LocalDB command availability not checked/available" }
if ($localdb.targetInstance -ne "MSSQLLocalDB") { Fail "MSSQLLocalDB status not classified" }
if ([string]::IsNullOrWhiteSpace($localdb.finalTargetInstanceStatus)) { Fail "final MSSQLLocalDB status missing" }
if ($localdb.startAttempted -ne $true) { Fail "MSSQLLocalDB start attempt not recorded" }
foreach ($flag in @("dbMutationAttempted","migrationRun","schemaCreationRun","seedRun","resetRun","destructiveCommandRun","credentialValuesPrinted","credentialValuesPersisted")) {
    if ($localdb.$flag -ne $false) { Fail "forbidden LocalDB action recorded: $flag" }
}

$db = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-database-existence-evidence.json")
if ($db.databaseName -ne "QQProductionIntraday") { Fail "QQProductionIntraday existence not classified" }
if ($db.databaseExistenceCheckAttempted -ne $true) { Fail "database existence check not attempted/classified" }
if ($db.productionDbCandidate -ne $false) { Fail "production DB candidate recorded" }
if ($db.attachCreateMigrateSeedRepairAttempted -ne $false -or $db.dbMutationAttempted -ne $false) { Fail "DB repair/mutation attempted" }

$conn = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-readonly-connection-evidence.json")
if ($conn.safeReadOnlyConnectionAttempted -ne $true) { Fail "safe read-only connection not attempted" }
if ($conn.credentialValuesPrinted -ne $false -or $conn.credentialValuesPersisted -ne $false) { Fail "connection credential value printed/persisted" }
foreach ($flag in @("dbMutationAttempted","migrationRun","schemaCreationRun","seedRun","resetRun","dropRun","truncateRun","insertRun","updateRun","deleteRun","destructiveCommandRun")) {
    if ($conn.$flag -ne $false) { Fail "forbidden DB action recorded: $flag" }
}

$schema = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-schema-table-evidence.json")
if ($schema.dbQueryable -eq $true -and $schema.schemaEvidenceBasedOnActualDbMetadata -ne $true) { Fail "queryable DB requires actual metadata schema evidence" }
foreach ($layer in $schema.schemaLayers) {
    if ($layer.classification -notin @("DB_PRESENT","DB_PRESENT_BUT_COLUMNS_INCOMPLETE","SOURCE_ONLY","MISSING","NOT_APPLICABLE","UNKNOWN_CONNECTION_UNAVAILABLE","LOCALDB_AVAILABLE_DATABASE_MISSING")) {
        Fail "invalid schema classification: $($layer.layer)"
    }
}

$rows = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-rowcount-window-evidence.json")
if ($rows.dbQueryable -eq $true -and $rows.rowWindowEvidenceCollected -ne $true) { Fail "queryable DB requires row/window evidence" }
foreach ($layer in $rows.countsByLayer) {
    foreach ($row in $layer.rows) {
        if ($row.symbol -notin @("AUDUSD","EURUSD","GBPUSD")) { Fail "unexpected rowcount symbol: $($row.symbol)" }
        if ($rows.dbQueryable -eq $false -and ($null -ne $row.rowCount -or $row.status -ne "UNKNOWN_CONNECTION_UNAVAILABLE")) { Fail "row evidence inconsistent with unavailable DB" }
    }
}

$marks = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-mark-reference-evidence.json")
if ($marks.externalMarketDataCalled -ne $false -or $marks.liveMarketDataRequested -ne $false -or $marks.marksFabricated -ne $false) { Fail "mark boundary failed" }
foreach ($symbol in $marks.symbols) {
    if ($symbol.symbol -notin @("AUDUSD","EURUSD","GBPUSD")) { Fail "unexpected mark symbol: $($symbol.symbol)" }
    if ($marks.dbQueryable -eq $false -and $symbol.classification -ne "UNKNOWN_CONNECTION_UNAVAILABLE") { Fail "mark status inconsistent with unavailable DB" }
}

$markPolicy = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-mark-policy-evidence.json")
if ($markPolicy.markPolicyClassification -notin @("EXACT_CLOSE_MARK_POLICY_READY","NEAREST_BEFORE_CLOSE_POLICY_READY","BAR_CLOSE_POLICY_READY","M30_MARK_POLICY_READY","NO_ACTIVE_MARK_POLICY_FOUND","POLICY_CONTRADICTORY")) { Fail "mark policy not explicitly classified" }
if ($markPolicy.policyContradictory -eq $true -and $markPolicy.markPolicyClassification -ne "POLICY_CONTRADICTORY") { Fail "mark policy contradiction inconsistent" }

$m30 = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-m30-policy-evidence.json")
if ($m30.m30Classification -notin @("M30_READY_WITH_DB_EVIDENCE","M30_DB_PRESENT_NO_RELEVANT_ROWS","M30_SOURCE_ONLY","M30_UNSUPPORTED_NOT_REQUIRED_FOR_CURRENT_SANDBOX_GROSS_PNL_V0","M30_BLOCKER_FOR_THEORETICAL_OR_ACCOUNTING_PNL","M30_UNKNOWN_DB_NOT_QUERYABLE","M30_POLICY_CONTRADICTORY")) { Fail "M30 not explicitly classified" }
if ($m30.m30EvidenceFabricated -ne $false) { Fail "M30 evidence fabricated" }

$fx = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-fx-policy-evidence.json")
if ($fx.fxAccountCurrencyClassification -notin @("QUOTE_CURRENCY_ONLY_READY_FOR_SANDBOX_GROSS_PNL_V0","ACCOUNT_CURRENCY_CONVERSION_READY_WITH_EXPLICIT_EVIDENCE","ACCOUNT_CURRENCY_CONVERSION_BLOCKED_MISSING_ACCOUNT_CURRENCY","ACCOUNT_CURRENCY_CONVERSION_BLOCKED_MISSING_FX_SOURCE","ACCOUNT_CURRENCY_POLICY_CONTRADICTORY")) { Fail "FX/account-currency not classified" }
if ($fx.accountCurrencyInferred -ne $false -or $fx.fxRatesInvented -ne $false) { Fail "FX/account currency invented" }
if ($fx.accountCurrencyAggregationReady -ne $false -or $fx.netPnlReady -ne $false -or $fx.accountingPnlReady -ne $false -or $fx.productionPnlReady -ne $false) { Fail "FX/PnL readiness promoted" }

$contracts = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-contract-status-update.json")
$map = @{}
foreach ($status in $contracts.statuses) {
    if ($status.status -notin @("YES","WITH_WARNINGS","BLOCKED")) { Fail "invalid contract status for $($status.contractId)" }
    $map[$status.contractId] = $status.status
}
if ($map["canonical-timing.v1"] -ne "YES") { Fail "canonical timing not YES" }
if ($map["environment-secret.v1"] -ne "YES") { Fail "environment secret not YES" }
if ($map["lmax-marketdata-db.v1"] -eq "YES" -and $schema.schemaEvidenceBasedOnActualDbMetadata -ne $true) { Fail "lmax-marketdata-db YES without actual DB schema evidence" }
if ($map["marketdata-readiness.v1"] -eq "YES" -and ($marks.dbQueryable -ne $true -or $markPolicy.markPolicyClassification -eq "NO_ACTIVE_MARK_POLICY_FOUND")) { Fail "marketdata readiness YES without mark/policy closure" }
if ($contracts.netPnlReady -ne $false -or $contracts.accountingPnlReady -ne $false -or $contracts.productionPnlReady -ne $false -or $contracts.ledgerCommitReady -ne $false -or $contracts.productionLiveReady -ne $false) { Fail "contract status promoted blocked readiness" }

$safety = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-boundary-safety-evidence.json")
if ($safety.status -ne "PASS") { Fail "boundary safety did not pass" }
$safety.PSObject.Properties | Where-Object { $_.Name -like "no*" } | ForEach-Object {
    if ($_.Value -ne $true) { Fail "boundary safety failed: $($_.Name)" }
}

$checks = Read-Json (Join-Path $artifactDir "phase-marketdata-localdb-availability-r004-build-checks-evidence.json")
if ($checks.dotnetBuildNoRestore.status -notin @("Passed","PassedWithWarnings")) { Fail "build evidence missing" }
if ($checks.focusedStaticChecks.jsonArtifactsParse -ne "Passed") { Fail "JSON static check missing" }
if ($checks.focusedStaticChecks.secretScan -ne "Passed") { Fail "secret scan missing" }
if ($checks.focusedStaticChecks.boundaryClaimScan -ne "Passed") { Fail "boundary scan missing" }
if ($checks.validator.status -notin @("Pending","Passed")) { Fail "validator evidence missing" }

Write-Host "MARKETDATA-LOCALDB-AVAILABILITY-R004 gate passed"

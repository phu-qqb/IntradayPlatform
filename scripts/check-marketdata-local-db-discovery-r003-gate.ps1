param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "MARKETDATA-LOCAL-DB-DISCOVERY-R003 gate failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "missing artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$artifactDir = Join-Path $RepoRoot "artifacts/readiness/marketdata-local-db-discovery-r003"
$required = @(
    "phase-marketdata-local-db-discovery-r003-summary.md",
    "phase-marketdata-local-db-discovery-r003-db-discovery-evidence.json",
    "phase-marketdata-local-db-discovery-r003-db-connection-evidence.json",
    "phase-marketdata-local-db-discovery-r003-schema-table-evidence.json",
    "phase-marketdata-local-db-discovery-r003-rowcount-window-evidence.json",
    "phase-marketdata-local-db-discovery-r003-mark-reference-evidence.json",
    "phase-marketdata-local-db-discovery-r003-mark-policy-discovery.json",
    "phase-marketdata-local-db-discovery-r003-m30-policy-evidence.json",
    "phase-marketdata-local-db-discovery-r003-fx-policy-evidence.json",
    "phase-marketdata-local-db-discovery-r003-contract-status-update.json",
    "phase-marketdata-local-db-discovery-r003-build-checks-evidence.json"
)

foreach ($name in $required) {
    $path = Join-Path $artifactDir $name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "required artifact missing: $name"
    }
}

$joined = (Get-ChildItem -LiteralPath $artifactDir -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$secretPatterns = @(
    "Password\s*=",
    "Pwd\s*=",
    "User\s+ID\s*=",
    "Uid\s*=",
    "Data\s+Source\s*=",
    "Server\s*=\s*\(",
    "Initial\s+Catalog\s*="
)
foreach ($pattern in $secretPatterns) {
    if ($joined -match $pattern) {
        Fail "possible credential/connection string value persisted: $pattern"
    }
}

$forbiddenClaims = @(
    '"productionLiveReady"\s*:\s*true',
    '"productionPnlReady"\s*:\s*true',
    '"accountingPnlReady"\s*:\s*true',
    '"netPnlReady"\s*:\s*true',
    '"ledgerCommitReady"\s*:\s*true',
    '"ledgerCommitAllowed"\s*:\s*true',
    '"dbMutationAttempted"\s*:\s*true',
    '"migrationRun"\s*:\s*true',
    '"schemaCreationRun"\s*:\s*true',
    '"seedRun"\s*:\s*true',
    '"destructiveCommandRun"\s*:\s*true',
    '"externalMarketDataCalled"\s*:\s*true',
    '"liveMarketDataRequested"\s*:\s*true'
)
foreach ($pattern in $forbiddenClaims) {
    if ($joined -match $pattern) {
        Fail "forbidden claim/action found: $pattern"
    }
}

$discovery = Read-Json (Join-Path $artifactDir "phase-marketdata-local-db-discovery-r003-db-discovery-evidence.json")
if ($discovery.discoveryBroaderThanQqProductionIntradayConnectionString -ne $true) { Fail "DB discovery did not broaden beyond QQPRODUCTIONINTRADAY_CONNECTION_STRING" }
if ($discovery.authoritativeCandidate.dbType -ne "SQL Server LocalDB") { Fail "authoritative DB candidate not classified as SQL Server LocalDB" }
if ($discovery.contradictoryDbEvidenceFound -ne $false -or $discovery.unsafeOrProductionDbUsageFound -ne $false) { Fail "contradictory or unsafe DB evidence recorded" }
if ($discovery.inspectedSources.Count -lt 5) { Fail "discovery sources too narrow" }

$connection = Read-Json (Join-Path $artifactDir "phase-marketdata-local-db-discovery-r003-db-connection-evidence.json")
if ($connection.connectionStringPrinted -ne $false -or $connection.connectionStringPersisted -ne $false -or $connection.credentialValuesRedacted -ne $true) { Fail "connection string redaction failed" }
if ($connection.dbMutationAttempted -ne $false -or $connection.migrationRun -ne $false -or $connection.schemaCreationRun -ne $false -or $connection.seedRun -ne $false -or $connection.destructiveCommandRun -ne $false) { Fail "DB mutation/migration/seed/create/destructive action recorded" }
if ($connection.readOnlyConnectionStatus -eq "FAILED_LOCALDB_INSTANCE_NOT_CREATED" -and [string]::IsNullOrWhiteSpace($connection.preciseUnavailableReason)) { Fail "DB unavailable reason is not precise" }

$schema = Read-Json (Join-Path $artifactDir "phase-marketdata-local-db-discovery-r003-schema-table-evidence.json")
if ($schema.sourceCodeSchemaPresenceIsNotDbPass -ne $true) { Fail "source-code schema was allowed as DB pass" }
if ($schema.dbPresentClaimed -ne $false) { Fail "DB presence claimed despite unavailable connection" }
foreach ($layer in $schema.schemaLayers) {
    if ($layer.classification -notin @("DB_PRESENT", "SOURCE_ONLY", "MISSING", "NOT_APPLICABLE", "UNKNOWN_CONNECTION_UNAVAILABLE")) {
        Fail "invalid schema classification for $($layer.layer)"
    }
}

$rows = Read-Json (Join-Path $artifactDir "phase-marketdata-local-db-discovery-r003-rowcount-window-evidence.json")
if ($rows.rowWindowEvidenceStatus -notin @("READ_ONLY_OBSERVED", "UNKNOWN_CONNECTION_UNAVAILABLE")) { Fail "invalid row/window evidence status" }
if ($rows.rowCountQueriesExecuted -ne $false -and $rows.dbQueryableReadOnly -ne $true) { Fail "rowcount queries executed without queryable DB" }
foreach ($layer in $rows.countsByLayer) {
    foreach ($count in $layer.counts) {
        if ($rows.dbQueryableReadOnly -eq $false -and ($null -ne $count.rowCount -or $count.status -ne "UNKNOWN_CONNECTION_UNAVAILABLE")) {
            Fail "row count inconsistent for unavailable DB: $($layer.layer)/$($count.symbol)"
        }
    }
}

$marks = Read-Json (Join-Path $artifactDir "phase-marketdata-local-db-discovery-r003-mark-reference-evidence.json")
if ($marks.marksInvented -ne $false -or $marks.externalMarketDataCalled -ne $false -or $marks.liveMarketDataRequested -ne $false) { Fail "mark evidence boundary failed" }
foreach ($symbol in $marks.symbolEvidence) {
    if ($marks.dbQueryableReadOnly -eq $false -and $symbol.status -ne "UNKNOWN_CONNECTION_UNAVAILABLE") { Fail "mark status inconsistent for unavailable DB: $($symbol.symbol)" }
}

$markPolicy = Read-Json (Join-Path $artifactDir "phase-marketdata-local-db-discovery-r003-mark-policy-discovery.json")
if ($markPolicy.nearestBeforeClosePolicyExists -ne $false -and $markPolicy.policyDiscoveryStatus -eq "NO_ACTIVE_MARK_POLICY_FOUND") { Fail "mark policy internally inconsistent" }

$m30 = Read-Json (Join-Path $artifactDir "phase-marketdata-local-db-discovery-r003-m30-policy-evidence.json")
if ($m30.m30PolicyClassification -notin @("M30_READY_WITH_DB_EVIDENCE", "M30_UNSUPPORTED_NOT_REQUIRED_FOR_CURRENT_SANDBOX_GROSS_PNL_V0", "M30_BLOCKER_FOR_THEORETICAL_OR_ACCOUNTING_PNL", "M30_UNKNOWN_DB_NOT_QUERYABLE")) { Fail "M30 not explicitly classified" }
if ($m30.m30EvidenceInvented -ne $false) { Fail "M30 evidence invented" }

$fx = Read-Json (Join-Path $artifactDir "phase-marketdata-local-db-discovery-r003-fx-policy-evidence.json")
if ($fx.fxRatesInvented -ne $false -or $fx.accountCurrencyInferred -ne $false) { Fail "FX/account currency invented" }
if ($fx.accountCurrencyAggregationReady -ne $false -or $fx.netPnlReady -ne $false -or $fx.accountingPnlReady -ne $false -or $fx.productionPnlReady -ne $false) { Fail "FX/PnL boundary promoted" }

$contracts = Read-Json (Join-Path $artifactDir "phase-marketdata-local-db-discovery-r003-contract-status-update.json")
$statusMap = @{}
foreach ($status in $contracts.statuses) {
    if ($status.status -notin @("YES", "WITH_WARNINGS", "BLOCKED")) { Fail "invalid contract status for $($status.contractId)" }
    $statusMap[$status.contractId] = $status.status
}
if ($statusMap["canonical-timing.v1"] -ne "YES") { Fail "canonical-timing.v1 must be YES" }
if ($statusMap["environment-secret.v1"] -ne "YES") { Fail "environment-secret.v1 must be YES" }
if ($statusMap["lmax-marketdata-db.v1"] -eq "YES" -and $rows.rowWindowEvidenceStatus -ne "READ_ONLY_OBSERVED") { Fail "lmax-marketdata-db.v1 cannot be YES without observed row/window evidence" }
if ($statusMap["marketdata-readiness.v1"] -eq "YES" -and $marks.markReferenceEvidenceStatus -notmatch "^PRESENT") { Fail "marketdata-readiness.v1 cannot be YES without mark evidence" }
if ($contracts.productionLiveReady -ne $false -or $contracts.ledgerCommitReady -ne $false -or $contracts.netPnlReady -ne $false -or $contracts.accountingPnlReady -ne $false) { Fail "contract status promoted forbidden readiness" }

$checks = Read-Json (Join-Path $artifactDir "phase-marketdata-local-db-discovery-r003-build-checks-evidence.json")
if ($checks.dotnetBuildNoRestore.status -notin @("Passed", "PassedWithWarnings")) { Fail "dotnet build evidence missing" }
if ($checks.focusedStaticChecks.jsonArtifactsParse -ne "Passed") { Fail "JSON parse evidence missing" }
if ($checks.focusedStaticChecks.secretScan -ne "Passed") { Fail "secret scan evidence missing" }
if ($checks.focusedStaticChecks.boundaryClaimScan -ne "Passed") { Fail "boundary claim scan evidence missing" }
if ($checks.validator.status -notin @("Pending", "Passed")) { Fail "validator evidence missing" }

Write-Host "MARKETDATA-LOCAL-DB-DISCOVERY-R003 gate passed"

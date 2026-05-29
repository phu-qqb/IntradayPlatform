param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "MARKETDATA-EVIDENCE-CLOSURE-R002 gate failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "missing artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$artifactDir = Join-Path $RepoRoot "artifacts/readiness/marketdata-evidence-closure-r002"
$required = @(
    "phase-marketdata-evidence-closure-r002-summary.md",
    "phase-marketdata-evidence-closure-r002-db-env-status.json",
    "phase-marketdata-evidence-closure-r002-schema-evidence.json",
    "phase-marketdata-evidence-closure-r002-rowcount-window-evidence.json",
    "phase-marketdata-evidence-closure-r002-mark-reference-evidence.json",
    "phase-marketdata-evidence-closure-r002-fx-policy-evidence.json",
    "phase-marketdata-evidence-closure-r002-m30-policy-evidence.json",
    "phase-marketdata-evidence-closure-r002-contract-status-update.json",
    "phase-marketdata-evidence-closure-r002-operator-evidence-template.json",
    "phase-marketdata-evidence-closure-r002-build-checks-evidence.json"
)

foreach ($name in $required) {
    $path = Join-Path $artifactDir $name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "required artifact missing: $name"
    }
}

$artifactText = Get-ChildItem -LiteralPath $artifactDir -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$secretPatterns = @(
    "Password\s*=",
    "Pwd\s*=",
    "User\s+ID\s*=",
    "Uid\s*=",
    "Server\s*=",
    "Data\s+Source\s*=",
    "Initial\s+Catalog\s*="
)
foreach ($pattern in $secretPatterns) {
    if ($joined -match $pattern) {
        Fail "artifact appears to contain credential/connection-string material matching $pattern"
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
    '"nonSelectQueryAttempted"\s*:\s*true',
    '"externalMarketDataCalled"\s*:\s*true',
    '"liveMarketDataRequested"\s*:\s*true'
)
foreach ($pattern in $forbiddenClaims) {
    if ($joined -match $pattern) {
        Fail "forbidden readiness/action claim found: $pattern"
    }
}

$env = Read-Json (Join-Path $artifactDir "phase-marketdata-evidence-closure-r002-db-env-status.json")
if ($env.connectionStringEnvVarName -ne "QQPRODUCTIONINTRADAY_CONNECTION_STRING") { Fail "unexpected connection string env var name" }
if ($env.credentialValuePrinted -ne $false -or $env.credentialValuePersisted -ne $false -or $env.credentialValuesRedacted -ne $true) { Fail "credential redaction audit failed" }
if ($env.connectionStringPresent -eq $false -and $env.dbEvidenceStatus -ne "OPERATOR_EVIDENCE_REQUIRED") { Fail "absent DB env var must classify DB evidence as operator-evidence-required" }
if ($env.dbMutationAttempted -ne $false -or $env.nonSelectQueryAttempted -ne $false) { Fail "DB mutation/non-SELECT recorded" }

$schema = Read-Json (Join-Path $artifactDir "phase-marketdata-evidence-closure-r002-schema-evidence.json")
if ($schema.sourceCodeSchemaIsSufficientForPass -ne $false) { Fail "source code schema alone cannot pass" }
if ($schema.actualDbEvidenceRequiredForPass -ne $true) { Fail "actual DB evidence requirement not preserved" }
foreach ($entry in $schema.sourceCodeSchemaPresence) {
    if ($entry.sourceCodePresent -ne $true) { Fail "source-code schema presence missing for $($entry.tableOrEntity)" }
    if ($entry.actualDbTableOrViewPresence -ne "OPERATOR_EVIDENCE_REQUIRED") { Fail "DB table/view evidence not classified for $($entry.tableOrEntity)" }
}

$rows = Read-Json (Join-Path $artifactDir "phase-marketdata-evidence-closure-r002-rowcount-window-evidence.json")
if ($rows.dbQueriesAttempted -ne $false -or $rows.nonSelectQueriesAttempted -ne $false -or $rows.dbMutationAttempted -ne $false) { Fail "rowcount evidence recorded a DB query/mutation" }
if ($rows.rowWindowEvidenceStatus -notin @("READ_ONLY_OBSERVED", "OPERATOR_EVIDENCE_REQUIRED")) { Fail "row/window evidence status invalid" }
foreach ($layer in $rows.countsByLayer) {
    foreach ($count in $layer.counts) {
        if ($rows.connectionStringPresent -eq $false -and $count.status -ne "OPERATOR_EVIDENCE_REQUIRED") { Fail "row count missing but not classified for $($count.symbol)/$($layer.layer)" }
    }
}

$marks = Read-Json (Join-Path $artifactDir "phase-marketdata-evidence-closure-r002-mark-reference-evidence.json")
if ($marks.marksInvented -ne $false -or $marks.externalMarketDataCalled -ne $false -or $marks.liveMarketDataRequested -ne $false) { Fail "mark evidence boundary failed" }
if ($marks.markReferenceEvidenceStatus -notin @("PRESENT", "MISSING_OPERATOR_EVIDENCE_REQUIRED")) { Fail "mark/reference evidence status invalid" }
foreach ($symbol in $marks.symbolEvidence) {
    if ($marks.markReferenceEvidenceStatus -eq "MISSING_OPERATOR_EVIDENCE_REQUIRED" -and $symbol.status -ne "MISSING_OPERATOR_EVIDENCE_REQUIRED") { Fail "missing mark evidence not explicit for $($symbol.symbol)" }
}

$fx = Read-Json (Join-Path $artifactDir "phase-marketdata-evidence-closure-r002-fx-policy-evidence.json")
if ($fx.fxRatesInvented -ne $false -or $fx.accountCurrencyInferred -ne $false) { Fail "FX/account currency invented" }
if ($fx.accountCurrencyAggregationReady -ne $false -or $fx.accountingPnlReady -ne $false -or $fx.netPnlReady -ne $false -or $fx.productionPnlReady -ne $false) { Fail "FX/accounting/net/production readiness incorrectly claimed" }

$m30 = Read-Json (Join-Path $artifactDir "phase-marketdata-evidence-closure-r002-m30-policy-evidence.json")
$allowedM30 = @(
    "M30_READY_WITH_DB_EVIDENCE",
    "M30_UNSUPPORTED_NOT_REQUIRED_FOR_CURRENT_SANDBOX_GROSS_PNL_V0",
    "M30_BLOCKER_FOR_THEORETICAL_OR_ACCOUNTING_PNL",
    "M30_OPERATOR_EVIDENCE_REQUIRED"
)
if ($m30.m30PolicyClassification -notin $allowedM30) { Fail "M30 policy not explicitly classified" }
if ($m30.m30EvidenceInvented -ne $false) { Fail "M30 evidence invented" }

$contracts = Read-Json (Join-Path $artifactDir "phase-marketdata-evidence-closure-r002-contract-status-update.json")
$statusMap = @{}
foreach ($status in $contracts.statuses) {
    $statusMap[$status.contractId] = $status.status
    if ($status.status -notin @("YES", "WITH_WARNINGS", "BLOCKED")) { Fail "invalid contract status for $($status.contractId)" }
}
if ($statusMap["canonical-timing.v1"] -ne "YES") { Fail "canonical-timing.v1 must be YES" }
if ($statusMap["environment-secret.v1"] -ne "YES") { Fail "environment-secret.v1 must be YES" }
if ($statusMap["lmax-marketdata-db.v1"] -eq "YES" -and $rows.rowWindowEvidenceStatus -ne "READ_ONLY_OBSERVED") { Fail "lmax-marketdata-db.v1 cannot be YES without observed row/window evidence" }
if ($statusMap["marketdata-readiness.v1"] -eq "YES" -and $marks.markReferenceEvidenceStatus -ne "PRESENT") { Fail "marketdata-readiness.v1 cannot be YES without mark/reference evidence" }
if ($contracts.productionLiveReady -ne $false -or $contracts.ledgerCommitReady -ne $false -or $contracts.accountingPnlReady -ne $false -or $contracts.netPnlReady -ne $false) { Fail "contract update promoted blocked readiness" }

$checks = Read-Json (Join-Path $artifactDir "phase-marketdata-evidence-closure-r002-build-checks-evidence.json")
if ($checks.dotnetBuildNoRestore.status -notin @("Passed", "PassedWithWarnings")) { Fail "dotnet build evidence missing" }
if ($checks.focusedStaticChecks.jsonArtifactsParse -ne "Passed") { Fail "JSON static check evidence missing" }
if ($checks.focusedStaticChecks.credentialValueScan -ne "Passed") { Fail "credential scan evidence missing" }
if ($checks.focusedStaticChecks.boundaryClaimScan -ne "Passed") { Fail "boundary scan evidence missing" }
if ($checks.validator.status -notin @("Pending", "Passed")) { Fail "validator evidence missing" }

Write-Host "MARKETDATA-EVIDENCE-CLOSURE-R002 gate passed"

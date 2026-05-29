param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "MARKETDATA-PRICING-READINESS-R001 gate failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "missing artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$artifactDir = Join-Path $RepoRoot "artifacts/readiness/marketdata-pricing-readiness"
$required = @(
    "phase-marketdata-pricing-readiness-r001-summary.md",
    "phase-marketdata-pricing-readiness-r001-product-readiness-r003-reference.json",
    "phase-marketdata-pricing-readiness-r001-db-connection-envvar-status.json",
    "phase-marketdata-pricing-readiness-r001-db-schema-rowcount-evidence.json",
    "phase-marketdata-pricing-readiness-r001-mark-reference-price-evidence.json",
    "phase-marketdata-pricing-readiness-r001-fx-reference-policy-evidence.json",
    "phase-marketdata-pricing-readiness-r001-m30-policy-evidence.json",
    "phase-marketdata-pricing-readiness-r001-tick-schema-completion-status.json",
    "phase-marketdata-pricing-readiness-r001-marketdata-contract-status-update.json",
    "phase-marketdata-pricing-readiness-r001-pnl-readiness-impact.json",
    "phase-marketdata-pricing-readiness-r001-no-secret-persistence-audit.json",
    "phase-marketdata-pricing-readiness-r001-no-external-audit.json",
    "phase-marketdata-pricing-readiness-r001-no-db-mutation-audit.json",
    "phase-marketdata-pricing-readiness-r001-no-order-fill-route-audit.json",
    "phase-marketdata-pricing-readiness-r001-forbidden-actions-audit.json",
    "phase-marketdata-pricing-readiness-r001-next-phase-recommendation.json",
    "phase-marketdata-pricing-readiness-r001-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    $path = Join-Path $artifactDir $name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "required artifact missing: $name"
    }
}

$envStatus = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-db-connection-envvar-status.json")
if ($envStatus.connectionStringEnvVarName -ne "QQPRODUCTIONINTRADAY_CONNECTION_STRING") { Fail "unexpected connection string env var name" }
if ($envStatus.credentialValuesRedacted -ne $true) { Fail "credential values are not marked redacted" }
if ($envStatus.connectionStringValuePrinted -ne $false -or $envStatus.connectionStringValuePersisted -ne $false) { Fail "credential value print/persist audit failed" }
if ($envStatus.connectionStringPresent -eq $false -and $envStatus.dbQueriesAttempted -ne $false) { Fail "DB query attempted without connection string" }

$schema = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-db-schema-rowcount-evidence.json")
if ($schema.dbMutationAttempted -ne $false -or $schema.nonSelectQueriesAttempted -ne $false) { Fail "DB mutation or non-SELECT query recorded" }
if ($schema.connectionStringPresent -eq $false) {
    foreach ($table in $schema.candidateTables) {
        if ($null -ne $table.rowCount) { Fail "row count invented while connection string is absent for $($table.tableName)" }
        if ($table.queryExecuted -ne $false) { Fail "query marked executed while connection string is absent for $($table.tableName)" }
    }
}

$marks = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-mark-reference-price-evidence.json")
if ($marks.liveMarketDataRequested -ne $false -or $marks.externalApiCalled -ne $false) { Fail "live or external mark source used" }
if ($marks.markPriceReadiness -ne "Missing" -or $marks.missingMarkPrices -ne $true) { Fail "mark prices must remain missing" }
foreach ($row in $marks.referencePriceRows) {
    if ($null -ne $row.markPrice) { Fail "mark price invented for $($row.symbol)" }
}

$fx = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-fx-reference-policy-evidence.json")
if ($fx.fxConversionInvented -ne $false -or $fx.accountCurrencyInvented -ne $false) { Fail "FX conversion or account currency invented" }
if ($fx.missingFxConversion -ne $true -or $fx.accountCurrencyAggregationReady -ne $false) { Fail "FX/account-currency blocker not preserved" }
foreach ($symbol in $fx.symbols) {
    if ($symbol.quotedCurrency -ne "USD") { Fail "non-USD quote currency recorded for $($symbol.symbol)" }
    if ($symbol.fxConversionReady -ne $false) { Fail "FX conversion claimed ready for $($symbol.symbol)" }
}

$m30 = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-m30-policy-evidence.json")
if ($m30.m30EvidencePresent -ne $false -or $m30.m30Invented -ne $false) { Fail "M30 evidence is claimed or invented" }
if ($m30.m30Readiness -ne "M30UnsupportedOrMissingEvidence") { Fail "unexpected M30 readiness status" }

$tick = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-tick-schema-completion-status.json")
if ($tick.tickSchemaReadiness -ne "Partial") { Fail "tick schema should remain Partial" }
if ($tick.lmaxMarketDataDbCanPass -ne $false -or $tick.marketdataReadinessCanPass -ne $false) { Fail "MarketData pass allowed despite missing evidence" }

$contracts = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-marketdata-contract-status-update.json")
$lmaxStatus = ($contracts.contractStatuses | Where-Object { $_.contractId -eq "lmax-marketdata-db.v1" }).status
$readinessStatus = ($contracts.contractStatuses | Where-Object { $_.contractId -eq "marketdata-readiness.v1" }).status
$timingStatus = ($contracts.contractStatuses | Where-Object { $_.contractId -eq "canonical-timing.v1" }).status
$secretStatus = ($contracts.contractStatuses | Where-Object { $_.contractId -eq "environment-secret.v1" }).status
if ($lmaxStatus -ne "WITH_WARNINGS") { Fail "lmax-marketdata-db.v1 misclassified: $lmaxStatus" }
if ($readinessStatus -ne "WITH_WARNINGS") { Fail "marketdata-readiness.v1 misclassified: $readinessStatus" }
if ($timingStatus -ne "YES") { Fail "canonical-timing.v1 not YES" }
if ($secretStatus -ne "YES") { Fail "environment-secret.v1 not YES" }
if ($contracts.marketDataPassClaimed -ne $false) { Fail "MarketData PASS falsely claimed" }
if ($contracts.productionExecutionPathEnabled -ne $false) { Fail "production execution path enabled" }

$pnl = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-pnl-readiness-impact.json")
if ($pnl.sandboxGrossRoundTripPnlPreviewV0Ready -ne $true) { Fail "existing sandbox gross PnL V0 readiness not preserved" }
if ($pnl.fullSandboxTheoreticalPnlReady -ne $false -or $pnl.paperAccountingPnlReady -ne $false -or $pnl.productionPnlReady -ne $false -or $pnl.ledgerCommitReady -ne $false) {
    Fail "blocked PnL/ledger status was promoted"
}

$secretAudit = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-no-secret-persistence-audit.json")
if ($secretAudit.status -ne "PASS" -or $secretAudit.credentialValuesPrinted -ne $false -or $secretAudit.credentialValuesPersisted -ne $false) { Fail "secret audit failed" }

$externalAudit = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-no-external-audit.json")
if ($externalAudit.status -ne "PASS" -or $externalAudit.externalApiCallOccurred -ne $false -or $externalAudit.lmaxExternalCallOccurred -ne $false -or $externalAudit.liveMarketDataRequested -ne $false) { Fail "external/no-live audit failed" }

$dbAudit = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-no-db-mutation-audit.json")
if ($dbAudit.status -ne "PASS" -or $dbAudit.dbMutationOccurred -ne $false -or $dbAudit.nonSelectQueryAttempted -ne $false -or $dbAudit.migrationCreatedOrApplied -ne $false) { Fail "DB mutation audit failed" }

$orderAudit = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-no-order-fill-route-audit.json")
if ($orderAudit.status -ne "PASS" -or $orderAudit.orderCreated -ne $false -or $orderAudit.routeCreated -ne $false -or $orderAudit.fillCreated -ne $false -or $orderAudit.executionReportCreated -ne $false) { Fail "order/fill/route audit failed" }

$forbidden = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-forbidden-actions-audit.json")
if ($forbidden.status -ne "PASS") { Fail "forbidden action audit did not pass" }
$forbidden.forbiddenActions.PSObject.Properties | ForEach-Object {
    if ($_.Value -ne $false) { Fail "forbidden action recorded: $($_.Name)" }
}

$evidence = Read-Json (Join-Path $artifactDir "phase-marketdata-pricing-readiness-r001-build-test-validator-evidence.json")
if ($evidence.dotnetBuildNoRestore.status -notin @("Passed", "PassedWithWarnings")) { Fail "dotnet build evidence missing or failed" }
if ($evidence.focusedStaticChecks.jsonArtifactsParse -ne "Passed") { Fail "JSON artifact static check missing" }
if ($evidence.focusedStaticChecks.forbiddenActionArtifactCheck -ne "Passed") { Fail "forbidden action static check missing" }
if ($evidence.validator.status -notin @("Pending", "Passed")) { Fail "validator evidence missing" }

Write-Host "MARKETDATA-PRICING-READINESS-R001 gate passed"

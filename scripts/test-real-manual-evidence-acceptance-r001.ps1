param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

function Assert-DecimalEqual([decimal]$Actual, [decimal]$Expected, [string]$Message) {
    if ([Math]::Abs($Actual - $Expected) -gt [decimal]0.000001) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Write-JsonFile([string]$Path, [object]$Value) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $Value | ConvertTo-Json -Depth 60 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function DummySha([string]$Nibble) {
    "sha256:$($Nibble * 64)"
}

function New-BrokerFixture([hashtable]$Overrides) {
    $fixture = [ordered]@{
        artifact_type = "real_broker_statement_manual_import_r001"
        environment = "sandbox"
        import_mode = "offline_manual"
        sample_only = $false
        real_broker_statement = $true
        source_file_name = "valid-real-broker-statement.json"
        source_file_sha256 = DummySha "A"
        raw_source_sha256_policy = "declared_raw_source_hash"
        imported_at_utc = "fixture"
        imported_by = "sandbox-operator"
        approval_id = "real-manual-evidence-acceptance-r001:test-broker"
        external_fetch = $false
        broker_api_call = $false
        market_data_fetch = $false
        account_data_fetch = $false
        db_mutation = $false
        ledger_commit = $false
        production_live_ready = $false
        trading_readiness_ready = $false
        broker = "LMAX"
        venue = "LMAX_GLOBAL"
        account_id_hash = "sha256:FEEC2C29E0EC68AB8E8078ED70A5FF7DBFDC78FABBF8843200C4AC9CD89032F8"
        account_currency = "USD"
        statement_period = [ordered]@{ start_utc = "fixture"; end_utc = "fixture" }
        statement_totals = [ordered]@{
            gross_pnl_usd = [decimal]-50.308800
            commission_usd = [decimal]26.268029
            net_pnl_usd = [decimal]-76.576829
        }
        positions = @()
        fills = @()
        cash_movements = @()
        fees = @()
        financing = @()
        excluded_lines = @(
            [ordered]@{ symbol = "USDJPY"; quantity = [decimal]50.0; reason = "unfilled" },
            [ordered]@{ symbol = "AUDUSD"; reason = "zero_quantity" },
            [ordered]@{ symbol = "CHFUSD"; reason = "zero_quantity" },
            [ordered]@{ symbol = "EURUSD"; reason = "zero_quantity" },
            [ordered]@{ symbol = "GBPUSD"; reason = "zero_quantity" }
        )
        raw_values_preserved = $true
        normalized_values = [ordered]@{}
        normalized_values_separated_from_raw = $true
    }
    foreach ($key in $Overrides.Keys) { $fixture[$key] = $Overrides[$key] }
    return $fixture
}

function New-AccountingFixture([hashtable]$Overrides) {
    $fixture = [ordered]@{
        artifact_type = "real_accounting_evidence_manual_import_r001"
        environment = "sandbox"
        import_mode = "offline_manual"
        sample_only = $false
        real_accounting_evidence = $true
        real_accounting_close = $false
        source_file_name = "valid-real-accounting-evidence.json"
        source_file_sha256 = DummySha "B"
        raw_source_sha256_policy = "declared_raw_source_hash"
        imported_at_utc = "fixture"
        imported_by = "sandbox-operator"
        approval_id = "real-manual-evidence-acceptance-r001:test-accounting"
        account_currency = "USD"
        accounting_policy_version = "sandbox-real-manual-test-policy-r001"
        accounting_basis = "fixture_policy"
        period = [ordered]@{ start_utc = "fixture"; end_utc = "fixture" }
        gross_pnl = [ordered]@{ currency = "USD"; amount = [decimal]-50.308800 }
        commission_expense = [ordered]@{ currency = "USD"; amount = [decimal]26.268029 }
        net_pnl = [ordered]@{ currency = "USD"; amount = [decimal]-76.576829 }
        realized_unrealized_classification = "sandbox_closed_round_trip_preview_only"
        fx_translation_policy = "prior_account_currency_fixture_policy"
        rounding_policy = "six_decimal_usd_preview"
        audit_trail = [ordered]@{ source = "deterministic_test_fixture"; preserved = $true }
        external_fetch = $false
        market_data_fetch = $false
        account_data_fetch = $false
        db_mutation = $false
        ledger_commit = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
    foreach ($key in $Overrides.Keys) { $fixture[$key] = $Overrides[$key] }
    return $fixture
}

function Write-RawLmaxBundle([string]$RawDir) {
    New-Item -ItemType Directory -Force -Path $RawDir | Out-Null
    @"
LMAX Global Trading Statement
Trading Statement Date: 30/04/2026 15:08:47
Statement Period From: 03/11/2025
Statement Period To: 03/11/2025
Account Number: LMAX-REAL-ACCOUNT-001
Account Name: Operator Real Evidence Account
Account Currency: USD
Traded Notional USD: 45125359.74
Opening Balance USD: 490697.13
Realised P/L USD: 6015.14
Commission USD Signed: -225.63
Financing USD Signed: -40.60
Closing Balance USD: 496446.04
Closing P/L USD: 463.61
Closing Equity USD: 496909.65
Margin on Open Positions USD: 10728.19
Available to Trade USD: 486181.46
"@ | Set-Content -LiteralPath (Join-Path $RawDir "LMAX-account-statement-2026-04-30.pdf") -Encoding UTF8

    @"
Currency,P&L,Commission,Financing
CAD,4492.54,-70.76315,
CHF,693.02,-15.25343,
JPY,741809.00,-9891.71546,
USD,-2837.39,-92.43356,-40.59720
"@ | Set-Content -LiteralPath (Join-Path $RawDir "currency-wallets.csv") -Encoding UTF8

    @"
Instrument,Open Quantity,Average Opening Price,Closing Price,Open P/L
NZD/USD,+125.3,0.57043,0.5708,463.61
"@ | Set-Content -LiteralPath (Join-Path $RawDir "open-positions.csv") -Encoding UTF8
}

$builder = Join-Path $RepoRoot "scripts\build-real-manual-evidence-acceptance-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-real-manual-evidence-acceptance-r001-gate.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$emptySubdir = "real-manual-evidence-acceptance-r001-empty-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $emptySubdir -PackageLocalDiscoveryOnly | Out-Null
$emptyArtifactDir = Join-Path $RepoRoot "artifacts\readiness\$emptySubdir"
$emptyMain = Read-JsonFile (Join-Path $emptyArtifactDir "real-manual-evidence-acceptance-r001.json")
$emptyDiscovery = Read-JsonFile (Join-Path $emptyArtifactDir "real-manual-evidence-discovery-report-r001.json")
Assert-Equal $emptyMain.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_BLOCKED_R001" "Empty staging must be accepted-blocked."
Assert-Equal $emptyMain.blocked_reason "NO_REAL_MANUAL_EVIDENCE_FILES_IN_STAGING" "Empty staging blocked reason mismatch."
Assert-Equal $emptyMain.staging_scan.broker_statement_files_seen 0 "Empty broker staging must be empty."
Assert-Equal $emptyMain.staging_scan.accounting_evidence_files_seen 0 "Empty accounting staging must be empty."
Assert-Equal $emptyMain.real_broker_evidence_lane.raw_lmax_bundle_seen $false "Empty raw LMAX bundle should be absent."
Assert-True ($emptyDiscovery.discovered_candidate_files_count -gt 0) "Discovery report should include local sample/schema candidates."
Assert-True ($emptyDiscovery.sample_files_count -gt 0) "Sample files should be discovered and reported."
Assert-Equal $emptyDiscovery.real_non_sample_candidate_count 0 "Empty discovery should not find real non-sample candidates."
Assert-Equal @($emptyDiscovery.recommended_operator_actions).Count 0 "Empty discovery should not recommend copies without real candidates."

$testSubdir = "real-manual-evidence-acceptance-r001-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $testSubdir | Out-Null
$testDir = Join-Path $RepoRoot "artifacts\readiness\$testSubdir"
$brokerStage = Join-Path $testDir "staging\broker-statements"
$accountingStage = Join-Path $testDir "staging\accounting-evidence"

Write-JsonFile (Join-Path $brokerStage "valid-real-broker-statement.json") (New-BrokerFixture @{})
Write-JsonFile (Join-Path $accountingStage "valid-real-accounting-evidence.json") (New-AccountingFixture @{})
Write-JsonFile (Join-Path $brokerStage "sample-broker-rejected.json") (New-BrokerFixture @{ sample_only = $true })
Write-JsonFile (Join-Path $brokerStage "missing-sha-broker-rejected.json") (New-BrokerFixture @{ source_file_sha256 = $null })
Write-JsonFile (Join-Path $brokerStage "external-fetch-broker-rejected.json") (New-BrokerFixture @{ external_fetch = $true })
Write-JsonFile (Join-Path $brokerStage "broker-api-broker-rejected.json") (New-BrokerFixture @{ broker_api_call = $true })
Write-JsonFile (Join-Path $brokerStage "commit-flags-broker-rejected.json") (New-BrokerFixture @{ db_mutation = $true; ledger_commit = $true })
Write-JsonFile (Join-Path $brokerStage "totals-mismatch-broker-rejected.json") (New-BrokerFixture @{ statement_totals = [ordered]@{ gross_pnl_usd = [decimal]-51.308800; commission_usd = [decimal]26.268029; net_pnl_usd = [decimal]-76.576829 } })
Write-JsonFile (Join-Path $accountingStage "sample-accounting-rejected.json") (New-AccountingFixture @{ sample_only = $true })
Write-JsonFile (Join-Path $accountingStage "missing-sha-accounting-rejected.json") (New-AccountingFixture @{ source_file_sha256 = $null })
Write-JsonFile (Join-Path $accountingStage "external-fetch-accounting-rejected.json") (New-AccountingFixture @{ external_fetch = $true })
Write-JsonFile (Join-Path $accountingStage "commit-flags-accounting-rejected.json") (New-AccountingFixture @{ db_mutation = $true; ledger_commit = $true })
Write-JsonFile (Join-Path $accountingStage "totals-mismatch-accounting-rejected.json") (New-AccountingFixture @{ net_pnl = [ordered]@{ currency = "USD"; amount = [decimal]-77.576829 } })

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $testSubdir | Out-Null

$testMain = Read-JsonFile (Join-Path $testDir "real-manual-evidence-acceptance-r001.json")
$testScan = Read-JsonFile (Join-Path $testDir "real-manual-evidence-staging-scan-r001.json")
$testValidation = Read-JsonFile (Join-Path $testDir "real-manual-evidence-validation-report-r001.json")
$testQuarantine = Read-JsonFile (Join-Path $testDir "real-manual-evidence-quarantine-preview-r001.json")

Assert-Equal $testMain.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_READY_R001" "Valid real broker and accounting fixtures should pass acceptance gate."
Assert-Equal $testMain.readiness.real_manual_broker_statement_acceptance $true "Real broker acceptance should be true for valid fixture."
Assert-Equal $testMain.readiness.real_manual_accounting_evidence_acceptance $true "Real accounting acceptance should be true for valid fixture."
Assert-Equal $testMain.readiness.broker_confirmed_pnl $false "Accepted evidence must not mark broker-confirmed PnL ready."
Assert-Equal $testMain.readiness.realized_accounting_close $false "Accepted evidence must not mark realized accounting close ready."
Assert-Equal $testMain.readiness.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $testMain.readiness.db_mutation $false "DB mutation must remain false."
Assert-Equal $testMain.readiness.production_live $false "Production/live must remain false."
Assert-Equal $testMain.readiness.trading_readiness $false "Trading readiness must remain false."
Assert-Equal @($testMain.accepted_real_evidence.broker_statements).Count 1 "Exactly one real broker fixture should be accepted."
Assert-Equal @($testMain.accepted_real_evidence.accounting_evidence).Count 1 "Exactly one real accounting fixture should be accepted."
Assert-True (@($testMain.rejected_evidence).Count -ge 10) "Invalid/sample fixtures should be rejected."
Assert-Equal $testQuarantine.quarantined_count @($testMain.rejected_evidence).Count "Rejected evidence should be represented in quarantine preview."
Assert-Equal $testQuarantine.no_destructive_file_movement $true "Quarantine preview must be non-destructive."
Assert-Equal $testQuarantine.no_db_mutation $true "Quarantine preview must not mutate DB."
Assert-Equal $testQuarantine.no_external_calls $true "Quarantine preview must not use external calls."

Assert-Equal $testScan.broker_statement_files_seen 7 "Broker test files seen mismatch."
Assert-Equal $testScan.accounting_evidence_files_seen 6 "Accounting test files seen mismatch."
Assert-True ($testScan.real_broker_statement_files_seen -ge 5) "Real broker evidence detection missing."
Assert-True ($testScan.real_accounting_evidence_files_seen -ge 4) "Real accounting evidence detection missing."
Assert-Equal $testValidation.accepted_real_broker_evidence_count 1 "Validation broker accepted count mismatch."
Assert-Equal $testValidation.accepted_real_accounting_evidence_count 1 "Validation accounting accepted count mismatch."
Assert-True ($testMain.discovery_summary.staged_candidate_count -ge 13) "Staged discovery should include test candidates."

$allReasons = @($testMain.rejected_evidence | ForEach-Object { $_.reasons }) -join "`n"
foreach ($expectedReason in @(
    "source_file_sha256 missing",
    "sample evidence not promotable",
    "external_fetch true",
    "broker_api_call true",
    "db_mutation true",
    "ledger_commit true",
    "statement totals mismatch",
    "accounting totals mismatch"
)) {
    Assert-True ($allReasons -like "*$expectedReason*") "Expected rejection reason missing: $expectedReason"
}

Assert-DecimalEqual ([decimal]$testMain.source_values.gross_usd) ([decimal]-50.308800) "Test gross USD mismatch."
Assert-DecimalEqual ([decimal]$testMain.source_values.commission_usd) ([decimal]26.268029) "Test commission USD mismatch."
Assert-DecimalEqual ([decimal]$testMain.source_values.net_usd) ([decimal]-76.576829) "Test net USD mismatch."
Assert-True ([decimal]$testMain.source_values.tolerance -le [decimal]0.000001) "Test tolerance must be no wider than 0.000001."

foreach ($label in $testMain.forbidden_ready_labels.PSObject.Properties) {
    Assert-Equal $label.Value $false "Forbidden ready label must remain false: $($label.Name)"
}
Assert-Equal $testMain.global_guards.external_calls $false "External calls must remain false."
Assert-Equal $testMain.global_guards.broker_api_calls $false "Broker API calls must remain false."
Assert-Equal $testMain.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $testMain.global_guards.account_data_fetch $false "Account-data fetch must remain false."
Assert-Equal $testMain.global_guards.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $testMain.global_guards.db_mutation $false "DB mutation must remain false."
Assert-Equal $testMain.global_guards.trading_activity $false "Trading activity must remain false."
Assert-Equal $testMain.global_guards.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $testMain.global_guards.trading_readiness_ready $false "Trading readiness ready must remain false."

$outsideSubdir = "real-manual-evidence-acceptance-r001-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $outsideSubdir -PackageLocalDiscoveryOnly | Out-Null
$outsideDir = Join-Path $RepoRoot "artifacts\readiness\$outsideSubdir"
$outsideDrop = Join-Path $outsideDir "operator-manual-evidence-drop"
Write-JsonFile (Join-Path $outsideDrop "outside-real-broker-statement.json") (New-BrokerFixture @{})
Write-JsonFile (Join-Path $outsideDrop "outside-real-accounting-evidence.json") (New-AccountingFixture @{})
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $outsideSubdir -PackageLocalDiscoveryOnly | Out-Null

$outsideMain = Read-JsonFile (Join-Path $outsideDir "real-manual-evidence-acceptance-r001.json")
$outsideDiscovery = Read-JsonFile (Join-Path $outsideDir "real-manual-evidence-discovery-report-r001.json")
$outsideSummary = Get-Content -Raw -LiteralPath (Join-Path $outsideDir "real-manual-evidence-acceptance-summary-r001.md")
Assert-Equal $outsideMain.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_BLOCKED_R001" "Outside-staging real evidence must not be accepted."
Assert-Equal $outsideMain.blocked_reason "REAL_MANUAL_EVIDENCE_FOUND_OUTSIDE_STAGING" "Outside-staging blocked reason mismatch."
Assert-Equal @($outsideMain.accepted_real_evidence.broker_statements).Count 0 "Outside broker candidate must not be accepted."
Assert-Equal @($outsideMain.accepted_real_evidence.accounting_evidence).Count 0 "Outside accounting candidate must not be accepted."
Assert-True ($outsideDiscovery.real_non_sample_candidate_count -ge 2) "Outside real non-sample candidates should be discovered."
Assert-True (@($outsideDiscovery.real_non_sample_candidates_outside_staging).Count -ge 2) "Outside candidates should be reported outside staging."
Assert-True (@($outsideDiscovery.recommended_operator_actions | Where-Object { $_ -like "Copy-Item -Force*outside-real-broker-statement.json*" }).Count -gt 0) "Broker copy command missing."
Assert-True (@($outsideDiscovery.recommended_operator_actions | Where-Object { $_ -like "Copy-Item -Force*outside-real-accounting-evidence.json*" }).Count -gt 0) "Accounting copy command missing."
Assert-True ($outsideSummary -like "*Copy-Item -Force*outside-real-broker-statement.json*") "Summary should include broker copy command."
Assert-True ($outsideSummary -like "*Copy-Item -Force*outside-real-accounting-evidence.json*") "Summary should include accounting copy command."
Assert-Equal $outsideMain.global_guards.external_calls $false "Outside discovery must keep external calls false."
Assert-Equal $outsideMain.global_guards.db_mutation $false "Outside discovery must keep DB mutation false."
Assert-Equal $outsideMain.global_guards.ledger_commit $false "Outside discovery must keep ledger commit false."

$periodSubdir = "real-manual-evidence-acceptance-r001-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $periodSubdir | Out-Null
$periodDir = Join-Path $RepoRoot "artifacts\readiness\$periodSubdir"
$periodBrokerStage = Join-Path $periodDir "staging\broker-statements"
$periodAccountingStage = Join-Path $periodDir "staging\accounting-evidence"
$wrongBrokerPeriod = [ordered]@{ start_utc = "wrong-explicit-period-start"; end_utc = "wrong-explicit-period-end" }
$wrongAccountingPeriod = [ordered]@{ start_utc = "wrong-explicit-period-start"; end_utc = "wrong-explicit-period-end" }
$brokerPeriodPath = Join-Path $periodBrokerStage "2026-05-29-real-broker-statement-wrong-period.json"
$accountingPeriodPath = Join-Path $periodAccountingStage "2026-05-29-real-accounting-evidence-wrong-period.json"
Write-JsonFile $brokerPeriodPath (New-BrokerFixture @{ statement_period = $wrongBrokerPeriod })
Write-JsonFile $accountingPeriodPath (New-AccountingFixture @{ period = $wrongAccountingPeriod })
(Get-Item -LiteralPath $brokerPeriodPath).LastWriteTimeUtc = [datetime]"2026-05-29T00:00:00Z"
(Get-Item -LiteralPath $accountingPeriodPath).LastWriteTimeUtc = [datetime]"2026-05-29T00:00:00Z"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $periodSubdir | Out-Null

$periodMain = Read-JsonFile (Join-Path $periodDir "real-manual-evidence-acceptance-r001.json")
$periodValidation = Read-JsonFile (Join-Path $periodDir "real-manual-evidence-validation-report-r001.json")
$periodDiscovery = Read-JsonFile (Join-Path $periodDir "real-manual-evidence-discovery-report-r001.json")
$brokerAfter = Read-JsonFile $brokerPeriodPath
$accountingAfter = Read-JsonFile $accountingPeriodPath
Assert-Equal $periodMain.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_BLOCKED_R001" "Wrong explicit periods must block."
Assert-Equal $periodMain.blocked_reason "BLOCKED_REAL_EVIDENCE_PERIOD_MISMATCH" "Combined period mismatch reason expected."
Assert-Equal $periodValidation.period_mismatch_count 2 "Period mismatch count mismatch."
Assert-Equal $periodDiscovery.period_mismatch_count 2 "Discovery period mismatch count mismatch."
foreach ($result in $periodValidation.reconciliation_validation_results) {
    Assert-Equal $result.totals_match $true "Matching totals should remain true despite wrong period."
    Assert-Equal $result.period_match $false "Wrong explicit period must not match."
}
Assert-Equal $brokerAfter.statement_period.start_utc "wrong-explicit-period-start" "Broker evidence date must not be modified."
Assert-Equal $accountingAfter.period.start_utc "wrong-explicit-period-start" "Accounting evidence date must not be modified."
Assert-True (($periodValidation.reconciliation_validation_results | ConvertTo-Json -Depth 20) -like "*BLOCKED_REAL_BROKER_STATEMENT_PERIOD_MISMATCH*") "Broker period mismatch reason missing."
Assert-True (($periodValidation.reconciliation_validation_results | ConvertTo-Json -Depth 20) -like "*BLOCKED_REAL_ACCOUNTING_EVIDENCE_PERIOD_MISMATCH*") "Accounting period mismatch reason missing."
Assert-Equal $periodMain.global_guards.external_calls $false "Period diagnostics must keep external calls false."
Assert-Equal $periodMain.global_guards.broker_api_calls $false "Period diagnostics must keep broker API false."
Assert-Equal $periodMain.global_guards.market_data_fetch $false "Period diagnostics must keep market-data false."
Assert-Equal $periodMain.global_guards.account_data_fetch $false "Period diagnostics must keep account-data false."
Assert-Equal $periodMain.global_guards.db_mutation $false "Period diagnostics must keep DB mutation false."
Assert-Equal $periodMain.global_guards.ledger_commit $false "Period diagnostics must keep ledger commit false."
Assert-Equal $periodMain.global_guards.production_live_ready $false "Period diagnostics must keep production/live false."
Assert-Equal $periodMain.global_guards.trading_readiness_ready $false "Period diagnostics must keep trading readiness false."

$rawSubdir = "real-manual-evidence-acceptance-r001-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $rawSubdir | Out-Null
$rawDir = Join-Path $RepoRoot "artifacts\readiness\$rawSubdir"
$rawBundleDir = Join-Path $rawDir "staging\raw-lmax-broker-statement"
Write-RawLmaxBundle $rawBundleDir
$statementPath = Join-Path $rawBundleDir "LMAX-account-statement-2026-04-30.pdf"
$statementBefore = Get-Content -Raw -LiteralPath $statementPath
(Get-Item -LiteralPath $statementPath).LastWriteTimeUtc = [datetime]"2026-05-29T00:00:00Z"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $rawSubdir | Out-Null

$rawMain = Read-JsonFile (Join-Path $rawDir "real-manual-evidence-acceptance-r001.json")
$rawDiscovery = Read-JsonFile (Join-Path $rawDir "real-manual-evidence-discovery-report-r001.json")
$rawValidation = Read-JsonFile (Join-Path $rawDir "real-manual-evidence-validation-report-r001.json")
$normalizedPath = Join-Path $rawDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json"
$normalized = Read-JsonFile $normalizedPath
$statementAfter = Get-Content -Raw -LiteralPath $statementPath

Assert-Equal $rawMain.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_PARTIAL_READY_R001" "Raw LMAX broker bundle should produce partial ready."
Assert-Equal $rawMain.blocked_reason "BROKER_STATEMENT_ACCEPTED_ACCOUNTING_EVIDENCE_MISSING" "Raw bundle blocked reason mismatch."
Assert-Equal $rawDiscovery.raw_lmax_bundle.seen $true "Raw LMAX bundle must be discovered."
Assert-Equal $rawDiscovery.raw_lmax_bundle.complete $true "Raw LMAX bundle must be complete."
Assert-Equal @($rawDiscovery.raw_lmax_bundle.files).Count 3 "Raw bundle hashes must cover PDF and two CSV files."
foreach ($fileHash in $rawDiscovery.raw_lmax_bundle.files) {
    Assert-True ($fileHash.sha256 -match "^sha256:[A-F0-9]{64}$") "Raw bundle file hash missing."
}
Assert-True (Test-Path -LiteralPath $normalizedPath) "Normalized broker statement artifact must be created."
Assert-Equal $normalized.artifact_type "manual_broker_statement_import" "Normalized artifact type mismatch."
Assert-Equal $normalized.sample_only $false "Normalized broker statement must be non-sample."
Assert-Equal $normalized.real_broker_statement $true "Normalized broker statement must be real broker evidence."
Assert-Equal $normalized.external_fetch $false "Normalized artifact external fetch must be false."
Assert-Equal $normalized.broker_api_call $false "Normalized artifact broker API call must be false."
Assert-Equal $normalized.market_data_fetch $false "Normalized artifact market-data fetch must be false."
Assert-Equal $normalized.account_data_fetch $false "Normalized artifact account-data fetch must be false."
Assert-Equal $normalized.db_mutation $false "Normalized artifact DB mutation must be false."
Assert-Equal $normalized.ledger_commit $false "Normalized artifact ledger commit must be false."
Assert-Equal $normalized.trading_statement_date "30/04/2026 15:08:47" "Trading statement date parse mismatch."
Assert-Equal $normalized.statement_period.from "03/11/2025" "Statement period from parse mismatch."
Assert-Equal $normalized.statement_period.to "03/11/2025" "Statement period to parse mismatch."
Assert-Equal $normalized.account_currency "USD" "Account currency mismatch."
Assert-True ($normalized.account_number_hash -match "^sha256:[A-F0-9]{64}$") "Account number must be hashed."
Assert-True (-not ($normalized.PSObject.Properties.Name -contains "account_number")) "Raw account number must not be exposed."
Assert-DecimalEqual ([decimal]$normalized.traded_notional_usd) ([decimal]45125359.74) "Traded notional mismatch."
Assert-DecimalEqual ([decimal]$normalized.opening_balance_usd) ([decimal]490697.13) "Opening balance mismatch."
Assert-DecimalEqual ([decimal]$normalized.realised_pnl_usd) ([decimal]6015.14) "Realised PnL mismatch."
Assert-DecimalEqual ([decimal]$normalized.commission_usd_signed) ([decimal]-225.63) "Commission signed mismatch."
Assert-DecimalEqual ([decimal]$normalized.commission_cost_usd) ([decimal]225.63) "Commission cost mismatch."
Assert-DecimalEqual ([decimal]$normalized.financing_usd_signed) ([decimal]-40.60) "Financing signed mismatch."
Assert-DecimalEqual ([decimal]$normalized.financing_cost_usd) ([decimal]40.60) "Financing cost mismatch."
Assert-DecimalEqual ([decimal]$normalized.closing_balance_usd) ([decimal]496446.04) "Closing balance mismatch."
Assert-DecimalEqual ([decimal]$normalized.closing_pnl_usd) ([decimal]463.61) "Closing PnL mismatch."
Assert-DecimalEqual ([decimal]$normalized.closing_equity_usd) ([decimal]496909.65) "Closing equity mismatch."
Assert-DecimalEqual ([decimal]$normalized.margin_on_open_positions_usd) ([decimal]10728.19) "Margin mismatch."
Assert-DecimalEqual ([decimal]$normalized.available_to_trade_usd) ([decimal]486181.46) "Available to trade mismatch."
Assert-Equal @($normalized.currency_wallets).Count 4 "Wallet row count mismatch."
Assert-True (@($normalized.currency_wallets | Where-Object { $_.currency -eq "CAD" -and [decimal]$_.pnl -eq [decimal]4492.54 -and [decimal]$_.commission -eq [decimal]-70.76315 }).Count -eq 1) "CAD wallet row missing."
Assert-True (@($normalized.currency_wallets | Where-Object { $_.currency -eq "CHF" -and [decimal]$_.pnl -eq [decimal]693.02 -and [decimal]$_.commission -eq [decimal]-15.25343 }).Count -eq 1) "CHF wallet row missing."
Assert-True (@($normalized.currency_wallets | Where-Object { $_.currency -eq "JPY" -and [decimal]$_.pnl -eq [decimal]741809.00 -and [decimal]$_.commission -eq [decimal]-9891.71546 }).Count -eq 1) "JPY wallet row missing."
Assert-True (@($normalized.currency_wallets | Where-Object { $_.currency -eq "USD" -and [decimal]$_.pnl -eq [decimal]-2837.39 -and [decimal]$_.commission -eq [decimal]-92.43356 -and [decimal]$_.financing -eq [decimal]-40.59720 }).Count -eq 1) "USD wallet row missing."
Assert-Equal @($normalized.open_positions).Count 1 "Open position row count mismatch."
Assert-Equal $normalized.open_positions[0].instrument "NZD/USD" "Open position symbol mismatch."
Assert-DecimalEqual ([decimal]$normalized.open_positions[0].open_quantity) ([decimal]125.3) "Open quantity mismatch."
Assert-DecimalEqual ([decimal]$normalized.open_positions[0].average_opening_price) ([decimal]0.57043) "Average opening price mismatch."
Assert-DecimalEqual ([decimal]$normalized.open_positions[0].closing_price) ([decimal]0.5708) "Closing price mismatch."
Assert-DecimalEqual ([decimal]$normalized.open_positions[0].open_pnl) ([decimal]463.61) "Open PnL mismatch."
Assert-Equal $rawMain.real_broker_evidence_lane.synthetic_sandbox_closeout_comparison.comparison_purpose "diagnostic_only_not_acceptance_gate" "Synthetic comparison purpose mismatch."
Assert-Equal $rawMain.real_broker_evidence_lane.synthetic_sandbox_closeout_comparison.matches_synthetic_closeout $false "Synthetic mismatch should be diagnostic."
Assert-Equal $rawMain.real_broker_evidence_lane.synthetic_sandbox_closeout_comparison.acceptance_impact "none" "Synthetic comparison must not affect acceptance."
Assert-Equal $rawMain.synthetic_sandbox_closeout_lane.not_used_as_real_broker_acceptance_gate $true "Synthetic closeout must not gate real broker acceptance."
Assert-Equal $rawMain.readiness.real_manual_broker_statement_acceptance $true "Raw broker statement should be accepted."
Assert-Equal $rawMain.readiness.real_manual_accounting_evidence_acceptance $false "Accounting evidence should remain missing."
Assert-Equal $rawMain.accounting_evidence_lane.blocked_reason "REAL_ACCOUNTING_EVIDENCE_MISSING" "Accounting blocked reason mismatch."
Assert-Equal $rawMain.readiness.broker_confirmed_pnl $false "Broker-confirmed PnL must remain false."
Assert-Equal $rawMain.real_broker_evidence_lane.broker_confirmed_pnl_blocked_reason "BROKER_STATEMENT_ACCEPTED_BUT_INTERNAL_TRADE_RECONCILIATION_NOT_DEFINED" "Broker-confirmed block reason mismatch."
Assert-Equal $rawMain.readiness.realized_accounting_close $false "Realized accounting close must remain false."
Assert-Equal $rawMain.readiness.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $rawMain.readiness.db_mutation $false "DB mutation must remain false."
Assert-Equal $rawMain.readiness.production_live $false "Production/live must remain false."
Assert-Equal $rawMain.readiness.trading_readiness $false "Trading readiness must remain false."
Assert-Equal $rawMain.global_guards.external_calls $false "Raw bundle must keep external calls false."
Assert-Equal $rawMain.global_guards.broker_api_calls $false "Raw bundle must keep broker API false."
Assert-Equal $rawMain.global_guards.market_data_fetch $false "Raw bundle must keep market-data false."
Assert-Equal $rawMain.global_guards.account_data_fetch $false "Raw bundle must keep account-data false."
Assert-Equal $rawMain.global_guards.db_mutation $false "Raw bundle must keep DB mutation false."
Assert-Equal $rawMain.global_guards.ledger_commit $false "Raw bundle must keep ledger commit false."
Assert-Equal $rawMain.global_guards.production_live_ready $false "Raw bundle must keep production/live false."
Assert-Equal $rawMain.global_guards.trading_readiness_ready $false "Raw bundle must keep trading readiness false."
Assert-Equal $statementBefore $statementAfter "Raw statement file must not be modified."
Assert-Equal $rawValidation.raw_lmax_broker_bundle_validation.synthetic_sandbox_closeout_comparison.acceptance_impact "none" "Validation must show synthetic comparison diagnostic only."

Write-Host "REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001_TESTS_PASS"

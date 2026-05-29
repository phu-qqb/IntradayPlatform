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

$sourcePath = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001\sandbox-broker-accounting-reconciliation-r001.json"
$artifactDir = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001"
$requiredArtifacts = @(
    "controlled-real-evidence-import-r001.json",
    "broker-statement-manual-import-schema-r001.json",
    "accounting-evidence-manual-import-schema-r001.json",
    "controlled-import-validation-policy-r001.json",
    "sample-manual-broker-statement-import-r001.json",
    "sample-manual-accounting-evidence-import-r001.json",
    "controlled-real-evidence-import-summary-r001.md"
)

Assert-True (Test-Path -LiteralPath $sourcePath) "Source reconciliation artifact must exist."
foreach ($name in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactDir $name)) "Required artifact missing: $name"
}

$source = Read-JsonFile $sourcePath
$main = Read-JsonFile (Join-Path $artifactDir "controlled-real-evidence-import-r001.json")
$brokerSchema = Read-JsonFile (Join-Path $artifactDir "broker-statement-manual-import-schema-r001.json")
$accountingSchema = Read-JsonFile (Join-Path $artifactDir "accounting-evidence-manual-import-schema-r001.json")
$policy = Read-JsonFile (Join-Path $artifactDir "controlled-import-validation-policy-r001.json")
$sampleBroker = Read-JsonFile (Join-Path $artifactDir "sample-manual-broker-statement-import-r001.json")
$sampleAccounting = Read-JsonFile (Join-Path $artifactDir "sample-manual-accounting-evidence-import-r001.json")

Assert-Equal $source.status "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001" "Source reconciliation status mismatch."
Assert-DecimalEqual ([decimal]$source.broker_reconciliation_preview.gross_pnl_usd) ([decimal]-50.308800) "Source gross USD mismatch."
Assert-DecimalEqual ([decimal]$source.broker_reconciliation_preview.commission_usd) ([decimal]26.268029) "Source commission USD mismatch."
Assert-DecimalEqual ([decimal]$source.broker_reconciliation_preview.net_pnl_usd) ([decimal]-76.576829) "Source net USD mismatch."

Assert-Equal $main.package "NEXT_CONTROLLED_REAL_EVIDENCE_IMPORT_R001" "Main package mismatch."
Assert-Equal $main.status "CONTROLLED_REAL_EVIDENCE_IMPORT_READY_R001" "Main status mismatch."
Assert-Equal $main.environment "sandbox" "Main environment mismatch."
Assert-Equal $main.mode "offline_manual_import_framework_only" "Main mode mismatch."
Assert-Equal $main.source_status "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001" "Main source status mismatch."
Assert-True ($main.source_artifact_hashes.sandbox_broker_accounting_reconciliation_r001 -match "^sha256:[A-F0-9]{64}$") "Source hash must be captured."

Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.ready $true "Broker manual import lane should be ready."
Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.external_fetch_allowed $false "Broker lane external fetch must be false."
Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.broker_api_allowed $false "Broker lane broker API must be false."
Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.requires_manual_file_drop $true "Broker lane must require manual file drop."
Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.requires_sha256 $true "Broker lane must require sha256."
Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.requires_account_id_hash $true "Broker lane must require account_id_hash."
Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.requires_statement_period $true "Broker lane must require statement period."
Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.requires_schema_validation $true "Broker lane must require schema validation."
Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.requires_operator_approval $true "Broker lane must require operator approval."
Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.quarantine_on_failure $true "Broker lane must quarantine on failure."

Assert-Equal $main.controlled_import_lanes.accounting_evidence_manual_import.ready $true "Accounting manual import lane should be ready."
Assert-Equal $main.controlled_import_lanes.accounting_evidence_manual_import.external_fetch_allowed $false "Accounting lane external fetch must be false."
Assert-Equal $main.controlled_import_lanes.accounting_evidence_manual_import.requires_sha256 $true "Accounting lane must require sha256."
Assert-Equal $main.controlled_import_lanes.accounting_evidence_manual_import.requires_policy_version $true "Accounting lane must require policy version."
Assert-Equal $main.controlled_import_lanes.accounting_evidence_manual_import.requires_operator_approval $true "Accounting lane must require operator approval."
Assert-Equal $main.controlled_import_lanes.accounting_evidence_manual_import.quarantine_on_failure $true "Accounting lane must quarantine on failure."

foreach ($label in @("controlled_real_evidence_import_framework", "manual_broker_statement_import_interface", "manual_accounting_evidence_import_interface", "import_validation_preview")) {
    Assert-Equal $main.ready_outputs.$label $true "Allowed ready label missing: $label"
}
foreach ($label in $main.forbidden_ready_labels.PSObject.Properties) {
    Assert-Equal $label.Value $false "Forbidden ready label must remain false: $($label.Name)"
}

Assert-True ($brokerSchema.required_fields -contains "source_file_sha256") "Broker schema must require source_file_sha256."
Assert-True ($brokerSchema.required_fields -contains "account_id_hash") "Broker schema must require account_id_hash."
Assert-True ($brokerSchema.required_fields -contains "approval_id") "Broker schema must require approval_id."
Assert-True ($brokerSchema.required_fields -contains "statement_period") "Broker schema must require statement_period."
Assert-True ($brokerSchema.required_fields -contains "statement_totals") "Broker schema must require statement_totals."
Assert-True ($brokerSchema.reject_conditions -contains "external_fetch true") "Broker schema must reject external_fetch true."
Assert-True ($brokerSchema.reject_conditions -contains "broker_api_call true") "Broker schema must reject broker_api_call true."
Assert-True ($brokerSchema.reject_conditions -contains "production/live mode") "Broker schema must reject production/live mode."
Assert-True ($brokerSchema.reject_conditions -contains "commit intent") "Broker schema must reject commit intent."
Assert-True ($brokerSchema.reject_conditions -contains "DB mutation intent") "Broker schema must reject DB mutation intent."

Assert-True ($accountingSchema.required_fields -contains "source_file_sha256") "Accounting schema must require source_file_sha256."
Assert-True ($accountingSchema.required_fields -contains "approval_id") "Accounting schema must require approval_id."
Assert-True ($accountingSchema.required_fields -contains "accounting_policy_version") "Accounting schema must require policy version."
Assert-True ($accountingSchema.required_fields -contains "period") "Accounting schema must require period."
Assert-True ($accountingSchema.reject_conditions -contains "external fetch") "Accounting schema must reject external fetch."
Assert-True ($accountingSchema.reject_conditions -contains "DB mutation") "Accounting schema must reject DB mutation."
Assert-True ($accountingSchema.reject_conditions -contains "ledger commit") "Accounting schema must reject ledger commit."
Assert-True ($accountingSchema.reject_conditions -contains "production/live mode") "Accounting schema must reject production/live mode."

Assert-Equal $policy.every_imported_evidence_file_must_have_sha256 $true "Policy must require sha256."
Assert-Equal $policy.every_imported_evidence_file_must_be_local_manual $true "Policy must require local/manual import."
Assert-Equal $policy.every_import_must_have_operator_approval $true "Policy must require operator approval."
Assert-Equal $policy.every_account_scoped_import_must_have_account_id_hash $true "Policy must require account_id_hash."
Assert-Equal $policy.every_import_must_have_period_boundaries $true "Policy must require period boundaries."
Assert-Equal $policy.quarantine_on_validation_failure $true "Policy must quarantine failures."
Assert-Equal $policy.no_import_may_trigger_external_calls $true "Policy must block external calls."
Assert-Equal $policy.no_import_may_trigger_db_mutation $true "Policy must block DB mutation."
Assert-Equal $policy.no_import_may_trigger_ledger_commit $true "Policy must block ledger commit."
Assert-Equal $policy.no_import_may_mark_production_live_ready $true "Policy must block production/live readiness."

Assert-Equal $sampleBroker.sample_only $true "Sample broker import must be sample_only."
Assert-Equal $sampleBroker.real_broker_statement $false "Sample broker import must not be a real broker statement."
Assert-True ($sampleBroker.source_file_sha256 -match "^sha256:[A-F0-9]{64}$") "Sample broker import must have sha256."
Assert-True ($sampleBroker.account_id_hash -match "^sha256:[A-F0-9]{64}$") "Sample broker import must have account_id_hash."
Assert-True (-not [string]::IsNullOrWhiteSpace($sampleBroker.approval_id)) "Sample broker import must have approval_id."
Assert-True ($sampleBroker.statement_period.start_utc -and $sampleBroker.statement_period.end_utc) "Sample broker import must have period boundaries."
Assert-Equal $sampleBroker.external_fetch $false "Sample broker import external_fetch must be false."
Assert-Equal $sampleBroker.broker_api_call $false "Sample broker import broker_api_call must be false."
Assert-Equal $sampleBroker.market_data_fetch $false "Sample broker import market_data_fetch must be false."
Assert-Equal $sampleBroker.account_data_fetch $false "Sample broker import account_data_fetch must be false."
Assert-Equal $sampleBroker.db_mutation $false "Sample broker import db_mutation must be false."
Assert-Equal $sampleBroker.ledger_commit $false "Sample broker import ledger_commit must be false."
Assert-Equal $sampleBroker.production_live_ready $false "Sample broker import production_live_ready must be false."
Assert-Equal $sampleBroker.trading_readiness_ready $false "Sample broker import trading_readiness_ready must be false."
Assert-DecimalEqual ([decimal]$sampleBroker.statement_totals.gross_pnl_usd) ([decimal]-50.308800) "Sample broker gross mismatch."
Assert-DecimalEqual ([decimal]$sampleBroker.statement_totals.commission_usd) ([decimal]26.268029) "Sample broker commission mismatch."
Assert-DecimalEqual ([decimal]$sampleBroker.statement_totals.net_pnl_usd) ([decimal]-76.576829) "Sample broker net mismatch."
Assert-Equal $sampleBroker.statement_totals.unmatched_items 0 "Sample broker unmatched items must be zero."
Assert-Equal $sampleBroker.validation_status "SAMPLE_MANUAL_BROKER_IMPORT_VALID" "Sample broker validation mismatch."

Assert-Equal $sampleAccounting.sample_only $true "Sample accounting import must be sample_only."
Assert-Equal $sampleAccounting.real_accounting_close $false "Sample accounting import must not be real accounting close."
Assert-True ($sampleAccounting.source_file_sha256 -match "^sha256:[A-F0-9]{64}$") "Sample accounting import must have sha256."
Assert-True (-not [string]::IsNullOrWhiteSpace($sampleAccounting.approval_id)) "Sample accounting import must have approval_id."
Assert-True ($sampleAccounting.period.start_utc -and $sampleAccounting.period.end_utc) "Sample accounting import must have period boundaries."
Assert-True (-not [string]::IsNullOrWhiteSpace($sampleAccounting.accounting_policy_version)) "Sample accounting import must have policy version."
Assert-Equal $sampleAccounting.external_fetch $false "Sample accounting external_fetch must be false."
Assert-Equal $sampleAccounting.market_data_fetch $false "Sample accounting market_data_fetch must be false."
Assert-Equal $sampleAccounting.account_data_fetch $false "Sample accounting account_data_fetch must be false."
Assert-Equal $sampleAccounting.db_mutation $false "Sample accounting db_mutation must be false."
Assert-Equal $sampleAccounting.ledger_commit $false "Sample accounting ledger_commit must be false."
Assert-Equal $sampleAccounting.production_live_ready $false "Sample accounting production_live_ready must be false."
Assert-Equal $sampleAccounting.trading_readiness_ready $false "Sample accounting trading_readiness_ready must be false."
Assert-DecimalEqual ([decimal]$sampleAccounting.gross_pnl.amount) ([decimal]-50.308800) "Sample accounting gross mismatch."
Assert-DecimalEqual ([decimal]$sampleAccounting.commission_expense.amount) ([decimal]26.268029) "Sample accounting commission mismatch."
Assert-DecimalEqual ([decimal]$sampleAccounting.net_pnl.amount) ([decimal]-76.576829) "Sample accounting net mismatch."
Assert-Equal $sampleAccounting.validation_status "SAMPLE_MANUAL_ACCOUNTING_IMPORT_VALID" "Sample accounting validation mismatch."

Assert-Equal $main.global_guards.external_calls $false "Global external calls must remain false."
Assert-Equal $main.global_guards.broker_api_calls $false "Global broker API calls must remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Global market-data fetch must remain false."
Assert-Equal $main.global_guards.account_data_fetch $false "Global account-data fetch must remain false."
Assert-Equal $main.global_guards.ledger_commit $false "Global ledger commit must remain false."
Assert-Equal $main.global_guards.db_mutation $false "Global DB mutation must remain false."
Assert-Equal $main.global_guards.trading_activity $false "Global trading activity must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Global production/live ready must remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Global trading readiness ready must remain false."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-controlled-real-evidence-import-r001.ps1")
) + @(Get-ChildItem -LiteralPath $artifactDir -File -Recurse | ForEach-Object { $_.FullName })
$forbiddenPatterns = @(
    "SubmitOrder",
    "SubmitOrderUnmanaged",
    "AtmStrategyCreate",
    '"external_fetch": true',
    '"broker_api_call": true',
    '"broker_api_calls": true',
    '"market_data_fetch": true',
    '"account_data_fetch": true',
    '"ledger_commit": true',
    '"db_mutation": true',
    '"production_live_ready": true',
    '"trading_readiness_ready": true',
    '"real_broker_statement": true',
    '"real_accounting_close": true',
    '"broker_api_statement_fetch": true',
    '"live_broker_reconciliation": true',
    '"real_broker_statement_reconciliation": true',
    '"realized_accounting_close": true',
    '"committed_ledger": true',
    "api_key",
    "apikey",
    "password"
)
foreach ($pattern in $forbiddenPatterns) {
    $matches = Select-String -Path $scanFiles -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
    if ($matches) {
        throw "Forbidden static pattern detected [$pattern]:`n$($matches | Out-String)"
    }
}

Write-Host "CONTROLLED_REAL_EVIDENCE_IMPORT_R001_GATE_PASS"

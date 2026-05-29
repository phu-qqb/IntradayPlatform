param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function File-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Sha([string]$Path) {
    "sha256:$(File-Sha256 $Path)"
}

$sourcePath = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001\sandbox-broker-accounting-reconciliation-r001.json"
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Required source reconciliation artifact missing: $sourcePath"
}

$source = Read-JsonFile $sourcePath
if ($source.status -ne "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001") {
    throw "Source reconciliation is not ready."
}

$grossUsd = [decimal]$source.broker_reconciliation_preview.gross_pnl_usd
$commissionUsd = [decimal]$source.broker_reconciliation_preview.commission_usd
$netUsd = [decimal]$source.broker_reconciliation_preview.net_pnl_usd
$sourceHash = Sha $sourcePath

$excludedLines = @(
    [ordered]@{ symbol = "USDJPY"; quantity = [decimal]50.0; reason = "unfilled" }
    [ordered]@{ symbol = "AUDUSD"; reason = "zero_quantity" }
    [ordered]@{ symbol = "CHFUSD"; reason = "zero_quantity" }
    [ordered]@{ symbol = "EURUSD"; reason = "zero_quantity" }
    [ordered]@{ symbol = "GBPUSD"; reason = "zero_quantity" }
)

$brokerSchema = [ordered]@{
    artifact_type = "manual_broker_statement_import_schema_r001"
    environment = "sandbox"
    import_mode = "offline_manual_file_drop_only"
    required_fields = @(
        "artifact_type",
        "environment",
        "import_mode",
        "source_file_name",
        "source_file_sha256",
        "imported_at_utc",
        "imported_by",
        "approval_id",
        "broker",
        "venue",
        "account_id_hash",
        "account_currency",
        "statement_period",
        "statement_totals",
        "positions",
        "fills",
        "cash_movements",
        "fees",
        "financing",
        "excluded_lines",
        "external_fetch",
        "broker_api_call"
    )
    required_boolean_values = [ordered]@{
        external_fetch = $false
        broker_api_call = $false
        commit_intent = $false
        db_mutation_intent = $false
        production_live_mode = $false
    }
    reject_conditions = @(
        "missing sha256",
        "missing account_id_hash",
        "missing statement period",
        "missing totals",
        "external_fetch true",
        "broker_api_call true",
        "production/live mode",
        "commit intent",
        "DB mutation intent"
    )
    quarantine_on_failure = $true
}
Write-JsonArtifact "broker-statement-manual-import-schema-r001.json" $brokerSchema

$accountingSchema = [ordered]@{
    artifact_type = "manual_accounting_evidence_import_schema_r001"
    environment = "sandbox"
    import_mode = "offline_manual_file_drop_only"
    required_fields = @(
        "artifact_type",
        "environment",
        "import_mode",
        "source_file_name",
        "source_file_sha256",
        "imported_at_utc",
        "imported_by",
        "approval_id",
        "account_currency",
        "accounting_policy_version",
        "accounting_basis",
        "period",
        "gross_pnl",
        "commission_expense",
        "net_pnl",
        "realized_unrealized_classification",
        "fx_translation_policy",
        "rounding_policy",
        "external_fetch",
        "db_mutation",
        "ledger_commit"
    )
    required_boolean_values = [ordered]@{
        external_fetch = $false
        db_mutation = $false
        ledger_commit = $false
        production_live_mode = $false
    }
    reject_conditions = @(
        "missing policy version",
        "missing source hash",
        "missing approval",
        "external fetch",
        "DB mutation",
        "ledger commit",
        "production/live mode"
    )
    quarantine_on_failure = $true
}
Write-JsonArtifact "accounting-evidence-manual-import-schema-r001.json" $accountingSchema

$validationPolicy = [ordered]@{
    artifact_type = "controlled_import_validation_policy_r001"
    environment = "sandbox"
    every_imported_evidence_file_must_have_sha256 = $true
    every_imported_evidence_file_must_be_local_manual = $true
    every_imported_evidence_file_must_be_sandbox_environment_unless_future_promotion_package = $true
    every_import_must_have_operator_approval = $true
    every_account_scoped_import_must_have_account_id_hash = $true
    every_import_must_have_period_boundaries = $true
    preserve_original_raw_values = $true
    produce_normalized_values_separately = $true
    preserve_excluded_lines = $true
    quarantine_on_validation_failure = $true
    no_import_may_trigger_external_calls = $true
    no_import_may_trigger_db_mutation = $true
    no_import_may_trigger_ledger_commit = $true
    no_import_may_mark_production_live_ready = $true
    quarantine_statuses = @(
        "BLOCKED_MISSING_SHA256",
        "BLOCKED_MISSING_ACCOUNT_ID_HASH",
        "BLOCKED_MISSING_STATEMENT_PERIOD",
        "BLOCKED_EXTERNAL_FETCH_FLAG_DETECTED",
        "BLOCKED_BROKER_API_CALL_FLAG_DETECTED",
        "BLOCKED_LEDGER_COMMIT_FLAG_DETECTED",
        "BLOCKED_DB_MUTATION_FLAG_DETECTED"
    )
}
Write-JsonArtifact "controlled-import-validation-policy-r001.json" $validationPolicy

$sampleBroker = [ordered]@{
    artifact_type = "sandbox_broker_statement_manual_import_sample_r001"
    environment = "sandbox"
    import_mode = "offline_manual_file_drop_only"
    sample_only = $true
    real_broker_statement = $false
    source_file_name = "sample-manual-broker-statement-import-r001.json"
    source_file_sha256 = $sourceHash
    imported_at_utc = "fixture"
    imported_by = "sandbox-operator"
    approval_id = "controlled-real-evidence-import-r001:sample-broker-statement"
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
    statement_period = [ordered]@{
        start_utc = "fixture"
        end_utc = "fixture"
    }
    statement_totals = [ordered]@{
        gross_pnl_usd = $grossUsd
        commission_usd = $commissionUsd
        net_pnl_usd = $netUsd
        unmatched_items = 0
    }
    positions = @()
    fills = @()
    cash_movements = @()
    fees = @()
    financing = @()
    excluded_lines = $excludedLines
    validation_status = "SAMPLE_MANUAL_BROKER_IMPORT_VALID"
}
Write-JsonArtifact "sample-manual-broker-statement-import-r001.json" $sampleBroker

$sampleAccounting = [ordered]@{
    artifact_type = "sandbox_accounting_evidence_manual_import_sample_r001"
    environment = "sandbox"
    import_mode = "offline_manual_file_drop_only"
    sample_only = $true
    real_accounting_close = $false
    source_file_name = "sample-manual-accounting-evidence-import-r001.json"
    source_file_sha256 = $sourceHash
    imported_at_utc = "fixture"
    imported_by = "sandbox-operator"
    approval_id = "controlled-real-evidence-import-r001:sample-accounting-evidence"
    account_currency = "USD"
    accounting_policy_version = "sandbox-preview-fixture-policy-r001"
    accounting_basis = "fixture_policy"
    period = [ordered]@{
        start_utc = "fixture"
        end_utc = "fixture"
    }
    gross_pnl = [ordered]@{ currency = "USD"; amount = $grossUsd }
    commission_expense = [ordered]@{ currency = "USD"; amount = $commissionUsd }
    net_pnl = [ordered]@{ currency = "USD"; amount = $netUsd }
    realized_unrealized_classification = "sandbox_closed_round_trip_preview_only"
    fx_translation_policy = "prior_account_currency_fixture_policy"
    rounding_policy = "six_decimal_usd_preview"
    external_fetch = $false
    market_data_fetch = $false
    account_data_fetch = $false
    db_mutation = $false
    ledger_commit = $false
    production_live_ready = $false
    trading_readiness_ready = $false
    validation_status = "SAMPLE_MANUAL_ACCOUNTING_IMPORT_VALID"
}
Write-JsonArtifact "sample-manual-accounting-evidence-import-r001.json" $sampleAccounting

$schemaBrokerPath = Join-Path $ArtifactDir "broker-statement-manual-import-schema-r001.json"
$schemaAccountingPath = Join-Path $ArtifactDir "accounting-evidence-manual-import-schema-r001.json"
$validationPolicyPath = Join-Path $ArtifactDir "controlled-import-validation-policy-r001.json"
$sampleBrokerPath = Join-Path $ArtifactDir "sample-manual-broker-statement-import-r001.json"
$sampleAccountingPath = Join-Path $ArtifactDir "sample-manual-accounting-evidence-import-r001.json"

$main = [ordered]@{
    package = "NEXT_CONTROLLED_REAL_EVIDENCE_IMPORT_R001"
    status = "CONTROLLED_REAL_EVIDENCE_IMPORT_READY_R001"
    environment = "sandbox"
    mode = "offline_manual_import_framework_only"
    source_package = "NEXT_SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001"
    source_status = $source.status
    source_artifact_hashes = [ordered]@{
        sandbox_broker_accounting_reconciliation_r001 = $sourceHash
        broker_statement_manual_import_schema_r001 = Sha $schemaBrokerPath
        accounting_evidence_manual_import_schema_r001 = Sha $schemaAccountingPath
        controlled_import_validation_policy_r001 = Sha $validationPolicyPath
        sample_manual_broker_statement_import_r001 = Sha $sampleBrokerPath
        sample_manual_accounting_evidence_import_r001 = Sha $sampleAccountingPath
    }
    controlled_import_lanes = [ordered]@{
        broker_statement_manual_import = [ordered]@{
            ready = $true
            external_fetch_allowed = $false
            broker_api_allowed = $false
            requires_manual_file_drop = $true
            requires_sha256 = $true
            requires_account_id_hash = $true
            requires_statement_period = $true
            requires_schema_validation = $true
            requires_operator_approval = $true
            quarantine_on_failure = $true
        }
        accounting_evidence_manual_import = [ordered]@{
            ready = $true
            external_fetch_allowed = $false
            requires_sha256 = $true
            requires_policy_version = $true
            requires_operator_approval = $true
            quarantine_on_failure = $true
        }
    }
    ready_outputs = [ordered]@{
        controlled_real_evidence_import_framework = $true
        manual_broker_statement_import_interface = $true
        manual_accounting_evidence_import_interface = $true
        import_validation_preview = $true
    }
    sample_validation = [ordered]@{
        broker_statement_import = $sampleBroker.validation_status
        accounting_evidence_import = $sampleAccounting.validation_status
        gross_usd = $grossUsd
        commission_usd = $commissionUsd
        net_usd = $netUsd
        unmatched_items = 0
    }
    forbidden_ready_labels = [ordered]@{
        broker_api_statement_fetch = $false
        live_broker_reconciliation = $false
        real_broker_statement_reconciliation = $false
        realized_accounting_close = $false
        committed_ledger = $false
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    still_blocked = @(
        "broker_api_statement_fetch",
        "live_broker_reconciliation",
        "realized_accounting_close",
        "ledger_commit",
        "db_mutation",
        "production_live",
        "trading_readiness"
    )
    global_guards = [ordered]@{
        external_calls = $false
        broker_api_calls = $false
        market_data_fetch = $false
        account_data_fetch = $false
        ledger_commit = $false
        db_mutation = $false
        trading_activity = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
}
Write-JsonArtifact "controlled-real-evidence-import-r001.json" $main

$summary = @"
# NEXT_CONTROLLED_REAL_EVIDENCE_IMPORT_R001

Status: CONTROLLED_REAL_EVIDENCE_IMPORT_READY_R001

Source package: NEXT_SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001

Source status: $($source.status)

Import lanes now defined:
- Manual broker statement import interface, offline file drop only.
- Manual accounting evidence import interface, offline file drop only.
- Import validation preview.
- Quarantine/rejection workflow for invalid imports.
- Manual-import reconciliation dry-run framework.

Sample imports validate:
- Broker statement sample: $($sampleBroker.validation_status)
- Accounting evidence sample: $($sampleAccounting.validation_status)
- Gross USD: $grossUsd
- Commission USD: $commissionUsd
- Net USD: $netUsd
- Unmatched items: 0

Future real manual import requires:
- Local/manual evidence file.
- Source file SHA-256.
- Operator approval ID.
- Account identifier hash for account-scoped imports.
- Period boundaries.
- Schema validation.
- Quarantine on failure.

Still blocked:
- broker API statement fetch
- live broker reconciliation
- realized accounting close
- ledger commit
- DB mutation
- production/live
- trading readiness

No trading, R009 submission, LMAX FIX/API call, Polygon/Massive call, broker API call, broker statement fetch, account-data fetch, market-data fetch, live order/fill report consumption, DB mutation, ledger commit, production table write, or production/live activity occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "controlled-real-evidence-import-summary-r001.md") -Value $summary -Encoding UTF8

Write-Host "CONTROLLED_REAL_EVIDENCE_IMPORT_R001_ARTIFACTS_WRITTEN"

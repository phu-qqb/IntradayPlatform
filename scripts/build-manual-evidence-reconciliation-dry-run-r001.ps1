param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\manual-evidence-reconciliation-dry-run-r001"
$BrokerInbox = Join-Path $ArtifactDir "inbox\broker-statements"
$AccountingInbox = Join-Path $ArtifactDir "inbox\accounting-evidence"
$QuarantineDir = Join-Path $ArtifactDir "quarantine"
$AcceptedDir = Join-Path $ArtifactDir "accepted"
foreach ($dir in @($ArtifactDir, $BrokerInbox, $AccountingInbox, $QuarantineDir, $AcceptedDir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

function Write-JsonArtifact([string]$Path, [object]$Value) {
    $Value | ConvertTo-Json -Depth 60 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-NamedJsonArtifact([string]$Name, [object]$Value) {
    Write-JsonArtifact (Join-Path $ArtifactDir $Name) $Value
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

function HasText($Value) {
    -not [string]::IsNullOrWhiteSpace([string]$Value)
}

function DecimalOrNull($Value) {
    if ($null -eq $Value) { return $null }
    return [decimal]$Value
}

function DiffRow([string]$Field, [decimal]$SourceValue, [decimal]$ImportedValue, [decimal]$Tolerance) {
    $delta = [decimal]::Round(($ImportedValue - $SourceValue), 6)
    [ordered]@{
        field = $Field
        source_value = $SourceValue
        imported_value = $ImportedValue
        delta = $delta
        tolerance = "0.000001"
        reconciled = ([Math]::Abs($delta) -le $Tolerance)
    }
}

function Validate-BrokerImport($Import, [string]$Path) {
    $reasons = @()
    if (-not (HasText $Import.artifact_type)) { $reasons += "artifact_type missing" }
    if ($Import.environment -ne "sandbox") { $reasons += "environment is not sandbox" }
    if (-not ([string]$Import.import_mode).Contains("offline")) { $reasons += "import_mode is not offline/manual" }
    if (-not (HasText $Import.source_file_sha256)) { $reasons += "source_file_sha256 missing" }
    if (-not (HasText $Import.imported_by)) { $reasons += "imported_by missing" }
    if (-not (HasText $Import.approval_id)) { $reasons += "approval_id missing" }
    if (-not (HasText $Import.account_id_hash)) { $reasons += "account_id_hash missing" }
    if (-not (HasText $Import.account_currency)) { $reasons += "account_currency missing" }
    if ($null -eq $Import.statement_period -or -not (HasText $Import.statement_period.start_utc) -or -not (HasText $Import.statement_period.end_utc)) { $reasons += "statement_period missing" }
    if ($null -eq $Import.statement_totals -or $null -eq $Import.statement_totals.gross_pnl_usd -or $null -eq $Import.statement_totals.commission_usd -or $null -eq $Import.statement_totals.net_pnl_usd) { $reasons += "statement_totals missing" }
    if ($Import.external_fetch -ne $false) { $reasons += "external_fetch true" }
    if ($Import.broker_api_call -ne $false) { $reasons += "broker_api_call true" }
    if ($null -ne $Import.market_data_fetch -and $Import.market_data_fetch -ne $false) { $reasons += "market_data_fetch true" }
    if ($null -ne $Import.account_data_fetch -and $Import.account_data_fetch -ne $false) { $reasons += "account_data_fetch true" }
    if ($null -ne $Import.db_mutation -and $Import.db_mutation -ne $false) { $reasons += "db_mutation true" }
    if ($null -ne $Import.ledger_commit -and $Import.ledger_commit -ne $false) { $reasons += "ledger_commit true" }
    if ($null -ne $Import.production_live_ready -and $Import.production_live_ready -ne $false) { $reasons += "production_live flag true" }
    if ($null -ne $Import.trading_readiness_ready -and $Import.trading_readiness_ready -ne $false) { $reasons += "trading flag true" }
    [ordered]@{
        path = $Path
        sha256 = Sha $Path
        valid = ($reasons.Count -eq 0)
        reasons = $reasons
    }
}

function Validate-AccountingImport($Import, [string]$Path) {
    $reasons = @()
    if (-not (HasText $Import.artifact_type)) { $reasons += "artifact_type missing" }
    if ($Import.environment -ne "sandbox") { $reasons += "environment is not sandbox" }
    if (-not ([string]$Import.import_mode).Contains("offline")) { $reasons += "import_mode is not offline/manual" }
    if (-not (HasText $Import.source_file_sha256)) { $reasons += "source_file_sha256 missing" }
    if (-not (HasText $Import.imported_by)) { $reasons += "imported_by missing" }
    if (-not (HasText $Import.approval_id)) { $reasons += "approval_id missing" }
    if (-not (HasText $Import.account_currency)) { $reasons += "account_currency missing" }
    if (-not (HasText $Import.accounting_policy_version)) { $reasons += "accounting_policy_version missing" }
    if (-not (HasText $Import.accounting_basis)) { $reasons += "accounting_basis missing" }
    if ($null -eq $Import.period -or -not (HasText $Import.period.start_utc) -or -not (HasText $Import.period.end_utc)) { $reasons += "period missing" }
    if ($null -eq $Import.gross_pnl -or $null -eq $Import.gross_pnl.amount) { $reasons += "gross_pnl missing" }
    if ($null -eq $Import.commission_expense -or $null -eq $Import.commission_expense.amount) { $reasons += "commission_expense missing" }
    if ($null -eq $Import.net_pnl -or $null -eq $Import.net_pnl.amount) { $reasons += "net_pnl missing" }
    if (-not (HasText $Import.realized_unrealized_classification)) { $reasons += "realized_unrealized_classification missing" }
    if (-not (HasText $Import.fx_translation_policy)) { $reasons += "fx_translation_policy missing" }
    if (-not (HasText $Import.rounding_policy)) { $reasons += "rounding_policy missing" }
    if ($Import.external_fetch -ne $false) { $reasons += "external_fetch true" }
    if ($null -ne $Import.market_data_fetch -and $Import.market_data_fetch -ne $false) { $reasons += "market_data_fetch true" }
    if ($null -ne $Import.account_data_fetch -and $Import.account_data_fetch -ne $false) { $reasons += "account_data_fetch true" }
    if ($Import.db_mutation -ne $false) { $reasons += "db_mutation true" }
    if ($Import.ledger_commit -ne $false) { $reasons += "ledger_commit true" }
    if ($null -ne $Import.production_live_ready -and $Import.production_live_ready -ne $false) { $reasons += "production_live flag true" }
    if ($null -ne $Import.trading_readiness_ready -and $Import.trading_readiness_ready -ne $false) { $reasons += "trading flag true" }
    [ordered]@{
        path = $Path
        sha256 = Sha $Path
        valid = ($reasons.Count -eq 0)
        reasons = $reasons
    }
}

$controlledPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\controlled-real-evidence-import-r001.json"
$brokerSchemaPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\broker-statement-manual-import-schema-r001.json"
$accountingSchemaPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\accounting-evidence-manual-import-schema-r001.json"
$validationPolicyPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\controlled-import-validation-policy-r001.json"
$sampleBrokerSourcePath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\sample-manual-broker-statement-import-r001.json"
$sampleAccountingSourcePath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\sample-manual-accounting-evidence-import-r001.json"
$reconciliationPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001\sandbox-broker-accounting-reconciliation-r001.json"
$closeoutPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-r001.json"

foreach ($path in @($controlledPath, $brokerSchemaPath, $accountingSchemaPath, $validationPolicyPath, $sampleBrokerSourcePath, $sampleAccountingSourcePath, $reconciliationPath, $closeoutPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required source artifact missing: $path"
    }
}

$controlled = Read-JsonFile $controlledPath
$reconciliation = Read-JsonFile $reconciliationPath
$closeout = Read-JsonFile $closeoutPath
if ($controlled.status -ne "CONTROLLED_REAL_EVIDENCE_IMPORT_READY_R001") { throw "Controlled import framework is not ready." }
if ($reconciliation.status -ne "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001") { throw "Source reconciliation is not ready." }

$validBrokerPath = Join-Path $BrokerInbox "sample-manual-broker-statement-import-r001.json"
$validAccountingPath = Join-Path $AccountingInbox "sample-manual-accounting-evidence-import-r001.json"
Copy-Item -LiteralPath $sampleBrokerSourcePath -Destination $validBrokerPath -Force
Copy-Item -LiteralPath $sampleAccountingSourcePath -Destination $validAccountingPath -Force

$invalidBroker = [ordered]@{
    artifact_type = "sandbox_broker_statement_manual_import_sample_r001"
    environment = "sandbox"
    import_mode = "offline_manual_file_drop_only"
    sample_only = $true
    real_broker_statement = $false
    source_file_name = "invalid-broker-import-r001.json"
    imported_at_utc = "fixture"
    imported_by = "sandbox-operator"
    approval_id = "controlled-real-evidence-import-r001:invalid-broker"
    external_fetch = $true
    broker_api_call = $true
    market_data_fetch = $true
    account_data_fetch = $true
    db_mutation = $false
    ledger_commit = $false
    production_live_ready = $true
    trading_readiness_ready = $true
    broker = "LMAX"
    venue = "LMAX_GLOBAL"
    account_currency = "USD"
    statement_totals = [ordered]@{
        gross_pnl_usd = -50.308800
        commission_usd = 26.268029
        net_pnl_usd = -76.576829
    }
}
$invalidBrokerPath = Join-Path $BrokerInbox "invalid-broker-import-r001.json"
Write-JsonArtifact $invalidBrokerPath $invalidBroker

$invalidAccounting = [ordered]@{
    artifact_type = "sandbox_accounting_evidence_manual_import_sample_r001"
    environment = "sandbox"
    import_mode = "offline_manual_file_drop_only"
    sample_only = $true
    real_accounting_close = $false
    source_file_name = "invalid-accounting-import-r001.json"
    imported_at_utc = "fixture"
    imported_by = "sandbox-operator"
    account_currency = "USD"
    accounting_basis = "fixture_policy"
    gross_pnl = [ordered]@{ currency = "USD"; amount = -50.308800 }
    commission_expense = [ordered]@{ currency = "USD"; amount = 26.268029 }
    net_pnl = [ordered]@{ currency = "USD"; amount = -76.576829 }
    external_fetch = $true
    market_data_fetch = $true
    account_data_fetch = $true
    db_mutation = $true
    ledger_commit = $true
    production_live_ready = $true
    trading_readiness_ready = $true
}
$invalidAccountingPath = Join-Path $AccountingInbox "invalid-accounting-import-r001.json"
Write-JsonArtifact $invalidAccountingPath $invalidAccounting

$sourceGross = [decimal]$closeout.gross_pnl_usd
$sourceCommission = [decimal]$closeout.commission_usd
$sourceNet = [decimal]$closeout.net_pnl_usd
$tolerance = [decimal]0.000001

$acceptedBroker = @()
$acceptedAccounting = @()
$quarantineItems = @()
$brokerDiffs = @()
$accountingDiffs = @()

foreach ($file in Get-ChildItem -LiteralPath $BrokerInbox -Filter *.json -File) {
    $import = Read-JsonFile $file.FullName
    $validation = Validate-BrokerImport $import $file.FullName
    if ($validation.valid) {
        $diffs = @(
            DiffRow "gross_pnl_usd" $sourceGross ([decimal]$import.statement_totals.gross_pnl_usd) $tolerance
            DiffRow "commission_usd" $sourceCommission ([decimal]$import.statement_totals.commission_usd) $tolerance
            DiffRow "net_pnl_usd" $sourceNet ([decimal]$import.statement_totals.net_pnl_usd) $tolerance
        )
        $brokerDiffs += $diffs
        $acceptedBroker += [ordered]@{
            path = $file.FullName
            sha256 = Sha $file.FullName
            sample_only = $import.sample_only
            real_broker_statement = $import.real_broker_statement
            external_fetch = $import.external_fetch
            broker_api_call = $import.broker_api_call
        }
    } else {
        $quarantineItems += [ordered]@{
            path = $file.FullName
            sha256 = Sha $file.FullName
            evidence_type = "broker_statement"
            reasons = $validation.reasons
            no_destructive_file_movement = $true
        }
    }
}

foreach ($file in Get-ChildItem -LiteralPath $AccountingInbox -Filter *.json -File) {
    $import = Read-JsonFile $file.FullName
    $validation = Validate-AccountingImport $import $file.FullName
    if ($validation.valid) {
        $diffs = @(
            DiffRow "gross_pnl" $sourceGross ([decimal]$import.gross_pnl.amount) $tolerance
            DiffRow "commission_expense" $sourceCommission ([decimal]$import.commission_expense.amount) $tolerance
            DiffRow "net_pnl" $sourceNet ([decimal]$import.net_pnl.amount) $tolerance
        )
        $accountingDiffs += $diffs
        $acceptedAccounting += [ordered]@{
            path = $file.FullName
            sha256 = Sha $file.FullName
            sample_only = $import.sample_only
            real_accounting_close = $import.real_accounting_close
            db_mutation = $import.db_mutation
            ledger_commit = $import.ledger_commit
        }
    } else {
        $quarantineItems += [ordered]@{
            path = $file.FullName
            sha256 = Sha $file.FullName
            evidence_type = "accounting_evidence"
            reasons = $validation.reasons
            no_destructive_file_movement = $true
        }
    }
}

$brokerSeen = @(Get-ChildItem -LiteralPath $BrokerInbox -Filter *.json -File).Count
$accountingSeen = @(Get-ChildItem -LiteralPath $AccountingInbox -Filter *.json -File).Count
$brokerReconciled = (@($brokerDiffs).Count -gt 0) -and (@($brokerDiffs | Where-Object { $_.reconciled -ne $true }).Count -eq 0)
$accountingReconciled = (@($accountingDiffs).Count -gt 0) -and (@($accountingDiffs | Where-Object { $_.reconciled -ne $true }).Count -eq 0)
$status = "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001"
if (@($acceptedBroker).Count -eq 0 -and @($acceptedAccounting).Count -eq 0) { $status = "BLOCKED_NO_MANUAL_IMPORTS_FOUND" }
if (-not $brokerReconciled -or -not $accountingReconciled) { $status = "BLOCKED_RECONCILIATION_TOTAL_MISMATCH" }

$diffReport = [ordered]@{
    package = "NEXT_MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_R001"
    status = "MANUAL_EVIDENCE_DIFF_REPORT_READY_R001"
    tolerance = "0.000001"
    broker_statement_diffs = $brokerDiffs
    accounting_evidence_diffs = $accountingDiffs
    broker_reconciled = $brokerReconciled
    accounting_reconciled = $accountingReconciled
}
Write-NamedJsonArtifact "manual-evidence-diff-report-r001.json" $diffReport

$quarantine = [ordered]@{
    package = "NEXT_MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_R001"
    status = "MANUAL_EVIDENCE_QUARANTINE_PREVIEW_READY_R001"
    quarantined_count = @($quarantineItems).Count
    items = $quarantineItems
    no_destructive_file_movement = $true
    no_db_mutation = $true
    no_external_calls = $true
}
Write-NamedJsonArtifact "manual-evidence-quarantine-preview-r001.json" $quarantine

$main = [ordered]@{
    package = "NEXT_MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_R001"
    status = $status
    environment = "sandbox"
    mode = "offline_manual_dry_run_only"
    source_packages = [ordered]@{
        controlled_real_evidence_import = "NEXT_CONTROLLED_REAL_EVIDENCE_IMPORT_R001"
        sandbox_broker_accounting_reconciliation = "NEXT_SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001"
        sandbox_preview_closeout = "NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001"
    }
    source_artifact_hashes = [ordered]@{
        controlled_real_evidence_import_r001 = Sha $controlledPath
        sandbox_broker_accounting_reconciliation_r001 = Sha $reconciliationPath
        sandbox_preview_closeout_r001 = Sha $closeoutPath
        broker_statement_manual_import_schema_r001 = Sha $brokerSchemaPath
        accounting_evidence_manual_import_schema_r001 = Sha $accountingSchemaPath
        controlled_import_validation_policy_r001 = Sha $validationPolicyPath
    }
    manual_imports = [ordered]@{
        broker_statement_imports_seen = $brokerSeen
        broker_statement_imports_accepted = @($acceptedBroker).Count
        broker_statement_imports_quarantined = @($quarantineItems | Where-Object { $_.evidence_type -eq "broker_statement" }).Count
        accounting_evidence_imports_seen = $accountingSeen
        accounting_evidence_imports_accepted = @($acceptedAccounting).Count
        accounting_evidence_imports_quarantined = @($quarantineItems | Where-Object { $_.evidence_type -eq "accounting_evidence" }).Count
    }
    accepted_imports = [ordered]@{
        broker_statements = $acceptedBroker
        accounting_evidence = $acceptedAccounting
    }
    quarantine_preview = [ordered]@{
        quarantined_count = @($quarantineItems).Count
        items = $quarantineItems
    }
    source_closeout_values = [ordered]@{
        gross_usd = $sourceGross
        commission_usd = $sourceCommission
        net_usd = $sourceNet
        reconciled = $closeout.reconciled
        tolerance = "0.000001"
    }
    broker_statement_reconciliation_dry_run = [ordered]@{
        ready = ($status -eq "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001")
        mode = "manual_offline_dry_run_only"
        real_broker_statement_reconciliation_ready = $false
        reconciled = $brokerReconciled
        unmatched_items = @()
        diffs = $brokerDiffs
    }
    accounting_evidence_reconciliation_dry_run = [ordered]@{
        ready = ($status -eq "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001")
        mode = "manual_offline_dry_run_only"
        realized_accounting_close_ready = $false
        reconciled = $accountingReconciled
        unmatched_items = @()
        diffs = $accountingDiffs
    }
    ready_outputs = [ordered]@{
        manual_broker_statement_reconciliation_dry_run = ($status -eq "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001")
        manual_accounting_evidence_reconciliation_dry_run = ($status -eq "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001")
        manual_evidence_diff_report = $true
        invalid_evidence_quarantine_preview = $true
    }
    forbidden_ready_labels = [ordered]@{
        broker_api_statement_fetch = $false
        live_broker_reconciliation = $false
        real_broker_statement_reconciliation = $false
        real_broker_confirmed_pnl = $false
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
        "real_broker_confirmed_pnl",
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
Write-NamedJsonArtifact "manual-evidence-reconciliation-dry-run-r001.json" $main

$summary = @"
# NEXT_MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_R001

Status: $status

Source packages:
- Controlled import framework: $($controlled.status)
- Sandbox broker/accounting reconciliation: $($reconciliation.status)
- Sandbox closeout: $($closeout.status)

Manual imports:
- Broker statement imports seen: $brokerSeen
- Broker statement imports accepted: $(@($acceptedBroker).Count)
- Broker statement imports quarantined: $(@($quarantineItems | Where-Object { $_.evidence_type -eq "broker_statement" }).Count)
- Accounting evidence imports seen: $accountingSeen
- Accounting evidence imports accepted: $(@($acceptedAccounting).Count)
- Accounting evidence imports quarantined: $(@($quarantineItems | Where-Object { $_.evidence_type -eq "accounting_evidence" }).Count)

Final values:
- Gross USD: $sourceGross
- Commission USD: $sourceCommission
- Net USD: $sourceNet

Reconciliation:
- Broker dry-run reconciled: $brokerReconciled
- Accounting dry-run reconciled: $accountingReconciled
- Unmatched items: 0
- Tolerance: 0.000001

Diff summary:
- Broker fields reconciled: $(@($brokerDiffs | Where-Object { $_.reconciled -eq $true }).Count) / $(@($brokerDiffs).Count)
- Accounting fields reconciled: $(@($accountingDiffs | Where-Object { $_.reconciled -eq $true }).Count) / $(@($accountingDiffs).Count)

Quarantine preview:
- Quarantined count: $(@($quarantineItems).Count)
- No destructive file movement occurred.

Still blocked:
- broker API statement fetch
- live broker reconciliation
- real broker-confirmed PnL
- realized accounting close
- ledger commit
- DB mutation
- production/live
- trading readiness

No trading, R009 submission, LMAX FIX/API call, Polygon/Massive call, broker API call, broker statement fetch, account-data fetch, market-data fetch, live order/fill report consumption, DB mutation, ledger commit, production table write, or production/live activity occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "manual-evidence-reconciliation-dry-run-summary-r001.md") -Value $summary -Encoding UTF8

Write-Host "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_R001_ARTIFACTS_WRITTEN"

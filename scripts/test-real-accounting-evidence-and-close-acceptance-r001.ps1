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
    $Value | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function New-AccountingEvidence([hashtable]$Overrides) {
    $fixture = [ordered]@{
        artifact_type = "real_accounting_evidence_import"
        environment = "sandbox"
        import_mode = "offline_manual"
        sample_only = $false
        real_accounting_evidence = $true
        source_file_name = "real-accounting-evidence.json"
        source_file_sha256 = "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        imported_by = "sandbox-operator"
        approval_id = "real-accounting-evidence-r001:test-evidence"
        account_currency = "USD"
        accounting_policy_version = "broker-statement-accounting-policy-r001"
        accounting_basis = "broker_statement_backed_sandbox_manual"
        period = [ordered]@{ from = "03/11/2025"; to = "03/11/2025" }
        realized_pnl_before_costs_usd = [decimal]6015.14
        commission_expense_usd = [decimal]225.63
        financing_expense_usd = [decimal]40.60
        realized_net_after_costs_usd = [decimal]5748.91
        unrealized_open_pnl_usd = [decimal]463.61
        equity_pnl_including_open_pnl_usd = [decimal]6212.52
        realized_unrealized_classification = "broker_statement_realized_unrealized_classification_r001"
        fx_translation_policy = "broker_statement_account_currency_usd"
        rounding_policy = "decimal_tolerance_0.000001"
        source_of_truth_hierarchy = "accepted_lmax_broker_statement_then_operator_accounting_evidence"
        audit_trail = [ordered]@{ operator_reviewed = $true; source = "test_fixture" }
        external_fetch = $false
        db_mutation = $false
        ledger_commit = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
    foreach ($key in $Overrides.Keys) { $fixture[$key] = $Overrides[$key] }
    return $fixture
}

function New-CloseApproval([hashtable]$Overrides) {
    $fixture = [ordered]@{
        artifact_type = "accounting_close_approval"
        environment = "sandbox"
        approval_mode = "offline_manual"
        sample_only = $false
        close_approval = $true
        close_scope = "broker_statement_period"
        account_currency = "USD"
        statement_period = [ordered]@{ from = "03/11/2025"; to = "03/11/2025" }
        approved_by = "sandbox-operator"
        approved_at_utc = "fixture"
        approval_id = "real-accounting-evidence-r001:test-close"
        approved_accounting_policy_version = "broker-statement-accounting-policy-r001"
        approved_source_artifact_hashes = [ordered]@{ broker_statement_accounting_dry_run = "sha256:BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB" }
        approved_values = [ordered]@{
            realized_pnl_before_costs_usd = [decimal]6015.14
            commission_expense_usd = [decimal]225.63
            financing_expense_usd = [decimal]40.60
            realized_net_after_costs_usd = [decimal]5748.91
            unrealized_open_pnl_usd = [decimal]463.61
            equity_pnl_including_open_pnl_usd = [decimal]6212.52
        }
        ledger_commit_authorized = $false
        db_mutation_authorized = $false
        production_live_authorized = $false
        trading_authorized = $false
    }
    foreach ($key in $Overrides.Keys) { $fixture[$key] = $Overrides[$key] }
    return $fixture
}

$builder = Join-Path $RepoRoot "scripts\build-real-accounting-evidence-and-close-acceptance-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-real-accounting-evidence-and-close-acceptance-r001-gate.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\real-accounting-evidence-and-close-acceptance-r001"
$main = Read-JsonFile (Join-Path $ArtifactDir "real-accounting-evidence-and-close-acceptance-r001.json")
$draft = Read-JsonFile (Join-Path $ArtifactDir "accounting-close-draft-from-broker-statement-r001.json")

Assert-True (@("REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_BLOCKED_R001", "REAL_ACCOUNTING_EVIDENCE_ACCEPTED_CLOSE_BLOCKED_R001", "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_READY_R001") -contains $main.status) "Current operator staging status must be valid."
Assert-Equal $draft.draft_only $true "Draft should be draft-only."
Assert-Equal $draft.real_accounting_evidence $false "Draft should not be real accounting evidence."
Assert-Equal $draft.realized_accounting_close $false "Draft should not close accounting."
Assert-DecimalEqual ([decimal]$draft.values.realized_pnl_before_costs_usd) ([decimal]6015.14) "Draft realised PnL mismatch."

if ($main.status -eq "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_READY_R001") {
    Assert-True ($main.staging_scan.accounting_evidence_files_seen -gt 0) "Ready current staging must include accounting evidence files."
    Assert-True ($main.staging_scan.accounting_close_approval_files_seen -gt 0) "Ready current staging must include close approval files."
    Assert-Equal $main.readiness.real_accounting_evidence_acceptance $true "Current valid staging should accept real accounting evidence."
    Assert-Equal $main.readiness.realized_accounting_close $true "Current valid staging should mark realized accounting close ready."
    Assert-Equal $main.readiness.ledger_commit $false "Ledger commit must remain false for current staging."
    Assert-Equal $main.readiness.db_mutation $false "DB mutation must remain false for current staging."
    Assert-Equal $main.readiness.production_live $false "Production/live must remain false for current staging."
    Assert-Equal $main.readiness.trading_readiness $false "Trading readiness must remain false for current staging."
} elseif ($main.status -eq "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_BLOCKED_R001" -and $main.staging_scan.accounting_evidence_files_seen -eq 0 -and $main.staging_scan.accounting_close_approval_files_seen -eq 0) {
    Assert-Equal $main.blocked_reason "NO_REAL_ACCOUNTING_EVIDENCE_OR_CLOSE_APPROVAL_STAGED" "Current empty staging blocked reason mismatch."
}

$emptySubdir = "real-accounting-evidence-and-close-acceptance-r001-empty-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $emptySubdir | Out-Null
$emptyDir = Join-Path $RepoRoot "artifacts\readiness\$emptySubdir"
$emptyMain = Read-JsonFile (Join-Path $emptyDir "real-accounting-evidence-and-close-acceptance-r001.json")
$emptyDraft = Read-JsonFile (Join-Path $emptyDir "accounting-close-draft-from-broker-statement-r001.json")
Assert-Equal $emptyMain.status "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_BLOCKED_R001" "Isolated empty staging should be blocked."
Assert-Equal $emptyMain.blocked_reason "NO_REAL_ACCOUNTING_EVIDENCE_OR_CLOSE_APPROVAL_STAGED" "Isolated empty staging reason mismatch."
Assert-Equal $emptyMain.staging_scan.accounting_evidence_files_seen 0 "Isolated empty accounting evidence staging must be empty."
Assert-Equal $emptyMain.staging_scan.accounting_close_approval_files_seen 0 "Isolated empty close approval staging must be empty."
Assert-Equal $emptyDraft.draft_only $true "Isolated empty draft should still be created."

$sampleSubdir = "real-accounting-evidence-and-close-acceptance-r001-sample-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $sampleSubdir | Out-Null
$sampleDir = Join-Path $RepoRoot "artifacts\readiness\$sampleSubdir"
Write-JsonFile (Join-Path $sampleDir "staging\accounting-evidence\sample-draft.json") (New-AccountingEvidence @{ sample_only = $true })
Write-JsonFile (Join-Path $sampleDir "staging\accounting-close-approval\sample-approval.json") (New-CloseApproval @{ sample_only = $true })
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $sampleSubdir | Out-Null
$sampleMain = Read-JsonFile (Join-Path $sampleDir "real-accounting-evidence-and-close-acceptance-r001.json")
$sampleScan = Read-JsonFile (Join-Path $sampleDir "real-accounting-evidence-staging-scan-r001.json")
Assert-Equal $sampleMain.readiness.real_accounting_evidence_acceptance $false "Sample accounting evidence must not promote."
Assert-Equal $sampleMain.readiness.realized_accounting_close $false "Sample approval must not close accounting."
Assert-True ($sampleScan.rejected_or_quarantined_count -ge 2) "Sample/draft files should be rejected or quarantined."

$invalidSubdir = "real-accounting-evidence-and-close-acceptance-r001-invalid-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $invalidSubdir | Out-Null
$invalidDir = Join-Path $RepoRoot "artifacts\readiness\$invalidSubdir"
Write-JsonFile (Join-Path $invalidDir "staging\accounting-evidence\missing-approval.json") (New-AccountingEvidence @{ approval_id = $null })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-evidence\missing-period.json") (New-AccountingEvidence @{ period = $null })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-evidence\period-mismatch.json") (New-AccountingEvidence @{ period = [ordered]@{ from = "04/11/2025"; to = "04/11/2025" } })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-evidence\totals-mismatch.json") (New-AccountingEvidence @{ realized_net_after_costs_usd = [decimal]5749.91 })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-evidence\external-fetch.json") (New-AccountingEvidence @{ external_fetch = $true })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-evidence\db-mutation.json") (New-AccountingEvidence @{ db_mutation = $true })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-evidence\ledger-commit.json") (New-AccountingEvidence @{ ledger_commit = $true })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-evidence\production-live.json") (New-AccountingEvidence @{ production_live_ready = $true })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-evidence\trading-ready.json") (New-AccountingEvidence @{ trading_readiness_ready = $true })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-close-approval\approval-period-mismatch.json") (New-CloseApproval @{ statement_period = [ordered]@{ from = "04/11/2025"; to = "04/11/2025" } })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-close-approval\approval-totals-mismatch.json") (New-CloseApproval @{ approved_values = [ordered]@{ realized_pnl_before_costs_usd = [decimal]6015.14; commission_expense_usd = [decimal]225.63; financing_expense_usd = [decimal]40.60; realized_net_after_costs_usd = [decimal]5749.91; unrealized_open_pnl_usd = [decimal]463.61; equity_pnl_including_open_pnl_usd = [decimal]6212.52 } })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-close-approval\approval-commit.json") (New-CloseApproval @{ ledger_commit_authorized = $true })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-close-approval\approval-prod.json") (New-CloseApproval @{ production_live_authorized = $true })
Write-JsonFile (Join-Path $invalidDir "staging\accounting-close-approval\approval-trading.json") (New-CloseApproval @{ trading_authorized = $true })
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $invalidSubdir | Out-Null
$invalidMain = Read-JsonFile (Join-Path $invalidDir "real-accounting-evidence-and-close-acceptance-r001.json")
$invalidQuarantine = Read-JsonFile (Join-Path $invalidDir "real-accounting-evidence-quarantine-preview-r001.json")
Assert-Equal $invalidMain.readiness.real_accounting_evidence_acceptance $false "Invalid accounting evidence must not be accepted."
Assert-Equal $invalidMain.readiness.realized_accounting_close $false "Invalid approval must not close accounting."
Assert-True ($invalidQuarantine.quarantined_count -ge 10) "Invalid fixtures should be quarantined."

$accountingOnlySubdir = "real-accounting-evidence-and-close-acceptance-r001-accounting-only-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $accountingOnlySubdir | Out-Null
$accountingOnlyDir = Join-Path $RepoRoot "artifacts\readiness\$accountingOnlySubdir"
Write-JsonFile (Join-Path $accountingOnlyDir "staging\accounting-evidence\valid-accounting.json") (New-AccountingEvidence @{})
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $accountingOnlySubdir | Out-Null
$accountingOnlyMain = Read-JsonFile (Join-Path $accountingOnlyDir "real-accounting-evidence-and-close-acceptance-r001.json")
Assert-Equal $accountingOnlyMain.status "REAL_ACCOUNTING_EVIDENCE_ACCEPTED_CLOSE_BLOCKED_R001" "Accounting-only status mismatch."
Assert-Equal $accountingOnlyMain.blocked_reason "ACCOUNTING_CLOSE_APPROVAL_MISSING" "Accounting-only blocked reason mismatch."
Assert-Equal $accountingOnlyMain.readiness.real_accounting_evidence_acceptance $true "Valid accounting evidence should be accepted."
Assert-Equal $accountingOnlyMain.readiness.realized_accounting_close $false "Close must remain false without approval."

$readySubdir = "real-accounting-evidence-and-close-acceptance-r001-ready-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $readySubdir | Out-Null
$readyDir = Join-Path $RepoRoot "artifacts\readiness\$readySubdir"
Write-JsonFile (Join-Path $readyDir "staging\accounting-evidence\valid-accounting.json") (New-AccountingEvidence @{})
Write-JsonFile (Join-Path $readyDir "staging\accounting-close-approval\valid-approval.json") (New-CloseApproval @{})
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $readySubdir | Out-Null
$readyMain = Read-JsonFile (Join-Path $readyDir "real-accounting-evidence-and-close-acceptance-r001.json")
Assert-Equal $readyMain.status "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_READY_R001" "Ready status mismatch."
Assert-Equal $readyMain.readiness.real_accounting_evidence_acceptance $true "Ready accounting acceptance mismatch."
Assert-Equal $readyMain.readiness.realized_accounting_close $true "Ready close mismatch."
Assert-Equal $readyMain.readiness.ledger_commit $false "Ledger commit must remain false even when close is ready."
Assert-Equal $readyMain.readiness.db_mutation $false "DB mutation must remain false even when close is ready."
Assert-Equal $readyMain.readiness.production_live $false "Production/live must remain false."
Assert-Equal $readyMain.readiness.trading_readiness $false "Trading readiness must remain false."
Assert-DecimalEqual ([decimal]$readyMain.values.realized_pnl_before_costs_usd) ([decimal]6015.14) "Ready realised PnL mismatch."
Assert-DecimalEqual ([decimal]$readyMain.values.commission_expense_usd) ([decimal]225.63) "Ready commission mismatch."
Assert-DecimalEqual ([decimal]$readyMain.values.financing_expense_usd) ([decimal]40.60) "Ready financing mismatch."
Assert-DecimalEqual ([decimal]$readyMain.values.realized_net_after_costs_usd) ([decimal]5748.91) "Ready net mismatch."
Assert-DecimalEqual ([decimal]$readyMain.values.unrealized_open_pnl_usd) ([decimal]463.61) "Ready unrealized mismatch."
Assert-DecimalEqual ([decimal]$readyMain.values.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Ready equity mismatch."

Assert-Equal $readyMain.global_guards.external_calls $false "External calls must remain false."
Assert-Equal $readyMain.global_guards.broker_api_calls $false "Broker API calls must remain false."
Assert-Equal $readyMain.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $readyMain.global_guards.account_data_fetch $false "Account-data fetch must remain false."
Assert-Equal $readyMain.global_guards.ledger_commit $false "Ledger commit guard must remain false."
Assert-Equal $readyMain.global_guards.db_mutation $false "DB mutation guard must remain false."
Assert-Equal $readyMain.global_guards.production_live_ready $false "Production/live guard must remain false."
Assert-Equal $readyMain.global_guards.trading_readiness_ready $false "Trading readiness guard must remain false."

Write-Host "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_R001_TEST_PASS"

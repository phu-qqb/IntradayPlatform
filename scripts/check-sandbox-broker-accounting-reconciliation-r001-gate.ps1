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

$closeoutPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-r001.json"
$readinessGatePath = Join-Path $RepoRoot "artifacts\readiness\accounting-broker-ledger-readiness-gate-r001\accounting-broker-ledger-readiness-gate-r001.json"
$artifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001"

$requiredArtifacts = @(
    "sandbox-broker-statement-fixture-r001.json",
    "broker-reconciliation-policy-r001.json",
    "accounting-pnl-policy-r001.json",
    "ledger-dry-run-policy-r001.json",
    "sandbox-broker-accounting-reconciliation-r001.json",
    "sandbox-broker-accounting-reconciliation-summary-r001.md"
)

Assert-True (Test-Path -LiteralPath $closeoutPath) "Sandbox closeout source must exist."
Assert-True (Test-Path -LiteralPath $readinessGatePath) "Readiness gate source must exist."
foreach ($name in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactDir $name)) "Required artifact missing: $name"
}

$closeout = Read-JsonFile $closeoutPath
$readinessGate = Read-JsonFile $readinessGatePath
$fixture = Read-JsonFile (Join-Path $artifactDir "sandbox-broker-statement-fixture-r001.json")
$brokerPolicy = Read-JsonFile (Join-Path $artifactDir "broker-reconciliation-policy-r001.json")
$accountingPolicy = Read-JsonFile (Join-Path $artifactDir "accounting-pnl-policy-r001.json")
$ledgerPolicy = Read-JsonFile (Join-Path $artifactDir "ledger-dry-run-policy-r001.json")
$main = Read-JsonFile (Join-Path $artifactDir "sandbox-broker-accounting-reconciliation-r001.json")

Assert-Equal $closeout.status "SANDBOX_PREVIEW_CLOSEOUT_READY_R001" "Source closeout status mismatch."
Assert-Equal $readinessGate.status "ACCOUNTING_BROKER_LEDGER_READINESS_GATE_BLOCKED_R001" "Source readiness gate status mismatch."
Assert-DecimalEqual ([decimal]$closeout.gross_pnl_usd) ([decimal]-50.308800) "Source gross USD mismatch."
Assert-DecimalEqual ([decimal]$closeout.commission_usd) ([decimal]26.268029) "Source commission USD mismatch."
Assert-DecimalEqual ([decimal]$closeout.net_pnl_usd) ([decimal]-76.576829) "Source net USD mismatch."

Assert-Equal $fixture.artifact_type "sandbox_broker_statement_fixture_r001" "Broker statement fixture artifact type mismatch."
Assert-Equal $fixture.environment "sandbox" "Broker statement fixture environment mismatch."
Assert-Equal $fixture.source "checked_in_fixture" "Broker statement fixture must be checked-in fixture."
Assert-Equal $fixture.external_fetch $false "Broker statement fixture must not use external fetch."
Assert-Equal $fixture.real_broker_statement $false "Broker statement fixture must not claim real broker statement."
Assert-Equal $fixture.account_currency "USD" "Broker statement fixture account currency mismatch."
Assert-DecimalEqual ([decimal]$fixture.statement_totals.gross_pnl_usd) ([decimal]-50.308800) "Statement fixture gross USD mismatch."
Assert-DecimalEqual ([decimal]$fixture.statement_totals.commission_usd) ([decimal]26.268029) "Statement fixture commission USD mismatch."
Assert-DecimalEqual ([decimal]$fixture.statement_totals.net_pnl_usd) ([decimal]-76.576829) "Statement fixture net USD mismatch."

Assert-Equal $brokerPolicy.broker_statement_reconciliation_mode "sandbox_fixture_preview_only" "Broker policy mode mismatch."
Assert-Equal $brokerPolicy.broker_statement_reconciliation_ready $true "Sandbox broker reconciliation preview policy should be ready."
Assert-Equal $brokerPolicy.real_broker_statement_reconciliation_ready $false "Real broker reconciliation must remain false."
Assert-Equal $brokerPolicy.external_fetch_allowed $false "Broker policy external fetch must remain false."

Assert-Equal $accountingPolicy.accounting_pnl_mode "sandbox_preview_only" "Accounting policy mode mismatch."
Assert-Equal $accountingPolicy.sandbox_accounting_pnl_preview_ready $true "Sandbox accounting preview policy should be ready."
Assert-Equal $accountingPolicy.realized_accounting_close_ready $false "Realized accounting close must remain false."
Assert-Equal $accountingPolicy.ledger_commit_ready $false "Accounting policy ledger commit ready must remain false."
Assert-Equal $accountingPolicy.db_mutation_ready $false "Accounting policy DB mutation ready must remain false."

Assert-Equal $ledgerPolicy.ledger_mode "dry_run_preview_only" "Ledger policy mode mismatch."
Assert-Equal $ledgerPolicy.ledger_commit_ready $false "Ledger commit ready must remain false."
Assert-Equal $ledgerPolicy.db_mutation_ready $false "Ledger DB mutation ready must remain false."
Assert-Equal $ledgerPolicy.commit_allowed $false "Ledger commit allowed must remain false."

Assert-Equal $main.package "NEXT_SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001" "Main package mismatch."
Assert-Equal $main.status "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001" "Main status mismatch."
Assert-Equal $main.environment "sandbox" "Main environment mismatch."
Assert-Equal $main.mode "preview_only" "Main mode mismatch."
Assert-Equal $main.account_currency "USD" "Main account currency mismatch."
foreach ($hashName in @("sandbox_preview_closeout", "readiness_gate", "broker_statement_fixture", "broker_reconciliation_policy", "accounting_pnl_policy", "ledger_dry_run_policy")) {
    Assert-True ($main.source_artifact_hashes.$hashName -match "^sha256:[A-F0-9]{64}$") "Missing source hash: $hashName"
}

Assert-Equal $main.broker_reconciliation_preview.ready $true "Sandbox broker reconciliation preview should be ready."
Assert-Equal $main.broker_reconciliation_preview.mode "sandbox_fixture_preview_only" "Broker reconciliation preview mode mismatch."
Assert-Equal $main.broker_reconciliation_preview.real_broker_statement_reconciliation_ready $false "Real broker reconciliation must remain false."
Assert-DecimalEqual ([decimal]$main.broker_reconciliation_preview.gross_pnl_usd) ([decimal]-50.308800) "Broker preview gross mismatch."
Assert-DecimalEqual ([decimal]$main.broker_reconciliation_preview.commission_usd) ([decimal]26.268029) "Broker preview commission mismatch."
Assert-DecimalEqual ([decimal]$main.broker_reconciliation_preview.net_pnl_usd) ([decimal]-76.576829) "Broker preview net mismatch."
Assert-Equal $main.broker_reconciliation_preview.reconciled_to_sandbox_closeout $true "Broker preview must reconcile to sandbox closeout."
Assert-Equal $main.broker_reconciliation_preview.reconciled_to_statement_fixture $true "Broker preview must reconcile to fixture."
Assert-Equal @($main.broker_reconciliation_preview.unmatched_items).Count 0 "Unmatched items must be empty."
Assert-True ([decimal]$main.broker_reconciliation_preview.tolerance -le [decimal]0.000001) "Tolerance must be no wider than 0.000001."

Assert-Equal $main.sandbox_accounting_pnl_preview.ready $true "Sandbox accounting PnL preview should be ready."
Assert-DecimalEqual ([decimal]$main.sandbox_accounting_pnl_preview.gross_pnl_usd) ([decimal]-50.308800) "Accounting preview gross mismatch."
Assert-DecimalEqual ([decimal]$main.sandbox_accounting_pnl_preview.commission_expense_usd) ([decimal]26.268029) "Accounting preview commission mismatch."
Assert-DecimalEqual ([decimal]$main.sandbox_accounting_pnl_preview.net_pnl_usd) ([decimal]-76.576829) "Accounting preview net mismatch."
Assert-Equal $main.sandbox_accounting_pnl_preview.realized_accounting_close_ready $false "Realized accounting close must remain false."
Assert-Equal $main.sandbox_accounting_pnl_preview.broker_confirmed_pnl $false "Broker-confirmed PnL must remain false."
Assert-Equal $main.sandbox_accounting_pnl_preview.ledger_committed $false "Ledger committed must remain false."

Assert-Equal $main.ledger_dry_run_preview.ready $true "Ledger dry-run preview should be ready."
Assert-Equal $main.ledger_dry_run_preview.mode "dry_run_preview_only" "Ledger dry-run mode mismatch."
Assert-Equal $main.ledger_dry_run_preview.commit_allowed $false "Ledger dry-run commit allowed must be false."
Assert-Equal $main.ledger_dry_run_preview.commit_eligible_entries 0 "Ledger dry-run commit eligible entries must be zero."
Assert-Equal $main.ledger_dry_run_preview.ledger_commit_ready $false "Ledger commit ready must remain false."
Assert-Equal $main.ledger_dry_run_preview.db_mutation_ready $false "DB mutation ready must remain false."
Assert-True (@($main.ledger_dry_run_preview.entries).Count -gt 0) "Ledger dry-run entries should exist."
Assert-Equal @($main.ledger_dry_run_preview.entries | Where-Object { $_.commit_eligible -eq $true -or $_.commit_allowed -eq $true }).Count 0 "No ledger dry-run entry may be commit eligible or allowed."

Assert-True (@($main.exclusion_evidence.excluded_unfilled | Where-Object { $_.symbol -eq "USDJPY" -and [decimal]$_.quantity -eq [decimal]50.0 -and [decimal]$_.pnl_impact_usd -eq [decimal]0.0 }).Count -eq 1) "Unfilled USDJPY 50.0 must remain excluded with zero PnL impact."
foreach ($symbol in @("AUDUSD", "CHFUSD", "EURUSD", "GBPUSD")) {
    Assert-True (@($main.exclusion_evidence.excluded_zero_quantity | Where-Object { $_.symbol -eq $symbol -and $_.reason -eq "zero_quantity" -and [decimal]$_.pnl_impact_usd -eq [decimal]0.0 }).Count -eq 1) "Zero-quantity excluded line missing or non-zero PnL impact: $symbol"
}
Assert-Equal $main.exclusion_evidence.excluded_reintroduced $false "Excluded lines must not be reintroduced."

Assert-Equal $main.ready_outputs.sandbox_broker_reconciliation_preview $true "Allowed sandbox broker preview ready output missing."
Assert-Equal $main.ready_outputs.sandbox_accounting_pnl_preview $true "Allowed sandbox accounting preview ready output missing."
Assert-Equal $main.ready_outputs.ledger_dry_run_preview_no_commit $true "Allowed ledger dry-run preview ready output missing."
Assert-Equal $main.forbidden_ready_labels.real_broker_statement_reconciliation $false "Real broker reconciliation must not be ready."
Assert-Equal $main.forbidden_ready_labels.broker_api_statement_fetch $false "Broker API statement fetch must not be ready."
Assert-Equal $main.forbidden_ready_labels.realized_accounting_close $false "Realized accounting close must not be ready."
Assert-Equal $main.forbidden_ready_labels.broker_confirmed_pnl $false "Broker-confirmed PnL must not be ready."
Assert-Equal $main.forbidden_ready_labels.committed_ledger $false "Committed ledger must not be ready."
Assert-Equal $main.forbidden_ready_labels.ledger_commit $false "Ledger commit must not be ready."
Assert-Equal $main.forbidden_ready_labels.db_mutation $false "DB mutation must not be ready."
Assert-Equal $main.forbidden_ready_labels.production_live $false "Production/live must not be ready."
Assert-Equal $main.forbidden_ready_labels.trading_readiness $false "Trading readiness must not be ready."

Assert-Equal $main.global_guards.external_calls $false "External calls must remain false."
Assert-Equal $main.global_guards.broker_api_calls $false "Broker API calls must remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.global_guards.trading_activity $false "Trading activity must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness ready must remain false."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-sandbox-broker-accounting-reconciliation-r001.ps1")
) + @(Get-ChildItem -LiteralPath $artifactDir -File -Recurse | ForEach-Object { $_.FullName })
$forbiddenPatterns = @(
    "SubmitOrder",
    "SubmitOrderUnmanaged",
    "AtmStrategyCreate",
    '"external_calls": true',
    '"broker_api_calls": true',
    '"market_data_fetch": true',
    '"ledger_commit": true',
    '"db_mutation": true',
    '"trading_activity": true',
    '"production_live_ready": true',
    '"trading_readiness_ready": true',
    '"real_broker_statement_reconciliation_ready": true',
    '"realized_accounting_close_ready": true',
    '"broker_confirmed_pnl": true',
    '"ledger_committed": true',
    '"commit_allowed": true',
    '"commit_eligible": true',
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

Write-Host "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001_GATE_PASS"

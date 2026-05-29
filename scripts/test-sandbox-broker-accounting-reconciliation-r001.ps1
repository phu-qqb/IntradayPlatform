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

$builder = Join-Path $RepoRoot "scripts\build-sandbox-broker-accounting-reconciliation-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-sandbox-broker-accounting-reconciliation-r001-gate.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001"
$main = Read-JsonFile (Join-Path $artifactDir "sandbox-broker-accounting-reconciliation-r001.json")
$fixture = Read-JsonFile (Join-Path $artifactDir "sandbox-broker-statement-fixture-r001.json")
$brokerPolicy = Read-JsonFile (Join-Path $artifactDir "broker-reconciliation-policy-r001.json")
$accountingPolicy = Read-JsonFile (Join-Path $artifactDir "accounting-pnl-policy-r001.json")
$ledgerPolicy = Read-JsonFile (Join-Path $artifactDir "ledger-dry-run-policy-r001.json")

Assert-Equal $main.status "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001" "Main status should be ready."
Assert-Equal $fixture.source "checked_in_fixture" "Fixture source should be checked-in fixture."
Assert-Equal $fixture.external_fetch $false "Fixture external fetch should be false."
Assert-Equal $brokerPolicy.broker_statement_reconciliation_ready $true "Sandbox broker reconciliation preview policy should be ready."
Assert-Equal $brokerPolicy.real_broker_statement_reconciliation_ready $false "Real broker reconciliation should remain false."
Assert-Equal $accountingPolicy.sandbox_accounting_pnl_preview_ready $true "Sandbox accounting PnL preview policy should be ready."
Assert-Equal $accountingPolicy.realized_accounting_close_ready $false "Realized accounting close should remain false."
Assert-Equal $ledgerPolicy.commit_allowed $false "Ledger commit should not be allowed."

Assert-DecimalEqual ([decimal]$main.broker_reconciliation_preview.gross_pnl_usd) ([decimal]-50.308800) "Broker preview gross mismatch."
Assert-DecimalEqual ([decimal]$main.broker_reconciliation_preview.commission_usd) ([decimal]26.268029) "Broker preview commission mismatch."
Assert-DecimalEqual ([decimal]$main.broker_reconciliation_preview.net_pnl_usd) ([decimal]-76.576829) "Broker preview net mismatch."
Assert-Equal $main.broker_reconciliation_preview.reconciled_to_sandbox_closeout $true "Broker preview should reconcile to closeout."
Assert-Equal $main.broker_reconciliation_preview.reconciled_to_statement_fixture $true "Broker preview should reconcile to fixture."
Assert-Equal @($main.broker_reconciliation_preview.unmatched_items).Count 0 "Unmatched items should be empty."

Assert-Equal $main.sandbox_accounting_pnl_preview.ready $true "Sandbox accounting PnL preview should be ready."
Assert-DecimalEqual ([decimal]$main.sandbox_accounting_pnl_preview.gross_pnl_usd) ([decimal]-50.308800) "Accounting preview gross mismatch."
Assert-DecimalEqual ([decimal]$main.sandbox_accounting_pnl_preview.commission_expense_usd) ([decimal]26.268029) "Accounting preview commission mismatch."
Assert-DecimalEqual ([decimal]$main.sandbox_accounting_pnl_preview.net_pnl_usd) ([decimal]-76.576829) "Accounting preview net mismatch."

Assert-Equal $main.ledger_dry_run_preview.ready $true "Ledger dry-run preview should be ready."
Assert-Equal $main.ledger_dry_run_preview.commit_allowed $false "Ledger dry-run commit allowed should be false."
Assert-Equal $main.ledger_dry_run_preview.commit_eligible_entries 0 "Ledger dry-run commit eligible entries should be zero."
Assert-Equal $main.ledger_dry_run_preview.ledger_commit_ready $false "Ledger commit ready should be false."
Assert-Equal $main.ledger_dry_run_preview.db_mutation_ready $false "DB mutation ready should be false."

Assert-True (@($main.exclusion_evidence.excluded_unfilled | Where-Object { $_.symbol -eq "USDJPY" -and [decimal]$_.quantity -eq [decimal]50.0 -and [decimal]$_.pnl_impact_usd -eq [decimal]0.0 }).Count -eq 1) "USDJPY 50.0 exclusion should be preserved."
foreach ($symbol in @("AUDUSD", "CHFUSD", "EURUSD", "GBPUSD")) {
    Assert-True (@($main.exclusion_evidence.excluded_zero_quantity | Where-Object { $_.symbol -eq $symbol -and [decimal]$_.pnl_impact_usd -eq [decimal]0.0 }).Count -eq 1) "Zero-quantity exclusion should be preserved: $symbol"
}
Assert-Equal $main.exclusion_evidence.excluded_reintroduced $false "Excluded lines should not be reintroduced."

Assert-Equal $main.global_guards.external_calls $false "External calls should be false."
Assert-Equal $main.global_guards.broker_api_calls $false "Broker API calls should be false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch should be false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation should be false."
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit should be false."
Assert-Equal $main.global_guards.trading_activity $false "Trading activity should be false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live should remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness should remain false."

foreach ($label in $main.forbidden_ready_labels.PSObject.Properties) {
    Assert-Equal $label.Value $false "Forbidden label must remain false: $($label.Name)"
}

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

Write-Host "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001_TESTS_PASS"

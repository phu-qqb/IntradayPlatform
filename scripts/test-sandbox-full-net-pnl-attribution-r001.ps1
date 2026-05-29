param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Assert-DecimalEqual([decimal]$Actual, [decimal]$Expected, [string]$Message) {
    if ([Math]::Abs($Actual - $Expected) -gt [decimal]0.000001) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-TestReadinessStatus(
    [bool]$R003Exists,
    [bool]$AggregationExists,
    [bool]$ConfirmationExists,
    [string]$ConfirmationStatus,
    [bool]$PreviewReady,
    [bool]$ExternalCalls,
    [bool]$DbMutation,
    [bool]$LedgerCommit,
    [bool]$AccountingReady,
    [bool]$ProductionReady
) {
    if (-not $R003Exists) { return "BLOCKED_SOURCE_R003_MISSING" }
    if (-not $AggregationExists) { return "BLOCKED_SOURCE_ACCOUNT_CURRENCY_AGGREGATION_MISSING" }
    if (-not $ConfirmationExists) { return "BLOCKED_SOURCE_COMMISSION_CONFIRMATION_MISSING" }
    if ($ConfirmationStatus -ne "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001") { return "BLOCKED_COMMISSION_NOT_CONFIRMED" }
    if ($PreviewReady -ne $true) { return "BLOCKED_SANDBOX_FULL_NET_PNL_PREVIEW_NOT_READY" }
    if ($ExternalCalls -eq $true) { return "BLOCKED_EXTERNAL_CALL_FLAG_DETECTED" }
    if ($DbMutation -eq $true) { return "BLOCKED_DB_MUTATION_FLAG_DETECTED" }
    if ($LedgerCommit -eq $true) { return "BLOCKED_LEDGER_COMMIT_FLAG_DETECTED" }
    if ($AccountingReady -eq $true) { return "BLOCKED_ACCOUNTING_PNL_LABEL_DETECTED" }
    if ($ProductionReady -eq $true) { return "BLOCKED_PRODUCTION_LIVE_LABEL_DETECTED" }
    return "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001"
}

$builder = Join-Path $RepoRoot "scripts\build-sandbox-full-net-pnl-attribution-r001.ps1"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-full-net-pnl-attribution-r001"
$output = Read-JsonFile (Join-Path $artifactDir "sandbox-full-net-pnl-attribution-output.json")
$source = Read-JsonFile (Join-Path $artifactDir "source-validation.json")
$labelGuard = Read-JsonFile (Join-Path $artifactDir "label-guard.json")
$boundary = Read-JsonFile (Join-Path $artifactDir "boundary-safety-evidence.json")

Assert-Equal (Get-TestReadinessStatus $false $true $true "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001" $true $false $false $false $false $false) "BLOCKED_SOURCE_R003_MISSING" "Missing R003 source must block."
Assert-Equal (Get-TestReadinessStatus $true $false $true "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001" $true $false $false $false $false $false) "BLOCKED_SOURCE_ACCOUNT_CURRENCY_AGGREGATION_MISSING" "Missing account-currency aggregation source must block."
Assert-Equal (Get-TestReadinessStatus $true $true $false "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001" $true $false $false $false $false $false) "BLOCKED_SOURCE_COMMISSION_CONFIRMATION_MISSING" "Missing commission confirmation source must block."
Assert-Equal (Get-TestReadinessStatus $true $true $true "BLOCKED_APPROVAL_MISSING" $true $false $false $false $false $false) "BLOCKED_COMMISSION_NOT_CONFIRMED" "Unconfirmed commission must block."
Assert-Equal (Get-TestReadinessStatus $true $true $true "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001" $false $false $false $false $false $false) "BLOCKED_SANDBOX_FULL_NET_PNL_PREVIEW_NOT_READY" "Missing sandbox full net PnL preview must block."

Assert-Equal $source.status "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001" "Source validation status should be ready."
Assert-Equal $output.status "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001" "Output status should be ready."
Assert-Equal $output.ready_outputs.sandbox_full_net_pnl_attribution_preview $true "Only sandbox attribution preview should be ready."
Assert-Equal @($output.symbol_attribution).Count 9 "Symbol-level attribution should include the 9 included R013D symbols."
Assert-True (@($output.symbol_attribution | Where-Object { $_.symbol -eq "USDCAD" }).Count -eq 1) "USDCAD attribution should exist."
Assert-True (@($output.symbol_attribution | Where-Object { $_.symbol -eq "USDZAR" }).Count -eq 1) "USDZAR attribution should exist."
Assert-True (@($output.currency_attribution).Count -ge 1) "Currency-level attribution should be produced."

$unfilled = @($output.exclusion_attribution.excluded_unfilled | Where-Object { $_.symbol -eq "USDJPY" -and [decimal]$_.quantity -eq [decimal]50.0 -and $_.reason -eq "unfilled" })
Assert-Equal $unfilled.Count 1 "Unfilled USDJPY 50.0 must be preserved in exclusion attribution."
Assert-Equal $output.exclusion_attribution.excluded_zero_quantity_count 4 "Zero-quantity lines must remain excluded."
Assert-Equal $output.exclusion_attribution.excluded_reintroduced $false "Excluded lines must not be reintroduced into PnL."
Assert-True (@($output.symbol_attribution | Where-Object { $_.symbol -eq "AUDUSD" -or $_.symbol -eq "CHFUSD" -or $_.symbol -eq "EURUSD" -or $_.symbol -eq "GBPUSD" }).Count -eq 0) "Zero-quantity lines must not appear in included symbol attribution."

Assert-DecimalEqual ([decimal]$output.attribution_bridge.gross_pnl_usd) ([decimal]-50.308800) "Gross USD must reconcile."
Assert-DecimalEqual ([decimal]$output.attribution_bridge.commission_usd) ([decimal]26.268029) "Commission USD must reconcile."
Assert-DecimalEqual ([decimal]$output.attribution_bridge.net_pnl_usd) ([decimal]-76.576829) "Net USD must reconcile."
Assert-Equal $output.attribution_bridge.formula "net_pnl_usd = gross_pnl_usd - commission_usd" "Formula must be explicit."
Assert-Equal $output.attribution_bridge.reconciled $true "Bridge must be reconciled."
Assert-True ([decimal]$output.attribution_bridge.tolerance -le [decimal]0.000001) "Tolerance must be no wider than 0.000001."
Assert-DecimalEqual (([decimal]$output.attribution_bridge.gross_pnl_usd) - ([decimal]$output.attribution_bridge.commission_usd)) ([decimal]$output.attribution_bridge.net_pnl_usd) "Formula must reconcile gross - commission to net."

Assert-Equal $output.external_calls $false "external_calls must remain false."
Assert-Equal $output.db_mutation $false "db_mutation must remain false."
Assert-Equal $output.ledger_commit $false "ledger_commit must remain false."
Assert-Equal $output.trading_activity $false "trading_activity must remain false."
Assert-Equal $output.accounting_pnl_ready $false "accounting_pnl_ready must remain false."
Assert-Equal $output.broker_statement_reconciliation_ready $false "broker_statement_reconciliation_ready must remain false."
Assert-Equal $output.production_live_ready $false "production_live_ready must remain false."
Assert-Equal $boundary.no_lmax_fix_api_call $true "No LMAX FIX/API call must be recorded."
Assert-Equal $boundary.no_polygon_massive_call $true "No Polygon/Massive call must be recorded."
Assert-Equal $boundary.no_broker_api_call $true "No broker API call must be recorded."
Assert-Equal $boundary.no_market_data_fetch $true "No market-data fetch must be recorded."

Assert-Equal $labelGuard.status "LABEL_GUARD_PASS" "Label guard should pass."
Assert-Equal $labelGuard.accounting_pnl_ready $false "Forbidden accounting ready label must be false."
Assert-Equal $labelGuard.realized_pnl_ready $false "Forbidden realized ready label must be false."
Assert-Equal $labelGuard.broker_confirmed_pnl_ready $false "Forbidden broker-confirmed ready label must be false."
Assert-Equal $labelGuard.ledger_pnl_ready $false "Forbidden ledger PnL ready label must be false."
Assert-Equal $labelGuard.production_pnl_ready $false "Forbidden production PnL ready label must be false."
Assert-Equal $labelGuard.live_pnl_ready $false "Forbidden live PnL ready label must be false."
Assert-Equal $labelGuard.trading_ready $false "Forbidden trading ready label must be false."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-sandbox-full-net-pnl-attribution-r001.ps1"),
    (Join-Path $RepoRoot "scripts\check-sandbox-full-net-pnl-attribution-r001-gate.ps1")
) + @(Get-ChildItem -LiteralPath $artifactDir -File -Recurse | ForEach-Object { $_.FullName })
$forbiddenPatterns = @(
    "SubmitOrder",
    "SubmitOrderUnmanaged",
    "AtmStrategyCreate",
    '"external_calls": true',
    '"db_mutation": true',
    '"ledger_commit": true',
    '"trading_activity": true',
    '"accounting_pnl_ready": true',
    '"production_live_ready": true',
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

Write-Host "SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001_TESTS_PASS"

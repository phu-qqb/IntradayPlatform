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

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001"
$sourcePaths = @(
    Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r003-lmax-public-charges-import\r013d-cost-adjusted-sandbox-preview.json"
    Join-Path $RepoRoot "artifacts\readiness\account-currency-aggregation-r001\account-currency-aggregation-preview.json"
    Join-Path $RepoRoot "artifacts\readiness\account-specific-commission-confirmation-r001\account-specific-commission-confirmation-output.json"
    Join-Path $RepoRoot "artifacts\readiness\sandbox-full-net-pnl-attribution-r001\sandbox-full-net-pnl-attribution-output.json"
)
$requiredOutput = @(
    "sandbox-preview-closeout-r001.json",
    "sandbox-preview-evidence-manifest-r001.json",
    "sandbox-preview-blocked-state-certificate-r001.json",
    "sandbox-preview-closeout-summary-r001.md"
)

foreach ($path in $sourcePaths) {
    Assert-True (Test-Path -LiteralPath $path) "Required prior source artifact missing: $path"
}
foreach ($name in $requiredOutput) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactDir $name)) "Required closeout artifact missing: $name"
}

$closeout = Read-JsonFile (Join-Path $artifactDir "sandbox-preview-closeout-r001.json")
$manifest = Read-JsonFile (Join-Path $artifactDir "sandbox-preview-evidence-manifest-r001.json")
$blocked = Read-JsonFile (Join-Path $artifactDir "sandbox-preview-blocked-state-certificate-r001.json")
$attribution = Read-JsonFile (Join-Path $RepoRoot "artifacts\readiness\sandbox-full-net-pnl-attribution-r001\sandbox-full-net-pnl-attribution-output.json")

Assert-Equal $attribution.status "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001" "Attribution source must be ready."
Assert-Equal $closeout.package "NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001" "Closeout package mismatch."
Assert-Equal $closeout.status "SANDBOX_PREVIEW_CLOSEOUT_READY_R001" "Closeout status mismatch."
Assert-Equal $closeout.environment "sandbox" "Closeout environment must be sandbox."
Assert-Equal $closeout.mode "preview_only" "Closeout mode must be preview_only."
Assert-Equal $closeout.account_currency "USD" "Account currency must be USD."
Assert-DecimalEqual ([decimal]$closeout.gross_pnl_usd) ([decimal]-50.308800) "Gross USD mismatch."
Assert-DecimalEqual ([decimal]$closeout.commission_usd) ([decimal]26.268029) "Commission USD mismatch."
Assert-DecimalEqual ([decimal]$closeout.net_pnl_usd) ([decimal]-76.576829) "Net USD mismatch."
Assert-Equal $closeout.reconciled $true "Closeout must be reconciled."
Assert-True ([decimal]$closeout.tolerance -le [decimal]0.000001) "Tolerance must be no wider than 0.000001."
Assert-DecimalEqual (([decimal]$closeout.gross_pnl_usd) - ([decimal]$closeout.commission_usd)) ([decimal]$closeout.net_pnl_usd) "Formula net = gross - commission must hold."

Assert-True (@($manifest.source_artifacts).Count -eq 4) "Manifest must link the four prior source packages."
foreach ($source in @($manifest.source_artifacts)) {
    Assert-True ((-not [string]::IsNullOrWhiteSpace($source.artifact_path)) -and (Test-Path -LiteralPath $source.artifact_path)) "Manifest source artifact path must exist."
    Assert-True ($source.sha256 -match "^sha256:[A-F0-9]{64}$") "Manifest source artifact hash must be sha256."
    Assert-True (-not [string]::IsNullOrWhiteSpace($source.package_status)) "Manifest source package status must be present."
    Assert-True (-not [string]::IsNullOrWhiteSpace($source.accepted_result)) "Manifest source accepted result must be present."
    Assert-True ($null -ne $source.key_values_consumed) "Manifest source key values consumed must be present."
}
Assert-Equal $manifest.source_summary.attribution_status "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001" "Manifest attribution status mismatch."

$entries = @($closeout.paper_ledger_shaped_preview_entries)
Assert-Equal $entries.Count 3 "Paper-ledger-shaped preview must contain exactly 3 entries."
Assert-True (@($entries | Where-Object { $_.entry_type -eq "gross_pnl_preview" -and [decimal]$_.amount -eq [decimal]-50.308800 }).Count -eq 1) "Gross paper-ledger preview entry missing."
Assert-True (@($entries | Where-Object { $_.entry_type -eq "commission_preview" -and [decimal]$_.signed_impact -eq [decimal]-26.268029 }).Count -eq 1) "Commission signed-impact preview entry missing."
Assert-True (@($entries | Where-Object { $_.entry_type -eq "net_pnl_preview" -and [decimal]$_.amount -eq [decimal]-76.576829 }).Count -eq 1) "Net paper-ledger preview entry missing."
foreach ($entry in $entries) {
    Assert-Equal $entry.environment "sandbox" "Paper-ledger entry environment must be sandbox."
    Assert-Equal $entry.mode "preview_only" "Paper-ledger entry mode must be preview_only."
    Assert-Equal $entry.commit_eligible $false "No paper-ledger preview entry may be commit eligible."
    Assert-Equal $entry.commit_status "NO_COMMIT_PREVIEW_ONLY" "Paper-ledger entry commit status mismatch."
    Assert-Equal $entry.committed_at_utc $null "Paper-ledger entry committed_at_utc must be null."
}
Assert-Equal $closeout.no_commit_confirmation.ledger_commit $false "No-commit confirmation ledger_commit must be false."
Assert-Equal $closeout.no_commit_confirmation.all_preview_entries_commit_eligible $false "No preview entry may be commit eligible."
Assert-Equal $closeout.no_commit_confirmation.committed_at_utc_present $false "No committed_at_utc may be present."

Assert-True (@($closeout.exclusion_evidence.excluded_unfilled | Where-Object { $_.symbol -eq "USDJPY" -and [decimal]$_.quantity -eq [decimal]50.0 }).Count -eq 1) "Unfilled USDJPY 50.0 must remain excluded."
Assert-Equal $closeout.exclusion_evidence.excluded_zero_quantity_count 4 "Four zero-quantity exclusions must remain."
foreach ($symbol in @("AUDUSD", "CHFUSD", "EURUSD", "GBPUSD")) {
    Assert-True ($closeout.exclusion_evidence.excluded_zero_quantity_symbols -contains $symbol) "Zero-quantity exclusion missing: $symbol"
}
Assert-Equal $closeout.exclusion_evidence.excluded_reintroduced $false "Excluded lines must not be reintroduced."
Assert-Equal $closeout.exclusion_evidence.excluded_lines_contribute_to_pnl $false "Excluded lines must not contribute to PnL."

Assert-Equal $blocked.accounting_pnl_ready $false "Accounting PnL must remain blocked."
Assert-Equal $blocked.realized_accounting_pnl_ready $false "Realized accounting PnL must remain blocked."
Assert-Equal $blocked.broker_statement_reconciliation_ready $false "Broker statement reconciliation must remain blocked."
Assert-Equal $blocked.ledger_commit_ready $false "Ledger commit readiness must remain blocked."
Assert-Equal $blocked.db_mutation_allowed $false "DB mutation must remain blocked."
Assert-Equal $blocked.production_live_ready $false "Production/live must remain blocked."
Assert-Equal $blocked.trading_readiness_ready $false "Trading readiness must remain blocked."
Assert-Equal $blocked.ledger_commit $false "Ledger commit must be false."
Assert-Equal $blocked.db_mutation $false "DB mutation must be false."
Assert-Equal $blocked.external_calls $false "External calls must be false."
Assert-Equal $blocked.trading_activity $false "Trading activity must be false."

Assert-Equal $closeout.blocked_state_certificate.accounting_pnl_ready $false "Closeout accounting PnL certificate mismatch."
Assert-Equal $closeout.blocked_state_certificate.realized_accounting_pnl_ready $false "Closeout realized accounting PnL certificate mismatch."
Assert-Equal $closeout.blocked_state_certificate.broker_statement_reconciliation_ready $false "Closeout broker reconciliation certificate mismatch."
Assert-Equal $closeout.blocked_state_certificate.ledger_commit_ready $false "Closeout ledger commit certificate mismatch."
Assert-Equal $closeout.blocked_state_certificate.db_mutation_allowed $false "Closeout DB mutation certificate mismatch."
Assert-Equal $closeout.blocked_state_certificate.production_live_ready $false "Closeout production/live certificate mismatch."
Assert-Equal $closeout.blocked_state_certificate.trading_readiness_ready $false "Closeout trading readiness certificate mismatch."

Assert-Equal $closeout.non_event_confirmation.no_trades $true "No trades must be confirmed."
Assert-Equal $closeout.non_event_confirmation.no_r009_submission $true "No R009 submission must be confirmed."
Assert-Equal $closeout.non_event_confirmation.no_lmax_fix_api_call $true "No LMAX FIX/API call must be confirmed."
Assert-Equal $closeout.non_event_confirmation.no_polygon_massive_call $true "No Polygon/Massive call must be confirmed."
Assert-Equal $closeout.non_event_confirmation.no_broker_api_call $true "No broker API call must be confirmed."
Assert-Equal $closeout.non_event_confirmation.no_market_data_fetch $true "No market-data fetch must be confirmed."
Assert-Equal $closeout.non_event_confirmation.no_account_data_fetch $true "No account-data fetch must be confirmed."
Assert-Equal $closeout.non_event_confirmation.no_order_fill_report_creation_from_live_sources $true "No live order/fill/report creation must be confirmed."
Assert-Equal $closeout.non_event_confirmation.no_db_mutation $true "No DB mutation must be confirmed."
Assert-Equal $closeout.non_event_confirmation.no_ledger_commit $true "No ledger commit must be confirmed."
Assert-Equal $closeout.non_event_confirmation.no_production_live_activity $true "No production/live activity must be confirmed."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-sandbox-preview-closeout-r001.ps1")
) + @(Get-ChildItem -LiteralPath $artifactDir -File -Recurse | ForEach-Object { $_.FullName })
$forbiddenPatterns = @(
    "SubmitOrder",
    "SubmitOrderUnmanaged",
    "AtmStrategyCreate",
    '"ledger_commit": true',
    '"db_mutation": true',
    '"external_calls": true',
    '"trading_activity": true',
    '"accounting_pnl_ready": true',
    '"realized_accounting_pnl_ready": true',
    '"broker_statement_reconciliation_ready": true',
    '"production_live_ready": true',
    '"trading_readiness_ready": true',
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

Write-Host "SANDBOX_PREVIEW_CLOSEOUT_R001_GATE_PASS"

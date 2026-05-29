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

$builder = Join-Path $RepoRoot "scripts\build-sandbox-preview-closeout-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-sandbox-preview-closeout-r001-gate.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001"
$closeout = Read-JsonFile (Join-Path $artifactDir "sandbox-preview-closeout-r001.json")
$manifest = Read-JsonFile (Join-Path $artifactDir "sandbox-preview-evidence-manifest-r001.json")
$blocked = Read-JsonFile (Join-Path $artifactDir "sandbox-preview-blocked-state-certificate-r001.json")

Assert-Equal $closeout.status "SANDBOX_PREVIEW_CLOSEOUT_READY_R001" "Closeout should be ready."
Assert-DecimalEqual ([decimal]$closeout.gross_pnl_usd) ([decimal]-50.308800) "Gross USD should match final preview."
Assert-DecimalEqual ([decimal]$closeout.commission_usd) ([decimal]26.268029) "Commission USD should match final preview."
Assert-DecimalEqual ([decimal]$closeout.net_pnl_usd) ([decimal]-76.576829) "Net USD should match final preview."
Assert-Equal $closeout.reconciled $true "Closeout should be reconciled."
Assert-True ([decimal]$closeout.tolerance -le [decimal]0.000001) "Tolerance should be no wider than 0.000001."
Assert-Equal @($closeout.paper_ledger_shaped_preview_entries).Count 3 "Paper-ledger preview should have three entries."
foreach ($entry in @($closeout.paper_ledger_shaped_preview_entries)) {
    Assert-Equal $entry.commit_eligible $false "Paper-ledger entry should not be commit eligible."
    Assert-Equal $entry.commit_status "NO_COMMIT_PREVIEW_ONLY" "Paper-ledger entry should be preview-only."
    Assert-Equal $entry.committed_at_utc $null "Paper-ledger entry should not have committed_at_utc."
}
Assert-True (@($closeout.exclusion_evidence.excluded_unfilled | Where-Object { $_.symbol -eq "USDJPY" -and [decimal]$_.quantity -eq [decimal]50.0 }).Count -eq 1) "Unfilled USDJPY 50.0 should remain excluded."
foreach ($symbol in @("AUDUSD", "CHFUSD", "EURUSD", "GBPUSD")) {
    Assert-True ($closeout.exclusion_evidence.excluded_zero_quantity_symbols -contains $symbol) "Zero-quantity symbol should remain excluded: $symbol"
}
Assert-Equal $closeout.exclusion_evidence.excluded_lines_contribute_to_pnl $false "Excluded lines should not contribute to PnL."

Assert-Equal @($manifest.source_artifacts).Count 4 "Manifest should include the four source packages."
foreach ($source in @($manifest.source_artifacts)) {
    Assert-True ($source.sha256 -match "^sha256:[A-F0-9]{64}$") "Source hash should be captured."
    Assert-True (-not [string]::IsNullOrWhiteSpace($source.package_status)) "Source status should be captured."
}

Assert-Equal $blocked.accounting_pnl_ready $false "Accounting PnL should remain blocked."
Assert-Equal $blocked.realized_accounting_pnl_ready $false "Realized accounting PnL should remain blocked."
Assert-Equal $blocked.broker_statement_reconciliation_ready $false "Broker statement reconciliation should remain blocked."
Assert-Equal $blocked.ledger_commit_ready $false "Ledger commit readiness should remain blocked."
Assert-Equal $blocked.db_mutation_allowed $false "DB mutation should remain blocked."
Assert-Equal $blocked.production_live_ready $false "Production/live should remain blocked."
Assert-Equal $blocked.trading_readiness_ready $false "Trading readiness should remain blocked."
Assert-Equal $blocked.ledger_commit $false "Ledger commit should be false."
Assert-Equal $blocked.db_mutation $false "DB mutation should be false."
Assert-Equal $blocked.external_calls $false "External calls should be false."
Assert-Equal $blocked.trading_activity $false "Trading activity should be false."

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

Write-Host "SANDBOX_PREVIEW_CLOSEOUT_R001_TESTS_PASS"

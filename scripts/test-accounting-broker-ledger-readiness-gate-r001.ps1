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

$builder = Join-Path $RepoRoot "scripts\build-accounting-broker-ledger-readiness-gate-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-accounting-broker-ledger-readiness-gate-r001.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\accounting-broker-ledger-readiness-gate-r001"
$main = Read-JsonFile (Join-Path $artifactDir "accounting-broker-ledger-readiness-gate-r001.json")
$broker = Read-JsonFile (Join-Path $artifactDir "broker-reconciliation-requirements-r001.json")
$accounting = Read-JsonFile (Join-Path $artifactDir "accounting-pnl-requirements-r001.json")
$ledger = Read-JsonFile (Join-Path $artifactDir "ledger-commit-requirements-r001.json")
$production = Read-JsonFile (Join-Path $artifactDir "production-live-readiness-blockers-r001.json")

Assert-Equal $main.status "ACCOUNTING_BROKER_LEDGER_READINESS_GATE_BLOCKED_R001" "Macro gate must remain blocked."
Assert-Equal $main.source_status "SANDBOX_PREVIEW_CLOSEOUT_READY_R001" "Source closeout status mismatch."
Assert-DecimalEqual ([decimal]$main.sandbox_preview_values.gross_usd) ([decimal]-50.308800) "Gross USD mismatch."
Assert-DecimalEqual ([decimal]$main.sandbox_preview_values.commission_usd) ([decimal]26.268029) "Commission USD mismatch."
Assert-DecimalEqual ([decimal]$main.sandbox_preview_values.net_usd) ([decimal]-76.576829) "Net USD mismatch."
Assert-Equal $main.sandbox_preview_values.reconciled $true "Reconciliation should remain true."
Assert-Equal $main.sandbox_preview_values.paper_ledger_preview_exists $true "Paper-ledger preview should exist."
Assert-Equal $main.sandbox_preview_values.paper_ledger_commit_eligible_entries 0 "No paper-ledger entries should be commit eligible."

foreach ($domainName in @("broker_statement_reconciliation", "accounting_pnl", "ledger_commit", "db_mutation", "production_live", "trading_readiness")) {
    $domain = $main.readiness_domains.$domainName
    Assert-Equal $domain.ready $false "Domain must remain blocked: $domainName"
    Assert-Equal $domain.status "BLOCKED" "Domain status must be BLOCKED: $domainName"
    Assert-True (@($domain.required_evidence).Count -gt 0) "Domain required evidence must be explicit: $domainName"
}

Assert-Equal $broker.broker_statement_reconciliation_ready $false "Broker reconciliation must remain blocked."
Assert-Equal $accounting.accounting_pnl_ready $false "Accounting PnL must remain blocked."
Assert-Equal $accounting.realized_accounting_pnl_ready $false "Realized accounting PnL must remain blocked."
Assert-Equal $ledger.ledger_commit_ready $false "Ledger commit must remain blocked."
Assert-Equal $ledger.db_mutation_ready $false "DB mutation must remain blocked."
Assert-Equal $production.production_live_ready $false "Production/live must remain blocked."
Assert-Equal $production.trading_readiness_ready $false "Trading readiness must remain blocked."

Assert-Equal $main.global_guards.ledger_commit $false "Global ledger commit guard must remain false."
Assert-Equal $main.global_guards.db_mutation $false "Global DB mutation guard must remain false."
Assert-Equal $main.global_guards.external_calls $false "Global external calls guard must remain false."
Assert-Equal $main.global_guards.trading_activity $false "Global trading activity guard must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Global production/live guard must remain false."
Assert-Equal $main.no_unblock_performed $true "Macro gate must not unblock anything."

$summaryPath = Join-Path $artifactDir "accounting-broker-ledger-readiness-summary-r001.md"
Assert-True (Test-Path -LiteralPath $summaryPath) "Markdown summary must exist."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-accounting-broker-ledger-readiness-gate-r001.ps1")
) + @(Get-ChildItem -LiteralPath $artifactDir -File -Recurse | ForEach-Object { $_.FullName })
$forbiddenPatterns = @(
    "SubmitOrder",
    "SubmitOrderUnmanaged",
    "AtmStrategyCreate",
    '"ready": true',
    '"ledger_commit": true',
    '"db_mutation": true',
    '"external_calls": true',
    '"trading_activity": true',
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

Write-Host "ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001_TESTS_PASS"

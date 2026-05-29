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

$sourceCloseoutPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-r001.json"
$sourceBlockedPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-blocked-state-certificate-r001.json"
$artifactDir = Join-Path $RepoRoot "artifacts\readiness\accounting-broker-ledger-readiness-gate-r001"

$requiredArtifacts = @(
    "accounting-broker-ledger-readiness-gate-r001.json",
    "broker-reconciliation-requirements-r001.json",
    "accounting-pnl-requirements-r001.json",
    "ledger-commit-requirements-r001.json",
    "production-live-readiness-blockers-r001.json",
    "accounting-broker-ledger-readiness-summary-r001.md"
)

Assert-True (Test-Path -LiteralPath $sourceCloseoutPath) "Sandbox closeout source artifact must exist."
Assert-True (Test-Path -LiteralPath $sourceBlockedPath) "Sandbox closeout blocked-state certificate must exist."
foreach ($name in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactDir $name)) "Required macro gate artifact missing: $name"
}

$sourceCloseout = Read-JsonFile $sourceCloseoutPath
$sourceBlocked = Read-JsonFile $sourceBlockedPath
$main = Read-JsonFile (Join-Path $artifactDir "accounting-broker-ledger-readiness-gate-r001.json")
$broker = Read-JsonFile (Join-Path $artifactDir "broker-reconciliation-requirements-r001.json")
$accounting = Read-JsonFile (Join-Path $artifactDir "accounting-pnl-requirements-r001.json")
$ledger = Read-JsonFile (Join-Path $artifactDir "ledger-commit-requirements-r001.json")
$production = Read-JsonFile (Join-Path $artifactDir "production-live-readiness-blockers-r001.json")

Assert-Equal $sourceCloseout.status "SANDBOX_PREVIEW_CLOSEOUT_READY_R001" "Source closeout status must be ready."
Assert-DecimalEqual ([decimal]$sourceCloseout.gross_pnl_usd) ([decimal]-50.308800) "Source gross USD mismatch."
Assert-DecimalEqual ([decimal]$sourceCloseout.commission_usd) ([decimal]26.268029) "Source commission USD mismatch."
Assert-DecimalEqual ([decimal]$sourceCloseout.net_pnl_usd) ([decimal]-76.576829) "Source net USD mismatch."
Assert-Equal $sourceCloseout.reconciled $true "Source reconciliation must be true."
Assert-True (@($sourceCloseout.paper_ledger_shaped_preview_entries).Count -gt 0) "Source paper-ledger preview must exist."
Assert-Equal @($sourceCloseout.paper_ledger_shaped_preview_entries | Where-Object { $_.commit_eligible -eq $true }).Count 0 "Source paper-ledger entries must not be commit eligible."

Assert-Equal $sourceBlocked.accounting_pnl_ready $false "Source accounting PnL must remain false."
Assert-Equal $sourceBlocked.realized_accounting_pnl_ready $false "Source realized accounting PnL must remain false."
Assert-Equal $sourceBlocked.broker_statement_reconciliation_ready $false "Source broker reconciliation must remain false."
Assert-Equal $sourceBlocked.ledger_commit_ready $false "Source ledger commit readiness must remain false."
Assert-Equal $sourceBlocked.db_mutation_allowed $false "Source DB mutation must remain false."
Assert-Equal $sourceBlocked.production_live_ready $false "Source production/live must remain false."
Assert-Equal $sourceBlocked.trading_readiness_ready $false "Source trading readiness must remain false."

Assert-Equal $main.package "NEXT_ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001" "Main package mismatch."
Assert-Equal $main.status "ACCOUNTING_BROKER_LEDGER_READINESS_GATE_BLOCKED_R001" "Main status must stay blocked."
Assert-Equal $main.environment "sandbox" "Environment must be sandbox."
Assert-Equal $main.mode "readiness_gate_only" "Mode must be readiness_gate_only."
Assert-Equal $main.source_package "NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001" "Source package mismatch."
Assert-Equal $main.source_status "SANDBOX_PREVIEW_CLOSEOUT_READY_R001" "Main source status mismatch."

foreach ($prop in @("sandbox_preview_closeout", "evidence_manifest", "blocked_state_certificate", "summary")) {
    Assert-True (-not [string]::IsNullOrWhiteSpace($main.source_artifacts.$prop)) "Source artifact path missing: $prop"
    Assert-True (Test-Path -LiteralPath $main.source_artifacts.$prop) "Source artifact path does not exist: $prop"
    Assert-True ($main.source_artifact_hashes.$prop -match "^sha256:[A-F0-9]{64}$") "Source artifact hash missing or invalid: $prop"
}

Assert-DecimalEqual ([decimal]$main.sandbox_preview_values.gross_usd) ([decimal]-50.308800) "Main gross USD mismatch."
Assert-DecimalEqual ([decimal]$main.sandbox_preview_values.commission_usd) ([decimal]26.268029) "Main commission USD mismatch."
Assert-DecimalEqual ([decimal]$main.sandbox_preview_values.net_usd) ([decimal]-76.576829) "Main net USD mismatch."
Assert-Equal $main.sandbox_preview_values.reconciled $true "Main source reconciliation must be true."
Assert-Equal $main.sandbox_preview_values.paper_ledger_preview_exists $true "Main must record paper-ledger preview exists."
Assert-Equal $main.sandbox_preview_values.paper_ledger_commit_eligible_entries 0 "Main must record zero commit-eligible preview entries."

foreach ($domainName in @("broker_statement_reconciliation", "accounting_pnl", "ledger_commit", "db_mutation", "production_live", "trading_readiness")) {
    $domain = $main.readiness_domains.$domainName
    Assert-Equal $domain.ready $false "Readiness domain must not be ready: $domainName"
    Assert-Equal $domain.status "BLOCKED" "Readiness domain must be BLOCKED: $domainName"
    Assert-True (@($domain.required_evidence).Count -gt 0) "Readiness domain must have explicit required evidence: $domainName"
}

Assert-Equal $main.global_guards.ledger_commit $false "Global ledger commit guard must be false."
Assert-Equal $main.global_guards.db_mutation $false "Global DB mutation guard must be false."
Assert-Equal $main.global_guards.external_calls $false "Global external calls guard must be false."
Assert-Equal $main.global_guards.trading_activity $false "Global trading activity guard must be false."
Assert-Equal $main.global_guards.production_live_ready $false "Global production/live ready guard must be false."

Assert-Equal $broker.broker_statement_reconciliation_ready $false "Broker reconciliation artifact must remain false."
Assert-Equal $broker.reason "requirements_defined_only_no_broker_statement_imported" "Broker reconciliation reason mismatch."
foreach ($required in @("broker statement source policy", "broker statement retrieval policy", "broker statement fixture or approved import artifact", "account identifier hash", "account currency", "statement period", "trade/fill identifier mapping policy", "commission mapping policy", "FX conversion mapping policy", "cash movement mapping policy", "fees/financing/swap mapping policy", "tolerance policy", "unmatched item policy", "reconciliation approval policy", "no-live-fetch test mode", "production fetch approval gate")) {
    Assert-True ($broker.required_evidence -contains $required) "Broker requirement missing: $required"
}

Assert-Equal $accounting.accounting_pnl_ready $false "Accounting PnL artifact must remain false."
Assert-Equal $accounting.realized_accounting_pnl_ready $false "Realized accounting PnL artifact must remain false."
Assert-Equal $accounting.reason "requirements_defined_only_no_accounting_policy_approved" "Accounting reason mismatch."
foreach ($required in @("accounting basis policy", "realized/unrealized classification policy", "trade date vs settlement date policy", "FX translation policy", "commission recognition policy", "financing/swap recognition policy", "rounding policy", "position lifecycle policy", "residual handling policy", "period close policy", "approval policy", "audit trail policy", "source-of-truth hierarchy")) {
    Assert-True ($accounting.required_evidence -contains $required) "Accounting requirement missing: $required"
}

Assert-Equal $ledger.ledger_commit_ready $false "Ledger commit artifact must remain false."
Assert-Equal $ledger.db_mutation_ready $false "Ledger DB mutation artifact must remain false."
Assert-Equal $ledger.reason "requirements_defined_only_no_commit_authorization" "Ledger reason mismatch."
foreach ($required in @("ledger schema approval", "account mapping approval", "journal entry model approval", "debit/credit convention approval", "idempotency key policy", "reversal policy", "correction policy", "commit authorization policy", "dry-run to commit promotion policy", "DB transaction policy", "audit log policy", "rollback policy", "segregation between preview and committed ledgers", "production table write approval", "operator approval policy")) {
    Assert-True ($ledger.required_evidence -contains $required) "Ledger requirement missing: $required"
}

Assert-Equal $production.production_live_ready $false "Production/live artifact must remain false."
Assert-Equal $production.trading_readiness_ready $false "Trading readiness artifact must remain false."
Assert-Equal $production.reason "sandbox_preview_closeout_only" "Production/live reason mismatch."
foreach ($required in @("no live credentials approved", "no production venue approved", "no live order routing approved", "no live market-data source approved", "no production risk limits approved", "no operator approval workflow for live", "no kill switch validation", "no post-trade reconciliation approval", "no ledger commit authorization", "no production monitoring approval", "no incident response runbook approval")) {
    Assert-True ($production.blockers -contains $required) "Production/live blocker missing: $required"
}

$summaryPath = Join-Path $artifactDir "accounting-broker-ledger-readiness-summary-r001.md"
Assert-True (Test-Path -LiteralPath $summaryPath) "Generated markdown summary must exist."

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

Write-Host "ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001_GATE_PASS"

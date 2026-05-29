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

$manualPath = Join-Path $RepoRoot "artifacts\readiness\manual-evidence-reconciliation-dry-run-r001\manual-evidence-reconciliation-dry-run-r001.json"
$controlledPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\controlled-real-evidence-import-r001.json"
$reconciliationPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001\sandbox-broker-accounting-reconciliation-r001.json"
$closeoutPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-r001.json"
$artifactDir = Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001"

$requiredArtifacts = @(
    "real-evidence-promotion-and-commit-readiness-gate-r001.json",
    "real-manual-evidence-acceptance-requirements-r001.json",
    "broker-confirmed-pnl-readiness-requirements-r001.json",
    "realized-accounting-close-readiness-requirements-r001.json",
    "ledger-db-commit-readiness-requirements-r001.json",
    "production-live-trading-readiness-requirements-r001.json",
    "real-evidence-promotion-and-commit-readiness-summary-r001.md"
)
foreach ($path in @($manualPath, $controlledPath, $reconciliationPath, $closeoutPath)) {
    Assert-True (Test-Path -LiteralPath $path) "Required source artifact missing: $path"
}
foreach ($name in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactDir $name)) "Required artifact missing: $name"
}

$manual = Read-JsonFile $manualPath
$controlled = Read-JsonFile $controlledPath
$reconciliation = Read-JsonFile $reconciliationPath
$closeout = Read-JsonFile $closeoutPath
$main = Read-JsonFile (Join-Path $artifactDir "real-evidence-promotion-and-commit-readiness-gate-r001.json")
$acceptance = Read-JsonFile (Join-Path $artifactDir "real-manual-evidence-acceptance-requirements-r001.json")
$brokerPnl = Read-JsonFile (Join-Path $artifactDir "broker-confirmed-pnl-readiness-requirements-r001.json")
$realizedClose = Read-JsonFile (Join-Path $artifactDir "realized-accounting-close-readiness-requirements-r001.json")
$commit = Read-JsonFile (Join-Path $artifactDir "ledger-db-commit-readiness-requirements-r001.json")
$production = Read-JsonFile (Join-Path $artifactDir "production-live-trading-readiness-requirements-r001.json")

Assert-Equal $manual.status "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001" "Manual dry-run source status mismatch."
Assert-Equal $controlled.status "CONTROLLED_REAL_EVIDENCE_IMPORT_READY_R001" "Controlled import source status mismatch."
Assert-Equal $reconciliation.status "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001" "Sandbox reconciliation source status mismatch."
Assert-Equal $closeout.status "SANDBOX_PREVIEW_CLOSEOUT_READY_R001" "Sandbox closeout source status mismatch."

Assert-DecimalEqual ([decimal]$main.source_values.gross_usd) ([decimal]-50.308800) "Gross USD mismatch."
Assert-DecimalEqual ([decimal]$main.source_values.commission_usd) ([decimal]26.268029) "Commission USD mismatch."
Assert-DecimalEqual ([decimal]$main.source_values.net_usd) ([decimal]-76.576829) "Net USD mismatch."
Assert-Equal $main.source_values.reconciled $true "Source reconciliation must be true."
Assert-True ([decimal]$main.source_values.tolerance -le [decimal]0.000001) "Tolerance must be no wider than 0.000001."

Assert-Equal $main.manual_dry_run_state.broker_imports_seen 2 "Broker imports seen mismatch."
Assert-Equal $main.manual_dry_run_state.broker_imports_accepted 1 "Broker imports accepted mismatch."
Assert-Equal $main.manual_dry_run_state.broker_imports_quarantined 1 "Broker imports quarantined mismatch."
Assert-Equal $main.manual_dry_run_state.accounting_imports_seen 2 "Accounting imports seen mismatch."
Assert-Equal $main.manual_dry_run_state.accounting_imports_accepted 1 "Accounting imports accepted mismatch."
Assert-Equal $main.manual_dry_run_state.accounting_imports_quarantined 1 "Accounting imports quarantined mismatch."
Assert-Equal $main.manual_dry_run_state.accepted_imports_are_sample_only $true "Accepted imports must be sample_only."
Assert-Equal $main.manual_dry_run_state.real_manual_broker_statement_present $false "Real manual broker statement must be absent."
Assert-Equal $main.manual_dry_run_state.real_manual_accounting_evidence_present $false "Real manual accounting evidence must be absent."

Assert-Equal $main.package "NEXT_REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001" "Main package mismatch."
Assert-Equal $main.status "REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_BLOCKED_R001" "Main status mismatch."
Assert-True (($main.blocked_reason -eq "NO_REAL_MANUAL_EVIDENCE_IMPORTED") -or ($main.blocked_reason -eq "ACCEPTED_IMPORTS_ARE_SAMPLE_ONLY")) "Blocked reason must be expected."
Assert-Equal $main.environment "sandbox" "Environment mismatch."
Assert-Equal $main.mode "promotion_readiness_gate_only" "Mode mismatch."
foreach ($hashName in @("manual_evidence_reconciliation_dry_run_r001", "controlled_real_evidence_import_r001", "sandbox_broker_accounting_reconciliation_r001", "sandbox_preview_closeout_r001")) {
    Assert-True ($main.source_artifact_hashes.$hashName -match "^sha256:[A-F0-9]{64}$") "Missing source hash: $hashName"
}

foreach ($domainName in @("real_manual_broker_statement_acceptance", "real_manual_accounting_evidence_acceptance", "broker_confirmed_pnl", "realized_accounting_close", "ledger_commit", "db_mutation", "production_live", "trading_readiness")) {
    $domain = $main.promotion_gates.$domainName
    Assert-Equal $domain.ready $false "Promotion domain must remain false: $domainName"
    Assert-True (-not [string]::IsNullOrWhiteSpace($domain.status)) "Promotion domain status missing: $domainName"
    Assert-True (@($domain.required_evidence).Count -gt 0) "Promotion domain required evidence missing: $domainName"
}

Assert-Equal $acceptance.real_manual_evidence_acceptance_ready $false "Real manual evidence acceptance must remain false."
Assert-Equal $acceptance.reason "requirements_defined_only_no_real_manual_evidence_imported" "Acceptance reason mismatch."
foreach ($required in @("sample_only must be false", "real_broker_statement must be true", "external_fetch must be false", "broker_api_call must be false", "source_file_sha256 required", "account_id_hash required", "statement period required", "no DB mutation", "no ledger commit", "no production/live flags")) {
    Assert-True ($acceptance.broker_statement_acceptance_requirements -contains $required) "Broker acceptance requirement missing: $required"
}
foreach ($required in @("sample_only must be false", "real_accounting_evidence must be true", "real_accounting_close must remain false until close approval exists", "source_file_sha256 required", "accounting_policy_version required", "accounting period required", "gross/commission/net required", "audit trail required", "no DB mutation", "no ledger commit", "no production/live flags")) {
    Assert-True ($acceptance.accounting_evidence_acceptance_requirements -contains $required) "Accounting acceptance requirement missing: $required"
}

Assert-Equal $brokerPnl.broker_confirmed_pnl_ready $false "Broker-confirmed PnL must remain false."
Assert-Equal $brokerPnl.reason "no_accepted_real_manual_broker_statement" "Broker-confirmed reason mismatch."
Assert-True (@($brokerPnl.required_evidence).Count -gt 0) "Broker-confirmed required evidence missing."

Assert-Equal $realizedClose.realized_accounting_close_ready $false "Realized accounting close must remain false."
Assert-Equal $realizedClose.reason "no_accepted_real_manual_accounting_evidence_or_close_approval" "Realized close reason mismatch."
Assert-True (@($realizedClose.required_evidence).Count -gt 0) "Realized close required evidence missing."

Assert-Equal $commit.ledger_commit_ready $false "Ledger commit must remain false."
Assert-Equal $commit.db_mutation_ready $false "DB mutation must remain false."
Assert-Equal $commit.reason "commit_requirements_defined_only_no_commit_authorization" "Commit reason mismatch."
Assert-True (@($commit.required_evidence).Count -gt 0) "Commit required evidence missing."

Assert-Equal $production.production_live_ready $false "Production/live must remain false."
Assert-Equal $production.trading_readiness_ready $false "Trading readiness must remain false."
Assert-Equal $production.reason "production_live_requirements_defined_only_no_live_approval" "Production reason mismatch."
Assert-True (@($production.required_evidence).Count -gt 0) "Production/trading required evidence missing."

foreach ($label in @("real_evidence_promotion_gate_defined", "commit_readiness_gate_defined", "production_live_readiness_gate_defined")) {
    Assert-Equal $main.ready_outputs.$label $true "Allowed ready label missing: $label"
}
foreach ($label in $main.forbidden_ready_labels.PSObject.Properties) {
    Assert-Equal $label.Value $false "Forbidden ready label must remain false: $($label.Name)"
}

Assert-Equal $main.global_guards.external_calls $false "External calls must remain false."
Assert-Equal $main.global_guards.broker_api_calls $false "Broker API calls must remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $main.global_guards.account_data_fetch $false "Account-data fetch must remain false."
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.global_guards.trading_activity $false "Trading activity must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness must remain false."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-real-evidence-promotion-and-commit-readiness-gate-r001.ps1")
)
$forbiddenPatterns = @(
    "SubmitOrder",
    "SubmitOrderUnmanaged",
    "AtmStrategyCreate",
    "Invoke-WebRequest",
    "Invoke-RestMethod",
    "curl ",
    "wget ",
    "api_key",
    "apikey",
    "password"
)
foreach ($pattern in $forbiddenPatterns) {
    $matches = Select-String -Path $scanFiles -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
    if ($matches) {
        throw "Forbidden call/secret/static pattern detected [$pattern]:`n$($matches | Out-String)"
    }
}

Write-Host "REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001_GATE_PASS"

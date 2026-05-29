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

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001"
$promotionPath = Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001\real-evidence-promotion-and-commit-readiness-gate-r001.json"
$manualPath = Join-Path $RepoRoot "artifacts\readiness\manual-evidence-reconciliation-dry-run-r001\manual-evidence-reconciliation-dry-run-r001.json"
$controlledPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\controlled-real-evidence-import-r001.json"
$reconciliationPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001\sandbox-broker-accounting-reconciliation-r001.json"
$closeoutPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-r001.json"

$requiredArtifacts = @(
    "real-manual-evidence-acceptance-r001.json",
    "real-manual-evidence-discovery-report-r001.json",
    "real-manual-evidence-staging-scan-r001.json",
    "real-manual-evidence-validation-report-r001.json",
    "real-manual-evidence-quarantine-preview-r001.json",
    "real-manual-evidence-acceptance-summary-r001.md"
)

foreach ($path in @($promotionPath, $manualPath, $controlledPath, $reconciliationPath, $closeoutPath)) {
    Assert-True (Test-Path -LiteralPath $path) "Required source artifact missing: $path"
}
foreach ($name in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactDir $name)) "Required artifact missing: $name"
}
foreach ($dir in @("staging\broker-statements", "staging\accounting-evidence", "staging\raw-lmax-broker-statement", "accepted", "rejected", "quarantine-preview")) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactDir $dir)) "Required local-only folder missing: $dir"
}

$promotion = Read-JsonFile $promotionPath
$manual = Read-JsonFile $manualPath
$controlled = Read-JsonFile $controlledPath
$reconciliation = Read-JsonFile $reconciliationPath
$closeout = Read-JsonFile $closeoutPath
$main = Read-JsonFile (Join-Path $artifactDir "real-manual-evidence-acceptance-r001.json")
$discovery = Read-JsonFile (Join-Path $artifactDir "real-manual-evidence-discovery-report-r001.json")
$scan = Read-JsonFile (Join-Path $artifactDir "real-manual-evidence-staging-scan-r001.json")
$validation = Read-JsonFile (Join-Path $artifactDir "real-manual-evidence-validation-report-r001.json")
$quarantine = Read-JsonFile (Join-Path $artifactDir "real-manual-evidence-quarantine-preview-r001.json")

Assert-Equal $promotion.status "REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_BLOCKED_R001" "Promotion gate source status mismatch."
Assert-Equal $manual.status "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001" "Manual dry-run source status mismatch."
Assert-Equal $controlled.status "CONTROLLED_REAL_EVIDENCE_IMPORT_READY_R001" "Controlled import source status mismatch."
Assert-Equal $reconciliation.status "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001" "Sandbox reconciliation source status mismatch."
Assert-Equal $closeout.status "SANDBOX_PREVIEW_CLOSEOUT_READY_R001" "Sandbox closeout source status mismatch."

Assert-Equal $main.package "NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001" "Main package mismatch."
Assert-True (@("REAL_MANUAL_EVIDENCE_ACCEPTANCE_BLOCKED_R001", "REAL_MANUAL_EVIDENCE_ACCEPTANCE_PARTIAL_READY_R001", "REAL_MANUAL_EVIDENCE_ACCEPTANCE_READY_R001") -contains $main.status) "Main status must be valid."
if ($main.status -eq "REAL_MANUAL_EVIDENCE_ACCEPTANCE_BLOCKED_R001") {
    Assert-True (@("NO_REAL_MANUAL_EVIDENCE_FILES_IN_STAGING", "REAL_MANUAL_EVIDENCE_FOUND_OUTSIDE_STAGING", "BLOCKED_ONLY_SAMPLE_EVIDENCE_FOUND", "BLOCKED_DISCOVERED_CANDIDATES_INVALID", "BLOCKED_RAW_LMAX_BROKER_BUNDLE_INCOMPLETE", "BLOCKED_RAW_LMAX_BROKER_BUNDLE_PARSE_FAILED") -contains $main.blocked_reason) "Blocked reason must be an allowed discovery/acceptance block."
}
Assert-Equal $main.environment "sandbox" "Environment mismatch."
Assert-Equal $main.mode "offline_manual_acceptance_gate_only" "Mode mismatch."
Assert-DecimalEqual ([decimal]$main.source_values.gross_usd) ([decimal]-50.308800) "Gross USD mismatch."
Assert-DecimalEqual ([decimal]$main.source_values.commission_usd) ([decimal]26.268029) "Commission USD mismatch."
Assert-DecimalEqual ([decimal]$main.source_values.net_usd) ([decimal]-76.576829) "Net USD mismatch."
Assert-True ([decimal]$main.source_values.tolerance -le [decimal]0.000001) "Tolerance must be no wider than 0.000001."

Assert-Equal $main.staging_scan.broker_statement_files_seen 0 "Default broker staging must be empty."
Assert-Equal $main.staging_scan.accounting_evidence_files_seen 0 "Default accounting staging must be empty."
Assert-Equal $main.staging_scan.real_broker_statement_files_seen 0 "Default real broker files must be zero."
Assert-Equal $main.staging_scan.real_accounting_evidence_files_seen 0 "Default real accounting files must be zero."
Assert-Equal $scan.broker_statement_files_seen 0 "Scan broker count mismatch."
Assert-Equal $scan.accounting_evidence_files_seen 0 "Scan accounting count mismatch."
Assert-Equal $scan.real_broker_statement_files_seen 0 "Scan real broker count mismatch."
Assert-Equal $scan.real_accounting_evidence_files_seen 0 "Scan real accounting count mismatch."
Assert-True ($discovery.discovered_candidate_files_count -ge 0) "Discovery report candidate count missing."
Assert-True ($discovery.sample_files_count -ge 0) "Discovery report sample count missing."
Assert-True ($discovery.real_non_sample_candidate_count -ge 0) "Discovery report real candidate count missing."
Assert-True ($discovery.discovered_outside_staging_count -ge 0) "Discovery report outside-staging count missing."
Assert-True ($discovery.staged_files_count -ge 0) "Discovery report staged count missing."
Assert-True ($discovery.period_mismatch_count -ge 0) "Discovery report period mismatch count missing."
Assert-True ($null -ne $discovery.raw_lmax_bundle) "Raw LMAX bundle discovery section missing."
Assert-Equal $discovery.external_calls $false "Discovery must not use external calls."
Assert-Equal $discovery.broker_api_calls $false "Discovery must not use broker API calls."
Assert-Equal $discovery.market_data_fetch $false "Discovery must not fetch market data."
Assert-Equal $discovery.account_data_fetch $false "Discovery must not fetch account data."
Assert-Equal $discovery.db_mutation $false "Discovery must not mutate DB."
Assert-Equal $discovery.ledger_commit $false "Discovery must not commit ledger."

if ($main.real_broker_evidence_lane.raw_lmax_bundle_seen -eq $true -and $main.real_broker_evidence_lane.raw_lmax_bundle_complete -eq $true) {
    Assert-Equal $main.readiness.real_manual_broker_statement_acceptance $true "Complete raw LMAX bundle should accept real broker statement evidence."
    Assert-True (@($main.accepted_real_evidence.broker_statements).Count -ge 1) "Accepted broker evidence should include raw LMAX normalization."
} else {
    Assert-Equal @($main.accepted_real_evidence.broker_statements).Count 0 "No real broker evidence should be accepted without a complete raw bundle or staged broker JSON."
}
Assert-Equal @($main.accepted_real_evidence.accounting_evidence).Count 0 "No real accounting evidence should be accepted by default."
Assert-Equal @($main.rejected_evidence).Count 0 "No default rejected evidence expected."
Assert-Equal $main.quarantine_preview.count 0 "No default quarantine expected."
Assert-Equal $quarantine.quarantined_count 0 "Quarantine artifact count mismatch."
Assert-Equal $quarantine.no_destructive_file_movement $true "Quarantine must be non-destructive."
Assert-Equal $quarantine.no_db_mutation $true "Quarantine must not mutate DB."
Assert-Equal $quarantine.no_external_calls $true "Quarantine must not use external calls."

Assert-Equal $validation.source_artifacts_validated.promotion_gate "REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_BLOCKED_R001" "Validation source promotion status mismatch."
if ($main.real_broker_evidence_lane.raw_lmax_bundle_seen -eq $true -and $main.real_broker_evidence_lane.raw_lmax_bundle_complete -eq $true) {
    Assert-True ($validation.accepted_real_broker_evidence_count -ge 1) "Validation broker accepted count should reflect accepted raw LMAX bundle."
} else {
    Assert-Equal $validation.accepted_real_broker_evidence_count 0 "Validation broker accepted count mismatch."
}
Assert-Equal $validation.accepted_real_accounting_evidence_count 0 "Validation accounting accepted count mismatch."

if ($main.real_broker_evidence_lane.raw_lmax_bundle_seen -eq $true -and $main.real_broker_evidence_lane.raw_lmax_bundle_complete -eq $true) {
    Assert-Equal $main.readiness.real_manual_broker_statement_acceptance $true "Real broker acceptance must be true for accepted raw bundle."
} else {
    Assert-Equal $main.readiness.real_manual_broker_statement_acceptance $false "Real broker acceptance must remain false without accepted raw bundle."
}
Assert-Equal $main.readiness.real_manual_accounting_evidence_acceptance $false "Real accounting acceptance must remain false."
Assert-Equal $main.readiness.broker_confirmed_pnl $false "Broker-confirmed PnL must remain false."
Assert-Equal $main.readiness.realized_accounting_close $false "Realized accounting close must remain false."
Assert-Equal $main.readiness.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.readiness.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.readiness.production_live $false "Production/live must remain false."
Assert-Equal $main.readiness.trading_readiness $false "Trading readiness must remain false."
Assert-Equal $main.ready_outputs.real_manual_evidence_acceptance_gate $true "Acceptance gate ready label missing."
Assert-Equal $main.synthetic_sandbox_closeout_lane.not_used_as_real_broker_acceptance_gate $true "Synthetic sandbox closeout must not gate real broker acceptance."
if ($main.real_broker_evidence_lane.raw_lmax_bundle_seen -eq $true -and $main.real_broker_evidence_lane.raw_lmax_bundle_complete -eq $true) {
    Assert-Equal $main.real_broker_evidence_lane.synthetic_sandbox_closeout_comparison.comparison_purpose "diagnostic_only_not_acceptance_gate" "Synthetic comparison must be diagnostic only."
    Assert-Equal $main.real_broker_evidence_lane.synthetic_sandbox_closeout_comparison.acceptance_impact "none" "Synthetic comparison acceptance impact must be none."
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
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness ready must remain false."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-real-manual-evidence-acceptance-r001.ps1"),
    (Join-Path $RepoRoot "scripts\test-real-manual-evidence-acceptance-r001.ps1")
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

Write-Host "REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001_GATE_PASS"

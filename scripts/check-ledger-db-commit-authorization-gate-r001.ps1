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

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\ledger-db-commit-authorization-gate-r001"
$AccountingClosePath = Join-Path $RepoRoot "artifacts\readiness\real-accounting-evidence-and-close-acceptance-r001\real-accounting-evidence-and-close-acceptance-r001.json"
$MainPath = Join-Path $ArtifactDir "ledger-db-commit-authorization-gate-r001.json"
$PolicyPath = Join-Path $ArtifactDir "ledger-db-commit-authorization-policy-r001.json"
$CommitCandidatePath = Join-Path $ArtifactDir "ledger-commit-candidate-r001.json"
$DbPlanPath = Join-Path $ArtifactDir "db-mutation-plan-preview-r001.json"
$SchemaPath = Join-Path $ArtifactDir "ledger-db-commit-authorization-schema-r001.json"
$ScanPath = Join-Path $ArtifactDir "ledger-db-commit-authorization-staging-scan-r001.json"
$ValidationPath = Join-Path $ArtifactDir "ledger-db-commit-authorization-validation-report-r001.json"
$QuarantinePath = Join-Path $ArtifactDir "ledger-db-commit-authorization-quarantine-preview-r001.json"
$SummaryPath = Join-Path $ArtifactDir "ledger-db-commit-authorization-gate-summary-r001.md"

foreach ($path in @($AccountingClosePath, $MainPath, $PolicyPath, $CommitCandidatePath, $DbPlanPath, $SchemaPath, $ScanPath, $ValidationPath, $QuarantinePath, $SummaryPath)) {
    Assert-True (Test-Path -LiteralPath $path) "Required artifact missing: $path"
}
foreach ($dir in @("staging\commit-authorization", "accepted", "rejected", "quarantine-preview", "commit-candidate", "db-mutation-plan-preview")) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $dir)) "Required local-only folder missing: $dir"
}

$source = Read-JsonFile $AccountingClosePath
$main = Read-JsonFile $MainPath
$policy = Read-JsonFile $PolicyPath
$candidate = Read-JsonFile $CommitCandidatePath
$dbPlan = Read-JsonFile $DbPlanPath
$schema = Read-JsonFile $SchemaPath
$scan = Read-JsonFile $ScanPath
$validation = Read-JsonFile $ValidationPath
$quarantine = Read-JsonFile $QuarantinePath

Assert-Equal $source.status "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_READY_R001" "Source accounting close status mismatch."
Assert-Equal $source.readiness.real_accounting_evidence_acceptance $true "Source real accounting evidence acceptance must be true."
Assert-Equal $source.readiness.realized_accounting_close $true "Source realized accounting close must be true."
Assert-Equal $source.readiness.ledger_commit $false "Source ledger commit must be false."
Assert-Equal $source.readiness.db_mutation $false "Source DB mutation must be false."
Assert-Equal $source.readiness.production_live $false "Source production/live must be false."
Assert-Equal $source.readiness.trading_readiness $false "Source trading readiness must be false."

Assert-Equal $policy.policy_type "ledger_db_commit_authorization_policy" "Policy type mismatch."
Assert-Equal $policy.mode "authorization_gate_only" "Policy mode mismatch."
Assert-Equal $policy.ledger_commit_candidate_allowed $true "Policy should allow commit candidate."
Assert-Equal $policy.actual_ledger_commit_allowed_in_this_package $false "Policy must not allow actual ledger commit."
Assert-Equal $policy.db_mutation_allowed_in_this_package $false "Policy must not allow DB mutation."
Assert-Equal $policy.commit_authorization_required $true "Policy must require commit authorization."
Assert-Equal $policy.operator_approval_required $true "Policy must require operator approval."
Assert-Equal $policy.idempotency_key_required $true "Policy must require idempotency key."
Assert-Equal $policy.rollback_plan_required $true "Policy must require rollback plan."
Assert-Equal $policy.audit_log_required $true "Policy must require audit log."

Assert-Equal $main.package "NEXT_LEDGER_DB_COMMIT_AUTHORIZATION_GATE_R001" "Main package mismatch."
Assert-True (@("LEDGER_DB_COMMIT_AUTHORIZATION_BLOCKED_R001", "LEDGER_DB_COMMIT_AUTHORIZATION_READY_R001") -contains $main.status) "Main status must be valid."
Assert-Equal $main.environment "sandbox" "Main environment mismatch."
Assert-Equal $main.mode "ledger_db_commit_authorization_gate_only" "Main mode mismatch."
Assert-Equal $main.commit_candidate.created $true "Commit candidate must be created."
Assert-Equal $main.commit_candidate.committed $false "Commit candidate must not be committed."
Assert-Equal $main.commit_candidate.ledger_commit $false "Commit candidate must not commit ledger."
Assert-Equal $main.commit_candidate.db_mutation $false "Commit candidate must not mutate DB."
Assert-Equal $main.db_mutation_plan_preview.created $true "DB mutation plan preview must be created."
Assert-Equal $main.db_mutation_plan_preview.dry_run_only $true "DB mutation plan must be dry-run only."
Assert-Equal $main.db_mutation_plan_preview.db_mutation $false "DB mutation plan must not mutate DB."
Assert-Equal $main.db_mutation_plan_preview.ledger_commit $false "DB mutation plan must not commit ledger."

Assert-DecimalEqual ([decimal]$main.values.realized_pnl_before_costs_usd) ([decimal]6015.14) "Main realized PnL mismatch."
Assert-DecimalEqual ([decimal]$main.values.commission_expense_usd) ([decimal]225.63) "Main commission mismatch."
Assert-DecimalEqual ([decimal]$main.values.financing_expense_usd) ([decimal]40.60) "Main financing mismatch."
Assert-DecimalEqual ([decimal]$main.values.realized_net_after_costs_usd) ([decimal]5748.91) "Main net mismatch."
Assert-DecimalEqual ([decimal]$main.values.unrealized_open_pnl_usd) ([decimal]463.61) "Main unrealized mismatch."
Assert-DecimalEqual ([decimal]$main.values.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Main equity mismatch."
Assert-DecimalEqual (([decimal]$main.values.realized_pnl_before_costs_usd) - ([decimal]$main.values.commission_expense_usd) - ([decimal]$main.values.financing_expense_usd)) ([decimal]$main.values.realized_net_after_costs_usd) "Main net formula failed."
Assert-DecimalEqual (([decimal]$main.values.realized_net_after_costs_usd) + ([decimal]$main.values.unrealized_open_pnl_usd)) ([decimal]$main.values.equity_pnl_including_open_pnl_usd) "Main equity formula failed."

Assert-Equal $candidate.commit_candidate $true "Candidate flag mismatch."
Assert-Equal $candidate.committed $false "Candidate must not be committed."
Assert-Equal $candidate.commit_eligible_without_authorization $false "Candidate must not be eligible without authorization."
Assert-Equal $candidate.ledger_commit $false "Candidate ledger commit must be false."
Assert-Equal $candidate.db_mutation $false "Candidate DB mutation must be false."
Assert-Equal $candidate.commit_status "AUTHORIZATION_REQUIRED_NO_COMMIT" "Candidate commit status mismatch."
Assert-Equal $candidate.committed_at_utc $null "Candidate committed_at_utc must be null."
Assert-Equal @($candidate.entries).Count 6 "Candidate must include six entries."
foreach ($entry in @($candidate.entries)) {
    Assert-Equal $entry.environment "sandbox" "Candidate entry environment mismatch."
    Assert-Equal $entry.mode "commit_candidate_preview_only" "Candidate entry mode mismatch."
    Assert-Equal $entry.commit_candidate $true "Candidate entry flag mismatch."
    Assert-Equal $entry.committed $false "Candidate entry must not be committed."
    Assert-Equal $entry.commit_eligible_without_authorization $false "Candidate entry must require authorization."
    Assert-Equal $entry.ledger_commit $false "Candidate entry ledger commit must be false."
    Assert-Equal $entry.db_mutation $false "Candidate entry DB mutation must be false."
    Assert-Equal $entry.commit_status "AUTHORIZATION_REQUIRED_NO_COMMIT" "Candidate entry commit status mismatch."
    Assert-Equal $entry.committed_at_utc $null "Candidate entry committed_at_utc must be null."
}

Assert-Equal $dbPlan.dry_run_only $true "DB plan must be dry-run only."
Assert-Equal $dbPlan.db_mutation $false "DB plan must not mutate DB."
Assert-Equal $dbPlan.ledger_commit $false "DB plan must not commit ledger."
Assert-True (-not [string]::IsNullOrWhiteSpace($dbPlan.idempotency_key)) "DB plan idempotency key missing."
Assert-Equal $schema.accepts_artifact_type "ledger_db_commit_authorization" "Authorization schema mismatch."

Assert-Equal $scan.commit_authorization_files_seen $main.staging_scan.commit_authorization_files_seen "Scan authorization count mismatch."
Assert-Equal $scan.accepted_commit_authorization_count $main.staging_scan.accepted_commit_authorization_count "Scan accepted count mismatch."
Assert-Equal $scan.rejected_or_quarantined_count $main.staging_scan.rejected_or_quarantined_count "Scan quarantine count mismatch."
Assert-Equal $validation.accepted_count $main.staging_scan.accepted_commit_authorization_count "Validation accepted count mismatch."
Assert-Equal $quarantine.quarantined_count $main.staging_scan.rejected_or_quarantined_count "Quarantine count mismatch."
Assert-Equal $quarantine.no_destructive_file_movement $true "Quarantine must be non-destructive."
Assert-Equal $quarantine.no_db_mutation $true "Quarantine must not mutate DB."
Assert-Equal $quarantine.no_external_calls $true "Quarantine must not use external calls."

if ($main.staging_scan.commit_authorization_files_seen -eq 0) {
    Assert-Equal $main.status "LEDGER_DB_COMMIT_AUTHORIZATION_BLOCKED_R001" "No staged authorization must block."
    Assert-Equal $main.blocked_reason "NO_LEDGER_DB_COMMIT_AUTHORIZATION_STAGED" "No staged authorization reason mismatch."
    Assert-Equal $main.readiness.ledger_db_commit_ready_for_future_commit_package $false "No authorization must not mark future commit ready."
}
if ($main.staging_scan.accepted_commit_authorization_count -gt 0) {
    Assert-Equal $main.status "LEDGER_DB_COMMIT_AUTHORIZATION_READY_R001" "Accepted authorization must mark authorization ready."
    Assert-Equal $main.readiness.ledger_db_commit_ready_for_future_commit_package $true "Accepted authorization must mark future commit package ready."
}

Assert-Equal $main.ready_outputs.ledger_commit_candidate_ready $true "Commit candidate ready output missing."
Assert-Equal $main.ready_outputs.db_mutation_plan_preview_ready $true "DB mutation plan ready output missing."
Assert-Equal $main.ready_outputs.ledger_db_commit_authorization_gate_ready $true "Authorization gate ready output missing."
Assert-Equal $main.readiness.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.readiness.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.readiness.production_live $false "Production/live must remain false."
Assert-Equal $main.readiness.trading_readiness $false "Trading readiness must remain false."
foreach ($label in $main.forbidden_ready_labels.PSObject.Properties) {
    Assert-Equal $label.Value $false "Forbidden ready label must remain false: $($label.Name)"
}
Assert-Equal $main.global_guards.external_calls $false "External calls must remain false."
Assert-Equal $main.global_guards.broker_api_calls $false "Broker API calls must remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $main.global_guards.account_data_fetch $false "Account-data fetch must remain false."
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit guard must remain false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation guard must remain false."
Assert-Equal $main.global_guards.trading_activity $false "Trading activity must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live guard must remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness guard must remain false."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-ledger-db-commit-authorization-gate-r001.ps1"),
    (Join-Path $RepoRoot "scripts\test-ledger-db-commit-authorization-gate-r001.ps1")
)
$forbiddenPatterns = @("SubmitOrder", "SubmitOrderUnmanaged", "AtmStrategyCreate", "Invoke-WebRequest", "Invoke-RestMethod", "curl ", "wget ", "api_key", "apikey", "password")
foreach ($pattern in $forbiddenPatterns) {
    $matches = Select-String -Path $scanFiles -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
    if ($matches) {
        throw "Forbidden call/secret/static pattern detected [$pattern]:`n$($matches | Out-String)"
    }
}

Write-Host "LEDGER_DB_COMMIT_AUTHORIZATION_GATE_R001_GATE_PASS"

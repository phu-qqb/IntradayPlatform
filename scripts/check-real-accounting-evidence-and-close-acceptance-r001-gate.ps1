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

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\real-accounting-evidence-and-close-acceptance-r001"
$DryRunDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-accounting-dry-run-and-close-gate-r001"
$ConfirmedDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001"
$ManualDir = Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001"

$DryRunGatePath = Join-Path $DryRunDir "broker-statement-accounting-dry-run-and-close-gate-r001.json"
$ConfirmedPath = Join-Path $ConfirmedDir "broker-statement-confirmed-pnl-r001.json"
$AcceptancePath = Join-Path $ManualDir "real-manual-evidence-acceptance-r001.json"

$MainPath = Join-Path $ArtifactDir "real-accounting-evidence-and-close-acceptance-r001.json"
$DraftPath = Join-Path $ArtifactDir "accounting-close-draft-from-broker-statement-r001.json"
$AccountingSchemaPath = Join-Path $ArtifactDir "real-accounting-evidence-schema-r001.json"
$ApprovalSchemaPath = Join-Path $ArtifactDir "accounting-close-approval-schema-r001.json"
$ScanPath = Join-Path $ArtifactDir "real-accounting-evidence-staging-scan-r001.json"
$AccountingValidationPath = Join-Path $ArtifactDir "real-accounting-evidence-validation-report-r001.json"
$ApprovalValidationPath = Join-Path $ArtifactDir "accounting-close-approval-validation-report-r001.json"
$QuarantinePath = Join-Path $ArtifactDir "real-accounting-evidence-quarantine-preview-r001.json"
$SummaryPath = Join-Path $ArtifactDir "real-accounting-evidence-and-close-acceptance-summary-r001.md"

foreach ($path in @($DryRunGatePath, $ConfirmedPath, $AcceptancePath, $MainPath, $DraftPath, $AccountingSchemaPath, $ApprovalSchemaPath, $ScanPath, $AccountingValidationPath, $ApprovalValidationPath, $QuarantinePath, $SummaryPath)) {
    Assert-True (Test-Path -LiteralPath $path) "Required artifact missing: $path"
}
foreach ($dir in @("staging\accounting-evidence", "staging\accounting-close-approval", "accepted", "rejected", "quarantine-preview", "draft-package")) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $dir)) "Required local-only folder missing: $dir"
}

$dryRunGate = Read-JsonFile $DryRunGatePath
$confirmed = Read-JsonFile $ConfirmedPath
$acceptance = Read-JsonFile $AcceptancePath
$main = Read-JsonFile $MainPath
$draft = Read-JsonFile $DraftPath
$accountingSchema = Read-JsonFile $AccountingSchemaPath
$approvalSchema = Read-JsonFile $ApprovalSchemaPath
$scan = Read-JsonFile $ScanPath
$accountingValidation = Read-JsonFile $AccountingValidationPath
$approvalValidation = Read-JsonFile $ApprovalValidationPath
$quarantine = Read-JsonFile $QuarantinePath

Assert-Equal $dryRunGate.status "BROKER_STATEMENT_ACCOUNTING_DRY_RUN_READY_CLOSE_BLOCKED_R001" "Source accounting dry-run gate status mismatch."
Assert-Equal $confirmed.status "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001" "Source broker statement confirmed PnL status mismatch."
Assert-Equal $acceptance.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_PARTIAL_READY_R001" "Source real manual evidence status mismatch."
Assert-Equal $acceptance.readiness.real_manual_broker_statement_acceptance $true "Source real broker statement acceptance must be true."
Assert-Equal $acceptance.readiness.real_manual_accounting_evidence_acceptance $false "Source accounting evidence acceptance must be false before package."
Assert-Equal $dryRunGate.readiness.ledger_commit $false "Source ledger commit must be false."
Assert-Equal $dryRunGate.readiness.db_mutation $false "Source DB mutation must be false."
Assert-Equal $dryRunGate.readiness.production_live $false "Source production/live must be false."
Assert-Equal $dryRunGate.readiness.trading_readiness $false "Source trading readiness must be false."
Assert-Equal $dryRunGate.global_guards.external_calls $false "Source external calls must be false."
Assert-Equal $dryRunGate.global_guards.broker_api_calls $false "Source broker API calls must be false."
Assert-Equal $dryRunGate.global_guards.market_data_fetch $false "Source market-data fetch must be false."
Assert-Equal $dryRunGate.global_guards.account_data_fetch $false "Source account-data fetch must be false."

Assert-Equal $draft.draft_only $true "Draft package must be draft only."
Assert-Equal $draft.real_accounting_evidence $false "Draft package must not be real accounting evidence."
Assert-Equal $draft.realized_accounting_close $false "Draft package must not mark realized accounting close."
Assert-Equal $draft.ledger_commit $false "Draft package must not commit ledger."
Assert-Equal $draft.db_mutation $false "Draft package must not mutate DB."
Assert-Equal $accountingSchema.accepts_artifact_type "real_accounting_evidence_import" "Accounting evidence schema mismatch."
Assert-Equal $approvalSchema.accepts_artifact_type "accounting_close_approval" "Close approval schema mismatch."

Assert-Equal $main.package "NEXT_REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_R001" "Main package mismatch."
Assert-True (@("REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_BLOCKED_R001", "REAL_ACCOUNTING_EVIDENCE_ACCEPTED_CLOSE_BLOCKED_R001", "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_READY_R001") -contains $main.status) "Main status must be valid."
Assert-Equal $main.environment "sandbox" "Main environment mismatch."
Assert-Equal $main.mode "offline_manual_accounting_acceptance_gate" "Main mode mismatch."
Assert-Equal $main.accounting_draft_package.created $true "Main must report draft package created."
Assert-Equal $main.accounting_draft_package.draft_only $true "Main draft package must be draft only."
Assert-Equal $main.accounting_draft_package.real_accounting_evidence $false "Main draft package must not be real accounting evidence."
Assert-Equal $main.accounting_draft_package.realized_accounting_close $false "Main draft package must not mark realized close."

Assert-DecimalEqual ([decimal]$main.values.realized_pnl_before_costs_usd) ([decimal]6015.14) "Main realised PnL mismatch."
Assert-DecimalEqual ([decimal]$main.values.commission_expense_usd) ([decimal]225.63) "Main commission expense mismatch."
Assert-DecimalEqual ([decimal]$main.values.financing_expense_usd) ([decimal]40.60) "Main financing expense mismatch."
Assert-DecimalEqual ([decimal]$main.values.realized_net_after_costs_usd) ([decimal]5748.91) "Main realised net mismatch."
Assert-DecimalEqual ([decimal]$main.values.unrealized_open_pnl_usd) ([decimal]463.61) "Main unrealized PnL mismatch."
Assert-DecimalEqual ([decimal]$main.values.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Main equity PnL mismatch."

Assert-Equal $scan.accounting_evidence_files_seen $main.staging_scan.accounting_evidence_files_seen "Scan accounting file count mismatch."
Assert-Equal $scan.accounting_close_approval_files_seen $main.staging_scan.accounting_close_approval_files_seen "Scan approval file count mismatch."
Assert-Equal $scan.accepted_accounting_evidence_count $main.staging_scan.accepted_accounting_evidence_count "Scan accepted accounting count mismatch."
Assert-Equal $scan.accepted_close_approval_count $main.staging_scan.accepted_close_approval_count "Scan accepted approval count mismatch."
Assert-Equal $scan.rejected_or_quarantined_count $main.staging_scan.rejected_or_quarantined_count "Scan quarantine count mismatch."
Assert-Equal $accountingValidation.accepted_count $main.staging_scan.accepted_accounting_evidence_count "Accounting validation accepted count mismatch."
Assert-Equal $approvalValidation.accepted_count $main.staging_scan.accepted_close_approval_count "Approval validation accepted count mismatch."
Assert-Equal $quarantine.quarantined_count $main.staging_scan.rejected_or_quarantined_count "Quarantine count mismatch."
Assert-Equal $quarantine.no_destructive_file_movement $true "Quarantine must be non-destructive."
Assert-Equal $quarantine.no_db_mutation $true "Quarantine must not mutate DB."
Assert-Equal $quarantine.no_external_calls $true "Quarantine must not use external calls."

if ($main.staging_scan.accounting_evidence_files_seen -eq 0 -and $main.staging_scan.accounting_close_approval_files_seen -eq 0) {
    Assert-Equal $main.status "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_BLOCKED_R001" "Empty staging must produce blocked status."
    Assert-Equal $main.blocked_reason "NO_REAL_ACCOUNTING_EVIDENCE_OR_CLOSE_APPROVAL_STAGED" "Empty staging blocked reason mismatch."
}
if ($main.staging_scan.accepted_accounting_evidence_count -gt 0 -and $main.staging_scan.accepted_close_approval_count -eq 0) {
    Assert-Equal $main.status "REAL_ACCOUNTING_EVIDENCE_ACCEPTED_CLOSE_BLOCKED_R001" "Accounting accepted without close approval status mismatch."
    Assert-Equal $main.blocked_reason "ACCOUNTING_CLOSE_APPROVAL_MISSING" "Accounting accepted without close approval reason mismatch."
}
if ($main.staging_scan.accepted_accounting_evidence_count -gt 0 -and $main.staging_scan.accepted_close_approval_count -gt 0) {
    Assert-Equal $main.status "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_READY_R001" "Both valid staged files must produce ready status."
    Assert-Equal $main.readiness.real_accounting_evidence_acceptance $true "Real accounting evidence acceptance must be true."
    Assert-Equal $main.readiness.realized_accounting_close $true "Realized accounting close must be true."
}

Assert-Equal $main.ready_outputs.accounting_close_draft_package_ready $true "Draft package ready output missing."
Assert-Equal $main.ready_outputs.real_accounting_evidence_acceptance_gate_ready $true "Acceptance gate ready output missing."
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
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.global_guards.trading_activity $false "Trading activity must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness ready must remain false."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-real-accounting-evidence-and-close-acceptance-r001.ps1"),
    (Join-Path $RepoRoot "scripts\test-real-accounting-evidence-and-close-acceptance-r001.ps1")
)
$forbiddenPatterns = @("SubmitOrder", "SubmitOrderUnmanaged", "AtmStrategyCreate", "Invoke-WebRequest", "Invoke-RestMethod", "curl ", "wget ", "api_key", "apikey", "password")
foreach ($pattern in $forbiddenPatterns) {
    $matches = Select-String -Path $scanFiles -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
    if ($matches) {
        throw "Forbidden call/secret/static pattern detected [$pattern]:`n$($matches | Out-String)"
    }
}

Write-Host "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_R001_GATE_PASS"

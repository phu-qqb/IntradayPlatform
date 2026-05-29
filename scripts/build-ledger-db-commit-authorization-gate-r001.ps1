param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputSubdir = "ledger-db-commit-authorization-gate-r001"
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\$OutputSubdir"
$AuthorizationStagingDir = Join-Path $ArtifactDir "staging\commit-authorization"
$AcceptedDir = Join-Path $ArtifactDir "accepted"
$RejectedDir = Join-Path $ArtifactDir "rejected"
$QuarantineDir = Join-Path $ArtifactDir "quarantine-preview"
$CommitCandidateDir = Join-Path $ArtifactDir "commit-candidate"
$DbPlanDir = Join-Path $ArtifactDir "db-mutation-plan-preview"

foreach ($dir in @($ArtifactDir, $AuthorizationStagingDir, $AcceptedDir, $RejectedDir, $QuarantineDir, $CommitCandidateDir, $DbPlanDir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Write-TextArtifact([string]$Name, [string]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Sha([string]$Path) {
    "sha256:$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash)"
}

function Prop($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary] -and $Object.Contains($Name)) { return $Object[$Name] }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Is-Missing($Value) {
    if ($null -eq $Value) { return $true }
    if ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value)) { return $true }
    return $false
}

function Is-True($Value) {
    if ($Value -eq $true) { return $true }
    if ($Value -is [string] -and $Value.ToLowerInvariant() -eq "true") { return $true }
    return $false
}

function Add-Reason([System.Collections.Generic.List[string]]$Reasons, [string]$Reason) {
    if (-not $Reasons.Contains($Reason)) { $Reasons.Add($Reason) | Out-Null }
}

function As-Decimal($Value, [string]$Name) {
    if (Is-Missing $Value) { throw "Required decimal value missing: $Name" }
    return [decimal]$Value
}

function Decimal-Matches([object]$Value, [decimal]$Expected) {
    if (Is-Missing $Value) { return $false }
    return ([Math]::Abs(([decimal]$Value) - $Expected) -le [decimal]0.000001)
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

function Assert-True($Actual, [string]$Message) {
    if ($Actual -ne $true) { throw "$Message Expected=[True] Actual=[$Actual]" }
}

function Assert-False($Actual, [string]$Message) {
    if ($Actual -ne $false) { throw "$Message Expected=[False] Actual=[$Actual]" }
}

function Assert-DecimalEqual([decimal]$Actual, [decimal]$Expected, [string]$Message) {
    if ([Math]::Abs($Actual - $Expected) -gt [decimal]0.000001) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

$AccountingCloseDir = Join-Path $RepoRoot "artifacts\readiness\real-accounting-evidence-and-close-acceptance-r001"
$AccountingDryRunDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-accounting-dry-run-and-close-gate-r001"
$BrokerConfirmedDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001"
$ManualDir = Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001"

$AccountingClosePath = Join-Path $AccountingCloseDir "real-accounting-evidence-and-close-acceptance-r001.json"
$AccountingValidationPath = Join-Path $AccountingCloseDir "real-accounting-evidence-validation-report-r001.json"
$CloseApprovalValidationPath = Join-Path $AccountingCloseDir "accounting-close-approval-validation-report-r001.json"
$AccountingDraftPath = Join-Path $AccountingCloseDir "accounting-close-draft-from-broker-statement-r001.json"
$AccountingDryRunGatePath = Join-Path $AccountingDryRunDir "broker-statement-accounting-dry-run-and-close-gate-r001.json"
$JournalDryRunPath = Join-Path $AccountingDryRunDir "broker-statement-journal-dry-run-r001.json"
$BrokerConfirmedPath = Join-Path $BrokerConfirmedDir "broker-statement-confirmed-pnl-r001.json"
$NormalizedPath = Join-Path $ManualDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json"

foreach ($path in @($AccountingClosePath, $AccountingValidationPath, $CloseApprovalValidationPath, $AccountingDraftPath, $AccountingDryRunGatePath, $JournalDryRunPath, $BrokerConfirmedPath, $NormalizedPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required source artifact missing: $path" }
}

$accountingClose = Read-JsonFile $AccountingClosePath
$accountingDryRunGate = Read-JsonFile $AccountingDryRunGatePath
$journalDryRun = Read-JsonFile $JournalDryRunPath
$brokerConfirmed = Read-JsonFile $BrokerConfirmedPath
$normalized = Read-JsonFile $NormalizedPath

Assert-Equal $accountingClose.status "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_READY_R001" "Source real accounting evidence and close acceptance must be ready."
Assert-True $accountingClose.readiness.real_accounting_evidence_acceptance "Real accounting evidence acceptance must be true."
Assert-True $accountingClose.readiness.realized_accounting_close "Realized accounting close must be true."
Assert-False $accountingClose.readiness.ledger_commit "Source ledger commit must be false."
Assert-False $accountingClose.readiness.db_mutation "Source DB mutation must be false."
Assert-False $accountingClose.readiness.production_live "Source production/live must be false."
Assert-False $accountingClose.readiness.trading_readiness "Source trading readiness must be false."
Assert-False $accountingClose.global_guards.external_calls "Source external calls must be false."
Assert-False $accountingClose.global_guards.broker_api_calls "Source broker API calls must be false."
Assert-False $accountingClose.global_guards.market_data_fetch "Source market-data fetch must be false."
Assert-False $accountingClose.global_guards.account_data_fetch "Source account-data fetch must be false."
Assert-Equal $brokerConfirmed.status "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001" "Broker statement confirmed PnL source status mismatch."
Assert-Equal $accountingDryRunGate.status "BROKER_STATEMENT_ACCOUNTING_DRY_RUN_READY_CLOSE_BLOCKED_R001" "Accounting dry-run gate source status mismatch."
Assert-True $journalDryRun.ready "Journal dry-run must be ready."
Assert-False $journalDryRun.commit_allowed "Journal dry-run must not allow commit."
Assert-False $journalDryRun.ledger_commit "Journal dry-run must not commit ledger."
Assert-False $journalDryRun.db_mutation "Journal dry-run must not mutate DB."
Assert-False $normalized.external_fetch "Normalized broker statement must not use external fetch."
Assert-False $normalized.broker_api_call "Normalized broker statement must not use broker API."
Assert-False $normalized.market_data_fetch "Normalized broker statement must not fetch market data."
Assert-False $normalized.account_data_fetch "Normalized broker statement must not fetch account data."

$values = $accountingClose.values
$realisedBeforeCosts = As-Decimal $values.realized_pnl_before_costs_usd "realized_pnl_before_costs_usd"
$commissionExpense = As-Decimal $values.commission_expense_usd "commission_expense_usd"
$financingExpense = As-Decimal $values.financing_expense_usd "financing_expense_usd"
$realisedNetAfterCosts = As-Decimal $values.realized_net_after_costs_usd "realized_net_after_costs_usd"
$unrealizedOpenPnl = As-Decimal $values.unrealized_open_pnl_usd "unrealized_open_pnl_usd"
$equityPnlIncludingOpen = As-Decimal $values.equity_pnl_including_open_pnl_usd "equity_pnl_including_open_pnl_usd"

Assert-DecimalEqual ($realisedBeforeCosts - $commissionExpense - $financingExpense) $realisedNetAfterCosts "Commit candidate realised net formula failed."
Assert-DecimalEqual ($realisedNetAfterCosts + $unrealizedOpenPnl) $equityPnlIncludingOpen "Commit candidate equity PnL formula failed."

$policy = [ordered]@{
    policy_type = "ledger_db_commit_authorization_policy"
    policy_version = "R001"
    environment = "sandbox"
    mode = "authorization_gate_only"
    source_of_truth = "accepted_real_accounting_close"
    ledger_commit_candidate_allowed = $true
    actual_ledger_commit_allowed_in_this_package = $false
    db_mutation_allowed_in_this_package = $false
    commit_authorization_required = $true
    operator_approval_required = $true
    idempotency_key_required = $true
    rollback_plan_required = $true
    audit_log_required = $true
    production_live_allowed = $false
    trading_allowed = $false
}
Write-JsonArtifact "ledger-db-commit-authorization-policy-r001.json" $policy
$PolicyPath = Join-Path $ArtifactDir "ledger-db-commit-authorization-policy-r001.json"

$sourceHash = Sha $AccountingClosePath
function New-CommitCandidateEntry([string]$Id, [string]$Subtype, [decimal]$Amount, [decimal]$SignedAmount, [string]$SourceComponent, [string]$Memo) {
    [ordered]@{
        commit_candidate_entry_id = $Id
        environment = "sandbox"
        mode = "commit_candidate_preview_only"
        account_currency = "USD"
        entry_type = "ledger_commit_candidate"
        entry_subtype = $Subtype
        amount_usd = $Amount
        signed_amount_usd = $SignedAmount
        source_component = $SourceComponent
        source_artifact_hash = $sourceHash
        commit_candidate = $true
        committed = $false
        commit_eligible_without_authorization = $false
        ledger_commit = $false
        db_mutation = $false
        commit_status = "AUTHORIZATION_REQUIRED_NO_COMMIT"
        committed_at_utc = $null
        memo = $Memo
    }
}

$commitEntries = @(
    (New-CommitCandidateEntry "LDC-R001-001" "realized_pnl_before_costs" $realisedBeforeCosts $realisedBeforeCosts "real_accounting_close.realized_pnl_before_costs_usd" "Realized PnL before costs candidate."),
    (New-CommitCandidateEntry "LDC-R001-002" "commission_expense" $commissionExpense (-1 * $commissionExpense) "real_accounting_close.commission_expense_usd" "Commission expense candidate."),
    (New-CommitCandidateEntry "LDC-R001-003" "financing_expense" $financingExpense (-1 * $financingExpense) "real_accounting_close.financing_expense_usd" "Financing expense candidate."),
    (New-CommitCandidateEntry "LDC-R001-004" "realized_net_after_costs" $realisedNetAfterCosts $realisedNetAfterCosts "derived.realized_pnl_minus_commission_minus_financing" "Realized net after costs candidate."),
    (New-CommitCandidateEntry "LDC-R001-005" "unrealized_open_pnl" $unrealizedOpenPnl $unrealizedOpenPnl "real_accounting_close.unrealized_open_pnl_usd" "Unrealized open PnL candidate."),
    (New-CommitCandidateEntry "LDC-R001-006" "equity_pnl_including_open_pnl" $equityPnlIncludingOpen $equityPnlIncludingOpen "derived.realized_net_after_costs_plus_open_pnl" "Equity PnL including open PnL candidate.")
)

$commitCandidate = [ordered]@{
    artifact_type = "ledger_commit_candidate_r001"
    environment = "sandbox"
    mode = "commit_candidate_preview_only"
    commit_candidate = $true
    committed = $false
    commit_eligible_without_authorization = $false
    ledger_commit = $false
    db_mutation = $false
    commit_status = "AUTHORIZATION_REQUIRED_NO_COMMIT"
    committed_at_utc = $null
    entries = $commitEntries
    formulas = [ordered]@{
        realized_net_after_costs = "6015.14 - 225.63 - 40.60 = 5748.91"
        equity_pnl_including_open_pnl = "5748.91 + 463.61 = 6212.52"
    }
    values = [ordered]@{
        realized_pnl_before_costs_usd = $realisedBeforeCosts
        commission_expense_usd = $commissionExpense
        financing_expense_usd = $financingExpense
        realized_net_after_costs_usd = $realisedNetAfterCosts
        unrealized_open_pnl_usd = $unrealizedOpenPnl
        equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    }
}
Write-JsonArtifact "ledger-commit-candidate-r001.json" $commitCandidate
$CommitCandidatePath = Join-Path $ArtifactDir "ledger-commit-candidate-r001.json"

$idempotencyKey = "ledger-db-commit-r001:$((Sha $AccountingClosePath).Substring(7, 16))"
$dbPlan = [ordered]@{
    artifact_type = "db_mutation_plan_preview_r001"
    environment = "sandbox"
    mode = "db_mutation_plan_preview_only"
    dry_run_only = $true
    db_mutation = $false
    ledger_commit = $false
    idempotency_key = $idempotencyKey
    target_tables = @(
        [ordered]@{ logical_table = "ledger_journal_entries"; proposed_mutation = "insert"; expected_rows = 6 },
        [ordered]@{ logical_table = "ledger_commit_batches"; proposed_mutation = "insert"; expected_rows = 1 },
        [ordered]@{ logical_table = "accounting_close_audit"; proposed_mutation = "insert"; expected_rows = 1 }
    )
    proposed_insert_update_type = "insert_only_preview"
    source_artifact_hashes = [ordered]@{
        real_accounting_evidence_and_close_acceptance_r001 = Sha $AccountingClosePath
        ledger_commit_candidate_r001 = Sha $CommitCandidatePath
    }
    expected_row_counts = [ordered]@{
        ledger_journal_entries = 6
        ledger_commit_batches = 1
        accounting_close_audit = 1
    }
    transaction_policy = "future_single_transaction_required_no_transaction_opened_here"
    rollback_policy = "future_explicit_rollback_plan_required_no_rollback_executed_here"
    audit_log_policy = "future_append_only_audit_log_required_no_audit_write_here"
}
Write-JsonArtifact "db-mutation-plan-preview-r001.json" $dbPlan
$DbPlanPath = Join-Path $ArtifactDir "db-mutation-plan-preview-r001.json"

$authorizationSchema = [ordered]@{
    artifact_type = "ledger_db_commit_authorization_schema_r001"
    accepts_artifact_type = "ledger_db_commit_authorization"
    required_fields = @(
        "artifact_type",
        "environment",
        "authorization_mode",
        "sample_only",
        "commit_authorization",
        "authorized_by",
        "authorized_at_utc",
        "authorization_id",
        "approved_source_artifact_hashes",
        "approved_values",
        "idempotency_key",
        "rollback_plan",
        "audit_log_plan"
    )
    expected_values = [ordered]@{
        realized_pnl_before_costs_usd = $realisedBeforeCosts
        commission_expense_usd = $commissionExpense
        financing_expense_usd = $financingExpense
        realized_net_after_costs_usd = $realisedNetAfterCosts
        unrealized_open_pnl_usd = $unrealizedOpenPnl
        equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    }
    authorization_may_authorize = @("ledger_db_commit_ready_for_future_commit_package")
    authorization_must_not_execute = @("ledger commit", "DB mutation", "production/live", "trading")
}
Write-JsonArtifact "ledger-db-commit-authorization-schema-r001.json" $authorizationSchema
$AuthorizationSchemaPath = Join-Path $ArtifactDir "ledger-db-commit-authorization-schema-r001.json"

function Validate-Authorization($Evidence, [string]$Path) {
    $reasons = [System.Collections.Generic.List[string]]::new()

    if ((Prop $Evidence "artifact_type") -ne "ledger_db_commit_authorization") { Add-Reason $reasons "artifact_type must be ledger_db_commit_authorization" }
    if ((Prop $Evidence "environment") -ne "sandbox") { Add-Reason $reasons "environment must be sandbox" }
    if ((Prop $Evidence "authorization_mode") -ne "offline_manual") { Add-Reason $reasons "authorization_mode must be offline_manual" }
    if ((Prop $Evidence "sample_only") -ne $false) { Add-Reason $reasons "sample_only must be false" }
    if ((Prop $Evidence "commit_authorization") -ne $true) { Add-Reason $reasons "commit_authorization must be true" }
    foreach ($field in @("authorized_by", "authorized_at_utc", "authorization_id", "approved_source_artifact_hashes", "approved_values", "idempotency_key", "rollback_plan", "audit_log_plan")) {
        if (Is-Missing (Prop $Evidence $field)) { Add-Reason $reasons "$field missing" }
    }
    $approved = Prop $Evidence "approved_values"
    if (-not (Decimal-Matches (Prop $approved "realized_pnl_before_costs_usd") $realisedBeforeCosts)) { Add-Reason $reasons "approved realized_pnl_before_costs_usd mismatch" }
    if (-not (Decimal-Matches (Prop $approved "commission_expense_usd") $commissionExpense)) { Add-Reason $reasons "approved commission_expense_usd mismatch" }
    if (-not (Decimal-Matches (Prop $approved "financing_expense_usd") $financingExpense)) { Add-Reason $reasons "approved financing_expense_usd mismatch" }
    if (-not (Decimal-Matches (Prop $approved "realized_net_after_costs_usd") $realisedNetAfterCosts)) { Add-Reason $reasons "approved realized_net_after_costs_usd mismatch" }
    if (-not (Decimal-Matches (Prop $approved "unrealized_open_pnl_usd") $unrealizedOpenPnl)) { Add-Reason $reasons "approved unrealized_open_pnl_usd mismatch" }
    if (-not (Decimal-Matches (Prop $approved "equity_pnl_including_open_pnl_usd") $equityPnlIncludingOpen)) { Add-Reason $reasons "approved equity_pnl_including_open_pnl_usd mismatch" }
    if (Is-True (Prop $Evidence "ledger_commit")) { Add-Reason $reasons "ledger_commit true" }
    if (Is-True (Prop $Evidence "db_mutation")) { Add-Reason $reasons "db_mutation true" }
    if (Is-True (Prop $Evidence "external_fetch")) { Add-Reason $reasons "external_fetch true" }
    if (Is-True (Prop $Evidence "market_data_fetch")) { Add-Reason $reasons "market_data_fetch true" }
    if (Is-True (Prop $Evidence "account_data_fetch")) { Add-Reason $reasons "account_data_fetch true" }
    if (Is-True (Prop $Evidence "production_live_authorized")) { Add-Reason $reasons "production_live_authorized true" }
    if (Is-True (Prop $Evidence "trading_authorized")) { Add-Reason $reasons "trading_authorized true" }

    [ordered]@{
        path = $Path
        sha256 = Sha $Path
        evidence_type = "commit_authorization_candidate"
        valid = ($reasons.Count -eq 0)
        reasons = @($reasons)
    }
}

function Read-CandidateJson([string]$Path) {
    try {
        return Read-JsonFile $Path
    } catch {
        return $null
    }
}

$authorizationFiles = @(Get-ChildItem -Path $AuthorizationStagingDir -Filter "*.json" -File -ErrorAction SilentlyContinue)
$authorizationResults = @()
$quarantineItems = @()

foreach ($file in $authorizationFiles) {
    $json = Read-CandidateJson $file.FullName
    if ($null -eq $json) {
        $result = [ordered]@{ path = $file.FullName; sha256 = Sha $file.FullName; evidence_type = "invalid_json"; valid = $false; reasons = @("invalid_json") }
    } elseif ((Prop $json "sample_only") -eq $true -or (Prop $json "draft_only") -eq $true) {
        $result = Validate-Authorization $json $file.FullName
        $result.evidence_type = "sample_or_draft_not_promotable"
    } else {
        $result = Validate-Authorization $json $file.FullName
    }
    $authorizationResults += $result
    if (-not $result.valid) { $quarantineItems += $result }
}

$acceptedAuthorizations = @($authorizationResults | Where-Object { $_.valid -eq $true })
$commitReadyForFuturePackage = ($acceptedAuthorizations.Count -gt 0)
$status = "LEDGER_DB_COMMIT_AUTHORIZATION_BLOCKED_R001"
$blockedReason = "NO_LEDGER_DB_COMMIT_AUTHORIZATION_STAGED"
if ($acceptedAuthorizations.Count -gt 0) {
    $status = "LEDGER_DB_COMMIT_AUTHORIZATION_READY_R001"
    $blockedReason = $null
} elseif ($authorizationFiles.Count -gt 0) {
    $status = "LEDGER_DB_COMMIT_AUTHORIZATION_BLOCKED_R001"
    $blockedReason = "LEDGER_DB_COMMIT_AUTHORIZATION_INVALID"
}

$stagingScan = [ordered]@{
    commit_authorization_files_seen = $authorizationFiles.Count
    accepted_commit_authorization_count = $acceptedAuthorizations.Count
    rejected_or_quarantined_count = $quarantineItems.Count
    commit_authorization_candidates = $authorizationResults
}
Write-JsonArtifact "ledger-db-commit-authorization-staging-scan-r001.json" $stagingScan
$StagingScanPath = Join-Path $ArtifactDir "ledger-db-commit-authorization-staging-scan-r001.json"

$validationReport = [ordered]@{
    artifact_type = "ledger_db_commit_authorization_validation_report_r001"
    accepted_count = $acceptedAuthorizations.Count
    rejected_count = @($authorizationResults | Where-Object { $_.valid -ne $true }).Count
    results = $authorizationResults
    expected_values = $authorizationSchema.expected_values
}
Write-JsonArtifact "ledger-db-commit-authorization-validation-report-r001.json" $validationReport
$ValidationReportPath = Join-Path $ArtifactDir "ledger-db-commit-authorization-validation-report-r001.json"

$quarantine = [ordered]@{
    artifact_type = "ledger_db_commit_authorization_quarantine_preview_r001"
    quarantined_count = $quarantineItems.Count
    items = $quarantineItems
    no_destructive_file_movement = $true
    no_db_mutation = $true
    no_external_calls = $true
}
Write-JsonArtifact "ledger-db-commit-authorization-quarantine-preview-r001.json" $quarantine
$QuarantinePath = Join-Path $ArtifactDir "ledger-db-commit-authorization-quarantine-preview-r001.json"

$stillBlocked = @("ledger_commit", "db_mutation", "production_live", "trading_readiness")
if (-not $commitReadyForFuturePackage) { $stillBlocked = @("ledger_db_commit_authorization") + $stillBlocked }

$main = [ordered]@{
    package = "NEXT_LEDGER_DB_COMMIT_AUTHORIZATION_GATE_R001"
    status = $status
    blocked_reason = $blockedReason
    environment = "sandbox"
    mode = "ledger_db_commit_authorization_gate_only"
    source_packages = [ordered]@{
        real_accounting_evidence_and_close_acceptance = "NEXT_REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_R001"
        broker_statement_accounting_dry_run_and_close_gate = "NEXT_BROKER_STATEMENT_ACCOUNTING_DRY_RUN_AND_CLOSE_GATE_R001"
        broker_statement_confirmed_pnl = "NEXT_BROKER_STATEMENT_CONFIRMED_PNL_R001"
    }
    source_artifacts = [ordered]@{
        real_accounting_evidence_and_close_acceptance_r001 = $AccountingClosePath
        real_accounting_evidence_validation_report_r001 = $AccountingValidationPath
        accounting_close_approval_validation_report_r001 = $CloseApprovalValidationPath
        accounting_close_draft_from_broker_statement_r001 = $AccountingDraftPath
        broker_statement_accounting_dry_run_and_close_gate_r001 = $AccountingDryRunGatePath
        broker_statement_journal_dry_run_r001 = $JournalDryRunPath
        broker_statement_confirmed_pnl_r001 = $BrokerConfirmedPath
        normalized_lmax_broker_statement_r001 = $NormalizedPath
        ledger_db_commit_authorization_policy_r001 = $PolicyPath
        ledger_commit_candidate_r001 = $CommitCandidatePath
        db_mutation_plan_preview_r001 = $DbPlanPath
        ledger_db_commit_authorization_schema_r001 = $AuthorizationSchemaPath
        ledger_db_commit_authorization_staging_scan_r001 = $StagingScanPath
        ledger_db_commit_authorization_validation_report_r001 = $ValidationReportPath
        ledger_db_commit_authorization_quarantine_preview_r001 = $QuarantinePath
    }
    source_artifact_hashes = [ordered]@{
        real_accounting_evidence_and_close_acceptance_r001 = Sha $AccountingClosePath
        real_accounting_evidence_validation_report_r001 = Sha $AccountingValidationPath
        accounting_close_approval_validation_report_r001 = Sha $CloseApprovalValidationPath
        accounting_close_draft_from_broker_statement_r001 = Sha $AccountingDraftPath
        broker_statement_accounting_dry_run_and_close_gate_r001 = Sha $AccountingDryRunGatePath
        broker_statement_journal_dry_run_r001 = Sha $JournalDryRunPath
        broker_statement_confirmed_pnl_r001 = Sha $BrokerConfirmedPath
        normalized_lmax_broker_statement_r001 = Sha $NormalizedPath
        ledger_db_commit_authorization_policy_r001 = Sha $PolicyPath
        ledger_commit_candidate_r001 = Sha $CommitCandidatePath
        db_mutation_plan_preview_r001 = Sha $DbPlanPath
        ledger_db_commit_authorization_schema_r001 = Sha $AuthorizationSchemaPath
        ledger_db_commit_authorization_staging_scan_r001 = Sha $StagingScanPath
        ledger_db_commit_authorization_validation_report_r001 = Sha $ValidationReportPath
        ledger_db_commit_authorization_quarantine_preview_r001 = Sha $QuarantinePath
    }
    source_statuses = [ordered]@{
        real_accounting_evidence_and_close_acceptance = $accountingClose.status
        broker_statement_accounting_dry_run_and_close_gate = $accountingDryRunGate.status
        broker_statement_confirmed_pnl = $brokerConfirmed.status
    }
    commit_candidate = [ordered]@{
        created = $true
        committed = $false
        ledger_commit = $false
        db_mutation = $false
    }
    db_mutation_plan_preview = [ordered]@{
        created = $true
        dry_run_only = $true
        db_mutation = $false
        ledger_commit = $false
    }
    staging_scan = [ordered]@{
        commit_authorization_files_seen = $authorizationFiles.Count
        accepted_commit_authorization_count = $acceptedAuthorizations.Count
        rejected_or_quarantined_count = $quarantineItems.Count
    }
    values = [ordered]@{
        realized_pnl_before_costs_usd = $realisedBeforeCosts
        commission_expense_usd = $commissionExpense
        financing_expense_usd = $financingExpense
        realized_net_after_costs_usd = $realisedNetAfterCosts
        unrealized_open_pnl_usd = $unrealizedOpenPnl
        equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    }
    readiness = [ordered]@{
        ledger_db_commit_ready_for_future_commit_package = $commitReadyForFuturePackage
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    ready_outputs = [ordered]@{
        ledger_commit_candidate_ready = $true
        db_mutation_plan_preview_ready = $true
        ledger_db_commit_authorization_gate_ready = $true
        ledger_db_commit_ready_for_future_commit_package = $commitReadyForFuturePackage
    }
    forbidden_ready_labels = [ordered]@{
        committed_ledger = $false
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    still_blocked = $stillBlocked
    global_guards = [ordered]@{
        external_calls = $false
        broker_api_calls = $false
        market_data_fetch = $false
        account_data_fetch = $false
        ledger_commit = $false
        db_mutation = $false
        trading_activity = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
}
Write-JsonArtifact "ledger-db-commit-authorization-gate-r001.json" $main

$summary = @"
# Ledger DB Commit Authorization Gate R001

Source real accounting evidence and close acceptance status: $($accountingClose.status)
Source broker statement accounting dry-run status: $($accountingDryRunGate.status)
Source broker statement confirmed PnL status: $($brokerConfirmed.status)

Commit candidate status: created, not committed, authorization required.
DB mutation plan preview status: created, dry-run only.

Commit authorization staging:
- Files seen: $($authorizationFiles.Count)
- Accepted authorizations: $($acceptedAuthorizations.Count)
- Rejected or quarantined: $($quarantineItems.Count)

Final status: $status
Blocked reason: $blockedReason

Values:
- Realized PnL before costs USD: $realisedBeforeCosts
- Commission expense USD: $commissionExpense
- Financing expense USD: $financingExpense
- Realized net after costs USD: $realisedNetAfterCosts
- Unrealized open PnL USD: $unrealizedOpenPnl
- Equity PnL including open PnL USD: $equityPnlIncludingOpen

Readiness:
- Ledger DB commit ready for future commit package: $commitReadyForFuturePackage
- Ledger commit: false
- DB mutation: false
- Production/live: false
- Trading readiness: false

No trading, R009 submission, LMAX FIX/API call, broker API call, Polygon/Massive call, market-data fetch, broker fetch, account-data fetch, DB mutation, ledger commit, production/live action, or trading activity occurred.
"@
Write-TextArtifact "ledger-db-commit-authorization-gate-summary-r001.md" $summary

Write-Host "LEDGER_DB_COMMIT_AUTHORIZATION_GATE_R001_BUILD_READY"

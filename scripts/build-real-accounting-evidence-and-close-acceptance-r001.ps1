param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputSubdir = "real-accounting-evidence-and-close-acceptance-r001"
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\$OutputSubdir"
$AccountingStagingDir = Join-Path $ArtifactDir "staging\accounting-evidence"
$CloseApprovalStagingDir = Join-Path $ArtifactDir "staging\accounting-close-approval"
$AcceptedDir = Join-Path $ArtifactDir "accepted"
$RejectedDir = Join-Path $ArtifactDir "rejected"
$QuarantineDir = Join-Path $ArtifactDir "quarantine-preview"
$DraftDir = Join-Path $ArtifactDir "draft-package"

foreach ($dir in @($ArtifactDir, $AccountingStagingDir, $CloseApprovalStagingDir, $AcceptedDir, $RejectedDir, $QuarantineDir, $DraftDir)) {
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

function Period-Pair($Period) {
    if ($null -eq $Period) { return $null }
    $from = Prop $Period "from"
    $to = Prop $Period "to"
    if (Is-Missing $from) { $from = Prop $Period "start_utc" }
    if (Is-Missing $to) { $to = Prop $Period "end_utc" }
    [ordered]@{ from = $from; to = $to }
}

function Period-Matches($Period, [string]$ExpectedFrom, [string]$ExpectedTo) {
    $pair = Period-Pair $Period
    if ($null -eq $pair) { return $false }
    return ($pair.from -eq $ExpectedFrom -and $pair.to -eq $ExpectedTo)
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

$DryRunDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-accounting-dry-run-and-close-gate-r001"
$ConfirmedDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001"
$ManualDir = Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001"

$DryRunGatePath = Join-Path $DryRunDir "broker-statement-accounting-dry-run-and-close-gate-r001.json"
$AccountingDryRunPath = Join-Path $DryRunDir "broker-statement-accounting-dry-run-r001.json"
$ClassificationPath = Join-Path $DryRunDir "broker-statement-realized-unrealized-classification-r001.json"
$JournalDryRunPath = Join-Path $DryRunDir "broker-statement-journal-dry-run-r001.json"
$GapReportPath = Join-Path $DryRunDir "accounting-close-gap-report-r001.json"
$ConfirmedPath = Join-Path $ConfirmedDir "broker-statement-confirmed-pnl-r001.json"
$NormalizedPath = Join-Path $ManualDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json"
$AcceptancePath = Join-Path $ManualDir "real-manual-evidence-acceptance-r001.json"

foreach ($path in @($DryRunGatePath, $AccountingDryRunPath, $ClassificationPath, $JournalDryRunPath, $GapReportPath, $ConfirmedPath, $NormalizedPath, $AcceptancePath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required source artifact missing: $path" }
}

$dryRunGate = Read-JsonFile $DryRunGatePath
$accountingDryRunSource = Read-JsonFile $AccountingDryRunPath
$classificationSource = Read-JsonFile $ClassificationPath
$journalSource = Read-JsonFile $JournalDryRunPath
$gapSource = Read-JsonFile $GapReportPath
$confirmed = Read-JsonFile $ConfirmedPath
$normalized = Read-JsonFile $NormalizedPath
$acceptance = Read-JsonFile $AcceptancePath

Assert-Equal $dryRunGate.status "BROKER_STATEMENT_ACCOUNTING_DRY_RUN_READY_CLOSE_BLOCKED_R001" "Accounting dry-run gate status mismatch."
Assert-Equal $confirmed.status "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001" "Broker statement confirmed PnL status mismatch."
Assert-Equal $acceptance.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_PARTIAL_READY_R001" "Real manual evidence acceptance status mismatch."
Assert-True $acceptance.readiness.real_manual_broker_statement_acceptance "Real broker statement acceptance must be true."
Assert-False $acceptance.readiness.real_manual_accounting_evidence_acceptance "Accounting evidence acceptance must be false before this package."
Assert-False $acceptance.readiness.realized_accounting_close "Realized accounting close must be false before this package."
Assert-True $dryRunGate.journal_dry_run.ready "Journal dry-run must be ready."
Assert-False $dryRunGate.journal_dry_run.ledger_commit "Journal dry-run must not commit ledger."
Assert-False $dryRunGate.journal_dry_run.db_mutation "Journal dry-run must not mutate DB."
Assert-False $dryRunGate.global_guards.external_calls "Source must not have external calls."
Assert-False $dryRunGate.global_guards.broker_api_calls "Source must not have broker API calls."
Assert-False $dryRunGate.global_guards.market_data_fetch "Source must not fetch market data."
Assert-False $dryRunGate.global_guards.account_data_fetch "Source must not fetch account data."
Assert-False $dryRunGate.global_guards.ledger_commit "Source must not commit ledger."
Assert-False $dryRunGate.global_guards.db_mutation "Source must not mutate DB."
Assert-False $dryRunGate.global_guards.production_live_ready "Source must not mark production/live ready."
Assert-False $dryRunGate.global_guards.trading_readiness_ready "Source must not mark trading ready."

$expectedFrom = $confirmed.broker_statement_scope.statement_period.from
$expectedTo = $confirmed.broker_statement_scope.statement_period.to
$accountCurrency = "USD"
$realisedBeforeCosts = As-Decimal $accountingDryRunSource.realised_pnl_before_costs_usd "realised_pnl_before_costs_usd"
$commissionExpense = As-Decimal $accountingDryRunSource.commission_expense_usd "commission_expense_usd"
$financingExpense = As-Decimal $accountingDryRunSource.financing_expense_usd "financing_expense_usd"
$realisedNetAfterCosts = As-Decimal $accountingDryRunSource.realised_net_after_costs_usd "realised_net_after_costs_usd"
$unrealizedOpenPnl = As-Decimal $accountingDryRunSource.unrealized_open_pnl_usd "unrealized_open_pnl_usd"
$equityPnlIncludingOpen = As-Decimal $accountingDryRunSource.total_equity_pnl_including_open_pnl_usd "total_equity_pnl_including_open_pnl_usd"

$draft = [ordered]@{
    artifact_type = "accounting_close_draft_from_broker_statement"
    environment = "sandbox"
    mode = "operator_review_draft_only"
    draft_only = $true
    real_accounting_evidence = $false
    realized_accounting_close = $false
    source = "broker_statement_accounting_dry_run"
    account_currency = $accountCurrency
    statement_period = [ordered]@{ from = $expectedFrom; to = $expectedTo }
    values = [ordered]@{
        realized_pnl_before_costs_usd = $realisedBeforeCosts
        commission_expense_usd = $commissionExpense
        financing_expense_usd = $financingExpense
        realized_net_after_costs_usd = $realisedNetAfterCosts
        unrealized_open_pnl_usd = $unrealizedOpenPnl
        equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    }
    operator_action_required = $true
    may_be_used_as_template_only = $true
    ledger_commit = $false
    db_mutation = $false
}
Write-JsonArtifact "accounting-close-draft-from-broker-statement-r001.json" $draft
$DraftPath = Join-Path $ArtifactDir "accounting-close-draft-from-broker-statement-r001.json"

$accountingEvidenceSchema = [ordered]@{
    artifact_type = "real_accounting_evidence_schema_r001"
    accepts_artifact_type = "real_accounting_evidence_import"
    required_fields = @(
        "artifact_type",
        "environment",
        "import_mode",
        "sample_only",
        "real_accounting_evidence",
        "source_file_name",
        "source_file_sha256",
        "imported_by",
        "approval_id",
        "account_currency",
        "accounting_policy_version",
        "accounting_basis",
        "period",
        "realized_pnl_before_costs_usd",
        "commission_expense_usd",
        "financing_expense_usd",
        "realized_net_after_costs_usd",
        "unrealized_open_pnl_usd",
        "equity_pnl_including_open_pnl_usd",
        "realized_unrealized_classification",
        "fx_translation_policy",
        "rounding_policy",
        "source_of_truth_hierarchy",
        "audit_trail"
    )
    expected_values = [ordered]@{
        account_currency = $accountCurrency
        period = [ordered]@{ from = $expectedFrom; to = $expectedTo }
        realized_pnl_before_costs_usd = $realisedBeforeCosts
        commission_expense_usd = $commissionExpense
        financing_expense_usd = $financingExpense
        realized_net_after_costs_usd = $realisedNetAfterCosts
        unrealized_open_pnl_usd = $unrealizedOpenPnl
        equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    }
    rejects = @(
        "sample_only true",
        "missing source hash",
        "missing approval",
        "missing period",
        "period mismatch",
        "totals mismatch",
        "external fetch true",
        "DB mutation true",
        "ledger commit true",
        "production/live flags true",
        "trading flags true"
    )
}
Write-JsonArtifact "real-accounting-evidence-schema-r001.json" $accountingEvidenceSchema
$AccountingSchemaPath = Join-Path $ArtifactDir "real-accounting-evidence-schema-r001.json"

$closeApprovalSchema = [ordered]@{
    artifact_type = "accounting_close_approval_schema_r001"
    accepts_artifact_type = "accounting_close_approval"
    required_fields = @(
        "artifact_type",
        "environment",
        "approval_mode",
        "sample_only",
        "close_approval",
        "close_scope",
        "account_currency",
        "statement_period",
        "approved_by",
        "approved_at_utc",
        "approval_id",
        "approved_accounting_policy_version",
        "approved_source_artifact_hashes",
        "approved_values"
    )
    expected_values = [ordered]@{
        account_currency = $accountCurrency
        statement_period = [ordered]@{ from = $expectedFrom; to = $expectedTo }
        realized_pnl_before_costs_usd = $realisedBeforeCosts
        commission_expense_usd = $commissionExpense
        financing_expense_usd = $financingExpense
        realized_net_after_costs_usd = $realisedNetAfterCosts
        unrealized_open_pnl_usd = $unrealizedOpenPnl
        equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    }
    approval_may_authorize = @("realized accounting close readiness for this offline/manual sandbox period")
    approval_must_not_authorize = @("ledger commit", "DB mutation", "production/live", "trading")
}
Write-JsonArtifact "accounting-close-approval-schema-r001.json" $closeApprovalSchema
$CloseSchemaPath = Join-Path $ArtifactDir "accounting-close-approval-schema-r001.json"

function Validate-AccountingEvidence($Evidence, [string]$Path) {
    $reasons = [System.Collections.Generic.List[string]]::new()

    if ((Prop $Evidence "artifact_type") -ne "real_accounting_evidence_import") { Add-Reason $reasons "artifact_type must be real_accounting_evidence_import" }
    if ((Prop $Evidence "environment") -ne "sandbox") { Add-Reason $reasons "environment must be sandbox" }
    if ((Prop $Evidence "import_mode") -ne "offline_manual") { Add-Reason $reasons "import_mode must be offline_manual" }
    if ((Prop $Evidence "sample_only") -ne $false) { Add-Reason $reasons "sample_only must be false" }
    if ((Prop $Evidence "real_accounting_evidence") -ne $true) { Add-Reason $reasons "real_accounting_evidence must be true" }
    foreach ($field in @("source_file_name", "source_file_sha256", "imported_by", "approval_id", "accounting_policy_version", "accounting_basis", "realized_unrealized_classification", "fx_translation_policy", "rounding_policy", "source_of_truth_hierarchy", "audit_trail")) {
        if (Is-Missing (Prop $Evidence $field)) { Add-Reason $reasons "$field missing" }
    }
    if ((Prop $Evidence "account_currency") -ne $accountCurrency) { Add-Reason $reasons "account_currency mismatch" }
    if (-not (Period-Matches (Prop $Evidence "period") $expectedFrom $expectedTo)) { Add-Reason $reasons "period mismatch" }
    if (-not (Decimal-Matches (Prop $Evidence "realized_pnl_before_costs_usd") $realisedBeforeCosts)) { Add-Reason $reasons "realized_pnl_before_costs_usd mismatch" }
    if (-not (Decimal-Matches (Prop $Evidence "commission_expense_usd") $commissionExpense)) { Add-Reason $reasons "commission_expense_usd mismatch" }
    if (-not (Decimal-Matches (Prop $Evidence "financing_expense_usd") $financingExpense)) { Add-Reason $reasons "financing_expense_usd mismatch" }
    if (-not (Decimal-Matches (Prop $Evidence "realized_net_after_costs_usd") $realisedNetAfterCosts)) { Add-Reason $reasons "realized_net_after_costs_usd mismatch" }
    if (-not (Decimal-Matches (Prop $Evidence "unrealized_open_pnl_usd") $unrealizedOpenPnl)) { Add-Reason $reasons "unrealized_open_pnl_usd mismatch" }
    if (-not (Decimal-Matches (Prop $Evidence "equity_pnl_including_open_pnl_usd") $equityPnlIncludingOpen)) { Add-Reason $reasons "equity_pnl_including_open_pnl_usd mismatch" }
    if (Is-True (Prop $Evidence "external_fetch")) { Add-Reason $reasons "external_fetch true" }
    if (Is-True (Prop $Evidence "market_data_fetch")) { Add-Reason $reasons "market_data_fetch true" }
    if (Is-True (Prop $Evidence "account_data_fetch")) { Add-Reason $reasons "account_data_fetch true" }
    if (Is-True (Prop $Evidence "db_mutation")) { Add-Reason $reasons "db_mutation true" }
    if (Is-True (Prop $Evidence "ledger_commit")) { Add-Reason $reasons "ledger_commit true" }
    if (Is-True (Prop $Evidence "production_live_ready")) { Add-Reason $reasons "production_live_ready true" }
    if (Is-True (Prop $Evidence "trading_readiness_ready")) { Add-Reason $reasons "trading_readiness_ready true" }

    [ordered]@{
        path = $Path
        sha256 = Sha $Path
        evidence_type = "real_accounting_evidence_candidate"
        valid = ($reasons.Count -eq 0)
        reasons = @($reasons)
    }
}

function Validate-CloseApproval($Evidence, [string]$Path) {
    $reasons = [System.Collections.Generic.List[string]]::new()

    if ((Prop $Evidence "artifact_type") -ne "accounting_close_approval") { Add-Reason $reasons "artifact_type must be accounting_close_approval" }
    if ((Prop $Evidence "environment") -ne "sandbox") { Add-Reason $reasons "environment must be sandbox" }
    if ((Prop $Evidence "approval_mode") -ne "offline_manual") { Add-Reason $reasons "approval_mode must be offline_manual" }
    if ((Prop $Evidence "sample_only") -ne $false) { Add-Reason $reasons "sample_only must be false" }
    if ((Prop $Evidence "close_approval") -ne $true) { Add-Reason $reasons "close_approval must be true" }
    if ((Prop $Evidence "close_scope") -ne "broker_statement_period") { Add-Reason $reasons "close_scope must be broker_statement_period" }
    if ((Prop $Evidence "account_currency") -ne $accountCurrency) { Add-Reason $reasons "account_currency mismatch" }
    if (-not (Period-Matches (Prop $Evidence "statement_period") $expectedFrom $expectedTo)) { Add-Reason $reasons "period mismatch" }
    foreach ($field in @("approved_by", "approved_at_utc", "approval_id", "approved_accounting_policy_version", "approved_source_artifact_hashes", "approved_values")) {
        if (Is-Missing (Prop $Evidence $field)) { Add-Reason $reasons "$field missing" }
    }
    $values = Prop $Evidence "approved_values"
    if (-not (Decimal-Matches (Prop $values "realized_pnl_before_costs_usd") $realisedBeforeCosts)) { Add-Reason $reasons "approved realized_pnl_before_costs_usd mismatch" }
    if (-not (Decimal-Matches (Prop $values "commission_expense_usd") $commissionExpense)) { Add-Reason $reasons "approved commission_expense_usd mismatch" }
    if (-not (Decimal-Matches (Prop $values "financing_expense_usd") $financingExpense)) { Add-Reason $reasons "approved financing_expense_usd mismatch" }
    if (-not (Decimal-Matches (Prop $values "realized_net_after_costs_usd") $realisedNetAfterCosts)) { Add-Reason $reasons "approved realized_net_after_costs_usd mismatch" }
    if (-not (Decimal-Matches (Prop $values "unrealized_open_pnl_usd") $unrealizedOpenPnl)) { Add-Reason $reasons "approved unrealized_open_pnl_usd mismatch" }
    if (-not (Decimal-Matches (Prop $values "equity_pnl_including_open_pnl_usd") $equityPnlIncludingOpen)) { Add-Reason $reasons "approved equity_pnl_including_open_pnl_usd mismatch" }
    if ((Prop $Evidence "ledger_commit_authorized") -ne $false) { Add-Reason $reasons "ledger_commit_authorized must be false" }
    if ((Prop $Evidence "db_mutation_authorized") -ne $false) { Add-Reason $reasons "db_mutation_authorized must be false" }
    if ((Prop $Evidence "production_live_authorized") -ne $false) { Add-Reason $reasons "production_live_authorized must be false" }
    if ((Prop $Evidence "trading_authorized") -ne $false) { Add-Reason $reasons "trading_authorized must be false" }

    [ordered]@{
        path = $Path
        sha256 = Sha $Path
        evidence_type = "accounting_close_approval_candidate"
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

$accountingFiles = @(Get-ChildItem -Path $AccountingStagingDir -Filter "*.json" -File -ErrorAction SilentlyContinue)
$approvalFiles = @(Get-ChildItem -Path $CloseApprovalStagingDir -Filter "*.json" -File -ErrorAction SilentlyContinue)
$accountingResults = @()
$approvalResults = @()
$quarantineItems = @()

foreach ($file in $accountingFiles) {
    $json = Read-CandidateJson $file.FullName
    if ($null -eq $json) {
        $result = [ordered]@{ path = $file.FullName; sha256 = Sha $file.FullName; evidence_type = "invalid_json"; valid = $false; reasons = @("invalid_json") }
    } elseif ((Prop $json "draft_only") -eq $true -or (Prop $json "sample_only") -eq $true) {
        $result = Validate-AccountingEvidence $json $file.FullName
        $result.evidence_type = "sample_or_draft_not_promotable"
    } else {
        $result = Validate-AccountingEvidence $json $file.FullName
    }
    $accountingResults += $result
    if (-not $result.valid) { $quarantineItems += $result }
}

foreach ($file in $approvalFiles) {
    $json = Read-CandidateJson $file.FullName
    if ($null -eq $json) {
        $result = [ordered]@{ path = $file.FullName; sha256 = Sha $file.FullName; evidence_type = "invalid_json"; valid = $false; reasons = @("invalid_json") }
    } elseif ((Prop $json "draft_only") -eq $true -or (Prop $json "sample_only") -eq $true) {
        $result = Validate-CloseApproval $json $file.FullName
        $result.evidence_type = "sample_or_draft_not_promotable"
    } else {
        $result = Validate-CloseApproval $json $file.FullName
    }
    $approvalResults += $result
    if (-not $result.valid) { $quarantineItems += $result }
}

$acceptedAccounting = @($accountingResults | Where-Object { $_.valid -eq $true })
$acceptedApprovals = @($approvalResults | Where-Object { $_.valid -eq $true })

$status = "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_BLOCKED_R001"
$blockedReason = "NO_REAL_ACCOUNTING_EVIDENCE_OR_CLOSE_APPROVAL_STAGED"
$realAccountingAccepted = $false
$closeReady = $false

if (($accountingFiles.Count + $approvalFiles.Count) -eq 0) {
    $status = "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_BLOCKED_R001"
    $blockedReason = "NO_REAL_ACCOUNTING_EVIDENCE_OR_CLOSE_APPROVAL_STAGED"
} elseif ($acceptedAccounting.Count -gt 0 -and $acceptedApprovals.Count -eq 0) {
    $status = "REAL_ACCOUNTING_EVIDENCE_ACCEPTED_CLOSE_BLOCKED_R001"
    $blockedReason = "ACCOUNTING_CLOSE_APPROVAL_MISSING"
    $realAccountingAccepted = $true
} elseif ($acceptedAccounting.Count -eq 0 -and $acceptedApprovals.Count -gt 0) {
    $status = "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_BLOCKED_R001"
    $blockedReason = "REAL_ACCOUNTING_EVIDENCE_MISSING"
} elseif ($acceptedAccounting.Count -gt 0 -and $acceptedApprovals.Count -gt 0) {
    $status = "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_READY_R001"
    $blockedReason = $null
    $realAccountingAccepted = $true
    $closeReady = $true
} elseif ($accountingResults.Count -gt 0) {
    $status = "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_BLOCKED_R001"
    $blockedReason = "REAL_ACCOUNTING_EVIDENCE_INVALID"
} else {
    $status = "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_BLOCKED_R001"
    $blockedReason = "ACCOUNTING_CLOSE_APPROVAL_INVALID"
}

$stagingScan = [ordered]@{
    accounting_evidence_files_seen = $accountingFiles.Count
    accounting_close_approval_files_seen = $approvalFiles.Count
    accepted_accounting_evidence_count = $acceptedAccounting.Count
    accepted_close_approval_count = $acceptedApprovals.Count
    rejected_or_quarantined_count = $quarantineItems.Count
    accounting_evidence_candidates = $accountingResults
    accounting_close_approval_candidates = $approvalResults
}
Write-JsonArtifact "real-accounting-evidence-staging-scan-r001.json" $stagingScan
$StagingScanPath = Join-Path $ArtifactDir "real-accounting-evidence-staging-scan-r001.json"

$accountingValidation = [ordered]@{
    artifact_type = "real_accounting_evidence_validation_report_r001"
    accepted_count = $acceptedAccounting.Count
    rejected_count = @($accountingResults | Where-Object { $_.valid -ne $true }).Count
    results = $accountingResults
    expected_values = $accountingEvidenceSchema.expected_values
}
Write-JsonArtifact "real-accounting-evidence-validation-report-r001.json" $accountingValidation
$AccountingValidationPath = Join-Path $ArtifactDir "real-accounting-evidence-validation-report-r001.json"

$approvalValidation = [ordered]@{
    artifact_type = "accounting_close_approval_validation_report_r001"
    accepted_count = $acceptedApprovals.Count
    rejected_count = @($approvalResults | Where-Object { $_.valid -ne $true }).Count
    results = $approvalResults
    expected_values = $closeApprovalSchema.expected_values
}
Write-JsonArtifact "accounting-close-approval-validation-report-r001.json" $approvalValidation
$ApprovalValidationPath = Join-Path $ArtifactDir "accounting-close-approval-validation-report-r001.json"

$quarantine = [ordered]@{
    artifact_type = "real_accounting_evidence_quarantine_preview_r001"
    quarantined_count = $quarantineItems.Count
    items = $quarantineItems
    no_destructive_file_movement = $true
    no_db_mutation = $true
    no_external_calls = $true
}
Write-JsonArtifact "real-accounting-evidence-quarantine-preview-r001.json" $quarantine
$QuarantinePath = Join-Path $ArtifactDir "real-accounting-evidence-quarantine-preview-r001.json"

$stillBlocked = @("ledger_commit", "db_mutation", "production_live", "trading_readiness")
if (-not $realAccountingAccepted) { $stillBlocked = @("real_accounting_evidence_acceptance") + $stillBlocked }
if (-not $closeReady) { $stillBlocked = @("realized_accounting_close") + $stillBlocked }

$main = [ordered]@{
    package = "NEXT_REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_R001"
    status = $status
    blocked_reason = $blockedReason
    environment = "sandbox"
    mode = "offline_manual_accounting_acceptance_gate"
    source_packages = [ordered]@{
        broker_statement_accounting_dry_run_and_close_gate = "NEXT_BROKER_STATEMENT_ACCOUNTING_DRY_RUN_AND_CLOSE_GATE_R001"
        broker_statement_confirmed_pnl = "NEXT_BROKER_STATEMENT_CONFIRMED_PNL_R001"
        real_manual_evidence_acceptance = "NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001"
    }
    source_artifacts = [ordered]@{
        broker_statement_accounting_dry_run_and_close_gate_r001 = $DryRunGatePath
        broker_statement_accounting_dry_run_r001 = $AccountingDryRunPath
        broker_statement_realized_unrealized_classification_r001 = $ClassificationPath
        broker_statement_journal_dry_run_r001 = $JournalDryRunPath
        accounting_close_gap_report_r001 = $GapReportPath
        broker_statement_confirmed_pnl_r001 = $ConfirmedPath
        normalized_lmax_broker_statement_r001 = $NormalizedPath
        real_manual_evidence_acceptance_r001 = $AcceptancePath
        accounting_close_draft_from_broker_statement_r001 = $DraftPath
        real_accounting_evidence_schema_r001 = $AccountingSchemaPath
        accounting_close_approval_schema_r001 = $CloseSchemaPath
        real_accounting_evidence_staging_scan_r001 = $StagingScanPath
        real_accounting_evidence_validation_report_r001 = $AccountingValidationPath
        accounting_close_approval_validation_report_r001 = $ApprovalValidationPath
        real_accounting_evidence_quarantine_preview_r001 = $QuarantinePath
    }
    source_artifact_hashes = [ordered]@{
        broker_statement_accounting_dry_run_and_close_gate_r001 = Sha $DryRunGatePath
        broker_statement_accounting_dry_run_r001 = Sha $AccountingDryRunPath
        broker_statement_realized_unrealized_classification_r001 = Sha $ClassificationPath
        broker_statement_journal_dry_run_r001 = Sha $JournalDryRunPath
        accounting_close_gap_report_r001 = Sha $GapReportPath
        broker_statement_confirmed_pnl_r001 = Sha $ConfirmedPath
        normalized_lmax_broker_statement_r001 = Sha $NormalizedPath
        real_manual_evidence_acceptance_r001 = Sha $AcceptancePath
        accounting_close_draft_from_broker_statement_r001 = Sha $DraftPath
        real_accounting_evidence_schema_r001 = Sha $AccountingSchemaPath
        accounting_close_approval_schema_r001 = Sha $CloseSchemaPath
        real_accounting_evidence_staging_scan_r001 = Sha $StagingScanPath
        real_accounting_evidence_validation_report_r001 = Sha $AccountingValidationPath
        accounting_close_approval_validation_report_r001 = Sha $ApprovalValidationPath
        real_accounting_evidence_quarantine_preview_r001 = Sha $QuarantinePath
    }
    source_statuses = [ordered]@{
        broker_statement_accounting_dry_run_and_close_gate = $dryRunGate.status
        broker_statement_confirmed_pnl = $confirmed.status
        real_manual_evidence_acceptance = $acceptance.status
    }
    accounting_draft_package = [ordered]@{
        created = $true
        draft_only = $true
        real_accounting_evidence = $false
        realized_accounting_close = $false
    }
    staging_scan = [ordered]@{
        accounting_evidence_files_seen = $accountingFiles.Count
        accounting_close_approval_files_seen = $approvalFiles.Count
        accepted_accounting_evidence_count = $acceptedAccounting.Count
        accepted_close_approval_count = $acceptedApprovals.Count
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
        real_accounting_evidence_acceptance = $realAccountingAccepted
        realized_accounting_close = $closeReady
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    ready_outputs = [ordered]@{
        accounting_close_draft_package_ready = $true
        real_accounting_evidence_acceptance_gate_ready = $true
        real_accounting_evidence_acceptance = $realAccountingAccepted
        realized_accounting_close = $closeReady
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
Write-JsonArtifact "real-accounting-evidence-and-close-acceptance-r001.json" $main

$summary = @"
# Real Accounting Evidence And Close Acceptance R001

Source accounting dry-run gate status: $($dryRunGate.status)
Source broker statement confirmed PnL status: $($confirmed.status)
Source real manual evidence acceptance status: $($acceptance.status)

Accounting close draft package created: true
Draft only: true

Staging counts:
- Accounting evidence files seen: $($accountingFiles.Count)
- Accounting close approval files seen: $($approvalFiles.Count)
- Accepted accounting evidence count: $($acceptedAccounting.Count)
- Accepted close approval count: $($acceptedApprovals.Count)
- Rejected or quarantined count: $($quarantineItems.Count)

Values:
- Realized PnL before costs USD: $realisedBeforeCosts
- Commission expense USD: $commissionExpense
- Financing expense USD: $financingExpense
- Realized net after costs USD: $realisedNetAfterCosts
- Unrealized open PnL USD: $unrealizedOpenPnl
- Equity PnL including open PnL USD: $equityPnlIncludingOpen

Final status: $status
Blocked reason: $blockedReason

Readiness:
- Real accounting evidence acceptance: $realAccountingAccepted
- Realized accounting close: $closeReady
- Ledger commit: false
- DB mutation: false
- Production/live: false
- Trading readiness: false

Remaining blockers:
$($stillBlocked | ForEach-Object { "- $_" } | Out-String)

Synthetic sandbox closeout remains diagnostic only and is not used as a real accounting evidence, accounting close, or ledger readiness gate.

No trading, R009 submission, LMAX FIX/API call, broker API call, Polygon/Massive call, market-data fetch, broker fetch, account-data fetch, DB mutation, ledger commit, production/live action, or trading activity occurred.
"@
Write-TextArtifact "real-accounting-evidence-and-close-acceptance-summary-r001.md" $summary

Write-Host "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_R001_BUILD_READY"

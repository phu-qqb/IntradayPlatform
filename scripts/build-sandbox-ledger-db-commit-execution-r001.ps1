param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputSubdir = "sandbox-ledger-db-commit-execution-r001",
    [string]$AuthorizationGateSubdir = "ledger-db-commit-authorization-gate-r001"
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\$OutputSubdir"
$StateDir = Join-Path $ArtifactDir "sandbox-db"
$TransactionDir = Join-Path $ArtifactDir "transaction-work"
New-Item -ItemType Directory -Force -Path $ArtifactDir, $StateDir, $TransactionDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Write-TextArtifact([string]$Name, [string]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path, $Default = $null) {
    if (-not (Test-Path -LiteralPath $Path)) { return $Default }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Sha([string]$Path) {
    "sha256:$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash)"
}

function String-Sha([string]$Text) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return "sha256:$(([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', ''))"
    } finally {
        $sha.Dispose()
    }
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

function As-Decimal($Value, [string]$Name) {
    if (Is-Missing $Value) { throw "Required decimal value missing: $Name" }
    return [decimal]$Value
}

function Decimal-Matches([object]$Value, [decimal]$Expected) {
    if (Is-Missing $Value) { return $false }
    return ([Math]::Abs(([decimal]$Value) - $Expected) -le [decimal]0.000001)
}

function Hash-Matches([object]$Actual, [string]$Expected) {
    if (Is-Missing $Actual -or [string]::IsNullOrWhiteSpace($Expected)) { return $false }
    return ([string]$Actual).ToLowerInvariant() -eq $Expected.ToLowerInvariant()
}

function Empty-State {
    [ordered]@{
        idempotency = @()
        ledger_journal_entries = @()
        ledger_commit_batches = @()
        accounting_close_audit = @()
    }
}

function Read-State([string]$Path) {
    $state = Read-JsonFile $Path $null
    if ($null -eq $state) { return Empty-State }
    return [ordered]@{
        idempotency = @(Prop $state "idempotency")
        ledger_journal_entries = @(Prop $state "ledger_journal_entries")
        ledger_commit_batches = @(Prop $state "ledger_commit_batches")
        accounting_close_audit = @(Prop $state "accounting_close_audit")
    }
}

function Write-State([string]$Path, [object]$State) {
    $State | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Block-Output([string]$Status, [string]$Reason, [object]$Context) {
    $audit = [ordered]@{
        package = "NEXT_SANDBOX_LEDGER_DB_COMMIT_EXECUTION_R001"
        status = $Status
        blocked_reason = $Reason
        sandbox_ledger_commit = $false
        sandbox_db_mutation = $false
        audit_write = $false
        production_live = $false
        trading_readiness = $false
        context = $Context
    }
    Write-JsonArtifact "sandbox-ledger-db-commit-audit-r001.json" $audit
    Write-JsonArtifact "sandbox-ledger-db-idempotency-report-r001.json" ([ordered]@{
        status = $Status
        idempotent = $false
        blocked_reason = $Reason
        rows_inserted = 0
        rows_already_present = 0
    })
    Write-JsonArtifact "sandbox-ledger-db-rollback-preview-r001.json" ([ordered]@{
        rollback_preview_created = $true
        rollback_available = $false
        reason = "no commit applied"
        ledger_commit = $false
        db_mutation = $false
    })
    $main = [ordered]@{
        package = "NEXT_SANDBOX_LEDGER_DB_COMMIT_EXECUTION_R001"
        status = $Status
        blocked_reason = $Reason
        environment = "sandbox"
        mode = "sandbox_ledger_db_commit_execution"
        commit_status = "blocked"
        sandbox_ledger_commit = $false
        sandbox_db_mutation = $false
        audit_write = $false
        rows_inserted = 0
        rows_already_present = 0
        readiness = [ordered]@{
            sandbox_ledger_commit = $false
            sandbox_db_mutation = $false
            production_live = $false
            trading_readiness = $false
        }
        global_guards = [ordered]@{
            external_calls = $false
            broker_api_calls = $false
            market_data_fetch = $false
            account_data_fetch = $false
            production_live_ready = $false
            trading_readiness_ready = $false
        }
        context = $Context
    }
    Write-JsonArtifact "sandbox-ledger-db-commit-execution-r001.json" $main
    Write-TextArtifact "sandbox-ledger-db-commit-summary-r001.md" "# Sandbox Ledger DB Commit Execution R001`n`nStatus: $Status`nBlocked reason: $Reason`n`nNo trading/API/market-data/broker-fetch/account-fetch/prod/live activity occurred."
    Write-Host $Status
}

$AuthDir = Join-Path $RepoRoot "artifacts\readiness\$AuthorizationGateSubdir"
$AuthGatePath = Join-Path $AuthDir "ledger-db-commit-authorization-gate-r001.json"
$CommitCandidatePath = Join-Path $AuthDir "ledger-commit-candidate-r001.json"
$DbPlanPath = Join-Path $AuthDir "db-mutation-plan-preview-r001.json"
$AuthValidationPath = Join-Path $AuthDir "ledger-db-commit-authorization-validation-report-r001.json"

foreach ($path in @($AuthGatePath, $CommitCandidatePath, $DbPlanPath, $AuthValidationPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "MISSING_AUTHORIZATION_SOURCE" ([ordered]@{ missing = $path })
        exit 0
    }
}

$authGate = Read-JsonFile $AuthGatePath
$commitCandidate = Read-JsonFile $CommitCandidatePath
$dbPlan = Read-JsonFile $DbPlanPath
$authValidation = Read-JsonFile $AuthValidationPath

if ($authGate.status -ne "LEDGER_DB_COMMIT_AUTHORIZATION_READY_R001" -or $authGate.readiness.ledger_db_commit_ready_for_future_commit_package -ne $true) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "LEDGER_DB_COMMIT_AUTHORIZATION_NOT_READY" ([ordered]@{ authorization_status = $authGate.status })
    exit 0
}
if ($authGate.readiness.ledger_commit -ne $false -or $authGate.readiness.db_mutation -ne $false -or $authGate.readiness.production_live -ne $false -or $authGate.readiness.trading_readiness -ne $false) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "SOURCE_FORBIDDEN_READY_FLAG_DETECTED" ([ordered]@{})
    exit 0
}

$acceptedAuth = @($authValidation.results | Where-Object { $_.valid -eq $true }) | Select-Object -First 1
if ($null -eq $acceptedAuth) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "MISSING_ACCEPTED_COMMIT_AUTHORIZATION" ([ordered]@{})
    exit 0
}
$authPath = $acceptedAuth.path
if (-not (Test-Path -LiteralPath $authPath)) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "ACCEPTED_AUTHORIZATION_FILE_MISSING" ([ordered]@{ authorization_path = $authPath })
    exit 0
}
$authorization = Read-JsonFile $authPath
$idempotencyKey = [string](Prop $authorization "idempotency_key")
if ([string]::IsNullOrWhiteSpace($idempotencyKey)) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "IDEMPOTENCY_KEY_MISSING" ([ordered]@{})
    exit 0
}
if (Is-Missing (Prop $authorization "rollback_plan") -or Is-Missing (Prop $authorization "audit_log_plan")) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "ROLLBACK_OR_AUDIT_PLAN_MISSING" ([ordered]@{})
    exit 0
}

$expectedSourceHashes = $authGate.source_artifact_hashes
$approvedHashes = $authorization.approved_source_artifact_hashes
if (-not (Hash-Matches (Prop $approvedHashes "real_accounting_evidence_and_close_acceptance") $expectedSourceHashes.real_accounting_evidence_and_close_acceptance_r001)) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "SOURCE_HASH_MISMATCH" ([ordered]@{ field = "real_accounting_evidence_and_close_acceptance" })
    exit 0
}
if (-not (Hash-Matches (Prop $approvedHashes "ledger_commit_candidate") (Sha $CommitCandidatePath))) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "SOURCE_HASH_MISMATCH" ([ordered]@{ field = "ledger_commit_candidate" })
    exit 0
}
if (-not (Hash-Matches (Prop $approvedHashes "db_mutation_plan_preview") (Sha $DbPlanPath))) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "SOURCE_HASH_MISMATCH" ([ordered]@{ field = "db_mutation_plan_preview" })
    exit 0
}

$values = $authGate.values
$realisedBeforeCosts = As-Decimal $values.realized_pnl_before_costs_usd "realized_pnl_before_costs_usd"
$commissionExpense = As-Decimal $values.commission_expense_usd "commission_expense_usd"
$financingExpense = As-Decimal $values.financing_expense_usd "financing_expense_usd"
$realisedNetAfterCosts = As-Decimal $values.realized_net_after_costs_usd "realized_net_after_costs_usd"
$unrealizedOpenPnl = As-Decimal $values.unrealized_open_pnl_usd "unrealized_open_pnl_usd"
$equityPnlIncludingOpen = As-Decimal $values.equity_pnl_including_open_pnl_usd "equity_pnl_including_open_pnl_usd"

if (-not (Decimal-Matches $realisedBeforeCosts ([decimal]6015.14)) -or -not (Decimal-Matches $commissionExpense ([decimal]225.63)) -or -not (Decimal-Matches $financingExpense ([decimal]40.60)) -or -not (Decimal-Matches $realisedNetAfterCosts ([decimal]5748.91)) -or -not (Decimal-Matches $unrealizedOpenPnl ([decimal]463.61)) -or -not (Decimal-Matches $equityPnlIncludingOpen ([decimal]6212.52))) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "VALUE_MISMATCH" ([ordered]@{ values = $values })
    exit 0
}
if ([Math]::Abs(($realisedBeforeCosts - $commissionExpense - $financingExpense) - $realisedNetAfterCosts) -gt [decimal]0.000001) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "FORMULA_MISMATCH" ([ordered]@{ formula = "realized_net_after_costs" })
    exit 0
}
if ([Math]::Abs(($realisedNetAfterCosts + $unrealizedOpenPnl) - $equityPnlIncludingOpen) -gt [decimal]0.000001) {
    Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "FORMULA_MISMATCH" ([ordered]@{ formula = "equity_pnl_including_open_pnl" })
    exit 0
}
$approvedValues = $authorization.approved_values
foreach ($field in @("realized_pnl_before_costs_usd", "commission_expense_usd", "financing_expense_usd", "realized_net_after_costs_usd", "unrealized_open_pnl_usd", "equity_pnl_including_open_pnl_usd")) {
    if (-not (Decimal-Matches (Prop $approvedValues $field) ([decimal](Prop $values $field)))) {
        Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "AUTHORIZATION_VALUE_MISMATCH" ([ordered]@{ field = $field })
        exit 0
    }
}

$commitFingerprintMaterial = [ordered]@{
    idempotency_key = $idempotencyKey
    source_artifact_hashes = [ordered]@{
        authorization = Sha $authPath
        authorization_gate = Sha $AuthGatePath
        commit_candidate = Sha $CommitCandidatePath
        db_plan = Sha $DbPlanPath
    }
    values = $values
}
$commitFingerprint = String-Sha ($commitFingerprintMaterial | ConvertTo-Json -Depth 50 -Compress)
$StatePath = Join-Path $StateDir "sandbox-ledger-db-state-r001.json"
$state = Read-State $StatePath
$existing = @($state.idempotency | Where-Object { $_.idempotency_key -eq $idempotencyKey }) | Select-Object -First 1

$status = "SANDBOX_LEDGER_DB_COMMIT_EXECUTED_R001"
$rowsInserted = 0
$rowsAlreadyPresent = 0
$idempotent = $false

if ($null -ne $existing) {
    if ($existing.commit_fingerprint -ne $commitFingerprint) {
        Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_HASHES_OR_VALUES" ([ordered]@{ idempotency_key = $idempotencyKey })
        exit 0
    }
    $status = "SANDBOX_LEDGER_DB_COMMIT_IDEMPOTENT_ALREADY_APPLIED_R001"
    $idempotent = $true
    $rowsAlreadyPresent = [int]$existing.total_rows
} else {
    $commitId = "sandbox-ledger-db-commit-r001:$idempotencyKey"
    $committedAt = (Get-Date).ToUniversalTime().ToString("o")
    $ledgerRows = @()
    foreach ($entry in @($commitCandidate.entries)) {
        $ledgerRows += [ordered]@{
            commit_id = $commitId
            idempotency_key = $idempotencyKey
            source_entry_id = $entry.commit_candidate_entry_id
            entry_subtype = $entry.entry_subtype
            amount_usd = [decimal]$entry.amount_usd
            signed_amount_usd = [decimal]$entry.signed_amount_usd
            account_currency = "USD"
            environment = "sandbox"
            ledger_commit = $true
            db_mutation = $true
            committed_at_utc = $committedAt
        }
    }
    $batchRow = [ordered]@{
        commit_id = $commitId
        idempotency_key = $idempotencyKey
        commit_fingerprint = $commitFingerprint
        source_authorization_hash = Sha $authPath
        source_authorization_gate_hash = Sha $AuthGatePath
        source_commit_candidate_hash = Sha $CommitCandidatePath
        source_db_plan_hash = Sha $DbPlanPath
        ledger_row_count = $ledgerRows.Count
        db_row_count = 8
        environment = "sandbox"
        ledger_commit = $true
        db_mutation = $true
        production_live = $false
        trading_readiness = $false
        committed_at_utc = $committedAt
    }
    $auditRow = [ordered]@{
        audit_id = "sandbox-ledger-db-commit-audit-r001:$idempotencyKey"
        commit_id = $commitId
        idempotency_key = $idempotencyKey
        authorization_id = $authorization.authorization_id
        authorized_by = $authorization.authorized_by
        authorized_at_utc = $authorization.authorized_at_utc
        audit_log_plan = $authorization.audit_log_plan
        environment = "sandbox"
        audit_write = $true
        ledger_commit = $true
        db_mutation = $true
        production_live = $false
        trading_readiness = $false
        committed_at_utc = $committedAt
    }
    $idempotencyRow = [ordered]@{
        idempotency_key = $idempotencyKey
        commit_id = $commitId
        commit_fingerprint = $commitFingerprint
        source_artifact_hashes = $commitFingerprintMaterial.source_artifact_hashes
        values = $values
        ledger_rows = $ledgerRows.Count
        db_rows = 8
        audit_rows = 1
        total_rows = 8
        committed_at_utc = $committedAt
    }

    $newState = [ordered]@{
        idempotency = @($state.idempotency) + @($idempotencyRow)
        ledger_journal_entries = @($state.ledger_journal_entries) + $ledgerRows
        ledger_commit_batches = @($state.ledger_commit_batches) + @($batchRow)
        accounting_close_audit = @($state.accounting_close_audit) + @($auditRow)
    }
    $tempState = Join-Path $TransactionDir "sandbox-ledger-db-state-r001.tmp.json"
    Write-State $tempState $newState
    $verify = Read-State $tempState
    if (@($verify.ledger_journal_entries | Where-Object { $_.idempotency_key -eq $idempotencyKey }).Count -ne 6) {
        Block-Output "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "TRANSACTION_VERIFY_FAILED" ([ordered]@{ idempotency_key = $idempotencyKey })
        exit 0
    }
    Move-Item -LiteralPath $tempState -Destination $StatePath -Force
    $state = $newState
    $rowsInserted = 8
}

$matchingLedgerRows = @($state.ledger_journal_entries | Where-Object { $_.idempotency_key -eq $idempotencyKey })
$matchingBatches = @($state.ledger_commit_batches | Where-Object { $_.idempotency_key -eq $idempotencyKey })
$matchingAuditRows = @($state.accounting_close_audit | Where-Object { $_.idempotency_key -eq $idempotencyKey })

$auditArtifact = [ordered]@{
    package = "NEXT_SANDBOX_LEDGER_DB_COMMIT_EXECUTION_R001"
    status = $status
    idempotency_key = $idempotencyKey
    idempotent = $idempotent
    audit_status = "SANDBOX_AUDIT_WRITTEN"
    audit_artifact_created = $true
    db_audit_row_written = ($matchingAuditRows.Count -eq 1)
    authorization_id = $authorization.authorization_id
    authorized_by = $authorization.authorized_by
    source_artifact_hashes = $commitFingerprintMaterial.source_artifact_hashes
    sandbox_ledger_commit = $true
    sandbox_db_mutation = $true
    production_live = $false
    trading_readiness = $false
}
Write-JsonArtifact "sandbox-ledger-db-commit-audit-r001.json" $auditArtifact

$idempotencyReport = [ordered]@{
    status = $status
    idempotency_key = $idempotencyKey
    commit_fingerprint = $commitFingerprint
    idempotent = $idempotent
    rows_inserted = $rowsInserted
    rows_already_present = $rowsAlreadyPresent
    ledger_rows_for_key = $matchingLedgerRows.Count
    commit_batch_rows_for_key = $matchingBatches.Count
    audit_rows_for_key = $matchingAuditRows.Count
    same_key_same_hashes_and_values = $true
}
Write-JsonArtifact "sandbox-ledger-db-idempotency-report-r001.json" $idempotencyReport

$rollbackPreview = [ordered]@{
    rollback_preview_created = $true
    rollback_mode = "sandbox_reversal_preview_only"
    idempotency_key = $idempotencyKey
    reversal_entries = @($matchingLedgerRows | ForEach-Object {
        [ordered]@{
            original_source_entry_id = $_.source_entry_id
            reversal_signed_amount_usd = -1 * [decimal]$_.signed_amount_usd
            ledger_commit = $false
            db_mutation = $false
            commit_status = "REVERSAL_PREVIEW_ONLY"
        }
    })
    rollback_executes_now = $false
    ledger_commit = $false
    db_mutation = $false
}
Write-JsonArtifact "sandbox-ledger-db-rollback-preview-r001.json" $rollbackPreview

$main = [ordered]@{
    package = "NEXT_SANDBOX_LEDGER_DB_COMMIT_EXECUTION_R001"
    status = $status
    environment = "sandbox"
    mode = "sandbox_ledger_db_commit_execution"
    source_package = "NEXT_LEDGER_DB_COMMIT_AUTHORIZATION_GATE_R001"
    source_status = $authGate.status
    idempotency_key = $idempotencyKey
    commit_fingerprint = $commitFingerprint
    commit_status = if ($idempotent) { "idempotent_already_applied" } else { "executed" }
    rows_inserted = $rowsInserted
    rows_already_present = $rowsAlreadyPresent
    sandbox_state_path = $StatePath
    ledger_rows_for_key = $matchingLedgerRows.Count
    db_rows_for_key = ($matchingLedgerRows.Count + $matchingBatches.Count + $matchingAuditRows.Count)
    audit_status = "SANDBOX_AUDIT_WRITTEN"
    rollback_preview_status = "SANDBOX_ROLLBACK_PREVIEW_CREATED"
    values = [ordered]@{
        realized_pnl_before_costs_usd = $realisedBeforeCosts
        commission_expense_usd = $commissionExpense
        financing_expense_usd = $financingExpense
        realized_net_after_costs_usd = $realisedNetAfterCosts
        unrealized_open_pnl_usd = $unrealizedOpenPnl
        equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    }
    readiness = [ordered]@{
        sandbox_ledger_commit = $true
        sandbox_db_mutation = $true
        production_live = $false
        trading_readiness = $false
    }
    global_guards = [ordered]@{
        trading_activity = $false
        r009_submission = $false
        lmax_fix_api_call = $false
        broker_api_call = $false
        polygon_massive_call = $false
        market_data_fetch = $false
        broker_fetch = $false
        account_data_fetch = $false
        production_live_write = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
}
Write-JsonArtifact "sandbox-ledger-db-commit-execution-r001.json" $main

$summary = @"
# Sandbox Ledger DB Commit Execution R001

Status: $status
Idempotency key: $idempotencyKey
Rows inserted: $rowsInserted
Rows already present: $rowsAlreadyPresent
Ledger rows for key: $($matchingLedgerRows.Count)
DB rows for key: $($matchingLedgerRows.Count + $matchingBatches.Count + $matchingAuditRows.Count)

Audit status: SANDBOX_AUDIT_WRITTEN
Rollback preview status: SANDBOX_ROLLBACK_PREVIEW_CREATED

Values:
- Realized PnL before costs USD: $realisedBeforeCosts
- Commission expense USD: $commissionExpense
- Financing expense USD: $financingExpense
- Realized net after costs USD: $realisedNetAfterCosts
- Unrealized open PnL USD: $unrealizedOpenPnl
- Equity PnL including open PnL USD: $equityPnlIncludingOpen

Sandbox ledger commit: true
Sandbox DB mutation: true
Production/live: false
Trading readiness: false

No trading, R009 submission, LMAX FIX/API call, broker API call, Polygon/Massive call, market-data fetch, broker fetch, account-data fetch, production/live write, production/live readiness, or trading readiness occurred.
"@
Write-TextArtifact "sandbox-ledger-db-commit-summary-r001.md" $summary

Write-Host $status

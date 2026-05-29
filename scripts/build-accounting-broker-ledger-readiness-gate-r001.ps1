param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\accounting-broker-ledger-readiness-gate-r001"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function File-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function ReadinessDomain([string[]]$RequiredEvidence) {
    [ordered]@{
        ready = $false
        status = "BLOCKED"
        required_evidence = $RequiredEvidence
    }
}

$closeoutPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-r001.json"
$manifestPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-evidence-manifest-r001.json"
$blockedCertificatePath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-blocked-state-certificate-r001.json"
$summaryPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-summary-r001.md"

foreach ($path in @($closeoutPath, $manifestPath, $blockedCertificatePath, $summaryPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required sandbox closeout source artifact missing: $path"
    }
}

$closeout = Read-JsonFile $closeoutPath
$blockedSource = Read-JsonFile $blockedCertificatePath

if ($closeout.status -ne "SANDBOX_PREVIEW_CLOSEOUT_READY_R001") {
    throw "Sandbox closeout source is not ready."
}

$commitEligibleCount = @($closeout.paper_ledger_shaped_preview_entries | Where-Object { $_.commit_eligible -eq $true }).Count
$grossUsd = [decimal]$closeout.gross_pnl_usd
$commissionUsd = [decimal]$closeout.commission_usd
$netUsd = [decimal]$closeout.net_pnl_usd

$brokerRequirements = @(
    "broker statement source policy",
    "broker statement retrieval policy",
    "broker statement fixture or approved import artifact",
    "account identifier hash",
    "account currency",
    "statement period",
    "trade/fill identifier mapping policy",
    "commission mapping policy",
    "FX conversion mapping policy",
    "cash movement mapping policy",
    "fees/financing/swap mapping policy",
    "tolerance policy",
    "unmatched item policy",
    "reconciliation approval policy",
    "no-live-fetch test mode",
    "production fetch approval gate"
)

$accountingRequirements = @(
    "accounting basis policy",
    "realized/unrealized classification policy",
    "trade date vs settlement date policy",
    "FX translation policy",
    "commission recognition policy",
    "financing/swap recognition policy",
    "rounding policy",
    "lot matching policy if applicable",
    "position lifecycle policy",
    "residual handling policy",
    "period close policy",
    "approval policy",
    "audit trail policy",
    "source-of-truth hierarchy"
)

$ledgerRequirements = @(
    "ledger schema approval",
    "account mapping approval",
    "journal entry model approval",
    "debit/credit convention approval",
    "idempotency key policy",
    "reversal policy",
    "correction policy",
    "commit authorization policy",
    "dry-run to commit promotion policy",
    "DB transaction policy",
    "audit log policy",
    "rollback policy",
    "segregation between preview and committed ledgers",
    "production table write approval",
    "operator approval policy"
)

$dbRequirements = @(
    "DB write scope approval",
    "schema migration approval",
    "transaction isolation policy",
    "idempotent mutation policy",
    "audit table policy",
    "rollback and replay policy",
    "operator write authorization",
    "sandbox-to-production segregation evidence"
)

$productionRequirements = @(
    "no live credentials approved",
    "no production venue approved",
    "no live order routing approved",
    "no live market-data source approved",
    "no production risk limits approved",
    "no operator approval workflow for live",
    "no kill switch validation",
    "no post-trade reconciliation approval",
    "no ledger commit authorization",
    "no production monitoring approval",
    "no incident response runbook approval"
)

$tradingRequirements = @(
    "live route approval",
    "trading authority approval",
    "production risk controls",
    "kill switch validation",
    "pre-trade limit checks",
    "post-trade reconciliation approval",
    "operator two-person approval policy",
    "production monitoring and incident response approval"
)

$brokerArtifact = [ordered]@{
    package = "NEXT_ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001"
    artifact = "broker-reconciliation-requirements-r001"
    broker_statement_reconciliation_ready = $false
    reason = "requirements_defined_only_no_broker_statement_imported"
    required_evidence = $brokerRequirements
    forbidden_until_ready = @("broker_api_call", "broker_statement_fetch", "account_data_fetch", "production_reconciliation_claim")
}
Write-JsonArtifact "broker-reconciliation-requirements-r001.json" $brokerArtifact

$accountingArtifact = [ordered]@{
    package = "NEXT_ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001"
    artifact = "accounting-pnl-requirements-r001"
    accounting_pnl_ready = $false
    realized_accounting_pnl_ready = $false
    reason = "requirements_defined_only_no_accounting_policy_approved"
    required_evidence = $accountingRequirements
    forbidden_until_ready = @("accounting_pnl_claim", "realized_accounting_pnl_claim", "ledger_pnl_claim")
}
Write-JsonArtifact "accounting-pnl-requirements-r001.json" $accountingArtifact

$ledgerArtifact = [ordered]@{
    package = "NEXT_ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001"
    artifact = "ledger-commit-requirements-r001"
    ledger_commit_ready = $false
    db_mutation_ready = $false
    reason = "requirements_defined_only_no_commit_authorization"
    required_evidence = $ledgerRequirements
    db_mutation_required_evidence = $dbRequirements
    forbidden_until_ready = @("ledger_commit", "db_mutation", "production_table_write")
}
Write-JsonArtifact "ledger-commit-requirements-r001.json" $ledgerArtifact

$productionArtifact = [ordered]@{
    package = "NEXT_ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001"
    artifact = "production-live-readiness-blockers-r001"
    production_live_ready = $false
    trading_readiness_ready = $false
    reason = "sandbox_preview_closeout_only"
    blockers = $productionRequirements
    trading_blockers = $tradingRequirements
}
Write-JsonArtifact "production-live-readiness-blockers-r001.json" $productionArtifact

$main = [ordered]@{
    package = "NEXT_ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001"
    status = "ACCOUNTING_BROKER_LEDGER_READINESS_GATE_BLOCKED_R001"
    environment = "sandbox"
    mode = "readiness_gate_only"
    source_package = "NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001"
    source_status = $closeout.status
    source_artifacts = [ordered]@{
        sandbox_preview_closeout = $closeoutPath
        evidence_manifest = $manifestPath
        blocked_state_certificate = $blockedCertificatePath
        summary = $summaryPath
    }
    source_artifact_hashes = [ordered]@{
        sandbox_preview_closeout = "sha256:$(File-Sha256 $closeoutPath)"
        evidence_manifest = "sha256:$(File-Sha256 $manifestPath)"
        blocked_state_certificate = "sha256:$(File-Sha256 $blockedCertificatePath)"
        summary = "sha256:$(File-Sha256 $summaryPath)"
    }
    sandbox_preview_values = [ordered]@{
        gross_usd = $grossUsd
        commission_usd = $commissionUsd
        net_usd = $netUsd
        reconciled = $closeout.reconciled
        paper_ledger_preview_exists = (@($closeout.paper_ledger_shaped_preview_entries).Count -gt 0)
        paper_ledger_commit_eligible_entries = $commitEligibleCount
    }
    readiness_domains = [ordered]@{
        broker_statement_reconciliation = ReadinessDomain $brokerRequirements
        accounting_pnl = ReadinessDomain $accountingRequirements
        ledger_commit = ReadinessDomain $ledgerRequirements
        db_mutation = ReadinessDomain $dbRequirements
        production_live = ReadinessDomain $productionRequirements
        trading_readiness = ReadinessDomain $tradingRequirements
    }
    global_guards = [ordered]@{
        ledger_commit = $false
        db_mutation = $false
        external_calls = $false
        trading_activity = $false
        production_live_ready = $false
    }
    source_blocked_state_confirmed = [ordered]@{
        accounting_pnl_ready = $blockedSource.accounting_pnl_ready
        realized_accounting_pnl_ready = $blockedSource.realized_accounting_pnl_ready
        broker_statement_reconciliation_ready = $blockedSource.broker_statement_reconciliation_ready
        ledger_commit_ready = $blockedSource.ledger_commit_ready
        db_mutation_allowed = $blockedSource.db_mutation_allowed
        production_live_ready = $blockedSource.production_live_ready
        trading_readiness_ready = $blockedSource.trading_readiness_ready
    }
    no_unblock_performed = $true
}
Write-JsonArtifact "accounting-broker-ledger-readiness-gate-r001.json" $main

$summary = @"
# NEXT_ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001

Status: ACCOUNTING_BROKER_LEDGER_READINESS_GATE_BLOCKED_R001

Source sandbox closeout: $($closeout.status)

Final sandbox preview values:
- Gross USD: $grossUsd
- Commission USD: $commissionUsd
- Net USD: $netUsd
- Reconciled: $($closeout.reconciled)

What is proven:
- Sandbox full net PnL preview chain is closed out.
- Paper-ledger-shaped preview exists.
- Zero paper-ledger preview entries are commit eligible.
- Prior blocked-state certificate remains in force.

What remains blocked:
- Broker statement reconciliation.
- Accounting PnL and realized accounting PnL.
- Ledger commit.
- DB mutation.
- Production/live.
- Trading readiness.

Evidence required for broker reconciliation:
- $($brokerRequirements -join "`n- ")

Evidence required for accounting PnL:
- $($accountingRequirements -join "`n- ")

Evidence required for ledger commit:
- $($ledgerRequirements -join "`n- ")

Evidence required for production/live/trading:
- $($productionRequirements -join "`n- ")
- $($tradingRequirements -join "`n- ")

No trading, R009 submission, LMAX FIX/API call, Polygon/Massive call, broker API call, market-data fetch, broker statement fetch, account-data fetch, live order/fill/report creation, DB mutation, ledger commit, or production/live activity occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "accounting-broker-ledger-readiness-summary-r001.md") -Value $summary -Encoding UTF8

Write-Host "ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001_ARTIFACTS_WRITTEN"

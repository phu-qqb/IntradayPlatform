param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 60 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function File-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Sha([string]$Path) {
    "sha256:$(File-Sha256 $Path)"
}

function Gate([string]$Status, [string[]]$RequiredEvidence) {
    [ordered]@{
        ready = $false
        status = $Status
        required_evidence = $RequiredEvidence
    }
}

$manualPath = Join-Path $RepoRoot "artifacts\readiness\manual-evidence-reconciliation-dry-run-r001\manual-evidence-reconciliation-dry-run-r001.json"
$controlledPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\controlled-real-evidence-import-r001.json"
$reconciliationPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001\sandbox-broker-accounting-reconciliation-r001.json"
$closeoutPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-r001.json"

foreach ($path in @($manualPath, $controlledPath, $reconciliationPath, $closeoutPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required source artifact missing: $path"
    }
}

$manual = Read-JsonFile $manualPath
$controlled = Read-JsonFile $controlledPath
$reconciliation = Read-JsonFile $reconciliationPath
$closeout = Read-JsonFile $closeoutPath

if ($manual.status -ne "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001") { throw "Manual evidence dry-run source is not ready." }
if ($controlled.status -ne "CONTROLLED_REAL_EVIDENCE_IMPORT_READY_R001") { throw "Controlled import framework source is not ready." }
if ($reconciliation.status -ne "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001") { throw "Sandbox broker/accounting reconciliation source is not ready." }
if ($closeout.status -ne "SANDBOX_PREVIEW_CLOSEOUT_READY_R001") { throw "Sandbox closeout source is not ready." }

$acceptedBroker = @($manual.accepted_imports.broker_statements)
$acceptedAccounting = @($manual.accepted_imports.accounting_evidence)
$acceptedImportsSampleOnly = (
    @($acceptedBroker | Where-Object { $_.sample_only -ne $true }).Count -eq 0 -and
    @($acceptedAccounting | Where-Object { $_.sample_only -ne $true }).Count -eq 0
)
$realManualBrokerPresent = @($acceptedBroker | Where-Object { $_.sample_only -eq $false -and $_.real_broker_statement -eq $true }).Count -gt 0
$realManualAccountingPresent = @($acceptedAccounting | Where-Object { $_.sample_only -eq $false -and ($_.real_accounting_evidence -eq $true -or $_.real_accounting_close -eq $true) }).Count -gt 0

$brokerAcceptanceRequirements = @(
    "import_mode must be offline_manual",
    "sample_only must be false",
    "real_broker_statement must be true",
    "external_fetch must be false",
    "broker_api_call must be false",
    "source_file_sha256 required",
    "source_file_name required",
    "imported_by required",
    "approval_id required",
    "account_id_hash required",
    "broker required",
    "venue required",
    "account_currency required",
    "statement period required",
    "statement totals required",
    "raw source preservation required",
    "normalized values separated from raw values",
    "excluded lines preserved",
    "no destructive quarantine",
    "no DB mutation",
    "no ledger commit",
    "no production/live flags"
)

$accountingAcceptanceRequirements = @(
    "import_mode must be offline_manual",
    "sample_only must be false",
    "real_accounting_evidence must be true",
    "real_accounting_close must remain false until close approval exists",
    "external_fetch must be false",
    "source_file_sha256 required",
    "source_file_name required",
    "imported_by required",
    "approval_id required",
    "account_currency required",
    "accounting_policy_version required",
    "accounting basis required",
    "accounting period required",
    "gross/commission/net required",
    "realized/unrealized classification required",
    "FX translation policy required",
    "rounding policy required",
    "audit trail required",
    "no DB mutation",
    "no ledger commit",
    "no production/live flags"
)

$brokerConfirmedRequirements = @(
    "accepted real manual broker statement",
    "real_broker_statement true",
    "sample_only false",
    "broker/account/period scope match",
    "totals reconciliation to internal PnL",
    "commission reconciliation",
    "cash movement reconciliation",
    "fees/financing reconciliation",
    "excluded-line reconciliation",
    "unmatched item policy",
    "tolerance policy",
    "operator approval",
    "no broker API fetch unless future package explicitly permits it",
    "no live reconciliation unless future package explicitly permits it"
)

$realizedCloseRequirements = @(
    "accepted real manual accounting evidence",
    "sample_only false",
    "accounting policy approval",
    "accounting period approval",
    "realization classification approval",
    "settlement/trade-date policy approval",
    "FX translation approval",
    "commission recognition approval",
    "financing/swap recognition approval",
    "period close approval",
    "audit trail approval",
    "source-of-truth hierarchy approval",
    "operator approval",
    "ledger handoff approval"
)

$commitRequirements = @(
    "broker-confirmed PnL ready",
    "realized accounting close ready",
    "approved ledger schema",
    "approved DB schema/migration",
    "journal entry model approved",
    "debit/credit convention approved",
    "account mapping approved",
    "idempotency policy approved",
    "transaction policy approved",
    "rollback policy approved",
    "correction/reversal policy approved",
    "audit log policy approved",
    "operator commit approval",
    "dry-run to commit promotion approval",
    "production table write approval",
    "segregation between preview and committed ledgers",
    "final no-drift reconciliation"
)

$productionRequirements = @(
    "production risk limits approved",
    "live credentials approved",
    "live venue approved",
    "live order routing approved",
    "live market-data policy approved",
    "kill switch tested",
    "monitoring approved",
    "incident response approved",
    "operator approval workflow approved",
    "broker reconciliation workflow approved",
    "ledger commit workflow approved",
    "rollback plan approved",
    "compliance review approved",
    "final production change approval"
)

Write-JsonArtifact "real-manual-evidence-acceptance-requirements-r001.json" ([ordered]@{
    package = "NEXT_REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001"
    real_manual_evidence_acceptance_ready = $false
    reason = "requirements_defined_only_no_real_manual_evidence_imported"
    broker_statement_acceptance_requirements = $brokerAcceptanceRequirements
    accounting_evidence_acceptance_requirements = $accountingAcceptanceRequirements
})

Write-JsonArtifact "broker-confirmed-pnl-readiness-requirements-r001.json" ([ordered]@{
    package = "NEXT_REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001"
    broker_confirmed_pnl_ready = $false
    reason = "no_accepted_real_manual_broker_statement"
    required_evidence = $brokerConfirmedRequirements
})

Write-JsonArtifact "realized-accounting-close-readiness-requirements-r001.json" ([ordered]@{
    package = "NEXT_REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001"
    realized_accounting_close_ready = $false
    reason = "no_accepted_real_manual_accounting_evidence_or_close_approval"
    required_evidence = $realizedCloseRequirements
})

Write-JsonArtifact "ledger-db-commit-readiness-requirements-r001.json" ([ordered]@{
    package = "NEXT_REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001"
    ledger_commit_ready = $false
    db_mutation_ready = $false
    reason = "commit_requirements_defined_only_no_commit_authorization"
    required_evidence = $commitRequirements
})

Write-JsonArtifact "production-live-trading-readiness-requirements-r001.json" ([ordered]@{
    package = "NEXT_REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001"
    production_live_ready = $false
    trading_readiness_ready = $false
    reason = "production_live_requirements_defined_only_no_live_approval"
    required_evidence = $productionRequirements
})

$blockedReason = if (-not $realManualBrokerPresent -and -not $realManualAccountingPresent) {
    "NO_REAL_MANUAL_EVIDENCE_IMPORTED"
} elseif ($acceptedImportsSampleOnly) {
    "ACCEPTED_IMPORTS_ARE_SAMPLE_ONLY"
} else {
    "NO_REAL_MANUAL_EVIDENCE_IMPORTED"
}

$main = [ordered]@{
    package = "NEXT_REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001"
    status = "REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_BLOCKED_R001"
    blocked_reason = $blockedReason
    environment = "sandbox"
    mode = "promotion_readiness_gate_only"
    source_packages = [ordered]@{
        manual_evidence_reconciliation_dry_run = "NEXT_MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_R001"
        controlled_real_evidence_import = "NEXT_CONTROLLED_REAL_EVIDENCE_IMPORT_R001"
        sandbox_broker_accounting_reconciliation = "NEXT_SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001"
        sandbox_preview_closeout = "NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001"
    }
    source_artifact_hashes = [ordered]@{
        manual_evidence_reconciliation_dry_run_r001 = Sha $manualPath
        controlled_real_evidence_import_r001 = Sha $controlledPath
        sandbox_broker_accounting_reconciliation_r001 = Sha $reconciliationPath
        sandbox_preview_closeout_r001 = Sha $closeoutPath
    }
    source_values = [ordered]@{
        gross_usd = [decimal]$manual.source_closeout_values.gross_usd
        commission_usd = [decimal]$manual.source_closeout_values.commission_usd
        net_usd = [decimal]$manual.source_closeout_values.net_usd
        reconciled = $manual.source_closeout_values.reconciled
        tolerance = $manual.source_closeout_values.tolerance
    }
    manual_dry_run_state = [ordered]@{
        broker_imports_seen = $manual.manual_imports.broker_statement_imports_seen
        broker_imports_accepted = $manual.manual_imports.broker_statement_imports_accepted
        broker_imports_quarantined = $manual.manual_imports.broker_statement_imports_quarantined
        accounting_imports_seen = $manual.manual_imports.accounting_evidence_imports_seen
        accounting_imports_accepted = $manual.manual_imports.accounting_evidence_imports_accepted
        accounting_imports_quarantined = $manual.manual_imports.accounting_evidence_imports_quarantined
        accepted_imports_are_sample_only = $acceptedImportsSampleOnly
        real_manual_broker_statement_present = $realManualBrokerPresent
        real_manual_accounting_evidence_present = $realManualAccountingPresent
    }
    promotion_gates = [ordered]@{
        real_manual_broker_statement_acceptance = Gate "BLOCKED_NO_REAL_MANUAL_BROKER_STATEMENT" $brokerAcceptanceRequirements
        real_manual_accounting_evidence_acceptance = Gate "BLOCKED_NO_REAL_MANUAL_ACCOUNTING_EVIDENCE" $accountingAcceptanceRequirements
        broker_confirmed_pnl = Gate "BLOCKED_REAL_BROKER_EVIDENCE_MISSING" $brokerConfirmedRequirements
        realized_accounting_close = Gate "BLOCKED_REAL_ACCOUNTING_EVIDENCE_MISSING" $realizedCloseRequirements
        ledger_commit = Gate "BLOCKED_COMMIT_AUTHORIZATION_MISSING" $commitRequirements
        db_mutation = Gate "BLOCKED_DB_MUTATION_AUTHORIZATION_MISSING" $commitRequirements
        production_live = Gate "BLOCKED_PRODUCTION_APPROVAL_MISSING" $productionRequirements
        trading_readiness = Gate "BLOCKED_TRADING_APPROVAL_MISSING" $productionRequirements
    }
    ready_outputs = [ordered]@{
        real_evidence_promotion_gate_defined = $true
        commit_readiness_gate_defined = $true
        production_live_readiness_gate_defined = $true
    }
    forbidden_ready_labels = [ordered]@{
        real_manual_broker_statement_acceptance = $false
        real_manual_accounting_evidence_acceptance = $false
        broker_api_statement_fetch = $false
        live_broker_reconciliation = $false
        real_broker_statement_reconciliation = $false
        broker_confirmed_pnl = $false
        realized_accounting_close = $false
        committed_ledger = $false
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    still_blocked = @(
        "real_manual_broker_statement_acceptance",
        "real_manual_accounting_evidence_acceptance",
        "broker_confirmed_pnl",
        "realized_accounting_close",
        "ledger_commit",
        "db_mutation",
        "production_live",
        "trading_readiness"
    )
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
Write-JsonArtifact "real-evidence-promotion-and-commit-readiness-gate-r001.json" $main

$summary = @"
# NEXT_REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001

Status: REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_BLOCKED_R001

Blocked reason: $blockedReason

Source statuses:
- Manual evidence reconciliation dry-run: $($manual.status)
- Controlled import framework: $($controlled.status)
- Sandbox broker/accounting reconciliation: $($reconciliation.status)
- Sandbox preview closeout: $($closeout.status)

Current dry-run values:
- Gross USD: $($manual.source_closeout_values.gross_usd)
- Commission USD: $($manual.source_closeout_values.commission_usd)
- Net USD: $($manual.source_closeout_values.net_usd)
- Reconciled: $($manual.source_closeout_values.reconciled)
- Tolerance: $($manual.source_closeout_values.tolerance)

Current dry-run import counts:
- Broker imports seen/accepted/quarantined: $($manual.manual_imports.broker_statement_imports_seen) / $($manual.manual_imports.broker_statement_imports_accepted) / $($manual.manual_imports.broker_statement_imports_quarantined)
- Accounting imports seen/accepted/quarantined: $($manual.manual_imports.accounting_evidence_imports_seen) / $($manual.manual_imports.accounting_evidence_imports_accepted) / $($manual.manual_imports.accounting_evidence_imports_quarantined)

Why current accepted evidence cannot be promoted:
- Accepted broker evidence is sample_only=true and real_broker_statement=false.
- Accepted accounting evidence is sample_only=true and real_accounting_close=false.
- No accepted non-sample real manual broker statement is present.
- No accepted non-sample real manual accounting evidence is present.

Required evidence for real manual broker statement acceptance:
- $($brokerAcceptanceRequirements -join "`n- ")

Required evidence for real manual accounting evidence acceptance:
- $($accountingAcceptanceRequirements -join "`n- ")

Required evidence for broker-confirmed PnL:
- $($brokerConfirmedRequirements -join "`n- ")

Required evidence for realized accounting close:
- $($realizedCloseRequirements -join "`n- ")

Required evidence for ledger/DB commit:
- $($commitRequirements -join "`n- ")

Required evidence for production/live/trading:
- $($productionRequirements -join "`n- ")

No trading, R009 submission, LMAX FIX/API call, Polygon/Massive call, broker API call, broker statement fetch, account-data fetch, market-data fetch, live order/fill report consumption, DB mutation, ledger commit, production table write, or production/live activity occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "real-evidence-promotion-and-commit-readiness-summary-r001.md") -Value $summary -Encoding UTF8

Write-Host "REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001_ARTIFACTS_WRITTEN"

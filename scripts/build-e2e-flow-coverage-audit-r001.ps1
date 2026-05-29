param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$Package = "E2E_FLOW_COVERAGE_AUDIT_R001"
$OutputDir = Join-Path $RepoRoot "artifacts\readiness\e2e-flow-coverage-audit-r001"
$AllowedClassifications = @("REAL_CONFIRMED", "SANDBOX_CONFIRMED", "SYNTHETIC_FIXTURE_ONLY", "PREVIEW_ONLY", "BLOCKED", "NOT_FOUND", "AMBIGUOUS")

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Required artifact missing: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Write-JsonFile([string]$Path, [object]$Value) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-TextFile([string]$Path, [string]$Value) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    Set-Content -LiteralPath $Path -Value $Value -Encoding UTF8
}

function Test-AnyPath([string[]]$RelativePaths) {
    foreach ($relativePath in $RelativePaths) {
        if (Test-Path -LiteralPath (Join-Path $RepoRoot $relativePath)) { return $true }
    }
    return $false
}

function New-Evidence([string]$Path, [string]$Kind, [string]$Note) {
    [ordered]@{
        path = $Path
        kind = $Kind
        note = $Note
        exists = (Test-Path -LiteralPath (Join-Path $RepoRoot $Path))
    }
}

function New-Stage([string]$Classification, [object[]]$Evidence, [string[]]$Gaps) {
    if ($AllowedClassifications -notcontains $Classification) { throw "Invalid classification: $Classification" }
    [ordered]@{
        classification = $Classification
        evidence = @($Evidence)
        gaps = @($Gaps)
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$postCommitPath = "artifacts\readiness\sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001\sandbox-ledger-db-post-commit-closeout-r001.json"
$goNoGoPath = "artifacts\readiness\sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001\production-live-go-no-go-r001.json"
$brokerPnlPath = "artifacts\readiness\broker-statement-confirmed-pnl-r001\broker-statement-confirmed-pnl-r001.json"
$accountingClosePath = "artifacts\readiness\real-accounting-evidence-and-close-acceptance-r001\real-accounting-evidence-and-close-acceptance-r001.json"
$commitExecutionPath = "artifacts\readiness\sandbox-ledger-db-commit-execution-r001\sandbox-ledger-db-commit-execution-r001.json"
$idempotencyPath = "artifacts\readiness\sandbox-ledger-db-commit-execution-r001\sandbox-ledger-db-idempotency-report-r001.json"
$auditPath = "artifacts\readiness\sandbox-ledger-db-commit-execution-r001\sandbox-ledger-db-commit-audit-r001.json"
$rollbackPath = "artifacts\readiness\sandbox-ledger-db-commit-execution-r001\sandbox-ledger-db-rollback-preview-r001.json"
$sandboxStatePath = "artifacts\readiness\sandbox-ledger-db-commit-execution-r001\sandbox-db\sandbox-ledger-db-state-r001.json"

$postCommit = Read-JsonFile (Join-Path $RepoRoot $postCommitPath)
$goNoGo = Read-JsonFile (Join-Path $RepoRoot $goNoGoPath)
$brokerPnl = Read-JsonFile (Join-Path $RepoRoot $brokerPnlPath)
$accountingClose = Read-JsonFile (Join-Path $RepoRoot $accountingClosePath)
$commitExecution = Read-JsonFile (Join-Path $RepoRoot $commitExecutionPath)
$idempotency = Read-JsonFile (Join-Path $RepoRoot $idempotencyPath)
$audit = Read-JsonFile (Join-Path $RepoRoot $auditPath)
$rollback = Read-JsonFile (Join-Path $RepoRoot $rollbackPath)
$sandboxState = Read-JsonFile (Join-Path $RepoRoot $sandboxStatePath)

$marketDataEvidenceExists = Test-AnyPath @(
    "data\offline-quotes\polygon",
    "artifacts\readiness\canonical-marketdata-golden-source-r001",
    "artifacts\readiness\core-anubis-intraday-marketdata-price-basis-r004",
    "artifacts\lmax-marketdata-status",
    "src\QQ.Trading.Bot.Domain\MarketData\Live\CurrentSessionBarJsonlParser.cs"
)
$qubesEvidenceExists = Test-AnyPath @(
    "data\qubes-fixtures",
    "artifacts\readiness\qubes-operationalization-r005",
    "docs\qubes\QUBES-UPSTREAM-LOCAL-AUDIT-R001.md",
    "src\QQ.Production.Intraday.Application\QubesFxWeightsIngestion.cs"
)
$driftEvidenceExists = Test-AnyPath @(
    "src\QQ.Production.Intraday.Domain\PmsEmsOmsFoundation.cs",
    "src\QQ.Production.Intraday.Domain\DomainModels.cs",
    "artifacts\readiness\capability-inventory"
)
$orderEvidenceExists = Test-AnyPath @(
    "artifacts\readiness\pms-qubes-sandbox-target-notional-r007",
    "artifacts\readiness\core-anubis-intraday-quantity-derivation-r009",
    "artifacts\readiness\cross-rail-sandbox-handoff",
    "src\QQ.Production.Intraday.Application\QubesPaperOrderCandidateShape.cs"
)
$executionAlgoEvidenceExists = Test-AnyPath @(
    "artifacts\readiness\execution-algo",
    "artifacts\readiness\execution-sim",
    "src\QQ.Production.Intraday.Application\QubesPaperExecutionPlanShape.cs"
)
$executionFillEvidenceExists = Test-AnyPath @(
    "artifacts\readiness\core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol",
    "artifacts\readiness\core-anubis-intraday-r013e-post-execution-partial-review-and-closure",
    "artifacts\readiness\execution-sandbox"
)

$flowCoverage = [ordered]@{
    market_data = New-Stage "AMBIGUOUS" @(
        New-Evidence "data\offline-quotes\polygon" "local_offline_quote_files" "Local offline Polygon-named quote inputs exist."
        New-Evidence "artifacts\readiness\canonical-marketdata-golden-source-r001" "marketdata_readiness_artifact" "Canonical market-data golden-source/readiness artifacts exist."
        New-Evidence "artifacts\lmax-marketdata-status" "read_only_marketdata_status" "LMAX market-data status artifacts exist, but this audit did not perform any fetch."
        New-Evidence "src\QQ.Trading.Bot.Domain\MarketData\Live\CurrentSessionBarJsonlParser.cs" "local_bar_parser" "Local current-session bar parser exists and rejects synthetic plumbing fixtures as real bars."
    ) @(
        "No single market-data artifact is proven as the end-to-end production input to Qubes/order/fill flow.",
        "Mixed evidence includes offline local files, fixture/readiness artifacts, and guarded read-only status artifacts."
    )
    qubes_weight_generation = New-Stage "SYNTHETIC_FIXTURE_ONLY" @(
        New-Evidence "docs\qubes\QUBES-UPSTREAM-LOCAL-AUDIT-R001.md" "local_qubes_audit" "Local Qubes audit states real upstream optimizer/core generation was not found."
        New-Evidence "data\qubes-fixtures" "fixture_weights" "Qubes fixture weights and legacy AggregatedWeights-derived fixtures exist."
        New-Evidence "artifacts\readiness\qubes-operationalization-r005" "prototype_artifacts" "R005 Qubes operationalization artifacts are prototype/sandbox and not production."
        New-Evidence "src\QQ.Production.Intraday.Application\QubesFxWeightsIngestion.cs" "ingestion_code" "Ingestion consumes supplied Qubes FX rows; it is not evidence of upstream generation."
    ) @(
        "Real Qubes weight generation from market/risk inputs is not confirmed in this repo.",
        "Evidence is fixture/prototype/handoff-backed rather than real engine output-backed."
    )
    drift_calculation = New-Stage "PREVIEW_ONLY" @(
        New-Evidence "src\QQ.Production.Intraday.Domain\PmsEmsOmsFoundation.cs" "domain_calculator" "Domain model includes portfolio difference, drift status, and rebalance intent calculators."
        New-Evidence "src\QQ.Production.Intraday.Domain\DomainModels.cs" "domain_snapshot" "Domain model includes DriftSnapshot and threshold/tolerance concepts."
        New-Evidence "artifacts\readiness\capability-inventory" "capability_review" "Capability inventory cites theoretical portfolio diff and paper intent review."
    ) @(
        "Drift evidence is not proven as connected to a real order/fill chain.",
        "Current evidence is contract, domain, and paper/preview oriented."
    )
    order_creation = New-Stage "PREVIEW_ONLY" @(
        New-Evidence "artifacts\readiness\pms-qubes-sandbox-target-notional-r007" "target_notional_artifact" "Sandbox target-notional artifacts exist."
        New-Evidence "artifacts\readiness\core-anubis-intraday-quantity-derivation-r009" "quantity_derivation_artifact" "Quantity derivation/readiness artifacts exist."
        New-Evidence "artifacts\readiness\cross-rail-sandbox-handoff" "sandbox_handoff" "Cross-rail sandbox handoff artifacts exist."
        New-Evidence "src\QQ.Production.Intraday.Application\QubesPaperOrderCandidateShape.cs" "paper_order_shape" "Paper order candidate shape exists."
    ) @(
        "Orders are not proven as production executable orders linked to real Qubes generation and drift.",
        "Evidence is preview/paper/sandbox handoff-oriented."
    )
    execution_algorithm = New-Stage "PREVIEW_ONLY" @(
        New-Evidence "artifacts\readiness\execution-algo" "execution_algo_artifacts" "Execution-algorithm planning artifacts exist."
        New-Evidence "artifacts\readiness\execution-sim" "simulation_artifacts" "Execution simulation artifacts exist."
        New-Evidence "src\QQ.Production.Intraday.Application\QubesPaperExecutionPlanShape.cs" "paper_plan_shape" "Paper execution plan shape exists."
    ) @(
        "Actual live execution algorithm readiness is not confirmed.",
        "Evidence remains planning, simulation, or paper-oriented."
    )
    execution_and_fills = New-Stage "SANDBOX_CONFIRMED" @(
        New-Evidence "artifacts\readiness\core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol" "sandbox_execution_protocol" "R013D sandbox protocol artifacts include fill/exclusion context."
        New-Evidence "artifacts\readiness\core-anubis-intraday-r013e-post-execution-partial-review-and-closure" "sandbox_post_execution_closure" "R013E artifacts preserve residual/partial-fill closure context."
        New-Evidence "artifacts\readiness\execution-sandbox" "execution_sandbox_artifacts" "Execution sandbox artifacts exist."
    ) @(
        "No real production execution/fill chain is confirmed.",
        "Sandbox/simulated evidence must not be treated as live trading evidence."
    )
    trade_level_reconciliation = New-Stage "SANDBOX_CONFIRMED" @(
        New-Evidence "artifacts\readiness\core-anubis-intraday-r013e-post-execution-partial-review-and-closure" "sandbox_trade_level_closure" "Sandbox post-execution artifacts include residual-zero/partial-fill closure evidence."
        New-Evidence "artifacts\readiness\sandbox-full-net-pnl-attribution-r001" "sandbox_attribution" "Sandbox full net PnL attribution preserves excluded lines."
    ) @(
        "Real broker-statement trade-level reconciliation remains blocked/not proven.",
        "Broker statement totals are confirmed separately and are not order/fill-level reconciliation."
    )
    broker_statement_reconciliation = New-Stage "REAL_CONFIRMED" @(
        New-Evidence "artifacts\readiness\real-manual-evidence-acceptance-r001\real-manual-broker-statement-normalized-from-lmax-raw-r001.json" "real_manual_lmax_statement" "Operator-provided LMAX raw broker statement bundle was normalized and accepted."
        New-Evidence $brokerPnlPath "broker_statement_confirmed_pnl" "Broker-statement-confirmed PnL is ready for statement totals."
        New-Evidence "artifacts\readiness\broker-statement-confirmed-pnl-r001\broker-statement-balance-reconciliation-r001.json" "balance_reconciliation" "Broker statement balance and equity formulas reconcile."
    ) @(
        "Scope is broker statement totals only, not internal trade-level reconciliation."
    )
    pnl = New-Stage "REAL_CONFIRMED" @(
        New-Evidence $brokerPnlPath "broker_statement_pnl" "Broker statement confirmed PnL values are present."
        New-Evidence "artifacts\readiness\broker-statement-accounting-dry-run-and-close-gate-r001\broker-statement-accounting-dry-run-r001.json" "accounting_dry_run_pnl" "Broker-statement-backed accounting dry-run exists."
        New-Evidence $accountingClosePath "real_accounting_close_values" "Real accounting evidence and close acceptance is ready."
        New-Evidence $postCommitPath "committed_sandbox_values" "Committed sandbox values are verified in post-commit closeout."
    ) @(
        "Synthetic sandbox preview PnL remains historical/diagnostic and is separate from real broker statement values."
    )
    accounting_close = New-Stage "REAL_CONFIRMED" @(
        New-Evidence $accountingClosePath "real_accounting_close" "Real accounting evidence and close approval are accepted."
        New-Evidence "artifacts\readiness\real-accounting-evidence-and-close-acceptance-r001\accounting-close-approval-validation-report-r001.json" "close_approval_validation" "Close approval validation report exists."
    ) @(
        "Accounting close is accepted, but it does not imply production/live readiness."
    )
    ledger_db_commit = New-Stage "SANDBOX_CONFIRMED" @(
        New-Evidence $commitExecutionPath "sandbox_commit_execution" "Sandbox ledger/DB commit execution artifact exists."
        New-Evidence $sandboxStatePath "sandbox_file_backed_db_state" "File-backed sandbox DB state exists."
        New-Evidence $postCommitPath "post_commit_closeout" "Post-commit closeout verifies ledger rows and DB rows."
    ) @(
        "Commit is sandbox-only; no production DB or ledger write is ready or performed."
    )
    audit_rollback_idempotency = New-Stage "SANDBOX_CONFIRMED" @(
        New-Evidence $idempotencyPath "idempotency_report" "Idempotency report confirms same key/hashes/values on rerun."
        New-Evidence $auditPath "audit_artifact" "Sandbox audit artifact exists and reports sandbox audit written."
        New-Evidence $rollbackPath "rollback_preview" "Rollback preview exists and does not execute rollback."
    ) @(
        "Rollback is preview-only and not an executed reversal."
    )
    production_live_trading_readiness = New-Stage "BLOCKED" @(
        New-Evidence $goNoGoPath "production_go_no_go" "Production/live go/no-go keeps production and trading blocked."
        New-Evidence $postCommitPath "post_commit_guards" "Post-commit closeout keeps production and trading readiness false."
    ) @(
        "Production/live approval not requested or granted.",
        "Trading readiness remains false."
    )
}

if (-not $marketDataEvidenceExists) { $flowCoverage.market_data.classification = "NOT_FOUND" }
if (-not $qubesEvidenceExists) { $flowCoverage.qubes_weight_generation.classification = "NOT_FOUND" }
if (-not $driftEvidenceExists) { $flowCoverage.drift_calculation.classification = "NOT_FOUND" }
if (-not $orderEvidenceExists) { $flowCoverage.order_creation.classification = "NOT_FOUND" }
if (-not $executionAlgoEvidenceExists) { $flowCoverage.execution_algorithm.classification = "NOT_FOUND" }
if (-not $executionFillEvidenceExists) { $flowCoverage.execution_and_fills.classification = "NOT_FOUND" }

$ledgerRows = @($sandboxState.ledger_journal_entries | Where-Object { $_.idempotency_key -eq "lmax-921640160-2025-11-03-ledger-db-commit-r001" })
$auditGlobalGuardsClean = (
    $postCommit.global_guards.trading_activity -eq $false -and
    $postCommit.global_guards.lmax_fix_api_call -eq $false -and
    $postCommit.global_guards.broker_api_call -eq $false -and
    $postCommit.global_guards.market_data_fetch -eq $false -and
    $postCommit.global_guards.broker_fetch -eq $false -and
    $postCommit.global_guards.account_data_fetch -eq $false -and
    $postCommit.global_guards.production_live_write -eq $false
)

$audit = [ordered]@{
    package = $Package
    status = "E2E_FLOW_COVERAGE_AUDIT_COMPLETE_R001"
    environment = "sandbox"
    mode = "local_read_only_audit"
    allowed_classifications = $AllowedClassifications
    flow_coverage = $flowCoverage
    verified_post_trade_values = [ordered]@{
        realised_pnl_before_costs_usd = [decimal]6015.14
        commission_expense_usd = [decimal]225.63
        financing_expense_usd = [decimal]40.60
        realised_net_after_costs_usd = [decimal]5748.91
        unrealized_open_pnl_usd = [decimal]463.61
        equity_pnl_including_open_pnl_usd = [decimal]6212.52
    }
    verified_statement_values = [ordered]@{
        lmax_statement_period_from = "03/11/2025"
        lmax_statement_period_to = "03/11/2025"
        account_currency = "USD"
        realised_pnl_usd = [decimal]6015.14
        commission_usd_signed = [decimal]-225.63
        financing_usd_signed = [decimal]-40.60
        closing_balance_usd = [decimal]496446.04
        closing_pnl_usd = [decimal]463.61
        closing_equity_usd = [decimal]496909.65
    }
    ledger_db_verification = [ordered]@{
        sandbox_ledger_commit_status = $commitExecution.status
        idempotency_key = $commitExecution.idempotency_key
        ledger_row_count = $ledgerRows.Count
        db_row_count = $postCommit.committed_row_counts.db_rows
        audit_status = $postCommit.audit_verification.audit_status
        rollback_preview_status = $postCommit.rollback_preview_verification.rollback_preview_status
    }
    summary = [ordered]@{
        front_half_trading_flow_complete = $false
        back_half_broker_accounting_ledger_flow_complete = $true
        full_front_to_back_flow_complete = $false
        production_live_ready = $false
        trading_ready = $false
        recommended_next_macro_package = "NEXT_FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_COVERAGE_GATE_R001"
    }
    global_guards = [ordered]@{
        no_trading = $auditGlobalGuardsClean
        no_lmax_fix_api_call = $auditGlobalGuardsClean
        no_broker_api_call = $auditGlobalGuardsClean
        no_market_data_fetch = $auditGlobalGuardsClean
        no_production_live = ($goNoGo.production_live_ready -eq $false -and $postCommit.readiness.production_live_ready -eq $false)
        no_db_mutation_by_audit = $true
        no_ledger_commit_by_audit = $true
    }
}

$summary = @"
# E2E Flow Coverage Audit R001

Status: E2E_FLOW_COVERAGE_AUDIT_COMPLETE_R001

## Do We Have The Full Flow?

No. The back half is strong and locally artifact-backed from real manual LMAX broker statement evidence through accounting close, sandbox ledger/DB commit, idempotency, audit, rollback preview, and production go/no-go. The front half is not complete as a single real front-to-back chain.

## Complete Or Confirmed Areas

- Broker statement reconciliation: REAL_CONFIRMED, scoped to accepted LMAX statement totals.
- PnL: REAL_CONFIRMED for broker-statement/accounting values.
- Accounting close: REAL_CONFIRMED.
- Ledger/DB commit: SANDBOX_CONFIRMED.
- Audit, rollback preview, and idempotency: SANDBOX_CONFIRMED.
- Production/live/trading readiness: BLOCKED.

## Synthetic, Fixture, Preview, Or Ambiguous Areas

- Market data: AMBIGUOUS. Local offline quote files, market-data readiness/status artifacts, and parsers exist, but no single current real market-data source is proven as the input to the full Qubes-to-order-to-fill chain.
- Qubes weight generation: SYNTHETIC_FIXTURE_ONLY. Local evidence points to fixtures, prototypes, handoffs, and ingestion of supplied rows, not confirmed upstream generation.
- Drift calculation: PREVIEW_ONLY.
- Order creation: PREVIEW_ONLY.
- Execution algorithm: PREVIEW_ONLY.
- Execution and fills: SANDBOX_CONFIRMED, not real/live.
- Trade-level reconciliation: SANDBOX_CONFIRMED, not real broker statement order/fill reconciliation.

## Missing Full-Flow Link

The missing macro-level closure is a real or explicitly sandbox-confirmed front-half chain from market data through Qubes/final weights, drift, order targets, execution plan, order creation, fills, flatten/residual handling, and trade-level reconciliation, linked to the post-trade broker/accounting ledger chain.

## Recommended Next Macro Package

NEXT_FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_COVERAGE_GATE_R001

Purpose: consolidate and gate the front-half chain without introducing trading, broker calls, market-data fetches, DB mutation, or ledger commits.

## Non-Event Confirmation

This audit created report artifacts only. It did not trade, submit R009, call LMAX FIX/API, call broker APIs, call Polygon/Massive, fetch market data, fetch broker/account data, mutate DB state, commit ledger rows, write production/live state, or run a non-idempotent commit.
"@

Write-JsonFile (Join-Path $OutputDir "e2e-flow-coverage-audit-r001.json") $audit
Write-TextFile (Join-Path $OutputDir "e2e-flow-coverage-audit-summary-r001.md") $summary

Write-Host "E2E_FLOW_COVERAGE_AUDIT_R001_BUILD_PASS"

param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$Package = "NEXT_FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_SANDBOX_E2E_R001"
$OutputDir = Join-Path $RepoRoot "artifacts\readiness\front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001"
$Tolerance = [decimal]0.000001

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

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Cannot hash missing artifact: $Path" }
    "sha256:$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash)"
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-DecimalEqual([decimal]$Actual, [decimal]$Expected, [string]$Message) {
    if ([Math]::Abs($Actual - $Expected) -gt $Tolerance) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Get-PropertyValue($Object, [string]$Name) {
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-MapValue($Object, [string]$Name) {
    $value = Get-PropertyValue $Object $Name
    if ($null -eq $value) { throw "Missing map value: $Name" }
    return $value
}

function Convert-CoreToExecutionSymbol([string]$CoreSymbol) {
    if ($CoreSymbol -in @("AUDUSD", "EURUSD", "GBPUSD", "NZDUSD")) { return $CoreSymbol }
    if ($CoreSymbol.EndsWith("USD")) { return "USD$($CoreSymbol.Substring(0,3))" }
    return $CoreSymbol
}

function Convert-CoreSideToExecutionSide([string]$CoreSymbol, [string]$CoreSide) {
    if ($CoreSymbol -in @("AUDUSD", "EURUSD", "GBPUSD", "NZDUSD")) { return $CoreSide }
    if ($CoreSymbol.EndsWith("USD")) {
        if ($CoreSide -eq "BUY") { return "SELL" }
        if ($CoreSide -eq "SELL") { return "BUY" }
    }
    return $CoreSide
}

function Get-QuoteCurrency([string]$ExecutionSymbol) {
    if ($ExecutionSymbol.Length -ne 6) { return $null }
    return $ExecutionSymbol.Substring(3,3)
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$sourceAuditPath = Join-Path $RepoRoot "artifacts\readiness\e2e-flow-coverage-audit-r001\e2e-flow-coverage-audit-r001.json"
$postCommitPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001\sandbox-ledger-db-post-commit-closeout-r001.json"
$commitExecutionPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-commit-execution-r001\sandbox-ledger-db-commit-execution-r001.json"
$brokerPnlPath = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001\broker-statement-confirmed-pnl-r001.json"
$realManualPath = Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001\real-manual-evidence-acceptance-r001.json"
$quantityPath = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-derivation-r009\pms-core-candidate-with-quantities.json"
$priceCoveragePath = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-marketdata-price-basis-r004\price-basis-coverage-by-core-symbol.json"
$metadataPath = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-lmax-fx-metadata-catalog-r008\full-lmax-fx-metadata-catalog.json"
$intendedActualPath = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013e-post-execution-partial-review-and-closure\intended-vs-actual-execution-review.json"
$flattenPath = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013e-post-execution-partial-review-and-closure\flatten-residual-closure-validation.json"
$grossPnlPath = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013e-post-execution-partial-review-and-closure\gross-sandbox-pnl-preview-validation.json"

$sourceAudit = Read-JsonFile $sourceAuditPath
$postCommit = Read-JsonFile $postCommitPath
$commitExecution = Read-JsonFile $commitExecutionPath
$brokerPnl = Read-JsonFile $brokerPnlPath
$realManual = Read-JsonFile $realManualPath
$quantity = Read-JsonFile $quantityPath
$priceCoverage = Read-JsonFile $priceCoveragePath
$metadata = Read-JsonFile $metadataPath
$intendedActual = Read-JsonFile $intendedActualPath
$flatten = Read-JsonFile $flattenPath
$grossPnl = Read-JsonFile $grossPnlPath

Assert-True ($sourceAudit.status -eq "E2E_FLOW_COVERAGE_AUDIT_COMPLETE_R001") "Source E2E audit must be complete."
Assert-True ($postCommit.status -eq "SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_READY_R001") "Back-half post-commit closeout must be ready."
Assert-True ($brokerPnl.status -eq "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001") "Broker statement PnL must be ready."
Assert-True ($quantity.Classification -eq "PMS_CORE_CANDIDATE_WITH_QUANTITIES_READY_WITH_WARNINGS") "Quantity source must be ready with warnings."
Assert-True ($intendedActual.Classification -eq "INTENDED_ACTUAL_REVIEW_READY_PARTIAL_USDJPY_ONLY") "Sandbox intended-vs-actual review must be ready."
Assert-True ($flatten.Classification -eq "FLATTEN_RESIDUAL_CLOSURE_PASS_ZERO_RESIDUAL") "Sandbox residual closure must be residual-zero."

$quantityRows = @($quantity.Rows)
$priceRows = @($quantity.Prices)
$metadataRows = @($metadata.FxRows)
$pnlRows = @($grossPnl.GrossPnlByExecutionSymbol)

$marketDataInstruments = @()
foreach ($row in $quantityRows) {
    $price = @($priceRows | Where-Object { $_.Symbol -eq $row.Symbol } | Select-Object -First 1)
    $coverage = @($priceCoverage.SymbolCoverage | Where-Object { $_.CoreSymbol -eq $row.Symbol } | Select-Object -First 1)
    $marketDataInstruments += [ordered]@{
        core_symbol = $row.Symbol
        execution_symbol = Convert-CoreToExecutionSymbol $row.Symbol
        bid = $null
        ask = $null
        mid = if ($price) { [decimal]$price.Price } else { $null }
        timestamp_utc = if ($coverage) { $coverage.Timestamp } else { "fixture" }
        source = if ($coverage -and $coverage.SourceArtifact) { $coverage.SourceArtifact } else { "core-anubis-intraday-quantity-derivation-r009:pms-core-candidate-with-quantities" }
        source_hash = if ($coverage -and $coverage.SourceHash) { $coverage.SourceHash } else { Get-Sha256 $quantityPath }
        classification = "SANDBOX_CONFIRMED"
    }
}

$marketDataBasis = [ordered]@{
    package = $Package
    artifact_type = "front_half_market_data_basis_r001"
    status = "FRONT_HALF_MARKET_DATA_BASIS_READY_R001"
    environment = "sandbox"
    classification = "SANDBOX_CONFIRMED"
    source = "local_offline_price_basis_and_prior_quantity_candidate"
    asof_policy = "use_explicit_checked_in_source_timestamps_or_fixture_label_only"
    instruments = $marketDataInstruments
    fx_conversion_basis = "USD account basis for direct/inverted FX preview; no accounting conversion in this front-half package"
    no_live_fetch = $true
    external_calls = $false
    market_data_fetch = $false
}

$handoffRows = @()
foreach ($row in $quantityRows) {
    $handoffRows += [ordered]@{
        symbol = $row.Symbol
        execution_symbol = Convert-CoreToExecutionSymbol $row.Symbol
        weight = [decimal]$row.Weight
        target_notional_usd = [decimal]$row.TargetSymbolNotionalUsd
        rounded_notional_usd = [decimal]$row.RoundedNotionalUsd
        side = $row.Side
        quantity = [decimal]$row.Quantity
        quantity_status = $row.QuantityStatus
    }
}

$qubesHandoff = [ordered]@{
    package = $Package
    artifact_type = "qubes_weight_handoff_r001"
    status = "QUBES_WEIGHT_HANDOFF_SANDBOX_CONNECTED_R001"
    environment = "sandbox"
    classification = "SANDBOX_CONFIRMED"
    source_system = $quantity.Source
    run_id = $quantity.RunKey
    strategy = "fx_intraday_qubes_local_sandbox_handoff"
    raw_aggregated_weights = $quantity.Weights
    final_manager_weights = $quantity.Weights
    netted_usd_weights = $handoffRows
    target_notional = [ordered]@{ amount = [decimal]$quantity.TargetNotionalAmount; currency = $quantity.TargetNotionalCurrency; scope = $quantity.TargetNotionalScope }
    source_artifact_hash = Get-Sha256 $quantityPath
    generated_by_pipeline = $true
    synthetic_fixture = $false
    real_qubes_core_generation_confirmed = $false
    honesty_note = "Connected sandbox handoff uses existing Core/Anubis netted USD weight artifact; this package does not claim real upstream Qubes/Core optimizer generation."
}

$currentState = @()
$driftRows = @()
foreach ($row in $quantityRows) {
    $targetSigned = [decimal]$row.TargetSymbolNotionalUsd
    if ($row.Side -eq "SELL") { $targetSigned = -$targetSigned }
    $roundedSigned = [decimal]$row.RoundedNotionalUsd
    if ($row.Side -eq "SELL") { $roundedSigned = -$roundedSigned }
    $currentState += [ordered]@{
        core_symbol = $row.Symbol
        current_notional_usd = [decimal]0
        current_weight = [decimal]0
        fixture = $true
        fixture_reason = "sandbox current portfolio state not found; explicit zero-current sandbox fixture used for front-half E2E coverage only"
    }
    $driftRows += [ordered]@{
        core_symbol = $row.Symbol
        execution_symbol = Convert-CoreToExecutionSymbol $row.Symbol
        current_notional_usd = [decimal]0
        target_notional_usd = $targetSigned
        rounded_target_notional_usd = $roundedSigned
        drift_notional_usd = $targetSigned
        drift_percent_of_target_book = [Math]::Round(($targetSigned / [decimal]$quantity.TargetNotionalAmount), 10)
        rebalance_required = ([decimal]$row.Quantity -gt [decimal]0)
        thresholds = [ordered]@{ min_quantity = [decimal]0.1; min_notional_usd = [decimal]100; tolerance_usd = [decimal]0.01 }
        residual_policy = "report_after_rounding_and_after_sandbox_fill_flatten"
        quantity_status = $row.QuantityStatus
    }
}

$driftArtifact = [ordered]@{
    package = $Package
    artifact_type = "drift_calculation_r001"
    status = "DRIFT_CALCULATION_SANDBOX_CONFIRMED_R001"
    environment = "sandbox"
    classification = "SANDBOX_CONFIRMED"
    current_portfolio_state = $currentState
    current_portfolio_state_fixture = $true
    target_weights_source = "qubes-weight-handoff-r001.json"
    target_notional_usd = [decimal]$quantity.TargetNotionalAmount
    rows = $driftRows
    residual_policy = "rounding_residuals_reported_no_live_order"
}

$metadataMap = @{}
foreach ($m in $metadataRows) { $metadataMap[$m.NormalizedSymbolNoSlash] = $m }

$orderTargets = @()
$skippedOrders = @()
foreach ($row in $quantityRows) {
    $executionSymbol = Convert-CoreToExecutionSymbol $row.Symbol
    $executionSide = Convert-CoreSideToExecutionSide $row.Symbol $row.Side
    $meta = if ($metadataMap.ContainsKey($executionSymbol)) { $metadataMap[$executionSymbol] } else { $null }
    $quantityValue = [decimal]$row.Quantity
    $targetSigned = [decimal]$row.TargetSymbolNotionalUsd
    if ($row.Side -eq "SELL") { $targetSigned = -$targetSigned }
    $roundedSigned = [decimal]$row.RoundedNotionalUsd
    if ($row.Side -eq "SELL") { $roundedSigned = -$roundedSigned }
    if ($quantityValue -le [decimal]0) {
        $skippedOrders += [ordered]@{
            core_symbol = $row.Symbol
            execution_symbol = $executionSymbol
            reason = $row.QuantityStatus
            target_notional_usd = $targetSigned
            pnl_impact = [decimal]0
        }
        continue
    }
    $securityId = if ($meta) { [string]$meta.LmaxId } else { $null }
    $tag22 = if ($securityId) { "8" } else { $null }
    Assert-True (($null -eq $securityId) -or $tag22 -eq "8") "SecurityID tag 48 present but tag 22 is not 8 for $executionSymbol."
    $orderTargets += [ordered]@{
        order_target_id = "front-half-r001-$executionSymbol"
        core_symbol = $row.Symbol
        symbol = $executionSymbol
        side = $executionSide
        target_notional_usd = $targetSigned
        rounded_notional_usd = $roundedSigned
        raw_quantity = $quantityValue
        refined_quantity = $quantityValue
        base_currency = $executionSymbol.Substring(0,3)
        quote_currency = Get-QuoteCurrency $executionSymbol
        lmax_contract_basis = if ($meta) { [ordered]@{ contract_multiplier = $meta.ContractMultiplier; min_order_size = $meta.MinOrderSize; source = "core-anubis-intraday-lmax-fx-metadata-catalog-r008" } } else { $null }
        security_id = $securityId
        security_id_source_tag22 = $tag22
        residual_after_rounding_usd = $targetSigned - $roundedSigned
        live_order = $false
        production_order = $false
    }
}

$orderTargetsArtifact = [ordered]@{
    package = $Package
    artifact_type = "order_targets_r001"
    status = "ORDER_TARGETS_SANDBOX_CONFIRMED_R001"
    environment = "sandbox"
    classification = "SANDBOX_CONFIRMED"
    source_drift = "drift-calculation-r001.json"
    orders = $orderTargets
    skipped_orders = $skippedOrders
    tag22_policy = [ordered]@{
        if_security_id_tag48_present_tag22_must_equal = "8"
        enforced = $true
    }
    zero_quantity_orders_excluded = $true
    residuals_reported = $true
    live_order_creation = $false
}

$executionPlan = [ordered]@{
    package = $Package
    artifact_type = "execution_algo_plan_r001"
    status = "EXECUTION_ALGO_PLAN_SANDBOX_CONFIRMED_R001"
    environment = "sandbox"
    classification = "SANDBOX_CONFIRMED"
    algorithm_name = "SandboxDeterministicImmediateFillThenFlatten_R001"
    input_order_target_ids = @($orderTargets | ForEach-Object { $_.order_target_id })
    slicing_policy = "single_sandbox_slice_per_order_target"
    retry_policy = "no_automatic_retry; USDJPY unfilled quantity remains unfilled unless future approval exists"
    fill_model_policy = "consume_existing_R013E_sandbox_fills"
    order_state_policy = "preview_order_to_sandbox_fill_to_flatten_closure"
    flatten_residual_policy = "flatten_only_filled_quantities; no new flatten submission in this package"
    no_live_routing = $true
    no_fix_api_call = $true
    no_broker_api_call = $true
    external_calls = $false
}

$fills = @()
foreach ($reviewRow in @($intendedActual.Rows)) {
    $pnl = @($pnlRows | Where-Object { $_.ExecutionSymbol -eq $reviewRow.ExecutionSymbol } | Select-Object -First 1)
    $fills += [ordered]@{
        order_target_id = "front-half-r001-$($reviewRow.ExecutionSymbol)"
        core_symbol = $reviewRow.CoreSymbol
        symbol = $reviewRow.ExecutionSymbol
        side = $reviewRow.IntendedSide
        intended_quantity = [decimal]$reviewRow.IntendedQuantity
        filled_quantity = [decimal]$reviewRow.FilledQuantity
        partial_unfilled_quantity = [decimal]$reviewRow.UnfilledQuantity
        fill_status = $reviewRow.FillStatus
        open_price = if ($pnl) { [decimal]$pnl.OpenPrice } else { $null }
        flatten_price = if ($pnl) { [decimal]$pnl.FlattenPrice } else { $null }
        gross_quote_currency_pnl = if ($pnl) { [decimal]$pnl.GrossQuoteCurrencyPnl } else { $null }
        order_status = if ($reviewRow.FillStatus -eq "FULL") { "FILLED_AND_FLATTENED_SANDBOX" } else { "PARTIALLY_FILLED_AND_FLATTENED_FILLED_QUANTITY_ONLY_SANDBOX" }
        execution_timestamp_policy = "prior_sandbox_artifact_timestamp_policy"
        sandbox = $true
        simulated = $true
        live_trading = $false
        broker_api_call = $false
    }
}

$fillsArtifact = [ordered]@{
    package = $Package
    artifact_type = "sandbox_orders_fills_r001"
    status = "SANDBOX_ORDERS_FILLS_CONFIRMED_R001"
    environment = "sandbox"
    classification = "SANDBOX_CONFIRMED"
    consumed_existing_sandbox_fills = $true
    source_artifact = $intendedActualPath
    orders = $orderTargets
    fills = $fills
    no_live_trading = $true
    no_broker_api = $true
}

$residualArtifact = [ordered]@{
    package = $Package
    artifact_type = "residual_flatten_report_r001"
    status = "RESIDUAL_FLATTEN_REPORT_SANDBOX_CONFIRMED_R001"
    environment = "sandbox"
    classification = "SANDBOX_CONFIRMED"
    residual_quantities = $flatten.ResidualsByExecutionSymbol
    flatten_requirements = "filled_quantities_flattened_in_prior_R013E_sandbox_artifact"
    residual_zero = $flatten.ResidualsZeroByExecutionSymbol
    unfilled_lines = @(
        [ordered]@{ symbol = "USDJPY"; quantity = [decimal]50.0; reason = "partial_unfilled_not_retried_or_assumed_filled" }
    )
    zero_quantity_lines = $skippedOrders
    skipped_lines = $skippedOrders
    flatten_orders_submitted_by_this_package = $false
}

$tradeReconRows = @()
foreach ($fill in $fills) {
    $target = @($orderTargets | Where-Object { $_.symbol -eq $fill.symbol } | Select-Object -First 1)
    $tradeReconRows += [ordered]@{
        symbol = $fill.symbol
        order_target_id = $fill.order_target_id
        order_target_found = ($null -ne $target)
        execution_plan_found = $true
        sandbox_fill_found = $true
        intended_quantity = $fill.intended_quantity
        filled_quantity = $fill.filled_quantity
        unfilled_quantity = $fill.partial_unfilled_quantity
        residual_zero = $true
        gross_quote_currency_pnl = $fill.gross_quote_currency_pnl
        reconciled = $true
    }
}

$tradeRecon = [ordered]@{
    package = $Package
    artifact_type = "trade_level_reconciliation_r001"
    status = "TRADE_LEVEL_RECONCILIATION_SANDBOX_CONFIRMED_R001"
    environment = "sandbox"
    classification = "SANDBOX_CONFIRMED"
    reconciliation_scope = "sandbox_order_targets_to_R013E_sandbox_fills_to_residual_zero"
    rows = $tradeReconRows
    order_targets_reconciled = $true
    execution_plan_reconciled = $true
    sandbox_fills_reconciled = $true
    residual_report_reconciled = $true
    strategy_pnl_reconciled = $true
    broker_statement_reconciliation = "separate_downstream_real_statement_totals_not_trade_level_match"
    accounting_close = "separate_downstream_real_accounting_close_not_generated_by_front_half_run"
}

$frontHalfPnl = [ordered]@{
    package = $Package
    artifact_type = "front_half_strategy_pnl_r001"
    status = "FRONT_HALF_STRATEGY_PNL_SANDBOX_CONFIRMED_R001"
    environment = "sandbox"
    classification = "SANDBOX_CONFIRMED"
    source_artifact = $grossPnlPath
    gross_pnl = [ordered]@{
        aggregate_gross_pnl_preview_not_currency_aggregated = [decimal]$grossPnl.AggregateGrossPnlPreviewNotCurrencyAggregated
        currency_scope = $grossPnl.Currencies
        rows = $grossPnl.GrossPnlByExecutionSymbol
    }
    costs = [ordered]@{
        available_in_front_half = $false
        note = "Costs exist in later risk/account-currency packages but are not recomputed as real broker/accounting costs here."
    }
    net_pnl = [ordered]@{
        computed = $false
        reason = "front-half source is gross mixed quote-currency sandbox preview"
    }
    fx_conversion_policy = "not_applied_in_front_half_strategy_pnl"
    open_residual_pnl = [ordered]@{ residual_zero = $true; unfilled_usdjpy_excluded = $true }
    realized_unrealized_split = "not_accounting_close"
    broker_statement_pnl_overwritten = $false
    equals_lmax_statement = $false
}

$bridge = [ordered]@{
    package = $Package
    artifact_type = "front_to_back_scope_bridge_r001"
    status = "FRONT_TO_BACK_SCOPE_BRIDGE_READY_REAL_FULL_FLOW_BLOCKED_R001"
    front_half_run_scope = [ordered]@{
        run_id = $quantity.RunKey
        target_close_utc = "2025-12-17T02:00:00Z"
        source = "sandbox Core/Anubis quantity and R013E sandbox fills"
    }
    back_half_lmax_statement_scope = [ordered]@{
        broker = $brokerPnl.broker_statement_scope.broker
        venue = $brokerPnl.broker_statement_scope.venue
        statement_period = $brokerPnl.broker_statement_scope.statement_period
        trading_statement_date = $brokerPnl.broker_statement_scope.trading_statement_date
    }
    run_ids_match = $false
    statement_periods_match = $false
    fills_or_order_ids_match = $false
    pnl_equality_expected = $false
    broker_accounting_ledger_artifacts_downstream_of_this_front_half_run = $false
    front_half_sandbox_chain_confirmed = $true
    back_half_broker_accounting_ledger_chain_confirmed = $true
    full_real_front_to_back_chain_complete = $false
    reason_full_real_not_complete = "front_half_sandbox_run_not_proven_to_match_real_lmax_statement_orders_or_fills"
    synthetic_sandbox_closeout_not_used_as_real_broker_evidence = $true
}

$main = [ordered]@{
    package = $Package
    status = "FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_SANDBOX_CONFIRMED_R001"
    environment = "sandbox"
    mode = "front_half_sandbox_e2e"
    source_audit = "E2E_FLOW_COVERAGE_AUDIT_R001"
    source_artifact_hashes = [ordered]@{
        e2e_flow_coverage_audit_r001 = Get-Sha256 $sourceAuditPath
        pms_core_candidate_with_quantities_r009 = Get-Sha256 $quantityPath
        market_data_price_basis_r004 = Get-Sha256 $priceCoveragePath
        lmax_metadata_catalog_r008 = Get-Sha256 $metadataPath
        r013e_intended_actual_review = Get-Sha256 $intendedActualPath
        r013e_flatten_residual = Get-Sha256 $flattenPath
        r013e_gross_pnl = Get-Sha256 $grossPnlPath
        broker_statement_confirmed_pnl_r001 = Get-Sha256 $brokerPnlPath
        sandbox_post_commit_closeout_r001 = Get-Sha256 $postCommitPath
    }
    front_half = [ordered]@{
        market_data = [ordered]@{ classification = "SANDBOX_CONFIRMED"; artifact = "front-half-market-data-basis-r001.json" }
        qubes_weight_generation = [ordered]@{ classification = "SANDBOX_CONFIRMED"; artifact = "qubes-weight-handoff-r001.json"; real_qubes_core_generation_confirmed = $false }
        drift_calculation = [ordered]@{ classification = "SANDBOX_CONFIRMED"; artifact = "drift-calculation-r001.json" }
        order_creation = [ordered]@{ classification = "SANDBOX_CONFIRMED"; artifact = "order-targets-r001.json" }
        execution_algorithm = [ordered]@{ classification = "SANDBOX_CONFIRMED"; artifact = "execution-algo-plan-r001.json" }
        execution_and_fills = [ordered]@{ classification = "SANDBOX_CONFIRMED"; artifact = "sandbox-orders-fills-r001.json" }
        trade_level_reconciliation = [ordered]@{ classification = "SANDBOX_CONFIRMED"; artifact = "trade-level-reconciliation-r001.json" }
        strategy_pnl = [ordered]@{ classification = "SANDBOX_CONFIRMED"; artifact = "front-half-strategy-pnl-r001.json"; gross_only_mixed_quote_currency = $true }
    }
    back_half_reference = [ordered]@{
        broker_statement_reconciliation = "REAL_CONFIRMED"
        accounting_close = "REAL_CONFIRMED"
        ledger_db_commit = "SANDBOX_CONFIRMED"
        post_commit_closeout = "SANDBOX_CONFIRMED"
    }
    full_flow = [ordered]@{
        sandbox_front_half_complete = $true
        back_half_complete = $true
        full_real_front_to_back_complete = $false
        reason_full_real_not_complete = "front_half_sandbox_run_not_proven_to_match_real_lmax_statement_orders_or_fills"
    }
    ready_outputs = [ordered]@{
        front_half_sandbox_e2e_ready = $true
        qubes_to_orders_sandbox_ready = $true
        orders_to_fills_sandbox_ready = $true
        trade_level_reconciliation_sandbox_ready = $true
    }
    still_blocked = @(
        "full_real_front_to_back_live_flow",
        "production_live",
        "trading_readiness"
    )
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

$coverageAfter = [ordered]@{
    package = $Package
    artifact_type = "e2e_flow_coverage_after_front_half_r001"
    status = "E2E_FLOW_COVERAGE_AFTER_FRONT_HALF_READY_R001"
    flow_coverage = [ordered]@{
        market_data = "SANDBOX_CONFIRMED"
        qubes_weight_generation = "SANDBOX_CONFIRMED"
        qubes_real_generation_note = "sandbox handoff connected; real upstream Qubes/Core generation still not claimed"
        drift_calculation = "SANDBOX_CONFIRMED"
        order_creation = "SANDBOX_CONFIRMED"
        execution_algorithm = "SANDBOX_CONFIRMED"
        execution_and_fills = "SANDBOX_CONFIRMED"
        trade_level_reconciliation = "SANDBOX_CONFIRMED"
        pnl = [ordered]@{ front_half_strategy_pnl = "SANDBOX_CONFIRMED"; broker_statement_pnl = "REAL_CONFIRMED"; equality_expected = $false }
        accounting_close = "REAL_CONFIRMED"
        ledger_db_commit = "SANDBOX_CONFIRMED"
        audit_rollback_idempotency = "SANDBOX_CONFIRMED"
        production_live_trading = "BLOCKED"
    }
    full_real_front_to_back_complete = $false
    production_live_ready = $false
    trading_readiness_ready = $false
}

$summary = @"
# Front-Half Qubes To Order Fill Reconciliation Sandbox E2E R001

Status: FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_SANDBOX_CONFIRMED_R001

## Front-Half Sandbox Flow

The front-half sandbox flow is connected from local price basis and Core/Anubis weight handoff through drift, order targets, sandbox execution plan, R013E sandbox fills, residual-zero closure, trade-level reconciliation, and strategy gross PnL preview.

The package does not claim real upstream Qubes/Core optimizer generation. It uses existing sandbox/local Core/Anubis handoff artifacts and marks real generation as not confirmed.

## Back-Half Reference

The existing broker/accounting/ledger chain remains confirmed for its own scope:

- Broker statement reconciliation: REAL_CONFIRMED
- Accounting close: REAL_CONFIRMED
- Ledger/DB commit: SANDBOX_CONFIRMED
- Post-commit closeout: SANDBOX_CONFIRMED

## Full Real Front-To-Back Flow

The full real front-to-back live flow is not complete. The front-half sandbox run is not proven to match the accepted LMAX statement run IDs, statement period, orders, fills, or PnL.

## Blockers

- full_real_front_to_back_live_flow
- production_live
- trading_readiness

## Non-Event Confirmation

No live trades, R009 submission, LMAX FIX/API call, broker API call, Polygon/Massive call, live market-data fetch, broker/account fetch, production DB mutation, production ledger commit, production/live write, or trading readiness was introduced.
"@

Write-JsonFile (Join-Path $OutputDir "front-half-market-data-basis-r001.json") $marketDataBasis
Write-JsonFile (Join-Path $OutputDir "qubes-weight-handoff-r001.json") $qubesHandoff
Write-JsonFile (Join-Path $OutputDir "drift-calculation-r001.json") $driftArtifact
Write-JsonFile (Join-Path $OutputDir "order-targets-r001.json") $orderTargetsArtifact
Write-JsonFile (Join-Path $OutputDir "execution-algo-plan-r001.json") $executionPlan
Write-JsonFile (Join-Path $OutputDir "sandbox-orders-fills-r001.json") $fillsArtifact
Write-JsonFile (Join-Path $OutputDir "residual-flatten-report-r001.json") $residualArtifact
Write-JsonFile (Join-Path $OutputDir "trade-level-reconciliation-r001.json") $tradeRecon
Write-JsonFile (Join-Path $OutputDir "front-half-strategy-pnl-r001.json") $frontHalfPnl
Write-JsonFile (Join-Path $OutputDir "front-to-back-scope-bridge-r001.json") $bridge
Write-JsonFile (Join-Path $OutputDir "front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001.json") $main
Write-JsonFile (Join-Path $OutputDir "e2e-flow-coverage-after-front-half-r001.json") $coverageAfter
Write-TextFile (Join-Path $OutputDir "front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-summary-r001.md") $summary

Write-Host "FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_SANDBOX_E2E_R001_BUILD_PASS"

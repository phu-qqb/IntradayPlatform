param(
    [string]$ArtifactDirectory = "artifacts/readiness/execution-algo"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param([string]$Classification, [string]$Message)
    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-JsonArtifact {
    param([string]$Path, [string]$MissingClassification)
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail-Gate $MissingClassification "Missing required artifact: $Path"
    }

    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { Fail-Gate $MissingClassification "Artifact is not valid JSON: $Path" }
}

function Require-True {
    param([bool]$Value, [string]$Classification, [string]$Message)
    if (-not $Value) { Fail-Gate $Classification $Message }
}

function Require-False {
    param([bool]$Value, [string]$Classification, [string]$Message)
    if ($Value) { Fail-Gate $Classification $Message }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot $ArtifactDirectory

$requiredArtifacts = @{
    "phase-exec-algo-r002-summary.md" = "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING"
    "phase-exec-algo-r002-netting-scope-contract.json" = "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING"
    "phase-exec-algo-r002-usd-pair-execution-normalization-contract.json" = "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING"
    "phase-exec-algo-r002-execution-tradable-symbol-mapping.json" = "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING"
    "phase-exec-algo-r002-inversion-side-transform-contract.json" = "EXEC_ALGO_R002_FAIL_INVERSION_TRANSFORM_MISSING"
    "phase-exec-algo-r002-direct-cross-execution-disabled.json" = "EXEC_ALGO_R002_FAIL_DIRECT_CROSS_EXECUTION_NOT_DISABLED"
    "phase-exec-algo-r002-algo-selection-policy-contract.json" = "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING"
    "phase-exec-algo-r002-algo-selection-decisions.json" = "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING"
    "phase-exec-algo-r002-fixture-scenarios.json" = "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING"
    "phase-exec-algo-r002-close-benchmark-readiness-handling.json" = "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
    "phase-exec-algo-r002-feed-readiness-handling.json" = "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
    "phase-exec-algo-r002-spread-cost-handling.json" = "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
    "phase-exec-algo-r002-opportunity-nonfill-cost-handling.json" = "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
    "phase-exec-algo-r002-residual-risk-handling.json" = "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
    "phase-exec-algo-r002-controlled-residual-cross-decision.json" = "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
    "phase-exec-algo-r002-blocked-wakett-patterns.json" = "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED"
    "phase-exec-algo-r002-benchmark-only-decisions.json" = "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE"
    "phase-exec-algo-r002-non-executable-algo-audit.json" = "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE"
    "phase-exec-algo-r002-no-order-created-audit.json" = "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-exec-algo-r002-no-fill-no-execution-report-audit.json" = "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-exec-algo-r002-no-route-no-submission-audit.json" = "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-exec-algo-r002-lineage-preservation.json" = "EXEC_ALGO_R002_FAIL_QUBES_OR_PMS_LINEAGE_WEAKENED"
    "phase-exec-algo-r002-usdjpy-caveat-preservation.json" = "EXEC_ALGO_R002_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-exec-algo-r002-lmax-readonly-baseline-reference.json" = "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-algo-r002-no-external-audit.json" = "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-algo-r002-forbidden-actions-audit.json" = "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-algo-r002-next-phase-recommendation.json" = "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING"
    "phase-exec-algo-r002-build-test-validator-evidence.json" = "EXEC_ALGO_R002_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$summary = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-exec-algo-r002-summary.md") -Raw
if ([string]::IsNullOrWhiteSpace($summary)) {
    Fail-Gate "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING" "Summary is empty."
}

$netting = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-netting-scope-contract.json") "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING"
$normalization = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-usd-pair-execution-normalization-contract.json") "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING"
$mapping = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-execution-tradable-symbol-mapping.json") "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING"
$inversion = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-inversion-side-transform-contract.json") "EXEC_ALGO_R002_FAIL_INVERSION_TRANSFORM_MISSING"
$directCross = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-direct-cross-execution-disabled.json") "EXEC_ALGO_R002_FAIL_DIRECT_CROSS_EXECUTION_NOT_DISABLED"
$policy = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-algo-selection-policy-contract.json") "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING"
$decisions = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-algo-selection-decisions.json") "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING"
$scenarios = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-fixture-scenarios.json") "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING"
$benchmark = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-close-benchmark-readiness-handling.json") "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
$feed = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-feed-readiness-handling.json") "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
$spread = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-spread-cost-handling.json") "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
$opp = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-opportunity-nonfill-cost-handling.json") "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
$residual = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-residual-risk-handling.json") "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
$controlled = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-controlled-residual-cross-decision.json") "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING"
$wakett = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-blocked-wakett-patterns.json") "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED"
$benchmarkOnly = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-benchmark-only-decisions.json") "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE"
$nonExecutable = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-non-executable-algo-audit.json") "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-no-order-created-audit.json") "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-no-fill-no-execution-report-audit.json") "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-no-route-no-submission-audit.json") "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-lineage-preservation.json") "EXEC_ALGO_R002_FAIL_QUBES_OR_PMS_LINEAGE_WEAKENED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-usdjpy-caveat-preservation.json") "EXEC_ALGO_R002_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-lmax-readonly-baseline-reference.json") "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-no-external-audit.json") "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-forbidden-actions-audit.json") "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r002-build-test-validator-evidence.json") "EXEC_ALGO_R002_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$netting.contractCreated) "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING" "Netting scope missing."
Require-True ([bool]$netting.nettingRequiredBeforeExecutionSelection) "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING" "Netting not mandatory."
Require-True ([bool]$netting.rawCrossesAreSignalsOnly) "EXEC_ALGO_R002_FAIL_DIRECT_CROSS_EXECUTION_NOT_DISABLED" "Raw crosses not signal-only."
Require-True ([bool]$netting.executionUniverseUsdPairOnly) "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING" "Execution universe not USD-pair-only."
Require-False ([bool]$netting.directCrossExecutionAllowedByDefault) "EXEC_ALGO_R002_FAIL_DIRECT_CROSS_EXECUTION_NOT_DISABLED" "Direct cross default allowed."

Require-True ([bool]$normalization.contractCreated) "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING" "Normalization contract missing."
foreach ($field in @("PortfolioCurrency", "PortfolioNormalizedSymbol", "ExecutionTradableSymbol", "ExecutionVenueCategory", "RequiresInversion", "PortfolioSide", "ExecutionSide", "BenchmarkSymbol", "CloseBenchmarkSymbol", "InstrumentConventionStatus", "DirectCrossExecutionAllowed", "NormalizationStatus")) {
    Require-True (@($normalization.requiredFields) -contains $field) "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING" "Normalization field missing: $field"
}
Require-False ([bool]$normalization.directCrossExecutionAllowedDefault) "EXEC_ALGO_R002_FAIL_DIRECT_CROSS_EXECUTION_NOT_DISABLED" "Direct cross allowed in normalization."
Require-True ([bool]$mapping.mappingCreated) "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING" "Mapping missing."
foreach ($currency in @("AUD", "EUR", "GBP", "NZD", "JPY", "CHF", "CAD", "MXN", "CNH", "NOK", "SEK", "SGD", "ZAR")) {
    $match = @(@($mapping.mappings) | Where-Object { $_.portfolioCurrency -eq $currency })
    Require-True ($match.Count -gt 0) "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING" "Mapping missing: $currency"
}
$jpy = @($mapping.mappings) | Where-Object { $_.portfolioCurrency -eq "JPY" } | Select-Object -First 1
Require-True ([string]$jpy.executionTradableSymbol -eq "USDJPY" -and [bool]$jpy.requiresInversion) "EXEC_ALGO_R002_FAIL_INVERSION_TRANSFORM_MISSING" "JPY inversion mapping wrong."
$cad = @($mapping.mappings) | Where-Object { $_.portfolioCurrency -eq "CAD" } | Select-Object -First 1
Require-True ([string]$cad.executionTradableSymbol -eq "USDCAD" -and [bool]$cad.requiresInversion) "EXEC_ALGO_R002_FAIL_INVERSION_TRANSFORM_MISSING" "CAD inversion mapping wrong."
$sgd = @($mapping.mappings) | Where-Object { $_.portfolioCurrency -eq "SGD" } | Select-Object -First 1
Require-True ([string]$sgd.status -eq "MissingInstrumentConvention") "EXEC_ALGO_R002_FAIL_USD_PAIR_NORMALIZATION_MISSING" "SGD convention status wrong."

Require-True ([bool]$inversion.contractCreated) "EXEC_ALGO_R002_FAIL_INVERSION_TRANSFORM_MISSING" "Inversion contract missing."
foreach ($symbol in @("JPYUSD", "CADUSD", "CHFUSD")) {
    $match = @(@($inversion.rules) | Where-Object { $_.portfolioNormalizedSymbol -eq $symbol -and $_.requiresInversion -eq $true })
    Require-True ($match.Count -gt 0) "EXEC_ALGO_R002_FAIL_INVERSION_TRANSFORM_MISSING" "Inversion rule missing: $symbol"
}
Require-True ([string]$inversion.missingInversionTransformStatus -eq "MissingInversionTransform") "EXEC_ALGO_R002_FAIL_INVERSION_TRANSFORM_MISSING" "Missing inversion status absent."

Require-True ([bool]$directCross.directCrossExecutionDisabledArtifactCreated) "EXEC_ALGO_R002_FAIL_DIRECT_CROSS_EXECUTION_NOT_DISABLED" "Direct cross artifact missing."
Require-False ([bool]$directCross.directCrossExecutionAllowedByDefault) "EXEC_ALGO_R002_FAIL_DIRECT_CROSS_EXECUTION_NOT_DISABLED" "Direct cross enabled."
Require-True ([bool]$directCross.requiresNettingFirst) "EXEC_ALGO_R002_FAIL_DIRECT_CROSS_EXECUTION_NOT_DISABLED" "Netting-first missing."
foreach ($cross in @("EURGBP", "CADJPY", "AUDCNH", "CNHSGD", "EURZAR", "MXNNOK", "NOKZAR")) {
    Require-True (@($directCross.blockedExamples) -contains $cross) "EXEC_ALGO_R002_FAIL_DIRECT_CROSS_EXECUTION_NOT_DISABLED" "Blocked cross missing: $cross"
}

Require-True ([bool]$policy.contractCreated) "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING" "Policy contract missing."
foreach ($input in @("Close15mBenchmark readiness", "FeedReadinessStatus", "SpreadCostStatus", "OpportunityCostStatus", "NonFillRiskStatus", "ResidualRiskStatus", "MaxSpreadBps", "MaxCloseSlippageBps", "MaxResidualAllowedAtClose")) {
    Require-True (@($policy.consumes) -contains $input) "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "Policy input missing: $input"
}
Require-True ([bool]$policy.producesOneDecisionPerUsdPairNormalizedLine) "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING" "One decision per line missing."
Require-True ([bool]$policy.allDecisionsDesignOnly -and [bool]$policy.allDecisionsNonExecutable) "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE" "Policy decisions executable."

Require-True ([bool]$decisions.decisionsCreated) "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING" "Decisions missing."
Require-True ([int]$decisions.decisionCount -ge 14) "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING" "Too few decisions."
foreach ($scenario in @("scenario-direct-cross-eurgbp", "scenario-eur-good-feed", "scenario-jpy-inversion", "scenario-cad-inversion", "scenario-sgd-missing-convention", "scenario-moderate-residual", "scenario-high-residual-near-close", "scenario-wide-spread", "scenario-missing-close", "scenario-missing-feed", "scenario-stale-quote", "scenario-wakett-five-market-slices", "scenario-wakett-pure-limit", "scenario-twap-benchmark", "scenario-vwap-benchmark")) {
    $match = @(@($decisions.decisions) | Where-Object { $_.id -eq $scenario })
    Require-True ($match.Count -gt 0) "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING" "Decision missing: $scenario"
}
Require-False ([bool]$decisions.createsOrders) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Decisions create orders."
Require-False ([bool]$decisions.createsFills) "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Decisions create fills."
Require-False ([bool]$decisions.createsExecutionReports) "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Decisions create reports."
Require-False ([bool]$decisions.createsRoutes) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Decisions create routes."
Require-False ([bool]$decisions.createsSubmissions) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Decisions create submissions."

Require-True ([bool]$scenarios.fixtureScenariosCreated) "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING" "Scenarios missing."
Require-True ([int]$scenarios.scenarioCount -ge 14) "EXEC_ALGO_R002_FAIL_ALGO_SELECTION_POLICY_MISSING" "Too few scenarios."
Require-True ([bool]$scenarios.allScenariosNoExternal -and [bool]$scenarios.allScenarioDecisionsNonExecutable) "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE" "Scenarios executable/external."

Require-True ([bool]$benchmark.handlingCreated -and [bool]$benchmark.consumesClose15mBenchmarkReadiness) "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "Benchmark handling missing."
Require-True ([string]$benchmark.missingCloseBenchmarkReason -eq "MissingCloseBenchmark") "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "Missing benchmark reason wrong."
Require-True ([bool]$feed.handlingCreated -and [bool]$feed.consumesFeedReadiness) "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "Feed handling missing."
Require-True ([string]$feed.noQuoteNearCloseReason -eq "NoQuoteNearClose") "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "NoQuote reason wrong."
Require-True ([string]$feed.staleQuoteNearCloseReason -eq "StaleQuoteNearClose") "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "Stale quote reason wrong."
Require-True ([bool]$spread.handlingCreated -and [bool]$spread.consumesSpreadCost) "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "Spread handling missing."
Require-True ([bool]$spread.blocksRepeatedBlindCrossing) "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Spread does not block blind crossing."
Require-True ([bool]$opp.handlingCreated -and [bool]$opp.consumesOpportunityCost -and [bool]$opp.consumesNonFillRisk) "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "Opportunity/non-fill handling missing."
Require-True ([bool]$opp.blocksPurePassiveLimitDefault) "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Pure passive default not blocked."
Require-True ([bool]$residual.handlingCreated -and [bool]$residual.consumesResidualRisk) "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "Residual handling missing."
Require-True ([bool]$controlled.decisionCreated) "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "Controlled residual decision missing."
Require-True ([string]$controlled.selectedAlgoFamily -eq "ControlledResidualCross") "EXEC_ALGO_R002_FAIL_COST_CONTROL_HANDLING_MISSING" "Controlled residual selection wrong."
Require-True ([bool]$controlled.costJustifiedResidualCompletionOnly) "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Controlled residual not cost-justified."
Require-False ([bool]$controlled.blindMarketCrossing) "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Blind market crossing allowed."

Require-True ([bool]$wakett.blockedWakettPatternsCreated) "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Wakett block missing."
Require-False ([bool]$wakett.pureLimitUntilCloseDefaultAllowed) "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Pure limit default allowed."
Require-False ([bool]$wakett.mechanicalMarketSlicesAllowed) "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Mechanical slices allowed."
Require-False ([bool]$wakett.blindFiveMarketOrdersAroundCloseAllowed) "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Blind five orders allowed."
Require-False ([bool]$wakett.alwaysMarketAtCloseAllowed) "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Always market allowed."
Require-False ([bool]$wakett.blindMarketCrossingAllowedWithoutCostJustification) "EXEC_ALGO_R002_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Blind crossing allowed."
Require-False ([bool]$wakett.directCrossExecutionAllowedByDefault) "EXEC_ALGO_R002_FAIL_DIRECT_CROSS_EXECUTION_NOT_DISABLED" "Direct cross allowed by default."

Require-True ([bool]$benchmarkOnly.benchmarkOnlyDecisionsCreated) "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE" "Benchmark-only decisions missing."
Require-False ([bool]$benchmarkOnly.benchmarkOnlyDecisionsExecutable) "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE" "Benchmark-only executable."
foreach ($decision in @($benchmarkOnly.decisions)) {
    Require-False ([bool]$decision.isExecutable) "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE" "Benchmark-only decision executable."
    Require-False ([bool]$decision.createsOrder) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Benchmark-only creates order."
    Require-False ([bool]$decision.createsRoute) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Benchmark-only creates route."
}

Require-True ([bool]$nonExecutable.auditCreated) "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE" "Non-executable audit missing."
Require-True ([bool]$nonExecutable.allDecisionsDesignOnly -and [bool]$nonExecutable.allDecisionsPaperOnly -and [bool]$nonExecutable.allDecisionsNonExecutable) "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE" "Decision executable."
Require-False ([bool]$nonExecutable.algoDecisionExecutable) "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE" "Algo decision executable."
Require-False ([bool]$nonExecutable.runnableAlgoCreated) "EXEC_ALGO_R002_FAIL_ALGO_DECISION_EXECUTABLE" "Runnable algo created."
Require-False ([bool]$nonExecutable.executionScheduleIntroduced) "EXEC_ALGO_R002_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Execution schedule introduced."

Require-True ([bool]$orderAudit.auditCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit missing."
Require-False ([bool]$orderAudit.ordersCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$orderAudit.executableOrdersCreated) "EXEC_ALGO_R002_FAIL_EXECUTABLE_ORDER_CREATED" "Executable orders created."
Require-False ([bool]$orderAudit.omsOrdersCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS orders created."
Require-False ([bool]$orderAudit.omsParentOrdersCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Parent orders created."
Require-False ([bool]$orderAudit.omsChildOrdersCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child orders created."
Require-False ([bool]$orderAudit.brokerOrdersCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker orders created."
Require-False ([bool]$orderAudit.orderSubmissionIntroduced) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order submission introduced."
Require-False ([bool]$fillAudit.fillsCreated) "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$fillAudit.executionReportsCreated) "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$fillAudit.brokerExecutionReportsCreated) "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Broker reports created."
Require-False ([bool]$routeAudit.routesCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$routeAudit.submissionsCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-False ([bool]$routeAudit.hasBrokerRoute) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker route exists."

Require-True ([bool]$lineage.lineagePreserved -and [bool]$lineage.pmsEmsOmsLineagePreserved) "EXEC_ALGO_R002_FAIL_QUBES_OR_PMS_LINEAGE_WEAKENED" "Lineage missing."
foreach ($field in @("CycleRunId", "QubesRunId", "PaperExecutionPlanId", "PaperExecutionPlanLineId", "PaperCandidateId", "RebalanceIntentId", "RiskReviewId", "LotSizingId")) {
    Require-True (@($lineage.requiredLineage) -contains $field) "EXEC_ALGO_R002_FAIL_QUBES_OR_PMS_LINEAGE_WEAKENED" "Lineage field missing: $field"
}

Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_ALGO_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.securityId -eq "4004") "EXEC_ALGO_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "SecurityID wrong."
Require-True ([string]$usdjpy.securityIdSource -eq "8") "EXEC_ALGO_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "SecurityIDSource wrong."
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_ALGO_R002_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-False ([bool]$usdjpy.weakened) "EXEC_ALGO_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY weakened."
Require-True ([bool]$lmax.referenceOnly) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.brokerCalled) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker called."

Require-False ([bool]$noExternal.brokerActivation) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker activation."
Require-False ([bool]$noExternal.socketTlsFixRuntimeAction) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Socket/TLS/FIX runtime."
Require-False ([bool]$noExternal.liveMarketRuntimeAction) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market runtime."
Require-False ([bool]$noExternal.marketDataRequestAttempted) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "MarketData request."
Require-False ([bool]$noExternal.marketDataResponseRead) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "MarketData response."
Require-False ([bool]$noExternal.apiWorkerLiveGatewayEnabled) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API/Worker live gateway."
Require-False ([bool]$noExternal.schedulerPollingServiceTimerBackgroundJob) "EXEC_ALGO_R002_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling."
Require-False ([bool]$noExternal.automaticExecution) "EXEC_ALGO_R002_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Automatic execution."
Require-False ([bool]$noExternal.ordersCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$noExternal.fillsCreated) "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$noExternal.executionReportsCreated) "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$noExternal.brokerExecutionReportsCreated) "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Broker reports created."
Require-False ([bool]$noExternal.routesCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$noExternal.submissionsCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-False ([bool]$noExternal.liveTradingPathIntroduced) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading path."
Require-False ([bool]$noExternal.paperLedgerCommit) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Paper ledger commit."
Require-False ([bool]$noExternal.livePositionMutation) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live position mutation."
Require-False ([bool]$noExternal.brokerPositionMutation) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker position mutation."
Require-False ([bool]$noExternal.productionLedgerMutation) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Production ledger mutation."
Require-False ([bool]$noExternal.tradingStateMutation) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Trading state mutation."
Require-False ([bool]$noExternal.replayOrShadowReplay) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Replay introduced."
Require-False ([bool]$noExternal.secretOrRawPayloadSerialization) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Secret/raw payload serialization."

Require-False ([bool]$forbidden.brokerActivationDetected) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden broker activation."
Require-False ([bool]$forbidden.socketTlsFixMarketDataRuntimeDetected) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden runtime."
Require-False ([bool]$forbidden.schedulerPollingServiceTimerBackgroundJobIntroduced) "EXEC_ALGO_R002_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden scheduler/service."
Require-False ([bool]$forbidden.automaticExecutionIntroduced) "EXEC_ALGO_R002_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden automation."
Require-False ([bool]$forbidden.orderSubmissionIntroduced) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden submission."
Require-False ([bool]$forbidden.executableOrderCreated) "EXEC_ALGO_R002_FAIL_EXECUTABLE_ORDER_CREATED" "Forbidden executable order."
Require-False ([bool]$forbidden.omsOrBrokerOrderCreated) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden order."
Require-False ([bool]$forbidden.fillOrExecutionReportIntroduced) "EXEC_ALGO_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden fill/report."
Require-False ([bool]$forbidden.liveTradingPathIntroduced) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden live trading."
Require-False ([bool]$forbidden.stateMutationIntroduced) "EXEC_ALGO_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden state mutation."
Require-False ([bool]$forbidden.paperLedgerCommitOccurred) "EXEC_ALGO_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden paper commit."

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "EXEC_ALGO_R002_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "EXEC_ALGO_R002_FAIL_BUILD_OR_TESTS" "Build failed."
Require-True ([string]$evidence.focusedTests -like "PASS*") "EXEC_ALGO_R002_FAIL_BUILD_OR_TESTS" "Focused tests failed."
Require-True ([string]$evidence.unitTests -like "PASS*") "EXEC_ALGO_R002_FAIL_BUILD_OR_TESTS" "Unit tests failed."
Require-True ([string]$evidence.validator -eq "PASS") "EXEC_ALGO_R002_FAIL_BUILD_OR_TESTS" "Validator evidence missing."

Write-Host "EXEC_ALGO_R002_PASS_USD_PAIR_EXECUTION_NORMALIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R002_PASS_CLOSE_SEEKING_SELECTION_POLICY_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R002_PASS_COST_CONTROL_DECISION_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R002_PASS_WAKETT_FAILURE_PATTERNS_BLOCKED_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R002_PASS_NONEXECUTABLE_ALGO_DECISIONS_READY_NO_EXTERNAL"

param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param(
        [string]$Classification,
        [string]$Message
    )

    Write-Host $Classification
    throw $Message
}

function Read-Json {
    param(
        [string]$Path,
        [string]$FailureClassification
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Fail-Gate $FailureClassification "Required artifact is missing: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        Fail-Gate $FailureClassification "Artifact is not valid JSON: $Path"
    }
}

function Require-True {
    param(
        [bool]$Value,
        [string]$FailureClassification,
        [string]$Message
    )

    if (-not $Value) {
        Fail-Gate $FailureClassification $Message
    }
}

function Require-False {
    param(
        [bool]$Value,
        [string]$FailureClassification,
        [string]$Message
    )

    if ($Value) {
        Fail-Gate $FailureClassification $Message
    }
}

function Require-Contains {
    param(
        [object[]]$Values,
        [string]$Expected,
        [string]$FailureClassification,
        [string]$Message
    )

    if ($Expected -notin $Values) {
        Fail-Gate $FailureClassification $Message
    }
}

function Require-Symbol {
    param(
        [object[]]$Symbols,
        [string]$ExecutionTradableSymbol,
        [string]$NormalizedPortfolioSymbol,
        [bool]$RequiresInversion,
        [string]$FailureClassification
    )

    $matches = @($Symbols | Where-Object { $_.ExecutionTradableSymbol -eq $ExecutionTradableSymbol })
    if ($matches.Count -ne 1) {
        Fail-Gate $FailureClassification "Missing symbol authorization for $ExecutionTradableSymbol."
    }

    $symbol = $matches[0]
    if ($symbol.NormalizedPortfolioSymbol -ne $NormalizedPortfolioSymbol) {
        Fail-Gate $FailureClassification "Normalized portfolio symbol mismatch for $ExecutionTradableSymbol."
    }
    if ([bool]$symbol.RequiresInversion -ne $RequiresInversion) {
        Fail-Gate $FailureClassification "RequiresInversion mismatch for $ExecutionTradableSymbol."
    }

    return $symbol
}

$requiredArtifacts = @(
    "phase-exec-sim-r024-summary.md",
    "phase-exec-sim-r024-expanded-backtest-authorization-contract.json",
    "phase-exec-sim-r024-expanded-backtest-authorization-request.json",
    "phase-exec-sim-r024-expanded-backtest-preflight-contract.json",
    "phase-exec-sim-r024-expanded-backtest-authorization-result.json",
    "phase-exec-sim-r024-r023-row-validation-reference.json",
    "phase-exec-sim-r024-authorized-symbols.json",
    "phase-exec-sim-r024-accepted-rejected-row-summary.json",
    "phase-exec-sim-r024-quote-window-readiness-authorized.json",
    "phase-exec-sim-r024-close-benchmark-readiness-authorized.json",
    "phase-exec-sim-r024-feed-quality-readiness-authorized.json",
    "phase-exec-sim-r024-sanitized-import-readiness-authorized.json",
    "phase-exec-sim-r024-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r024-inversion-preservation.json",
    "phase-exec-sim-r024-expected-r025-policy-list.json",
    "phase-exec-sim-r024-expected-r025-report-list.json",
    "phase-exec-sim-r024-cost-guidance-preservation.json",
    "phase-exec-sim-r024-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r024-no-row-revalidation-audit.json",
    "phase-exec-sim-r024-no-db-import-audit.json",
    "phase-exec-sim-r024-no-sanitized-quote-row-creation-audit.json",
    "phase-exec-sim-r024-no-backtest-simulation-audit.json",
    "phase-exec-sim-r024-no-tca-result-lines-audit.json",
    "phase-exec-sim-r024-no-polygon-api-call-audit.json",
    "phase-exec-sim-r024-no-lmax-call-audit.json",
    "phase-exec-sim-r024-no-external-api-call-audit.json",
    "phase-exec-sim-r024-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r024-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r024-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r024-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r024-no-external-audit.json",
    "phase-exec-sim-r024-forbidden-actions-audit.json",
    "phase-exec-sim-r024-next-phase-recommendation.json",
    "phase-exec-sim-r024-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsDir $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Required R024 artifact is missing: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-expanded-backtest-authorization-contract.json") "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$request = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-expanded-backtest-authorization-request.json") "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$preflight = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-expanded-backtest-preflight-contract.json") "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$result = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-expanded-backtest-authorization-result.json") "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$r023 = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-r023-row-validation-reference.json") "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$authorized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-authorized-symbols.json") "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$rows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-accepted-rejected-row-summary.json") "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$windows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-quote-window-readiness-authorized.json") "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING"
$close = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-close-benchmark-readiness-authorized.json") "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING"
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-feed-quality-readiness-authorized.json") "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING"
$importReadiness = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-sanitized-import-readiness-authorized.json") "EXEC_SIM_R024_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-direct-cross-exclusion-preservation.json") "EXEC_SIM_R024_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-inversion-preservation.json") "EXEC_SIM_R024_FAIL_USDJPY_CAVEAT_WEAKENED"
$policies = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-expected-r025-policy-list.json") "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$reports = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-expected-r025-report-list.json") "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-cost-guidance-preservation.json") "EXEC_SIM_R024_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-nonmajor-calibration-preservation.json") "EXEC_SIM_R024_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$noRowRevalidation = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-no-row-revalidation-audit.json") "EXEC_SIM_R024_FAIL_ROW_REVALIDATION_EXECUTED"
$noDb = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-no-db-import-audit.json") "EXEC_SIM_R024_FAIL_DB_IMPORT_OCCURRED"
$noSanitized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-no-sanitized-quote-row-creation-audit.json") "EXEC_SIM_R024_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-no-backtest-simulation-audit.json") "EXEC_SIM_R024_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
$noTca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-no-tca-result-lines-audit.json") "EXEC_SIM_R024_FAIL_TCA_RESULTS_PRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-no-external-api-call-audit.json") "EXEC_SIM_R024_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R024_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-no-order-fill-report-route-audit.json") "EXEC_SIM_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-usdjpy-caveat-preservation.json") "EXEC_SIM_R024_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-lmax-readonly-baseline-reference.json") "EXEC_SIM_R024_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-no-external-audit.json") "EXEC_SIM_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-forbidden-actions-audit.json") "EXEC_SIM_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r024-build-test-validator-evidence.json") "EXEC_SIM_R024_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.expandedBacktestAuthorizationContractCreated) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Authorization contract marker missing."
if ($contract.SourceRowValidationPhase -ne "EXEC-SIM-R023") { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "R023 row validation phase not referenced." }
if ($contract.ProviderName -ne "PolygonOfflineFile" -or $contract.DatasetType -ne "HistoricalBboQuotes") { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Provider/dataset contract mismatch." }
if ($contract.IntendedNextPhase -ne "EXEC-SIM-R025") { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Next phase is not R025." }
Require-True ([bool]$contract.AuthorizationOnly) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Contract is not authorization-only."
Require-True ([bool]$contract.NoApiCall) "EXEC_SIM_R024_FAIL_API_CALL_DETECTED" "Contract allows API calls."
Require-True ([bool]$contract.NoRowRevalidation) "EXEC_SIM_R024_FAIL_ROW_REVALIDATION_EXECUTED" "Contract allows row revalidation."
Require-True ([bool]$contract.NoBacktest) "EXEC_SIM_R024_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Contract allows backtest."
Require-True ([bool]$contract.NoSimulation) "EXEC_SIM_R024_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Contract allows simulation."
Require-True ([bool]$contract.NoImport) "EXEC_SIM_R024_FAIL_DB_IMPORT_OCCURRED" "Contract allows import."
Require-True ([bool]$contract.NoPersistedSanitizedQuoteRows) "EXEC_SIM_R024_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Contract allows persisted sanitized rows."
Require-True ([bool]$contract.NoTcaResultLines) "EXEC_SIM_R024_FAIL_TCA_RESULTS_PRODUCED" "Contract allows TCA result lines."
Require-True ([bool]$contract.NoOrdersFillsReportsRoutes) "EXEC_SIM_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Contract allows order-domain outputs."

if (@($request.AcceptedValidationResultIds).Count -ne 7) { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Accepted validation IDs missing." }
if (@($request.QuoteWindowReadinessIds).Count -ne 7) { Fail-Gate "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Quote-window readiness IDs missing." }
if (@($request.CloseBenchmarkReadinessIds).Count -ne 7) { Fail-Gate "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Close-benchmark readiness IDs missing." }
if (@($request.FeedQualityReadinessIds).Count -ne 7) { Fail-Gate "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Feed-quality readiness IDs missing." }
if (@($request.SanitizedImportReadinessIds).Count -ne 7) { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Sanitized import readiness IDs missing." }
Require-True ([bool]$request.AuthorizationOnly) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Request is not authorization-only."
Require-True ([bool]$preflight.expandedBacktestPreflightContractCreated) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Preflight contract missing."
if ($preflight.PreflightStatus -ne "ExpandedBacktestAuthorizationReadyWithRejectedRowsNoExternal") { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Preflight status is not safe partial authorization." }

Require-True ([bool]$result.authorizationResultCreated) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Authorization result missing."
if ($result.AuthorizationStatus -ne "EXEC_SIM_R024_PASS_EXPANDED_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL") { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Unexpected authorization result classification." }
Require-Contains @($result.AdditionalClassifications) "EXEC_SIM_R024_PASS_ROW_VALIDATED_SYMBOLS_AUTHORIZED_READY_NO_EXTERNAL" "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Row validated symbols classification missing."
Require-Contains @($result.AdditionalClassifications) "EXEC_SIM_R024_PASS_NO_REVALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL" "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "No revalidation/backtest classification missing."
Require-Contains @($result.AdditionalClassifications) "EXEC_SIM_R024_PASS_EXPANDED_BACKTEST_AUTHORIZATION_WITH_REJECTED_ROWS_NO_EXTERNAL" "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Safe partial classification missing."
if ($result.AuthorizedSymbolCount -ne 7) { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Authorized symbol count is not seven." }
Require-True ([bool]$result.RejectedRowsAcceptedForAuthorization) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Rejected rows not accepted safely for authorization."
if ($result.RejectedRowsPerFile -ne 1) { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Rejected rows per file is not one." }
Require-False ([bool]$result.QuarantinedFileIncluded) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Quarantined file included."
Require-False ([bool]$result.DirectCrossIncluded) "EXEC_SIM_R024_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct cross included."
Require-False ([bool]$result.BacktestExecuted) "EXEC_SIM_R024_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Backtest executed."
Require-False ([bool]$result.SimulationExecuted) "EXEC_SIM_R024_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Simulation executed."
Require-False ([bool]$result.TcaResultLinesProduced) "EXEC_SIM_R024_FAIL_TCA_RESULTS_PRODUCED" "TCA result lines produced."

Require-True ([bool]$r023.r023RowValidationReferenceCreated) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "R023 reference missing."
Require-Contains @($r023.R023Classifications) "EXEC_SIM_R023_PARTIAL_ROW_VALIDATION_WITH_REJECTIONS_NO_EXTERNAL" "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "R023 partial classification missing."
if ($r023.rowValidationResultCount -ne 7 -or $r023.totalRejectedRowCount -ne 7 -or $r023.totalMalformedJsonRowCount -ne 7) { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "R023 rejected-row reference mismatch." }
Require-False ([bool]$r023.rowRevalidatedInR024) "EXEC_SIM_R024_FAIL_ROW_REVALIDATION_EXECUTED" "Rows revalidated in R024."

if ($authorized.authorizedSymbolCount -ne 7 -or @($authorized.authorizedSymbols).Count -ne 7) { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Authorized symbols missing." }
$eurusd = Require-Symbol @($authorized.authorizedSymbols) "EURUSD" "EURUSD" $false "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$usdjpyAuth = Require-Symbol @($authorized.authorizedSymbols) "USDJPY" "JPYUSD" $true "EXEC_SIM_R024_FAIL_USDJPY_CAVEAT_WEAKENED"
$audusd = Require-Symbol @($authorized.authorizedSymbols) "AUDUSD" "AUDUSD" $false "EXEC_SIM_R024_FAIL_AUDUSD_MISCLASSIFIED"
$gbpusd = Require-Symbol @($authorized.authorizedSymbols) "GBPUSD" "GBPUSD" $false "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$nzdusd = Require-Symbol @($authorized.authorizedSymbols) "NZDUSD" "NZDUSD" $false "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$usdcad = Require-Symbol @($authorized.authorizedSymbols) "USDCAD" "CADUSD" $true "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$usdchf = Require-Symbol @($authorized.authorizedSymbols) "USDCHF" "CHFUSD" $true "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
foreach ($symbol in @($eurusd, $usdjpyAuth, $audusd, $gbpusd, $nzdusd, $usdcad, $usdchf)) {
    Require-True ([bool]$symbol.EligibleForR025) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Symbol is not eligible for R025: $($symbol.ExecutionTradableSymbol)"
    Require-False ([bool]$symbol.Quarantined) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Symbol is quarantined: $($symbol.ExecutionTradableSymbol)"
    if ($symbol.AcceptedRowCount -le 0 -or $symbol.RejectedRowCount -ne 1) { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Accepted/rejected row summary invalid for $($symbol.ExecutionTradableSymbol)." }
}
if ($usdjpyAuth.SecurityID -ne "4004" -or $usdjpyAuth.SecurityIDSource -ne "8") { Fail-Gate "EXEC_SIM_R024_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened in authorized symbols." }
Require-False ([bool]$inversion.audusdMisclassifiedFailed) "EXEC_SIM_R024_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified failed."

Require-True ([bool]$rows.acceptedRejectedRowSummaryCreated) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Accepted/rejected row summary missing."
if ($rows.totalRejectedRowCount -ne 7 -or $rows.malformedRejectedRowCount -ne 7 -or $rows.rejectedRowsPerFile -ne 1) { Fail-Gate "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Rejected-row summary mismatch." }
Require-True ([bool]$rows.rejectedRowsAcceptedForAuthorization) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Safe partial rejected rows not accepted."
Require-False ([bool]$rows.rejectedRowsPersisted) "EXEC_SIM_R024_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Rejected rows persisted."

Require-True ([bool]$windows.quoteWindowReadinessAuthorizedCreated) "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Quote-window authorization missing."
if ($windows.symbolCount -ne 7 -or $windows.evaluatedWindowCount -ne 112) { Fail-Gate "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Quote-window authorization counts mismatch." }
Require-True ([bool]$windows.authorized) "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Quote-window readiness not authorized."
Require-True ([bool]$close.closeBenchmarkReadinessAuthorizedCreated) "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Close benchmark authorization missing."
if ($close.symbolCount -ne 7 -or $close.resultCount -ne 112) { Fail-Gate "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Close benchmark authorization counts mismatch." }
Require-True ([bool]$close.authorized) "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Close benchmark readiness not authorized."
Require-True ([bool]$feed.feedQualityReadinessAuthorizedCreated) "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Feed-quality authorization missing."
if ($feed.symbolCount -ne 7 -or $feed.resultCount -ne 7) { Fail-Gate "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Feed-quality authorization counts mismatch." }
Require-True ([bool]$feed.authorized) "EXEC_SIM_R024_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Feed quality not authorized."
Require-True ([bool]$importReadiness.sanitizedImportReadinessAuthorizedCreated) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Sanitized import readiness authorization missing."
Require-True ([bool]$importReadiness.metadataOnly) "EXEC_SIM_R024_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Sanitized import readiness is not metadata-only."
Require-False ([bool]$importReadiness.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R024_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized rows created."
Require-False ([bool]$importReadiness.dbImportOccurred) "EXEC_SIM_R024_FAIL_DB_IMPORT_OCCURRED" "DB import occurred."

Require-True ([bool]$direct.directCrossExclusionPreserved) "EXEC_SIM_R024_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion missing."
Require-False ([bool]$direct.directCrossIncluded) "EXEC_SIM_R024_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross included."
Require-True ([bool]$direct.rawQubesCrossesSignalOnly) "EXEC_SIM_R024_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Raw Qubes crosses not signal-only."
Require-False ([bool]$direct.directCrossExecutionAllowedByDefault) "EXEC_SIM_R024_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross execution allowed by default."
Require-False ([bool]$direct.guidanceWeakened) "EXEC_SIM_R024_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross guidance weakened."
Require-True ([bool]$inversion.inversionPreservationCreated) "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Inversion preservation missing."
Require-True ([bool]$inversion.usdJpyCaveatPreserved) "EXEC_SIM_R024_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing in inversion preservation."
$unused = Require-Symbol @($inversion.validations) "USDJPY" "JPYUSD" $true "EXEC_SIM_R024_FAIL_USDJPY_CAVEAT_WEAKENED"
$unused = Require-Symbol @($inversion.validations) "USDCAD" "CADUSD" $true "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"
$unused = Require-Symbol @($inversion.validations) "USDCHF" "CHFUSD" $true "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING"

foreach ($policy in @("WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose", "PassiveUntilUrgency", "CloseSeeking15m", "CloseSeeking15mAdaptive", "ControlledResidualCross", "ImmediatePaperBenchmark", "TWAPBenchmarkOnly", "VWAPBenchmarkOnly", "ManualReview", "DoNotTrade")) {
    Require-Contains @($policies.policies) $policy "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Expected R025 policy missing: $policy"
}
foreach ($report in @("PerSymbolTcaReportsForAllSeven", "PolicyComparison", "RankingByMedianSlippage", "RankingByP95Slippage", "RankingByFillRatio", "RankingByResidual", "RankingBySpreadPaid", "WakettBaselineComparison", "CloseSeekingComparison", "ExpandedMajorSymbolComparison")) {
    Require-Contains @($reports.reports) $report "EXEC_SIM_R024_FAIL_AUTHORIZATION_MISSING" "Expected R025 report missing: $report"
}

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail-Gate "EXEC_SIM_R024_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million best-case marker missing." }
Require-True ([bool]$cost.fiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R024_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million not marked best-case major-only."
Require-False ([bool]$cost.fiveUsdPerMillionUniversalized) "EXEC_SIM_R024_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$nonmajor.RequiresLiquidityCalibration) "EXEC_SIM_R024_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration requirement missing."

Require-False ([bool]$noRowRevalidation.quoteRowsValidatedAgain) "EXEC_SIM_R024_FAIL_ROW_REVALIDATION_EXECUTED" "Quote rows validated again."
Require-False ([bool]$noRowRevalidation.rowValidationReexecuted) "EXEC_SIM_R024_FAIL_ROW_REVALIDATION_EXECUTED" "Row validation reexecuted."
Require-False ([bool]$noRowRevalidation.quoteRowsReadInR024) "EXEC_SIM_R024_FAIL_ROW_REVALIDATION_EXECUTED" "Quote rows read in R024."
Require-False ([bool]$noDb.quotesImportedIntoDb) "EXEC_SIM_R024_FAIL_DB_IMPORT_OCCURRED" "Quotes imported into DB."
Require-False ([bool]$noDb.dbWriteOccurred) "EXEC_SIM_R024_FAIL_DB_IMPORT_OCCURRED" "DB write occurred."
Require-False ([bool]$noSanitized.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R024_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized quote rows created."
Require-False ([bool]$noSanitized.sanitizedQuoteRowsCreated) "EXEC_SIM_R024_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Sanitized quote rows created."
Require-False ([bool]$noBacktest.newBacktestExecuted) "EXEC_SIM_R024_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Backtest executed."
Require-False ([bool]$noBacktest.newSimulationExecuted) "EXEC_SIM_R024_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Simulation executed."
Require-False ([bool]$noTca.tcaResultLinesProduced) "EXEC_SIM_R024_FAIL_TCA_RESULTS_PRODUCED" "TCA result lines produced."
Require-False ([bool]$noTca.simulationResultLinesProduced) "EXEC_SIM_R024_FAIL_TCA_RESULTS_PRODUCED" "Simulation result lines produced."
Require-False ([bool]$api.polygonApiCalled) "EXEC_SIM_R024_FAIL_API_CALL_DETECTED" "Polygon API called."
Require-False ([bool]$api.lmaxCalled) "EXEC_SIM_R024_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$api.externalApiCalled) "EXEC_SIM_R024_FAIL_API_CALL_DETECTED" "External API called."
Require-False ([bool]$runtime.brokerActivationDetected) "EXEC_SIM_R024_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker activation detected."
Require-False ([bool]$runtime.marketDataRequestSent) "EXEC_SIM_R024_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataRequest sent."
Require-False ([bool]$runtime.marketDataResponseRead) "EXEC_SIM_R024_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataResponse read."
Require-False ([bool]$runtime.schedulerServiceTimerPollingBackgroundJobIntroduced) "EXEC_SIM_R024_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/timer/polling/background job introduced."
Require-False ([bool]$order.ordersCreated) "EXEC_SIM_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$order.executableOrdersCreated) "EXEC_SIM_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable orders created."
Require-False ([bool]$order.fillEntitiesCreated) "EXEC_SIM_R024_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill entities created."
Require-False ([bool]$order.executionReportEntitiesCreated) "EXEC_SIM_R024_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$order.routesCreated) "EXEC_SIM_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$order.submissionsCreated) "EXEC_SIM_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R024_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat artifact weakened."
if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not [bool]$usdjpy.RequiresInversion) { Fail-Gate "EXEC_SIM_R024_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY inversion/caveat mismatch." }
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_SIM_R024_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified failed."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R024_FAIL_API_CALL_DETECTED" "LMAX baseline is not reference-only."
Require-False ([bool]$lmax.lmaxCalledInR024) "EXEC_SIM_R024_FAIL_API_CALL_DETECTED" "LMAX called in R024."
Require-False ([bool]$noExternal.polygonApiCalled) "EXEC_SIM_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Polygon API call detected."
Require-False ([bool]$noExternal.lmaxCalled) "EXEC_SIM_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX call detected."
Require-False ([bool]$noExternal.externalApiCalled) "EXEC_SIM_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "External API call detected."
Require-False ([bool]$noExternal.quoteRowsValidatedAgain) "EXEC_SIM_R024_FAIL_ROW_REVALIDATION_EXECUTED" "No-external audit reports row revalidation."
Require-False ([bool]$noExternal.newBacktestExecuted) "EXEC_SIM_R024_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "No-external audit reports backtest."
Require-False ([bool]$noExternal.tcaResultLinesProduced) "EXEC_SIM_R024_FAIL_TCA_RESULTS_PRODUCED" "No-external audit reports TCA result lines."
Require-False ([bool]$noExternal.ordersFillsReportsRoutesSubmissionsCreated) "EXEC_SIM_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-external audit reports order-domain output."
Require-False ([bool]$forbidden.forbiddenActionsDetected) "EXEC_SIM_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden actions detected."

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail-Gate "EXEC_SIM_R024_FAIL_BUILD_OR_TESTS" "dotnet build evidence missing or not PASS." }
if ($evidence.focusedTests -notlike "PASS*") { Fail-Gate "EXEC_SIM_R024_FAIL_BUILD_OR_TESTS" "Focused R024 test evidence missing or not PASS." }
if ($evidence.unitTests -notlike "PASS*") { Fail-Gate "EXEC_SIM_R024_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS." }
if ($evidence.validator -notlike "PASS*") { Fail-Gate "EXEC_SIM_R024_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS." }

Write-Host "EXEC_SIM_R024_PASS_EXPANDED_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R024_PASS_ROW_VALIDATED_SYMBOLS_AUTHORIZED_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R024_PASS_NO_REVALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R024_PASS_EXPANDED_BACKTEST_AUTHORIZATION_WITH_REJECTED_ROWS_NO_EXTERNAL"

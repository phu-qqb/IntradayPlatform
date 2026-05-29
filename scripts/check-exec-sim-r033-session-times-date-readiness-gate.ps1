param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param([string]$Classification, [string]$Message)
    Write-Host $Classification
    throw $Message
}

function Read-Json {
    param([string]$Path, [string]$FailureClassification)
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
    param([bool]$Value, [string]$FailureClassification, [string]$Message)
    if (-not $Value) { Fail-Gate $FailureClassification $Message }
}

function Require-False {
    param([bool]$Value, [string]$FailureClassification, [string]$Message)
    if ($Value) { Fail-Gate $FailureClassification $Message }
}

function Require-Contains {
    param([object[]]$Values, [string]$Expected, [string]$FailureClassification, [string]$Message)
    if ($Expected -notin $Values) { Fail-Gate $FailureClassification $Message }
}

$requiredArtifacts = @(
    "phase-exec-sim-r033-summary.md",
    "phase-exec-sim-r033-r032-decision-reference.json",
    "phase-exec-sim-r033-true-session-time-readiness-contract.json",
    "phase-exec-sim-r033-session-calendar-contract.json",
    "phase-exec-sim-r033-additional-date-range-readiness-contract.json",
    "phase-exec-sim-r033-operator-session-time-input-requirements.json",
    "phase-exec-sim-r033-operator-date-range-input-requirements.json",
    "phase-exec-sim-r033-session-window-derivation-requirements.json",
    "phase-exec-sim-r033-opening-build-window-derivation.json",
    "phase-exec-sim-r033-intraday-rebalance-window-derivation.json",
    "phase-exec-sim-r033-closing-flatten-window-derivation.json",
    "phase-exec-sim-r033-proxy-window-caveat-preservation.json",
    "phase-exec-sim-r033-no-overnight-preservation.json",
    "phase-exec-sim-r033-previous-evening-planning-preservation.json",
    "phase-exec-sim-r033-required-symbols-preservation.json",
    "phase-exec-sim-r033-inversion-preservation.json",
    "phase-exec-sim-r033-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r033-cost-guidance-preservation.json",
    "phase-exec-sim-r033-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r033-needs-operator-input.json",
    "phase-exec-sim-r033-readiness-statuses.json",
    "phase-exec-sim-r033-no-download-audit.json",
    "phase-exec-sim-r033-no-validation-import-backtest-audit.json",
    "phase-exec-sim-r033-no-tca-result-lines-audit.json",
    "phase-exec-sim-r033-no-polygon-api-call-audit.json",
    "phase-exec-sim-r033-no-lmax-call-audit.json",
    "phase-exec-sim-r033-no-external-api-call-audit.json",
    "phase-exec-sim-r033-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r033-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r033-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r033-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r033-no-external-audit.json",
    "phase-exec-sim-r033-forbidden-actions-audit.json",
    "phase-exec-sim-r033-next-phase-recommendation.json",
    "phase-exec-sim-r033-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail-Gate "EXEC_SIM_R033_FAIL_SESSION_TIME_CONTRACT_MISSING" "Required R033 artifact missing: $artifact"
    }
}

$r032 = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-r032-decision-reference.json") "EXEC_SIM_R033_FAIL_SESSION_TIME_CONTRACT_MISSING"
$session = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-true-session-time-readiness-contract.json") "EXEC_SIM_R033_FAIL_SESSION_TIME_CONTRACT_MISSING"
$calendar = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-session-calendar-contract.json") "EXEC_SIM_R033_FAIL_SESSION_TIME_CONTRACT_MISSING"
$dates = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-additional-date-range-readiness-contract.json") "EXEC_SIM_R033_FAIL_DATE_RANGE_CONTRACT_MISSING"
$sessionReq = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-operator-session-time-input-requirements.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$dateReq = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-operator-date-range-input-requirements.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$derivation = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-session-window-derivation-requirements.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-opening-build-window-derivation.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$intraday = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-intraday-rebalance-window-derivation.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-closing-flatten-window-derivation.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$proxy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-proxy-window-caveat-preservation.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$overnight = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-no-overnight-preservation.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$previousEvening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-previous-evening-planning-preservation.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$symbols = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-required-symbols-preservation.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-inversion-preservation.json") "EXEC_SIM_R033_FAIL_USDJPY_CAVEAT_WEAKENED"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-direct-cross-exclusion-preservation.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-cost-guidance-preservation.json") "EXEC_SIM_R033_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-nonmajor-calibration-preservation.json") "EXEC_SIM_R033_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$needs = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-needs-operator-input.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$statuses = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-readiness-statuses.json") "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING"
$noDownload = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-no-download-audit.json") "EXEC_SIM_R033_FAIL_DOWNLOAD_EXECUTED"
$noValidation = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-no-validation-import-backtest-audit.json") "EXEC_SIM_R033_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
$noTca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-no-tca-result-lines-audit.json") "EXEC_SIM_R033_FAIL_TCA_RESULTS_PRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-no-external-api-call-audit.json") "EXEC_SIM_R033_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R033_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-no-order-fill-report-route-audit.json") "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-usdjpy-caveat-preservation.json") "EXEC_SIM_R033_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-lmax-readonly-baseline-reference.json") "EXEC_SIM_R033_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-no-external-audit.json") "EXEC_SIM_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-forbidden-actions-audit.json") "EXEC_SIM_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r033-build-test-validator-evidence.json") "EXEC_SIM_R033_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$r032.r032DecisionReferenceCreated) "EXEC_SIM_R033_FAIL_SESSION_TIME_CONTRACT_MISSING" "R032 decision reference missing."
Require-True ([bool]$r032.NeedsOperatorSessionTimesFromR032) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "R032 session-time need not referenced."
Require-True ([bool]$r032.MoreDatesRecommendedFromR032) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "R032 more-dates decision not referenced."
Require-True ([bool]$r032.ParameterRefinementDeferredFromR032) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "R032 parameter deferral not referenced."

Require-True ([bool]$session.trueSessionTimeReadinessContractCreated) "EXEC_SIM_R033_FAIL_SESSION_TIME_CONTRACT_MISSING" "True session contract missing."
if ($session.SourceDecisionPhase -ne "EXEC-SIM-R032") { Fail-Gate "EXEC_SIM_R033_FAIL_SESSION_TIME_CONTRACT_MISSING" "R032 source decision missing." }
if ($session.BarIntervalMinutes -ne 15) { Fail-Gate "EXEC_SIM_R033_FAIL_SESSION_TIME_CONTRACT_MISSING" "Bar interval mismatch." }
if ($session.SessionTimeStatus -ne "NeedsOperatorSessionTimes") { Fail-Gate "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "Session time status should need operator input." }
Require-False ([bool]$session.TrueModelSessionTimesSupplied) "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "True session times incorrectly marked supplied."
Require-False ([bool]$session.ExactTrueSessionTimesInvented) "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "Exact true session times invented."
Require-True ([bool]$session.ProxyWindowsAreNotConfirmedTrueSessionTimes) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Proxy caveat missing."
Require-False ([bool]$session.OvernightAllowed) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Overnight allowed."
Require-True ([bool]$session.MustEndFlat) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "MustEndFlat missing."
if ($null -ne $session.SessionOpenUtc -or $null -ne $session.SessionCloseUtc -or $null -ne $session.OpeningBuildWindowStartUtc -or $null -ne $session.ClosingFlattenWindowEndUtc) {
    Fail-Gate "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "Exact session timestamps were populated without operator input."
}

Require-True ([bool]$calendar.sessionCalendarContractCreated) "EXEC_SIM_R033_FAIL_SESSION_TIME_CONTRACT_MISSING" "Calendar contract missing."
if ($calendar.CalendarPolicyStatus -ne "NeedsOperatorSessionTimes") { Fail-Gate "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "Calendar status should need operator input." }
Require-False ([bool]$calendar.ExactCalendarInvented) "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "Calendar invented."
Require-True ([bool]$calendar.NoOvernightAllowed) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "No-overnight missing from calendar."
Require-True ([bool]$calendar.MustEndFlat) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "MustEndFlat missing from calendar."

Require-True ([bool]$dates.additionalDateRangeReadinessContractCreated) "EXEC_SIM_R033_FAIL_DATE_RANGE_CONTRACT_MISSING" "Date range contract missing."
if ($dates.DateRangeStatus -ne "NeedsOperatorDateRanges") { Fail-Gate "EXEC_SIM_R033_FAIL_DATE_RANGES_INVENTED" "Date range status should need operator input." }
if (@($dates.RequestedDateRanges).Count -ne 0 -or @($dates.RequestedMarketRegimes).Count -ne 0) { Fail-Gate "EXEC_SIM_R033_FAIL_DATE_RANGES_INVENTED" "Exact date ranges/regimes invented." }
Require-False ([bool]$dates.ExactDatesInvented) "EXEC_SIM_R033_FAIL_DATE_RANGES_INVENTED" "Exact dates invented."
if ($dates.MinimumDateCount -lt 5 -or $dates.MinimumWindowCountPerCategory -lt 5) { Fail-Gate "EXEC_SIM_R033_FAIL_DATE_RANGE_CONTRACT_MISSING" "Minimum date/window requirements too weak." }
Require-True ([bool]$dates.IncludeOpeningBuild) "EXEC_SIM_R033_FAIL_DATE_RANGE_CONTRACT_MISSING" "OpeningBuild not required."
Require-True ([bool]$dates.IncludeIntradayRebalance) "EXEC_SIM_R033_FAIL_DATE_RANGE_CONTRACT_MISSING" "IntradayRebalance not required."
Require-True ([bool]$dates.IncludeClosingFlatten) "EXEC_SIM_R033_FAIL_DATE_RANGE_CONTRACT_MISSING" "ClosingFlatten not required."
Require-True ([bool]$dates.RequiresUtcRanges) "EXEC_SIM_R033_FAIL_DATE_RANGE_CONTRACT_MISSING" "UTC ranges not required."
Require-True ([bool]$dates.RequiresOperatorProvidedFiles) "EXEC_SIM_R033_FAIL_DATE_RANGE_CONTRACT_MISSING" "Operator-provided files not required."
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")) {
    Require-Contains @($dates.RequiredSymbols) $symbol "EXEC_SIM_R033_FAIL_DATE_RANGE_CONTRACT_MISSING" "Required symbol missing: $symbol"
}
foreach ($category in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    Require-Contains @($dates.RequiredSessionWindowCategories) $category "EXEC_SIM_R033_FAIL_DATE_RANGE_CONTRACT_MISSING" "Required category missing: $category"
}

Require-True ([bool]$sessionReq.operatorSessionTimeInputRequirementsCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Session input requirements missing."
Require-True ([bool]$sessionReq.NeedsOperatorSessionTimes) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "NeedsOperatorSessionTimes missing."
Require-False ([bool]$sessionReq.ExactSessionTimesInvented) "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "Session times invented in requirements."
Require-Contains @($sessionReq.RequiredInputs) "model session timezone" "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Timezone requirement missing."
Require-Contains @($sessionReq.RequiredInputs) "no-overnight confirmation" "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "No-overnight requirement missing."
Require-True ([bool]$dateReq.operatorDateRangeInputRequirementsCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Date input requirements missing."
Require-True ([bool]$dateReq.NeedsOperatorDateRanges) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "NeedsOperatorDateRanges missing."
Require-False ([bool]$dateReq.ExactDateRangesInvented) "EXEC_SIM_R033_FAIL_DATE_RANGES_INVENTED" "Date ranges invented in requirements."

Require-True ([bool]$derivation.sessionWindowDerivationRequirementsCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Derivation requirements missing."
Require-True ([bool]$derivation.RequiresTrueSessionTimes) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "True session-time requirement missing."
Require-True ([bool]$derivation.ProxyWindowsPreservedOnlyAsProxy) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Proxy caveat missing from derivation."
Require-True ([bool]$opening.openingBuildWindowDerivationCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Opening derivation missing."
Require-True ([bool]$opening.TargetKnownPreviousEvening) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Previous-evening target missing."
Require-False ([bool]$opening.PreSessionExecutionAuthorized) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Pre-session execution authorized."
Require-False ([bool]$opening.OvernightExposureAuthorized) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Overnight exposure authorized."
Require-False ([bool]$opening.ProxyWindowConfirmedAsTrueSession) "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "Opening proxy confirmed as true."
Require-True ([bool]$intraday.intradayRebalanceWindowDerivationCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Intraday derivation missing."
if ($intraday.OrderKnownApproximatelyMinutesBeforeClose -ne 13) { Fail-Gate "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "T-minus-13 missing." }
Require-True ([bool]$intraday.RequiresTrueSessionTimes) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Intraday true session-time need missing."
Require-True ([bool]$closing.closingFlattenWindowDerivationCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Closing derivation missing."
Require-True ([bool]$closing.MustEndFlat) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Closing MustEndFlat missing."
Require-False ([bool]$closing.OvernightAllowed) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Closing overnight allowed."
Require-True ([bool]$closing.NoOvernightCritical) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Closing no-overnight critical missing."

Require-True ([bool]$proxy.proxyWindowCaveatPreservationCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Proxy caveat artifact missing."
Require-False ([bool]$proxy.TrueModelSessionTimesConfirmed) "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "True session times incorrectly confirmed."
Require-True ([bool]$proxy.NeedsOperatorSessionTimes) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Proxy needs session times missing."
Require-False ([bool]$proxy.ProxyWindowsConfirmedAsTrueSessionWindows) "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "Proxy windows confirmed as true."
Require-True ([bool]$overnight.noOvernightPreservationCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "No-overnight preservation missing."
Require-False ([bool]$overnight.OvernightAllowed) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Overnight allowed."
Require-True ([bool]$overnight.MustEndFlat) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "MustEndFlat missing."
Require-True ([bool]$previousEvening.previousEveningPlanningPreservationCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Previous-evening preservation missing."
Require-True ([bool]$previousEvening.PreviousEveningPlanningAllowed) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Previous-evening planning missing."
Require-False ([bool]$previousEvening.PreSessionExecutionAuthorized) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Pre-session execution authorized."
Require-False ([bool]$previousEvening.OvernightExposureAuthorized) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Overnight exposure authorized."

Require-True ([bool]$symbols.requiredSymbolsPreservationCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Required symbols artifact missing."
Require-True ([bool]$symbols.UsdPairOnlyExecutionUniverse) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "USD-pair-only execution weakened."
if ($symbols.AudUsdStatus -ne "not failed") { Fail-Gate "EXEC_SIM_R033_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified." }
Require-True ([bool]$inversion.inversionPreservationCreated) "EXEC_SIM_R033_FAIL_USDJPY_CAVEAT_WEAKENED" "Inversion preservation missing."
Require-True ([bool]$inversion.usdJpyCaveatPreserved) "EXEC_SIM_R033_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
Require-False ([bool]$inversion.audusdMisclassifiedFailed) "EXEC_SIM_R033_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified in inversion artifact."
$usdjpyValidation = @($inversion.validations | Where-Object { $_.ExecutionTradableSymbol -eq "USDJPY" })[0]
if ($null -eq $usdjpyValidation -or $usdjpyValidation.NormalizedPortfolioSymbol -ne "JPYUSD" -or -not [bool]$usdjpyValidation.RequiresInversion -or $usdjpyValidation.SecurityID -ne "4004" -or $usdjpyValidation.SecurityIDSource -ne "8") {
    Fail-Gate "EXEC_SIM_R033_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY inversion/caveat mismatch."
}
foreach ($pair in @(@("USDCAD","CADUSD"), @("USDCHF","CHFUSD"))) {
    $item = @($inversion.validations | Where-Object { $_.ExecutionTradableSymbol -eq $pair[0] })[0]
    if ($null -eq $item -or $item.NormalizedPortfolioSymbol -ne $pair[1] -or -not [bool]$item.RequiresInversion) {
        Fail-Gate "EXEC_SIM_R033_FAIL_USDJPY_CAVEAT_WEAKENED" "$($pair[0]) inversion mismatch."
    }
}
Require-True ([bool]$direct.directCrossExclusionPreserved) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Direct-cross exclusion missing."
Require-False ([bool]$direct.directCrossIncluded) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Direct-cross included."
Require-False ([bool]$direct.directCrossExecutionAllowedByDefault) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Direct-cross execution allowed."
Require-False ([bool]$direct.guidanceWeakened) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Direct-cross guidance weakened."
Require-True ([bool]$cost.fiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R033_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million best-case missing."
Require-False ([bool]$cost.fiveUsdPerMillionUniversalized) "EXEC_SIM_R033_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$nonmajor.RequiresLiquidityCalibration) "EXEC_SIM_R033_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration missing."
Require-False ([bool]$nonmajor.calibrationRequirementWeakened) "EXEC_SIM_R033_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration weakened."

Require-True ([bool]$needs.needsOperatorInputCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Needs operator input artifact missing."
Require-True ([bool]$needs.NeedsOperatorSessionTimes) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "NeedsOperatorSessionTimes not emitted."
Require-True ([bool]$needs.NeedsOperatorDateRanges) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "NeedsOperatorDateRanges not emitted."
Require-False ([bool]$needs.ExactSessionTimesInvented) "EXEC_SIM_R033_FAIL_SESSION_TIMES_INVENTED" "Needs-input artifact invented session times."
Require-False ([bool]$needs.ExactDateRangesInvented) "EXEC_SIM_R033_FAIL_DATE_RANGES_INVENTED" "Needs-input artifact invented date ranges."
Require-Contains @($needs.SafeClassifications) "EXEC_SIM_R033_NEEDS_OPERATOR_SESSION_TIMES_NO_EXTERNAL" "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Needs session classification missing."
Require-Contains @($needs.SafeClassifications) "EXEC_SIM_R033_NEEDS_OPERATOR_DATE_RANGES_NO_EXTERNAL" "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Needs dates classification missing."
Require-True ([bool]$statuses.readinessStatusesCreated) "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Readiness statuses missing."
Require-Contains @($statuses.CurrentStatuses) "NeedsOperatorSessionTimes" "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Current session status missing."
Require-Contains @($statuses.CurrentStatuses) "NeedsOperatorDateRanges" "EXEC_SIM_R033_FAIL_OPERATOR_REQUIREMENTS_MISSING" "Current date status missing."

Require-False ([bool]$noDownload.filesDownloaded) "EXEC_SIM_R033_FAIL_DOWNLOAD_EXECUTED" "Files downloaded."
Require-False ([bool]$noValidation.quoteRowsValidated) "EXEC_SIM_R033_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED" "Quote rows validated."
Require-False ([bool]$noValidation.quotesImportedIntoDb) "EXEC_SIM_R033_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED" "Quotes imported."
Require-False ([bool]$noValidation.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R033_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED" "Sanitized rows persisted."
Require-False ([bool]$noValidation.newBacktestExecuted) "EXEC_SIM_R033_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED" "Backtest executed."
Require-False ([bool]$noValidation.newSimulationExecuted) "EXEC_SIM_R033_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED" "Simulation executed."
Require-False ([bool]$noTca.tcaResultLinesProduced) "EXEC_SIM_R033_FAIL_TCA_RESULTS_PRODUCED" "TCA result lines produced."
Require-False ([bool]$api.polygonApiCalled) "EXEC_SIM_R033_FAIL_API_CALL_DETECTED" "Polygon API called."
Require-False ([bool]$api.lmaxCalled) "EXEC_SIM_R033_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$api.externalApiCalled) "EXEC_SIM_R033_FAIL_API_CALL_DETECTED" "External API called."
Require-False ([bool]$runtime.brokerActivationDetected) "EXEC_SIM_R033_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker activation detected."
Require-False ([bool]$runtime.socketOpened) "EXEC_SIM_R033_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Socket opened."
Require-False ([bool]$runtime.tlsOpened) "EXEC_SIM_R033_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "TLS opened."
Require-False ([bool]$runtime.fixOpened) "EXEC_SIM_R033_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "FIX opened."
Require-False ([bool]$runtime.marketDataRequestSent) "EXEC_SIM_R033_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataRequest sent."
Require-False ([bool]$runtime.marketDataResponseRead) "EXEC_SIM_R033_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataResponse read."
Require-False ([bool]$runtime.schedulerServiceTimerPollingBackgroundJobIntroduced) "EXEC_SIM_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/timer/background job introduced."
Require-False ([bool]$runtime.automaticExecutionIntroduced) "EXEC_SIM_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Automatic execution introduced."
Require-False ([bool]$order.ordersCreated) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$order.fillsCreated) "EXEC_SIM_R033_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$order.executionReportsCreated) "EXEC_SIM_R033_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$order.routesCreated) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$order.submissionsCreated) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-False ([bool]$order.liveBrokerProductionTradingStateMutated) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "State mutated."
Require-False ([bool]$order.paperLedgerStateCommitted) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Paper ledger committed."
Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R033_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not [bool]$usdjpy.RequiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    Fail-Gate "EXEC_SIM_R033_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
}
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_SIM_R033_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R033_FAIL_API_CALL_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.lmaxCalledInR033) "EXEC_SIM_R033_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$noExternal.externalApiCalled) "EXEC_SIM_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "External API called."
Require-False ([bool]$noExternal.filesDownloaded) "EXEC_SIM_R033_FAIL_DOWNLOAD_EXECUTED" "Files downloaded."
Require-False ([bool]$noExternal.quoteRowsValidated) "EXEC_SIM_R033_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED" "Quote rows validated."
Require-False ([bool]$noExternal.quotesImportedIntoDb) "EXEC_SIM_R033_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED" "Quotes imported."
Require-False ([bool]$noExternal.newSimulationOrBacktestExecuted) "EXEC_SIM_R033_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED" "Simulation/backtest executed."
Require-False ([bool]$noExternal.tcaResultLinesProduced) "EXEC_SIM_R033_FAIL_TCA_RESULTS_PRODUCED" "TCA lines produced."
Require-False ([bool]$noExternal.executableSchedulesCreated) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable schedules created."
Require-False ([bool]$noExternal.ordersFillsReportsRoutesSubmissionsCreated) "EXEC_SIM_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order-domain outputs created."
Require-False ([bool]$forbidden.forbiddenActionsDetected) "EXEC_SIM_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden actions detected."

if ($evidence.dotnetBuildNoRestore -notlike "PASS*" -or $evidence.focusedTests -notlike "PASS*" -or $evidence.unitTests -notlike "PASS*" -or $evidence.validator -notlike "PASS*") {
    Fail-Gate "EXEC_SIM_R033_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence is missing or not passing."
}

Write-Host "EXEC_SIM_R033_PASS_SESSION_TIME_READINESS_CONTRACT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R033_PASS_ADDITIONAL_DATE_RANGE_REQUIREMENTS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R033_PASS_OPERATOR_INPUT_REQUIREMENTS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R033_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R033_NEEDS_OPERATOR_SESSION_TIMES_NO_EXTERNAL"
Write-Host "EXEC_SIM_R033_NEEDS_OPERATOR_DATE_RANGES_NO_EXTERNAL"

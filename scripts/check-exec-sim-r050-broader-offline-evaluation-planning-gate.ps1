$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactsRoot = Join-Path $RepoRoot "artifacts\readiness\execution-sim"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R050 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Has-Prop($Object, [string]$Name) {
    return $Object.PSObject.Properties.Name -contains $Name
}

function Field($Object, [string]$Name) {
    if (-not (Has-Prop $Object $Name)) {
        return $null
    }
    return $Object.$Name
}

function Assert-FalseIfPresent($Object, [string]$Name, [string]$Message) {
    if ((Has-Prop $Object $Name) -and $Object.$Name -ne $false) {
        Fail $Message
    }
}

function Assert-TrueIfPresent($Object, [string]$Name, [string]$Message) {
    if ((Has-Prop $Object $Name) -and $Object.$Name -ne $true) {
        Fail $Message
    }
}

$requiredArtifacts = @(
    "phase-exec-sim-r050-summary.md",
    "phase-exec-sim-r050-r009-contract-reference.json",
    "phase-exec-sim-r050-r049-result-reference.json",
    "phase-exec-sim-r050-broader-offline-evaluation-plan.json",
    "phase-exec-sim-r050-additional-date-coverage-recommendation.json",
    "phase-exec-sim-r050-market-regime-coverage-recommendation.json",
    "phase-exec-sim-r050-instrument-coverage-recommendation.json",
    "phase-exec-sim-r050-core-usd-pair-universe-preservation.json",
    "phase-exec-sim-r050-deferred-instrument-calibration.json",
    "phase-exec-sim-r050-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r050-future-download-requirements.json",
    "phase-exec-sim-r050-future-validation-requirements.json",
    "phase-exec-sim-r050-future-simulation-report-requirements.json",
    "phase-exec-sim-r050-design-only-success-criteria.json",
    "phase-exec-sim-r050-stop-hold-criteria.json",
    "phase-exec-sim-r050-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r050-legacy-compatibility-preservation.json",
    "phase-exec-sim-r050-cost-guidance-preservation.json",
    "phase-exec-sim-r050-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r050-non-executable-status-preservation.json",
    "phase-exec-sim-r050-no-download-audit.json",
    "phase-exec-sim-r050-no-validation-import-backtest-audit.json",
    "phase-exec-sim-r050-no-tca-result-lines-audit.json",
    "phase-exec-sim-r050-no-polygon-api-call-audit.json",
    "phase-exec-sim-r050-no-lmax-call-audit.json",
    "phase-exec-sim-r050-no-external-api-call-audit.json",
    "phase-exec-sim-r050-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r050-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r050-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r050-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r050-no-external-audit.json",
    "phase-exec-sim-r050-forbidden-actions-audit.json",
    "phase-exec-sim-r050-next-phase-recommendation.json",
    "phase-exec-sim-r050-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$r009 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-r009-contract-reference.json")
if ($r009.ContractVersion -ne "0.3.0-design-only-candidate") { Fail "R009 contract version missing" }
if ($r009.PrimaryCandidate -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") { Fail "R009 primary candidate missing" }
if ($r009.SecondaryCandidate -ne "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0") { Fail "R009 secondary candidate missing" }
if ($r009.ConditionalResidualModule -ne "ControlledResidualCross_BalancedResidualCross_v0") { Fail "R009 conditional module missing" }
Assert-TrueIfPresent $r009 "DesignOnly" "R009 reference is not design-only"
Assert-TrueIfPresent $r009 "NonExecutable" "R009 reference is not non-executable"
Assert-FalseIfPresent $r009 "ExecutablePromotionAuthorized" "R009 reference authorizes executable promotion"

$r049 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-r049-result-reference.json")
if ((Field $r049 "SimulatedVariantCount") -ne 8) { Fail "R049 variant count missing" }
if ((Field $r049 "QuoteWindowCount") -ne 945) { Fail "R049 quote window count missing" }
if ((Field $r049 "CandidateTcaResultCoverage") -ne "7560/7560") { Fail "R049 TCA coverage missing" }
Assert-FalseIfPresent $r049 "ExecutablePromotionAuthorized" "R049 reference authorizes executable promotion"

$plan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-broader-offline-evaluation-plan.json")
$minDates = Field $plan "MinimumAdditionalTradingDates"
if ($null -eq $minDates) { $minDates = Field $plan "MinimumAdditionalTradingDatesBeforePromotionDiscussion" }
if ($minDates -lt 20) { Fail "Additional date plan is below 20 trading dates" }
Assert-FalseIfPresent $plan "DownloadsExecuted" "Plan says downloads executed"
Assert-FalseIfPresent $plan "BacktestExecuted" "Plan says backtest executed"
Assert-FalseIfPresent $plan "SimulationExecuted" "Plan says simulation executed"
Assert-FalseIfPresent $plan "TcaResultLinesProduced" "Plan says TCA lines produced"
Assert-FalseIfPresent $plan "ExecutablePromotionAuthorized" "Plan authorizes executable promotion"

$dates = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-additional-date-coverage-recommendation.json")
if ($dates.MinimumAdditionalTradingDates -lt 20) { Fail "Date recommendation below 20 trading dates" }
Assert-FalseIfPresent $dates "DownloadsExecuted" "Downloads executed in date recommendation"
Assert-FalseIfPresent $dates "DownloadsExecutedInR050" "Downloads executed in date recommendation"

$regimes = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-market-regime-coverage-recommendation.json")
$regimeCount = 0
if (Has-Prop $regimes "RegimeBucketsRecommended") { $regimeCount = $regimes.RegimeBucketsRecommended.Count }
elseif (Has-Prop $regimes "RequiredRegimeBuckets") { $regimeCount = $regimes.RequiredRegimeBuckets.Count }
elseif (Has-Prop $regimes "MarketRegimeBuckets") { $regimeCount = $regimes.MarketRegimeBuckets.Count }
if ($regimeCount -lt 4) { Fail "Regime coverage recommendation incomplete" }
Assert-FalseIfPresent $regimes "SimulationExecuted" "Regime artifact indicates simulation"

$instruments = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-instrument-coverage-recommendation.json")
$core = Field $instruments "CoreUsdPairUniverse"
if ($null -eq $core) { $core = Field $instruments "CoreSymbols" }
if ($core.Count -ne 7) { Fail "Instrument recommendation missing core USD pairs" }
Assert-FalseIfPresent $instruments "DirectCrossesAsExecutionInstruments" "Direct crosses included as execution instruments"
Assert-FalseIfPresent $instruments "ExecutablePromotionAuthorized" "Instrument recommendation authorizes executable promotion"
Assert-FalseIfPresent $instruments "NonMajorEmScandiCnhNow" "Nonmajor/EM/scandi/CNH included immediately"

$coreUniverse = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-core-usd-pair-universe-preservation.json")
$coreSymbols = Field $coreUniverse "CoreSymbols"
if ($null -eq $coreSymbols) { $coreSymbols = Field $coreUniverse "Symbols" }
if ($coreSymbols.Count -ne 7) { Fail "Core USD-pair universe not preserved" }
Assert-FalseIfPresent $coreUniverse "ExtendCoreUniverseNow" "Core universe extended immediately"
Assert-FalseIfPresent $coreUniverse "ExecutablePromotionAuthorized" "Core universe authorizes executable promotion"
Assert-FalseIfPresent $coreUniverse "AudUsdMisclassified" "AUDUSD misclassified"
Assert-FalseIfPresent $coreUniverse "UsdJpyCaveatWeakened" "USDJPY caveat weakened"

$deferred = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-deferred-instrument-calibration.json")
Assert-FalseIfPresent $deferred "ImmediateCoreInclusion" "Deferred instruments included immediately"
Assert-FalseIfPresent $deferred "FiveUsdPerMillionUniversalized" "5 USD/million universalized in deferred calibration"
$requiresLiquidity = (Field $deferred "RequiresLiquidityCalibration")
$requiresNonmajor = (Field $deferred "NonmajorEmScandiCnhCalibrationRequired")
if ($requiresLiquidity -ne $true -and $requiresNonmajor -ne $true) { Fail "Deferred instrument calibration requirement missing" }

$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-direct-cross-exclusion-preservation.json")
Assert-TrueIfPresent $direct "DirectCrossExecutionDisabled" "Direct cross execution not disabled"
Assert-FalseIfPresent $direct "DirectCrossExclusionWeakened" "Direct-cross exclusion weakened"

$futureDownload = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-future-download-requirements.json")
Assert-FalseIfPresent $futureDownload "DownloadsExecutedInR050" "Downloads executed in R050"
Assert-FalseIfPresent $futureDownload "DownloadCommandsGeneratedInR050" "Download commands generated in R050"
Assert-FalseIfPresent $futureDownload "ExternalApiCalled" "External API called in future download planning"
Assert-FalseIfPresent $futureDownload "PolygonApiCalled" "Polygon called in future download planning"
Assert-FalseIfPresent $futureDownload "LmaxCalled" "LMAX called in future download planning"

$futureValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-future-validation-requirements.json")
Assert-FalseIfPresent $futureValidation "ValidationExecutedInR050" "Validation executed in R050"

$futureSimulation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-future-simulation-report-requirements.json")
Assert-FalseIfPresent $futureSimulation "SimulationExecutedInR050" "Simulation executed in R050"
Assert-TrueIfPresent $futureSimulation "NoTcaResultLinesInR050" "Future simulation artifact weakens no TCA in R050"

$success = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-design-only-success-criteria.json")
Assert-FalseIfPresent $success "ExecutablePromotionAuthorized" "Success criteria authorizes executable promotion"
Assert-FalseIfPresent $success "LiveTradingAuthorized" "Success criteria authorizes live trading"

$hold = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-stop-hold-criteria.json")
Assert-FalseIfPresent $hold "ExecutablePromotionAuthorized" "Stop/hold criteria authorizes executable promotion"

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-canonical-quarter-hour-policy-preservation.json")
Assert-FalseIfPresent $canonical "CanonicalQuarterHourPolicyWeakened" "Canonical quarter-hour policy weakened"

$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-legacy-compatibility-preservation.json")
Assert-FalseIfPresent $legacy "LegacyOutputTimestampsAreFutureCanonical" "Legacy timestamps used as future canonical"
Assert-FalseIfPresent $legacy "Legacy06UsedAsFutureCanonical" "Legacy :06 used as future canonical"

$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-cost-guidance-preservation.json")
Assert-FalseIfPresent $cost "FiveUsdPerMillionUniversalized" "5 USD/million universalized"

$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-nonmajor-calibration-preservation.json")
Assert-FalseIfPresent $nonmajor "NonmajorCalibrationWeakened" "Nonmajor calibration weakened"

$nonExec = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-non-executable-status-preservation.json")
Assert-FalseIfPresent $nonExec "ExecutablePromotionAuthorized" "Executable promotion authorized"
Assert-FalseIfPresent $nonExec "LiveTradingAuthorized" "Live trading authorized"

$downloadAudit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-no-download-audit.json")
foreach ($flag in @("FilesDownloaded", "DownloadCommandsExecuted", "DownloadCommandsGenerated")) {
    Assert-FalseIfPresent $downloadAudit $flag "Download forbidden action detected: $flag"
}

$validationAudit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-no-validation-import-backtest-audit.json")
foreach ($flag in @("QuoteRowsValidated", "QuoteRowsImported", "DbImportOccurred", "DbImportExecuted", "PersistedSanitizedRowsCreated", "BacktestExecuted", "SimulationExecuted")) {
    Assert-FalseIfPresent $validationAudit $flag "Validation/import/backtest forbidden action detected: $flag"
}

$tcaAudit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-no-tca-result-lines-audit.json")
Assert-FalseIfPresent $tcaAudit "TcaResultLinesProduced" "TCA result lines produced"

foreach ($fileAndField in @(
    @("phase-exec-sim-r050-no-polygon-api-call-audit.json", "PolygonApiCalled"),
    @("phase-exec-sim-r050-no-lmax-call-audit.json", "LmaxCalled"),
    @("phase-exec-sim-r050-no-external-api-call-audit.json", "ExternalApiCalled"),
    @("phase-exec-sim-r050-no-broker-marketdata-runtime-audit.json", "BrokerMarketDataRuntimeStarted")
)) {
    $doc = Read-Json (Join-Path $ArtifactsRoot $fileAndField[0])
    Assert-FalseIfPresent $doc $fileAndField[1] "Forbidden runtime/API action detected: $($fileAndField[1])"
}

$orderAudit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-no-order-fill-report-route-audit.json")
foreach ($flag in @("OrdersCreated", "FillsCreated", "ExecutionReportsCreated", "RoutesCreated", "SubmissionsCreated", "ChildSlicesCreated", "ChildOrdersCreated", "ExecutableSchedulesCreated")) {
    Assert-FalseIfPresent $orderAudit $flag "Order-domain forbidden action detected: $flag"
}

$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-usdjpy-caveat-preservation.json")
Assert-FalseIfPresent $usdjpy "UsdJpyCaveatWeakened" "USDJPY caveat weakened"
if ((Field $usdjpy "SecurityID") -ne "4004" -or (Field $usdjpy "SecurityIDSource") -ne "8") {
    Fail "USDJPY SecurityID caveat missing"
}

$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-no-external-audit.json")
Assert-FalseIfPresent $noExternal "PolygonApiCalled" "Polygon API called"
Assert-FalseIfPresent $noExternal "LmaxCalled" "LMAX called"
Assert-FalseIfPresent $noExternal "ExternalApiCalled" "External API called"
Assert-FalseIfPresent $noExternal "FilesDownloaded" "Files downloaded"

$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-forbidden-actions-audit.json")
foreach ($flag in @("ForbiddenActionsDetected", "FilesDownloaded", "QuoteRowsValidated", "QuoteRowsImported", "DbImportOccurred", "DbImportExecuted", "PersistedSanitizedRowsCreated", "BacktestExecuted", "SimulationExecuted", "TcaResultLinesProduced", "ExecutableSchedulesCreated", "ChildSlicesCreated", "ChildOrdersCreated", "OrdersCreated", "FillsCreated", "ExecutionReportsCreated", "RoutesCreated", "SubmissionsCreated", "StateMutated", "ExecutablePromotionAuthorized")) {
    Assert-FalseIfPresent $forbidden $flag "Forbidden action detected: $flag"
}

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) { Fail "Build evidence missing or failing" }
if ($evidence.FocusedR050StaticChecks.Status -ne "PASS") { Fail "Focused R050 static checks missing or failing" }
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) { Fail "Unit test evidence missing" }

Write-Host "EXEC-SIM-R050 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R050_PASS_BROADER_OFFLINE_EVALUATION_PLAN_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R050_PASS_ADDITIONAL_DATE_REGIME_REQUIREMENTS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R050_PASS_INSTRUMENT_COVERAGE_DECISION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R050_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"

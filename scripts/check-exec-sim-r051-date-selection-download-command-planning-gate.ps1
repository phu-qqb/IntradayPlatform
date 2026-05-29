$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactsRoot = Join-Path $RepoRoot "artifacts\readiness\execution-sim"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R051 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Assert-FalseField($Object, [string]$FieldName, [string]$Message) {
    if (-not ($Object.PSObject.Properties.Name -contains $FieldName)) {
        Fail "Missing field $FieldName"
    }
    if ($Object.$FieldName -ne $false) {
        Fail $Message
    }
}

function Assert-TrueField($Object, [string]$FieldName, [string]$Message) {
    if (-not ($Object.PSObject.Properties.Name -contains $FieldName)) {
        Fail "Missing field $FieldName"
    }
    if ($Object.$FieldName -ne $true) {
        Fail $Message
    }
}

$requiredArtifacts = @(
    "phase-exec-sim-r051-summary.md",
    "phase-exec-sim-r051-r050-plan-reference.json",
    "phase-exec-sim-r051-r038-session-reference.json",
    "phase-exec-sim-r051-available-legacy-date-source.json",
    "phase-exec-sim-r051-selected-additional-dates.json",
    "phase-exec-sim-r051-date-selection-rationale.json",
    "phase-exec-sim-r051-date-regime-labels.json",
    "phase-exec-sim-r051-derived-utc-windows.json",
    "phase-exec-sim-r051-download-command-generation-contract.json",
    "phase-exec-sim-r051-operator-download-commands.md",
    "phase-exec-sim-r051-operator-download-commands.json",
    "phase-exec-sim-r051-powershell-command-plan.ps1.txt",
    "phase-exec-sim-r051-expected-file-counts.json",
    "phase-exec-sim-r051-expected-file-naming-plan.json",
    "phase-exec-sim-r051-operator-post-download-checklist.md",
    "phase-exec-sim-r051-r052-file-authorization-requirements.json",
    "phase-exec-sim-r051-core-usd-pair-universe-preservation.json",
    "phase-exec-sim-r051-inversion-preservation.json",
    "phase-exec-sim-r051-canonical-session-preservation.json",
    "phase-exec-sim-r051-legacy-compatibility-preservation.json",
    "phase-exec-sim-r051-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r051-cost-guidance-preservation.json",
    "phase-exec-sim-r051-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r051-no-download-audit.json",
    "phase-exec-sim-r051-no-validation-import-backtest-audit.json",
    "phase-exec-sim-r051-no-tca-result-lines-audit.json",
    "phase-exec-sim-r051-no-polygon-api-call-audit.json",
    "phase-exec-sim-r051-no-lmax-call-audit.json",
    "phase-exec-sim-r051-no-external-api-call-audit.json",
    "phase-exec-sim-r051-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r051-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r051-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r051-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r051-no-external-audit.json",
    "phase-exec-sim-r051-forbidden-actions-audit.json",
    "phase-exec-sim-r051-next-phase-recommendation.json",
    "phase-exec-sim-r051-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$knownEvidenceDates = @(
    "2025-10-10","2025-10-14","2025-10-15","2025-10-16","2025-10-17","2025-10-20",
    "2025-10-21","2025-10-22","2025-10-23","2025-10-24","2025-10-27","2025-10-28",
    "2025-10-29","2025-10-30","2025-10-31","2025-11-03","2025-11-04","2025-11-05",
    "2025-11-06","2025-11-07","2025-11-10","2025-11-12","2025-11-13","2025-11-14",
    "2025-11-17","2025-11-18","2025-11-19","2025-11-21","2025-11-24","2025-11-25",
    "2025-11-26","2025-11-27","2025-11-28","2025-12-01","2025-12-02","2025-12-03",
    "2025-12-04","2025-12-05","2025-12-08","2025-12-09","2025-12-10","2025-12-11",
    "2025-12-12","2025-12-15","2025-12-16"
)
$previouslyTested = @("2025-10-14","2025-10-15","2025-10-16","2025-10-17","2025-10-20")
$partialOutliers = @("2025-10-10","2025-11-21","2025-11-27","2025-11-28")

$source = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-available-legacy-date-source.json")
if ($source.UniqueLegacyTradingDateCount -ne 45) { Fail "Legacy date source does not report 45 dates" }
if ($source.WeekendTimestampCount -ne 0) { Fail "Legacy source reports weekend timestamps" }
Assert-TrueField $source "EligibleDateEvidenceSufficientFor20AdditionalDates" "Eligible date evidence not sufficient"
Assert-FalseField $source "NeedsOperatorDateSelection" "Needs operator date selection despite selected dates"
Assert-FalseField $source "NeedsOperatorAdditionalDates" "Needs operator additional dates despite selected dates"

$selected = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-selected-additional-dates.json")
if ($selected.SelectedDateCount -ne 20) { Fail "Selected date count is not 20" }
Assert-TrueField $selected "PreviouslyTestedDatesExcluded" "Previously tested dates not excluded"
Assert-TrueField $selected "PartialOutlierDaysExcluded" "Partial outlier days not excluded"
Assert-FalseField $selected "WeekendDatesSelected" "Weekend dates selected"

$selectedDates = @($selected.SelectedDates | ForEach-Object { $_.LocalSessionDate })
if (($selectedDates | Select-Object -Unique).Count -ne 20) { Fail "Selected dates are not unique" }
foreach ($date in $selectedDates) {
    if ($knownEvidenceDates -notcontains $date) { Fail "Selected date invented outside evidence: $date" }
    if ($previouslyTested -contains $date) { Fail "Previously tested R044/R049 date selected: $date" }
    if ($partialOutliers -contains $date) { Fail "Partial/outlier date selected: $date" }
    $day = ([datetime]$date).DayOfWeek
    if ($day -in @([DayOfWeek]::Saturday, [DayOfWeek]::Sunday)) { Fail "Weekend selected: $date" }
}

$rationale = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-date-selection-rationale.json")
Assert-TrueField $rationale "EligibleEvidenceWasSufficient" "Rationale does not confirm sufficient evidence"
Assert-FalseField $rationale "NeedsOperatorDateSelection" "Rationale still needs operator date selection"
Assert-FalseField $rationale "NeedsOperatorAdditionalDates" "Rationale still needs additional dates"

$regimes = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-date-regime-labels.json")
Assert-FalseField $regimes "RegimeLabelEvidenceAvailable" "Regime evidence unexpectedly claimed"
Assert-FalseField $regimes "MacroLabelsInvented" "Macro labels invented"
Assert-FalseField $regimes "VolatilityLabelsInvented" "Volatility labels invented"
if ($regimes.Labels.Count -ne 20) { Fail "Regime label count not 20" }

$windows = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-derived-utc-windows.json")
if ($windows.WindowCount -ne 20) { Fail "UTC window count is not 20" }
Assert-TrueField $windows "TimezoneAwareDerivation" "Timezone-aware UTC derivation missing"
Assert-TrueField $windows "DstRespected" "DST not respected"
Assert-FalseField $windows "UtcOffsetHardCoded" "UTC offset hard-coded"
foreach ($window in $windows.Windows) {
    if ($selectedDates -notcontains $window.LocalSessionDate) { Fail "UTC window date not selected: $($window.LocalSessionDate)" }
    if ([string]::IsNullOrWhiteSpace($window.UtcWindowStart) -or [string]::IsNullOrWhiteSpace($window.UtcWindowEnd)) {
        Fail "UTC window missing start/end"
    }
    if ($window.Suffix -notmatch '^\d{14}-\d{14}$') { Fail "Invalid suffix for $($window.LocalSessionDate)" }
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-download-command-generation-contract.json")
Assert-TrueField $contract "CommandsAreTextOnly" "Commands not marked text-only"
Assert-FalseField $contract "CommandsExecutedInR051" "Commands executed in R051"
Assert-TrueField $contract "NoPolygonCall" "Polygon no-call flag missing"
Assert-TrueField $contract "NoExternalApiCall" "External API no-call flag missing"
Assert-TrueField $contract "NoDownload" "No-download flag missing"
if ($contract.ExpectedQuoteFiles -ne 140 -or $contract.ExpectedManifestFiles -ne 140) {
    Fail "Command contract expected file counts incorrect"
}

$commands = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-operator-download-commands.json")
Assert-TrueField $commands "CommandsAreTextOnly" "Command JSON not text-only"
Assert-FalseField $commands "CommandsExecutedInR051" "Command JSON says commands executed"
Assert-FalseField $commands "DownloadExecuted" "Command JSON says download executed"
Assert-FalseField $commands "FilesDownloaded" "Command JSON says files downloaded"
if ($commands.Commands.Count -ne 20) { Fail "Command count is not 20" }
if ($commands.ExpectedFutureQuoteFileCount -ne 140 -or $commands.ExpectedFutureManifestCount -ne 140) {
    Fail "Command JSON expected file counts incorrect"
}

$commandPlanPath = Join-Path $ArtifactsRoot "phase-exec-sim-r051-powershell-command-plan.ps1.txt"
$commandPlanText = Get-Content -Raw -LiteralPath $commandPlanPath
if ($commandPlanText -notmatch "download-polygon-fx-bbo-offline.ps1") { Fail "PowerShell command plan missing download script reference" }
if ($commandPlanText -notmatch "Text artifact only") { Fail "PowerShell command plan not marked text artifact only" }

$counts = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-expected-file-counts.json")
if ($counts.SelectedDateCount -ne 20 -or $counts.SymbolCount -ne 7) { Fail "Expected file count inputs wrong" }
if ($counts.ExpectedFutureQuoteFileCount -ne 140 -or $counts.ExpectedFutureManifestCount -ne 140 -or $counts.ExpectedTotalFutureFiles -ne 280) {
    Fail "Expected file counts wrong"
}
Assert-FalseField $counts "FilesDownloadedInR051" "Expected file counts claim downloads"

$naming = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-expected-file-naming-plan.json")
if ($naming.LowerSymbols.Count -ne 7 -or $naming.Suffixes.Count -ne 20) { Fail "File naming plan missing symbols or suffixes" }
Assert-TrueField $naming "NoFileExistenceClaimInR051" "File naming plan claims file existence"

$r052 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-r052-file-authorization-requirements.json")
if ($r052.ExpectedEntryCount -ne 140 -or $r052.ExpectedQuoteFileCount -ne 140 -or $r052.ExpectedManifestCount -ne 140) {
    Fail "R052 expected counts missing"
}
foreach ($flag in @("R052MustNotValidateManifests", "R052MustNotValidateRows", "R052MustNotImport", "R052MustNotBacktest")) {
    Assert-TrueField $r052 $flag "R052 requirement missing: $flag"
}

$core = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-core-usd-pair-universe-preservation.json")
Assert-TrueField $core "CoreUsdPairUniversePreserved" "Core USD-pair universe not preserved"
Assert-FalseField $core "AudUsdMisclassified" "AUDUSD misclassified"
Assert-FalseField $core "ExecutablePromotionAuthorized" "Core universe authorizes executable promotion"

$inversion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-inversion-preservation.json")
Assert-TrueField $inversion "InversionsPreserved" "Inversions not preserved"
Assert-FalseField $inversion "InversionWeakened" "Inversion weakened"
if ($inversion.Mappings.USDJPY.SecurityID -ne "4004" -or $inversion.Mappings.USDJPY.SecurityIDSource -ne "8") { Fail "USDJPY caveat missing in inversion map" }

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-canonical-session-preservation.json")
Assert-TrueField $canonical "TimezoneAwareUtcWindowsDerived" "Timezone-aware windows not derived"
Assert-FalseField $canonical "CanonicalQuarterHourPolicyWeakened" "Canonical session weakened"

$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-legacy-compatibility-preservation.json")
Assert-FalseField $legacy "LegacyOutputTimestampsAreFutureCanonical" "Legacy timestamps used as future canonical"
Assert-FalseField $legacy "Legacy06UsedAsFutureCanonical" "Legacy :06 used as future canonical"

$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-direct-cross-exclusion-preservation.json")
Assert-TrueField $direct "DirectCrossExecutionDisabled" "Direct cross execution not disabled"
Assert-FalseField $direct "DirectCrossExclusionWeakened" "Direct cross exclusion weakened"

$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-cost-guidance-preservation.json")
Assert-FalseField $cost "FiveUsdPerMillionUniversalized" "5 USD/million universalized"

$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-nonmajor-calibration-preservation.json")
Assert-TrueField $nonmajor "NonmajorEmScandiCnhCalibrationRequired" "Nonmajor calibration missing"
Assert-FalseField $nonmajor "NonmajorCalibrationWeakened" "Nonmajor calibration weakened"

$downloadAudit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-no-download-audit.json")
foreach ($flag in @("FilesDownloaded", "DownloadCommandsExecuted", "PowerShellDownloadCommandsExecuted")) {
    Assert-FalseField $downloadAudit $flag "Download forbidden action detected: $flag"
}

$validationAudit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-no-validation-import-backtest-audit.json")
foreach ($flag in @("QuoteRowsValidated", "ManifestValidationExecuted", "QuoteRowsImported", "DbImportOccurred", "PersistedSanitizedRowsCreated", "BacktestExecuted", "SimulationExecuted")) {
    Assert-FalseField $validationAudit $flag "Validation/import/backtest forbidden action detected: $flag"
}

$tcaAudit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-no-tca-result-lines-audit.json")
Assert-FalseField $tcaAudit "TcaResultLinesProduced" "TCA result lines produced"

foreach ($fileAndField in @(
    @("phase-exec-sim-r051-no-polygon-api-call-audit.json", "PolygonApiCalled"),
    @("phase-exec-sim-r051-no-lmax-call-audit.json", "LmaxCalled"),
    @("phase-exec-sim-r051-no-external-api-call-audit.json", "ExternalApiCalled"),
    @("phase-exec-sim-r051-no-broker-marketdata-runtime-audit.json", "BrokerMarketDataRuntimeStarted")
)) {
    $doc = Read-Json (Join-Path $ArtifactsRoot $fileAndField[0])
    Assert-FalseField $doc $fileAndField[1] "Forbidden runtime/API action detected: $($fileAndField[1])"
}

$orderAudit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-no-order-fill-report-route-audit.json")
foreach ($flag in @("OrdersCreated", "FillsCreated", "ExecutionReportsCreated", "RoutesCreated", "SubmissionsCreated", "ChildSlicesCreated", "ChildOrdersCreated", "ExecutableSchedulesCreated")) {
    Assert-FalseField $orderAudit $flag "Order-domain forbidden action detected: $flag"
}

$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-usdjpy-caveat-preservation.json")
Assert-TrueField $usdjpy "UsdJpyCaveatPreserved" "USDJPY caveat not preserved"
Assert-FalseField $usdjpy "UsdJpyCaveatWeakened" "USDJPY caveat weakened"
if ($usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") { Fail "USDJPY SecurityID caveat missing" }

$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-no-external-audit.json")
Assert-TrueField $noExternal "NoExternal" "NoExternal audit missing"
Assert-FalseField $noExternal "PolygonApiCalled" "Polygon API called"
Assert-FalseField $noExternal "LmaxCalled" "LMAX called"
Assert-FalseField $noExternal "ExternalApiCalled" "External API called"
Assert-FalseField $noExternal "FilesDownloaded" "Files downloaded"

$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-forbidden-actions-audit.json")
foreach ($flag in @("ForbiddenActionsDetected", "FilesDownloaded", "PowerShellDownloadCommandsExecuted", "QuoteRowsValidated", "ManifestValidationExecuted", "QuoteRowsImported", "DbImportOccurred", "PersistedSanitizedRowsCreated", "BacktestExecuted", "SimulationExecuted", "TcaResultLinesProduced", "ExecutableSchedulesCreated", "ChildSlicesCreated", "ChildOrdersCreated", "OrdersCreated", "FillsCreated", "ExecutionReportsCreated", "RoutesCreated", "SubmissionsCreated", "StateMutated", "ExecutablePromotionAuthorized")) {
    Assert-FalseField $forbidden $flag "Forbidden action detected: $flag"
}

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) { Fail "Build evidence missing or failing" }
if ($evidence.FocusedR051StaticChecks.Status -ne "PASS") { Fail "Focused R051 static checks missing or failing" }
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) { Fail "Unit test evidence missing" }

Write-Host "EXEC-SIM-R051 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R051_PASS_ADDITIONAL_DATE_SELECTION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R051_PASS_OFFLINE_DOWNLOAD_COMMAND_PLAN_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R051_PASS_R052_FILE_AUTHORIZATION_REQUIREMENTS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R051_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"

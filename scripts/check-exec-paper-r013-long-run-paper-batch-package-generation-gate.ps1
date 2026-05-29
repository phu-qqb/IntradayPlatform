param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$classification, [string]$message) {
    Write-Error "$classification $message"
    exit 1
}

function Read-Json([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_PAPER_R013_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-paper-r013-summary.md",
    "phase-exec-paper-r013-r060-plan-reference.json",
    "phase-exec-paper-r013-r012-maturity-reference.json",
    "phase-exec-paper-r013-aggregatedweights-source-analysis.json",
    "phase-exec-paper-r013-target-close-selection-contract.json",
    "phase-exec-paper-r013-selected-legacy-groups.json",
    "phase-exec-paper-r013-bar-role-selection-results.json",
    "phase-exec-paper-r013-regime-labeling-results.json",
    "phase-exec-paper-r013-generated-fixture-inventory.json",
    "phase-exec-paper-r013-generated-fixture-validation.json",
    "phase-exec-paper-r013-batch-manifest.json",
    "phase-exec-paper-r013-batch-manifest-validation.json",
    "phase-exec-paper-r013-manual-noexternal-command-package.md",
    "phase-exec-paper-r013-manual-noexternal-command-package.json",
    "phase-exec-paper-r013-operator-run-package.md",
    "phase-exec-paper-r013-operator-run-package.json",
    "phase-exec-paper-r013-expected-r014-execution-shape.json",
    "phase-exec-paper-r013-expected-output-counts.json",
    "phase-exec-paper-r013-hold-missing-evidence-diagnostics.json",
    "phase-exec-paper-r013-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r013-legacy-compatibility-preservation.json",
    "phase-exec-paper-r013-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r013-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r013-cost-guidance-preservation.json",
    "phase-exec-paper-r013-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r013-no-broker-activation-audit.json",
    "phase-exec-paper-r013-no-live-marketdata-audit.json",
    "phase-exec-paper-r013-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r013-no-new-pms-cycle-audit.json",
    "phase-exec-paper-r013-no-manualnoexternal-command-run-audit.json",
    "phase-exec-paper-r013-no-new-backtest-audit.json",
    "phase-exec-paper-r013-no-new-simulation-audit.json",
    "phase-exec-paper-r013-no-tca-result-lines-audit.json",
    "phase-exec-paper-r013-no-executable-schedule-audit.json",
    "phase-exec-paper-r013-no-child-slices-audit.json",
    "phase-exec-paper-r013-no-child-orders-audit.json",
    "phase-exec-paper-r013-no-order-created-audit.json",
    "phase-exec-paper-r013-no-real-fill-audit.json",
    "phase-exec-paper-r013-no-execution-report-audit.json",
    "phase-exec-paper-r013-no-route-no-submission-audit.json",
    "phase-exec-paper-r013-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r013-no-polygon-api-call-audit.json",
    "phase-exec-paper-r013-no-lmax-call-audit.json",
    "phase-exec-paper-r013-no-external-api-call-audit.json",
    "phase-exec-paper-r013-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r013-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r013-no-external-audit.json",
    "phase-exec-paper-r013-forbidden-actions-audit.json",
    "phase-exec-paper-r013-next-phase-recommendation.json",
    "phase-exec-paper-r013-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_PAPER_R013_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r060 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-r060-plan-reference.json")
$maturity = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-r012-maturity-reference.json")
$source = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-aggregatedweights-source-analysis.json")
$selectionContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-target-close-selection-contract.json")
$selected = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-selected-legacy-groups.json")
$barRoles = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-bar-role-selection-results.json")
$regimes = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-regime-labeling-results.json")
$fixtureInventory = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-generated-fixture-inventory.json")
$fixtureValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-generated-fixture-validation.json")
$manifest = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-batch-manifest.json")
$manifestValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-batch-manifest-validation.json")
$commands = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-manual-noexternal-command-package.json")
$operator = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-operator-run-package.json")
$r014Shape = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-expected-r014-execution-shape.json")
$counts = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-expected-output-counts.json")
$diagnostics = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-hold-missing-evidence-diagnostics.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-build-test-validator-evidence.json")

if ($r060.MinimumTargetCloses -lt 100 -or
    $r060.MinimumOpeningBuildCloses -lt 30 -or
    $r060.MinimumIntradayRebalanceCloses -lt 30 -or
    $r060.MinimumClosingFlattenCloses -lt 30 -or
    -not $r060.ManualOnly -or
    $r060.SchedulerAllowed -or
    $r060.ServiceAllowed -or
    $r060.PollingAllowed -or
    $r060.AutomaticExecutionAllowed) {
    Fail "EXEC_PAPER_R013_FAIL_R060_REFERENCE_INVALID" "R060 plan reference is missing or unsafe."
}

if ($maturity.R009MaturityStatus -ne "StableForLongRunPaperOnlyExpansion" -or
    -not $maturity.AcceptedForLongRunPaperOnlyPlanning -or
    -not $maturity.DesignOnly -or
    -not $maturity.PaperOnly -or
    -not $maturity.NonExecutable -or
    -not $maturity.NotAnOrder -or
    -not $maturity.NotSubmitted -or
    -not $maturity.NoBrokerRoute -or
    $maturity.ExecutablePromotionAuthorized -or
    $maturity.BrokerReady -or
    $maturity.LiveReady) {
    Fail "EXEC_PAPER_R013_FAIL_R012_MATURITY_REFERENCE_INVALID" "R012 maturity reference is missing or executable."
}

if (-not $source.Exists -or
    $source.HeaderColumnCount -ne 91 -or
    $source.TickerMappingCount -ne 91 -or
    -not $source.ReadAsLocalTextOnly -or
    $source.ExternalApiCalled -or
    $source.CommandsExecutedDuringExtraction -or
    $source.EligibleCanonicalGroups -lt 100) {
    Fail "EXEC_PAPER_R013_FAIL_SOURCE_ANALYSIS_INVALID" "AggregatedWeights source analysis is invalid."
}

if (-not $selectionContract.KeepOnlyCanonicalQuarterHour -or
    $selectionContract.Legacy06UsedAsFutureCanonical -or
    $selectionContract.MinimumOpeningBuild -lt 30 -or
    $selectionContract.MinimumIntradayRebalance -lt 30 -or
    $selectionContract.MinimumClosingFlatten -lt 30 -or
    $selectionContract.RegimeLabelsInvented -or
    -not $selectionContract.UnknownRegimeAllowedWhenEvidenceUnavailable) {
    Fail "EXEC_PAPER_R013_FAIL_TARGET_CLOSE_SELECTION_CONTRACT" "Target close selection contract is unsafe."
}

if ($selected.SelectedGroupCount -ne 100 -or $selected.SelectionStatus -ne "SelectedFullLongRun100Groups") {
    Fail "EXEC_PAPER_R013_FAIL_SELECTED_GROUPS" "Expected 100 selected legacy groups."
}
foreach ($group in (As-Array $selected.Groups)) {
    if ($group.ValidWeightRowCount -ne 91 -or
        $group.CanonicalTargetCloseLocal -match "T\d{2}:(06|21|36|51):00" -or
        $group.CanonicalTargetCloseLocal -notmatch "T\d{2}:(00|15|30|45):00" -or
        [string]::IsNullOrWhiteSpace([string]$group.BarRole)) {
        Fail "EXEC_PAPER_R013_FAIL_SELECTED_GROUPS" "Selected group is invalid."
    }
}

if (-not $barRoles.OpeningBuildMinimumMet -or
    -not $barRoles.IntradayRebalanceMinimumMet -or
    -not $barRoles.ClosingFlattenMinimumMet) {
    Fail "EXEC_PAPER_R013_FAIL_BAR_ROLE_COVERAGE" "Bar-role minimums are not met."
}
foreach ($role in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    $entry = (As-Array $barRoles.Results) | Where-Object { $_.BarRole -eq $role } | Select-Object -First 1
    if ($null -eq $entry -or $entry.BatchEntryCount -lt 30) {
        Fail "EXEC_PAPER_R013_FAIL_BAR_ROLE_COVERAGE" "Expected at least 30 entries for $role."
    }
}

if ($regimes.RegimeLabelsInvented -or
    $regimes.RegimeLabelingMethod -ne "EvidenceUnavailableDefaultUnknown" -or
    $regimes.UnknownRegimeCount -ne 100) {
    Fail "EXEC_PAPER_R013_FAIL_REGIME_LABELS_INVENTED" "Regime labels were invented or not marked Unknown."
}

if ($fixtureInventory.FixtureCount -ne 100 -or $fixtureInventory.ExpectedFixtureCount -ne 100) {
    Fail "EXEC_PAPER_R013_FAIL_FIXTURE_INVENTORY" "Expected 100 generated fixtures."
}
foreach ($fixture in (As-Array $fixtureInventory.Inventory)) {
    if ($fixture.RowCount -ne 91 -or $fixture.ContainsTimestampRows) {
        Fail "EXEC_PAPER_R013_FAIL_FIXTURE_ROWS_INVALID" "Generated fixture includes invalid rows: $($fixture.FixturePath)"
    }
}
if (-not $fixtureValidation.AllFixturesValid -or $fixtureValidation.ValidFixtureCount -ne 100) {
    Fail "EXEC_PAPER_R013_FAIL_FIXTURE_VALIDATION" "Generated fixture validation failed."
}
foreach ($validation in (As-Array $fixtureValidation.Validation)) {
    if (-not $validation.Valid -or
        $validation.RowCount -ne 91 -or
        $validation.ContainsTimestampRows -or
        $validation.InvalidRowCount -ne 0) {
        Fail "EXEC_PAPER_R013_FAIL_FIXTURE_VALIDATION" "Invalid fixture validation entry."
    }
}

if ($manifest.ManifestStatus -ne "FullLongRunBatchReady" -or
    $manifest.BatchEntryCount -ne 100 -or
    $manifest.Legacy06UsedAsFutureCanonical -or
    -not $manifest.ManualOnly -or
    $manifest.SchedulerAllowed -or
    $manifest.ServiceAllowed -or
    $manifest.PollingAllowed -or
    $manifest.AutomaticExecutionAllowed -or
    $manifest.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R013_FAIL_BATCH_MANIFEST" "Batch manifest is incomplete or unsafe."
}
if (-not $manifestValidation.AllEntriesValid -or
    -not $manifestValidation.TargetClosesCanonicalQuarterHour -or
    $manifestValidation.Legacy06UsedAsFutureCanonical -or
    -not $manifestValidation.NoPaperLedgerCommitPreserved -or
    $manifestValidation.ContainsExecutablePermissionFields) {
    Fail "EXEC_PAPER_R013_FAIL_BATCH_MANIFEST_VALIDATION" "Batch manifest validation failed."
}

foreach ($entry in (As-Array $manifest.Entries)) {
    if (-not $entry.NoPaperLedgerCommit -or
        $entry.CadenceMinutes -ne 15 -or
        $entry.CanonicalTargetCloseLocal -match "T\d{2}:(06|21|36|51):00" -or
        $entry.CanonicalTargetCloseLocal -notmatch "T\d{2}:(00|15|30|45):00" -or
        $entry.FixtureSource -ne "LegacyAggregatedWeightsExtraction" -or
        -not $entry.LegacyCompatibilityMappingUsed -or
        -not $entry.ReadinessBindingRequired -or
        $entry.RiskOperatorApprovalScope -ne "DesignOnlyPreviewOnly" -or
        $entry.RegimeLabel -ne "Unknown") {
        Fail "EXEC_PAPER_R013_FAIL_BATCH_MANIFEST" "Manifest entry is invalid."
    }
}

if ($commands.CommandCount -ne 100 -or
    -not $commands.CommandsTextOnly -or
    $commands.CommandsExecuted -or
    -not $commands.ManualOnly -or
    $commands.SchedulerAllowed -or
    $commands.ServiceAllowed -or
    $commands.PollingAllowed -or
    $commands.AutomaticExecutionAllowed) {
    Fail "EXEC_PAPER_R013_FAIL_COMMAND_PACKAGE" "Command package is unsafe."
}
foreach ($command in (As-Array $commands.Commands)) {
    $line = [string]$command.CommandLine
    if ($command.CommandExecuted -or
        -not $command.NoPaperLedgerCommit -or
        -not $command.ManualOnly -or
        $line -notmatch "--mode ManualNoExternal" -or
        $line -notmatch "--output-artifacts-dir" -or
        $line -notmatch "--requested-cycle-run-id" -or
        $line -notmatch "--qubes-run-id" -or
        $line -notmatch "--qubes-fixture-path" -or
        $line -notmatch "--expected-cadence-minutes 15" -or
        $line -notmatch "--no-paper-ledger-commit true" -or
        $line -match "--mode no-external-paper-cycle" -or
        $line -match "\s--output\s" -or
        $line -match "--(broker|live|order|route|submit|fill|scheduler|service|poll)") {
        Fail "EXEC_PAPER_R013_FAIL_COMMAND_PACKAGE" "Unsafe command package entry."
    }
}

if ((As-Array $operator.CommandsToRunNow).Count -ne 0 -or
    -not $operator.ManualOnly -or
    -not $operator.SafetyValidationRequiredBeforeAnyFutureRun -or
    $operator.SchedulerAllowed -or
    $operator.ServiceAllowed -or
    $operator.PollingAllowed -or
    $operator.BrokerRuntimeAllowed -or
    $operator.LiveMarketDataAllowed -or
    $operator.OrderCreationAllowed -or
    $operator.RouteSubmissionAllowed -or
    $operator.PaperLedgerCommitAllowed) {
    Fail "EXEC_PAPER_R013_FAIL_OPERATOR_PACKAGE" "Operator package is unsafe."
}

if (-not $r014Shape.OneManualNoExternalRunPerAcceptedBatchEntry -or
    -not $r014Shape.CommandsSafetyValidatedBeforeRun -or
    $r014Shape.SchedulerServicePollingAllowed -or
    -not $r014Shape.NoPaperLedgerCommit -or
    -not $r014Shape.CollectPreviewLines -or
    -not $r014Shape.AggregateLongRunPreviewReview -or
    $r014Shape.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R013_FAIL_EXPECTED_R014_SHAPE" "Expected R014 shape is unsafe."
}

if ($counts.ExpectedFixtures -ne 100 -or
    $counts.ExpectedBatchEntries -ne 100 -or
    $counts.ExpectedMaximumPreviewLines -ne 700 -or
    -not $counts.FullPackage) {
    Fail "EXEC_PAPER_R013_FAIL_EXPECTED_OUTPUT_COUNTS" "Expected output counts are wrong."
}
if ($diagnostics.InvalidFixtureCount -ne 0 -or
    $diagnostics.MissingTargetCloseCount -ne 0 -or
    -not $diagnostics.MissingRegimeEvidence -or
    -not $diagnostics.RegimeLabelDefaultedToUnknown) {
    Fail "EXEC_PAPER_R013_FAIL_HOLD_DIAGNOSTICS" "Hold/missing evidence diagnostics are incomplete."
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R013_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical quarter-hour policy is weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R013_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Legacy compatibility is weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or -not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_PAPER_R013_FAIL_AUDUSD_MISCLASSIFIED" "USD-pair normalization or AUDUSD status is weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossNettingFirst -or -not $directCross.DirectCrossExecutionDisabled -or $directCross.ExclusionWeakened) {
    Fail "EXEC_PAPER_R013_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion is weakened."
}
if ($cost.FiveUsdPerMillionUniversalized -or $cost.FiveUsdPerMillion -ne "BestCaseMajorOnly" -or -not $cost.NonmajorCalibrationRequired) {
    Fail "EXEC_PAPER_R013_FAIL_COST_GUIDANCE_UNIVERSALIZED" "Cost guidance is weakened."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_PAPER_R013_FAIL_NONMAJOR_CALIBRATION_WEAKENED" "Nonmajor calibration is weakened."
}
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    -not $usdjpy.RequiresInversion -or
    $usdjpy.SecurityID -ne 4004 -or
    [string]$usdjpy.SecurityIDSource -ne "8" -or
    $usdjpy.CaveatWeakened) {
    Fail "EXEC_PAPER_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat is weakened."
}

foreach ($auditName in @(
    "phase-exec-paper-r013-no-broker-activation-audit.json",
    "phase-exec-paper-r013-no-live-marketdata-audit.json",
    "phase-exec-paper-r013-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r013-no-new-pms-cycle-audit.json",
    "phase-exec-paper-r013-no-manualnoexternal-command-run-audit.json",
    "phase-exec-paper-r013-no-new-backtest-audit.json",
    "phase-exec-paper-r013-no-new-simulation-audit.json",
    "phase-exec-paper-r013-no-tca-result-lines-audit.json",
    "phase-exec-paper-r013-no-executable-schedule-audit.json",
    "phase-exec-paper-r013-no-child-slices-audit.json",
    "phase-exec-paper-r013-no-child-orders-audit.json",
    "phase-exec-paper-r013-no-order-created-audit.json",
    "phase-exec-paper-r013-no-real-fill-audit.json",
    "phase-exec-paper-r013-no-execution-report-audit.json",
    "phase-exec-paper-r013-no-route-no-submission-audit.json",
    "phase-exec-paper-r013-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r013-no-polygon-api-call-audit.json",
    "phase-exec-paper-r013-no-lmax-call-audit.json",
    "phase-exec-paper-r013-no-external-api-call-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_PAPER_R013_FAIL_FORBIDDEN_ACTION_DETECTED" "Audit failed: $auditName"
    }
}

if (-not $noExternal.NoExternal -or $noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.DownloadsExecuted) {
    Fail "EXEC_PAPER_R013_FAIL_EXTERNAL_API_CALLED" "No-external audit failed."
}
if ($forbidden.ForbiddenActionsDetected -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.NewPmsCycle -or
    $forbidden.ManualNoExternalCommandsRun -or
    $forbidden.BacktestOrSimulation -or
    $forbidden.TcaResultLinesCreated -or
    $forbidden.ExecutableSchedule -or
    $forbidden.ChildSlicesOrOrders -or
    $forbidden.OrdersFillsReportsRoutesSubmissions -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation -or
    $forbidden.R009ExecutablePromotion) {
    Fail "EXEC_PAPER_R013_FAIL_FORBIDDEN_ACTION_DETECTED" "Forbidden action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR013Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R013Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_PAPER_R013_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE_MISSING" "Build/tests/validator evidence missing or incomplete."
}

Write-Host "EXEC-PAPER-R013 validation passed"

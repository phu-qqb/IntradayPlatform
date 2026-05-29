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
        Fail "EXEC_PAPER_R015_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-paper-r015-summary.md",
    "phase-exec-paper-r015-r014-held-line-reference.json",
    "phase-exec-paper-r015-r009-contract-reference.json",
    "phase-exec-paper-r015-held-line-diagnosis.json",
    "phase-exec-paper-r015-held-line-grouping-by-symbol.json",
    "phase-exec-paper-r015-held-line-grouping-by-target-close.json",
    "phase-exec-paper-r015-held-line-grouping-by-bar-role.json",
    "phase-exec-paper-r015-existing-readiness-artifact-inventory.json",
    "phase-exec-paper-r015-readiness-rebinding-search-results.json",
    "phase-exec-paper-r015-rebound-line-results.json",
    "phase-exec-paper-r015-still-held-line-diagnostics.json",
    "phase-exec-paper-r015-missing-readiness-window-requirements.json",
    "phase-exec-paper-r015-missing-offline-quote-download-plan.md",
    "phase-exec-paper-r015-missing-offline-quote-download-plan.json",
    "phase-exec-paper-r015-future-validation-requirements.json",
    "phase-exec-paper-r015-held-lines-retry-plan.json",
    "phase-exec-paper-r015-updated-partial-maturity-decision.json",
    "phase-exec-paper-r015-next-operator-action-package.json",
    "phase-exec-paper-r015-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r015-legacy-compatibility-preservation.json",
    "phase-exec-paper-r015-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r015-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r015-cost-guidance-preservation.json",
    "phase-exec-paper-r015-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r015-no-download-audit.json",
    "phase-exec-paper-r015-no-polygon-api-call-audit.json",
    "phase-exec-paper-r015-no-lmax-call-audit.json",
    "phase-exec-paper-r015-no-external-api-call-audit.json",
    "phase-exec-paper-r015-no-broker-activation-audit.json",
    "phase-exec-paper-r015-no-live-marketdata-audit.json",
    "phase-exec-paper-r015-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r015-no-new-pms-cycle-audit.json",
    "phase-exec-paper-r015-no-manualnoexternal-command-run-audit.json",
    "phase-exec-paper-r015-no-new-backtest-audit.json",
    "phase-exec-paper-r015-no-new-simulation-audit.json",
    "phase-exec-paper-r015-no-tca-result-lines-audit.json",
    "phase-exec-paper-r015-no-executable-schedule-audit.json",
    "phase-exec-paper-r015-no-child-slices-audit.json",
    "phase-exec-paper-r015-no-child-orders-audit.json",
    "phase-exec-paper-r015-no-order-created-audit.json",
    "phase-exec-paper-r015-no-real-fill-audit.json",
    "phase-exec-paper-r015-no-execution-report-audit.json",
    "phase-exec-paper-r015-no-route-no-submission-audit.json",
    "phase-exec-paper-r015-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r015-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r015-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r015-no-external-audit.json",
    "phase-exec-paper-r015-forbidden-actions-audit.json",
    "phase-exec-paper-r015-next-phase-recommendation.json",
    "phase-exec-paper-r015-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_PAPER_R015_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$reference = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-r014-held-line-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-r009-contract-reference.json")
$diagnosis = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-held-line-diagnosis.json")
$bySymbol = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-held-line-grouping-by-symbol.json")
$byTargetClose = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-held-line-grouping-by-target-close.json")
$byBarRole = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-held-line-grouping-by-bar-role.json")
$inventory = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-existing-readiness-artifact-inventory.json")
$search = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-readiness-rebinding-search-results.json")
$rebound = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-rebound-line-results.json")
$stillHeld = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-still-held-line-diagnostics.json")
$windows = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-missing-readiness-window-requirements.json")
$downloadPlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-missing-offline-quote-download-plan.json")
$futureValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-future-validation-requirements.json")
$retry = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-held-lines-retry-plan.json")
$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-updated-partial-maturity-decision.json")
$operator = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-next-operator-action-package.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-build-test-validator-evidence.json")

if ($reference.HeldLineCountFromDiagnostics -ne 420 -or
    $reference.HeldPreviewLineCountLoaded -ne 420 -or
    $reference.TotalPreviewLineCount -ne 700 -or
    -not $reference.ReusedOnly) {
    Fail "EXEC_PAPER_R015_FAIL_R014_REFERENCE" "R014 held-line reference is invalid."
}

if ($contract.ContractVersion -ne "0.3.0-design-only-candidate" -or
    -not $contract.DesignOnly -or
    -not $contract.PaperOnly -or
    -not $contract.NonExecutable -or
    -not $contract.NotAnOrder -or
    -not $contract.NotSubmitted -or
    -not $contract.NoBrokerRoute -or
    $contract.ExecutablePromotionAuthorized -or
    $contract.BrokerReady -or
    $contract.LiveReady) {
    Fail "EXEC_PAPER_R015_FAIL_R009_PROMOTED" "R009 contract is executable or missing design-only constraints."
}

if ($diagnosis.HeldLineCount -ne 420 -or
    $diagnosis.DirectCrossExecutionHoldCount -ne 0 -or
    $diagnosis.InversionMismatchHoldCount -ne 0 -or
    $diagnosis.CanonicalTargetCloseMissingCount -ne 0 -or
    $diagnosis.RiskOperatorApprovalMissingCount -ne 0) {
    Fail "EXEC_PAPER_R015_FAIL_HELD_LINE_DIAGNOSIS" "Held-line diagnosis is inconsistent or unsafe."
}

$missingCounts = @{}
foreach ($field in (As-Array $diagnosis.MissingFieldCounts)) { $missingCounts[[string]$field.MissingField] = [int]$field.Count }
if ($missingCounts["MissingQuoteWindowReadinessBinding"] -ne 420 -or
    $missingCounts["MissingCloseBenchmarkReadinessBinding"] -ne 420 -or
    $missingCounts["MissingFeedQualityReadinessBinding"] -ne 420) {
    Fail "EXEC_PAPER_R015_FAIL_MISSING_FIELD_COUNTS" "Expected all R014 held lines to be missing the three readiness bindings before rebinding."
}

if (@(As-Array $bySymbol.Groups).Count -ne 7 -or
    @((As-Array $bySymbol.Groups) | Where-Object { $_.HeldLineCount -ne 60 }).Count -ne 0) {
    Fail "EXEC_PAPER_R015_FAIL_SYMBOL_GROUPING" "Held-line symbol grouping is invalid."
}
if (@(As-Array $byTargetClose.Groups).Count -lt 1 -or @(As-Array $byBarRole.Groups).Count -ne 3) {
    Fail "EXEC_PAPER_R015_FAIL_GROUPINGS" "Target-close or bar-role grouping is missing."
}

if (-not $inventory.R053CheckedFirst -or -not $inventory.R042Checked -or
    $inventory.SearchScope -ne "LocalArtifactsOnly" -or
    $inventory.ArtifactCount -lt 6) {
    Fail "EXEC_PAPER_R015_FAIL_READINESS_INVENTORY" "Readiness inventory does not show local R053/R042 search."
}

if ($search.HeldLineCount -ne 420 -or
    $search.ReboundCompleteLineCount -ne $rebound.ReboundLineCount -or
    $search.StillHeldLineCount -ne $stillHeld.StillHeldLineCount -or
    ($search.ReboundCompleteLineCount + $search.StillHeldLineCount) -ne 420 -or
    $search.ReadinessBindingsInvented -or
    -not $search.LocalArtifactSearchOnly) {
    Fail "EXEC_PAPER_R015_FAIL_REBINDING_COUNTS" "Rebinding counts are inconsistent or invented."
}

foreach ($line in (As-Array $search.Results)) {
    if ($line.ReboundComplete -and
        (-not $line.QuoteWindowBindingFound -or -not $line.CloseBenchmarkBindingFound -or -not $line.FeedQualityBindingFound)) {
        Fail "EXEC_PAPER_R015_FAIL_REBOUND_INCOMPLETE" "A rebound line is missing one or more readiness bindings."
    }
    if ($line.ReadinessBindingInvented) {
        Fail "EXEC_PAPER_R015_FAIL_INVENTED_READINESS" "Readiness binding was invented."
    }
}

if ($stillHeld.StillHeldLineCount -ne $windows.StillHeldLineCount -or
    $windows.MissingWindowCount -ne $stillHeld.StillHeldLineCount) {
    Fail "EXEC_PAPER_R015_FAIL_MISSING_WINDOW_COUNT" "Missing readiness window count does not match still-held lines."
}

foreach ($window in (As-Array $windows.Windows)) {
    if ([string]$window.TargetCloseLocal -match "T\d{2}:(06|21|36|51):00") {
        Fail "EXEC_PAPER_R015_FAIL_LEGACY_CANONICAL" "Legacy timestamp used as future canonical target close."
    }
    $start = [DateTimeOffset]::Parse([string]$window.WindowStartUtc).ToUniversalTime()
    $end = [DateTimeOffset]::Parse([string]$window.WindowEndUtc).ToUniversalTime()
    if (($end - $start).TotalMinutes -ne 13) {
        Fail "EXEC_PAPER_R015_FAIL_WINDOW_DEFINITION" "Missing readiness window is not TargetCloseUtc - 13 minutes."
    }
}

if (-not $downloadPlan.DownloadRequired -or
    -not $downloadPlan.CommandsAreTemplatesOnly -or
    $downloadPlan.CommandsExecutedInR015 -ne 0 -or
    $downloadPlan.FilesDownloadedInR015 -ne 0 -or
    $downloadPlan.OutputFilesClaimedToExist -or
    $downloadPlan.CommandTemplateCount -lt 1) {
    Fail "EXEC_PAPER_R015_FAIL_DOWNLOAD_PLAN" "Download plan is missing, executed, or claims files exist."
}
foreach ($command in (As-Array $downloadPlan.Commands)) {
    if (-not $command.CommandIsOperatorRunOnly -or
        $command.CommandExecutedInR015 -or
        $command.OutputFilesClaimedToExist -or
        [string]$command.CommandTemplate -notmatch "download-polygon-fx-bbo-offline\.ps1" -or
        [string]$command.CommandTemplate -notmatch "-FromUtc" -or
        [string]$command.CommandTemplate -notmatch "-ToUtc" -or
        [string]$command.CommandTemplate -notmatch "-Symbols") {
        Fail "EXEC_PAPER_R015_FAIL_DOWNLOAD_TEMPLATE" "Invalid operator download command template."
    }
}

if (-not $futureValidation.MustRemainNoExternal -or -not $futureValidation.MustNotRunDownloadsInValidationGate) {
    Fail "EXEC_PAPER_R015_FAIL_FUTURE_VALIDATION_REQUIREMENTS" "Future validation requirements are unsafe."
}
if ($retry.ManualNoExternalRunNow -or $retry.DownloadsRunNow -or
    $retry.ReboundLineCount -ne $rebound.ReboundLineCount -or
    $retry.StillHeldLineCount -ne $stillHeld.StillHeldLineCount) {
    Fail "EXEC_PAPER_R015_FAIL_RETRY_PLAN" "Retry plan is unsafe or inconsistent."
}

if ($decision.PriorHeldLineCount -ne 420 -or
    $decision.ReboundLineCount -ne $rebound.ReboundLineCount -or
    $decision.StillHeldLineCount -ne $stillHeld.StillHeldLineCount -or
    $decision.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R015_FAIL_DECISION" "Updated maturity decision is inconsistent or executable."
}

$expected = if ($rebound.ReboundLineCount -gt 0) {
    @(
        "EXEC_PAPER_R015_PASS_HELD_LINE_READINESS_DIAGNOSIS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R015_PASS_EXISTING_READINESS_REBINDING_READY_NO_EXTERNAL",
        "EXEC_PAPER_R015_PASS_MISSING_READINESS_PACKAGE_READY_NO_EXTERNAL",
        "EXEC_PAPER_R015_PASS_NO_DOWNLOAD_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
} else {
    @(
        "EXEC_PAPER_R015_PASS_HELD_LINE_READINESS_DIAGNOSIS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R015_NEEDS_OPERATOR_MISSING_READINESS_DOWNLOADS_NO_EXTERNAL",
        "EXEC_PAPER_R015_PASS_MISSING_READINESS_PACKAGE_READY_NO_EXTERNAL",
        "EXEC_PAPER_R015_PASS_NO_DOWNLOAD_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}
foreach ($classification in $expected) {
    if ((As-Array $decision.Classifications) -notcontains $classification) {
        Fail "EXEC_PAPER_R015_FAIL_CLASSIFICATION" "Missing classification: $classification"
    }
}

if ($operator.DownloadTemplateCount -ne $downloadPlan.CommandTemplateCount -or
    $operator.StillHeldLineCount -ne $stillHeld.StillHeldLineCount) {
    Fail "EXEC_PAPER_R015_FAIL_OPERATOR_PACKAGE" "Operator action package is inconsistent."
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or
    $canonical.Legacy06UsedAsFutureCanonical -or
    $canonical.HeldCanonicalQuarterHourFailures -ne 0) {
    Fail "EXEC_PAPER_R015_FAIL_CANONICAL_POLICY" "Canonical quarter-hour policy was weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R015_FAIL_LEGACY_POLICY" "Legacy compatibility policy was weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or $usdPair.DirectCrossExecutionAllowed) {
    Fail "EXEC_PAPER_R015_FAIL_USD_PAIR_POLICY" "USD-pair-only policy was weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossExecutionDisabled -or $directCross.DirectCrossHoldCount -ne 0) {
    Fail "EXEC_PAPER_R015_FAIL_DIRECT_CROSS_POLICY" "Direct-cross exclusion was weakened."
}
if ($cost.FiveUsdPerMillionUniversalized) {
    Fail "EXEC_PAPER_R015_FAIL_COST_UNIVERSALIZED" "5 USD/million was universalized."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_PAPER_R015_FAIL_NONMAJOR_CALIBRATION" "Nonmajor calibration requirement was weakened."
}
if (-not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne 4004 -or [string]$usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatWeakened) {
    Fail "EXEC_PAPER_R015_FAIL_USDJPY_CAVEAT" "USDJPY caveat was weakened."
}
if ($lmax.LmaxUsedInThisPhase -or $lmax.LmaxCalledInThisPhase) {
    Fail "EXEC_PAPER_R015_FAIL_LMAX_USED" "LMAX was used in R015."
}

foreach ($auditName in @(
    "phase-exec-paper-r015-no-download-audit.json",
    "phase-exec-paper-r015-no-polygon-api-call-audit.json",
    "phase-exec-paper-r015-no-lmax-call-audit.json",
    "phase-exec-paper-r015-no-external-api-call-audit.json",
    "phase-exec-paper-r015-no-broker-activation-audit.json",
    "phase-exec-paper-r015-no-live-marketdata-audit.json",
    "phase-exec-paper-r015-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r015-no-new-pms-cycle-audit.json",
    "phase-exec-paper-r015-no-manualnoexternal-command-run-audit.json",
    "phase-exec-paper-r015-no-new-backtest-audit.json",
    "phase-exec-paper-r015-no-new-simulation-audit.json",
    "phase-exec-paper-r015-no-tca-result-lines-audit.json",
    "phase-exec-paper-r015-no-executable-schedule-audit.json",
    "phase-exec-paper-r015-no-child-slices-audit.json",
    "phase-exec-paper-r015-no-child-orders-audit.json",
    "phase-exec-paper-r015-no-order-created-audit.json",
    "phase-exec-paper-r015-no-real-fill-audit.json",
    "phase-exec-paper-r015-no-execution-report-audit.json",
    "phase-exec-paper-r015-no-route-no-submission-audit.json",
    "phase-exec-paper-r015-no-paper-ledger-commit-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_PAPER_R015_FAIL_AUDIT" "Forbidden action audit failed: $auditName"
    }
}

if (-not $noExternal.NoExternal -or
    $noExternal.PolygonCalled -or
    $noExternal.LmaxCalled -or
    $noExternal.ExternalApiCalled -or
    $noExternal.DownloadsExecuted -or
    $forbidden.ForbiddenActionsDetected -or
    $forbidden.DownloadsExecuted -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.PmsEmsOmsCycleRun -or
    $forbidden.ManualNoExternalCommandRun -or
    $forbidden.BacktestSimulationRun -or
    $forbidden.TcaResultLinesCreated -or
    $forbidden.ExecutableSchedule -or
    $forbidden.ChildSlicesOrOrders -or
    $forbidden.OrdersFillsReportsRoutesSubmissions -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation -or
    $forbidden.R009ExecutablePromotion) {
    Fail "EXEC_PAPER_R015_FAIL_FORBIDDEN_ACTION" "Forbidden action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR015Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R015Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_PAPER_R015_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE" "Build/tests/validator evidence is missing."
}

Write-Output "EXEC_PAPER_R015_VALIDATION_PASSED"

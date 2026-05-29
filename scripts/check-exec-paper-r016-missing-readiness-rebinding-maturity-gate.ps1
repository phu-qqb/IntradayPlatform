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
        Fail "EXEC_PAPER_R016_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-paper-r016-summary.md",
    "phase-exec-paper-r016-r015-missing-readiness-reference.json",
    "phase-exec-paper-r016-r014-preview-reference.json",
    "phase-exec-paper-r016-file-intake-contract.json",
    "phase-exec-paper-r016-expected-file-entries.json",
    "phase-exec-paper-r016-accepted-file-entries.json",
    "phase-exec-paper-r016-missing-file-diagnostics.json",
    "phase-exec-paper-r016-manifest-validation-results.json",
    "phase-exec-paper-r016-row-level-validation-results.json",
    "phase-exec-paper-r016-row-count-comparison.json",
    "phase-exec-paper-r016-duplicate-out-of-order-handling.json",
    "phase-exec-paper-r016-quote-window-readiness-results.json",
    "phase-exec-paper-r016-close-benchmark-readiness-results.json",
    "phase-exec-paper-r016-feed-quality-readiness-results.json",
    "phase-exec-paper-r016-held-line-rebinding-results.json",
    "phase-exec-paper-r016-still-held-line-diagnostics.json",
    "phase-exec-paper-r016-reaggregated-preview-line-status.json",
    "phase-exec-paper-r016-updated-operator-review-report.md",
    "phase-exec-paper-r016-updated-operator-review-report.json",
    "phase-exec-paper-r016-updated-long-run-maturity-decision.json",
    "phase-exec-paper-r016-next-phase-recommendation.json",
    "phase-exec-paper-r016-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r016-legacy-compatibility-preservation.json",
    "phase-exec-paper-r016-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r016-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r016-cost-guidance-preservation.json",
    "phase-exec-paper-r016-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r016-no-db-import-audit.json",
    "phase-exec-paper-r016-no-persisted-sanitized-row-audit.json",
    "phase-exec-paper-r016-no-new-pms-cycle-audit.json",
    "phase-exec-paper-r016-no-manualnoexternal-command-run-audit.json",
    "phase-exec-paper-r016-no-new-backtest-audit.json",
    "phase-exec-paper-r016-no-new-simulation-audit.json",
    "phase-exec-paper-r016-no-tca-result-lines-audit.json",
    "phase-exec-paper-r016-no-executable-schedule-audit.json",
    "phase-exec-paper-r016-no-child-slices-audit.json",
    "phase-exec-paper-r016-no-child-orders-audit.json",
    "phase-exec-paper-r016-no-order-created-audit.json",
    "phase-exec-paper-r016-no-real-fill-audit.json",
    "phase-exec-paper-r016-no-execution-report-audit.json",
    "phase-exec-paper-r016-no-route-no-submission-audit.json",
    "phase-exec-paper-r016-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r016-no-polygon-api-call-audit.json",
    "phase-exec-paper-r016-no-lmax-call-audit.json",
    "phase-exec-paper-r016-no-external-api-call-audit.json",
    "phase-exec-paper-r016-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r016-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r016-no-external-audit.json",
    "phase-exec-paper-r016-forbidden-actions-audit.json",
    "phase-exec-paper-r016-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_PAPER_R016_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r015 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-r015-missing-readiness-reference.json")
$r014 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-r014-preview-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-file-intake-contract.json")
$expectedFiles = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-expected-file-entries.json")
$acceptedFiles = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-accepted-file-entries.json")
$missingFiles = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-missing-file-diagnostics.json")
$manifest = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-manifest-validation-results.json")
$rowValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-row-level-validation-results.json")
$rowCounts = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-row-count-comparison.json")
$duplicates = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-duplicate-out-of-order-handling.json")
$quote = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-quote-window-readiness-results.json")
$close = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-close-benchmark-readiness-results.json")
$feed = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-feed-quality-readiness-results.json")
$rebinding = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-held-line-rebinding-results.json")
$stillHeld = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-still-held-line-diagnostics.json")
$reaggregated = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-reaggregated-preview-line-status.json")
$review = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-updated-operator-review-report.json")
$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-updated-long-run-maturity-decision.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-build-test-validator-evidence.json")

if ($r015.R015StillHeldLineCount -ne 322 -or
    $r015.R015ReboundLineCount -ne 98 -or
    $r015.MissingWindowCount -ne 322 -or
    $r015.DownloadTemplateCount -ne 91 -or
    $r015.CommandsExecutedInR015 -ne 0 -or
    $r015.FilesDownloadedInR015 -ne 0) {
    Fail "EXEC_PAPER_R016_FAIL_R015_REFERENCE" "R015 missing-readiness reference is invalid."
}

if ($r014.PreviewLineCount -ne 700 -or
    $r014.ExpectedMaximumPreviewLineCount -ne 700 -or
    -not $r014.ReusedExistingPreviewLinesOnly -or
    $r014.ManualNoExternalRunNow) {
    Fail "EXEC_PAPER_R016_FAIL_R014_REFERENCE" "R014 preview reference is invalid or reran ManualNoExternal."
}

if (-not $contract.ReadLocalFilesOnly -or
    $contract.ExternalApiAllowed -or
    $contract.DownloadsAllowed -or
    $contract.RequiredProvider -ne "PolygonOfflineFile" -or
    $contract.RequiredDataset -ne "HistoricalBboQuotes" -or
    $contract.RequiredFormat -ne "NDJSON" -or
    -not $contract.RequiresManifest -or
    -not $contract.RequiresSha256Match) {
    Fail "EXEC_PAPER_R016_FAIL_FILE_INTAKE_CONTRACT" "File intake contract is unsafe."
}

if ($expectedFiles.ExpectedFileEntryCount -ne 91 -or
    $acceptedFiles.AcceptedFileEntryCount -ne 90 -or
    $missingFiles.MissingFileEntryCount -ne 1) {
    Fail "EXEC_PAPER_R016_FAIL_FILE_INTAKE_COUNTS" "File intake counts are unexpected."
}
foreach ($missing in (As-Array $missingFiles.MissingFiles)) {
    if ($missing.QuoteFileExists -and $missing.ManifestExists) {
        Fail "EXEC_PAPER_R016_FAIL_MISSING_FILE_DIAGNOSTICS" "Missing file diagnostics claim an existing complete pair is missing."
    }
}

if ($manifest.ManifestValidatedEntryCount -ne 90 -or
    $manifest.AcceptedManifestCount -ne 90 -or
    $manifest.QuarantinedManifestCount -ne 0) {
    Fail "EXEC_PAPER_R016_FAIL_MANIFEST_COUNTS" "Manifest validation counts are unexpected."
}
foreach ($result in (As-Array $manifest.Results)) {
    if (-not $result.ManifestAccepted -or
        $result.Provider -ne "PolygonOfflineFile" -or
        $result.Dataset -ne "HistoricalBboQuotes" -or
        $result.Format -ne "NDJSON" -or
        $result.ContainsSecrets -or
        $result.ContainsRawProviderPayload -or
        -not $result.Sha256Matches -or
        -not $result.FileSizeMatches -or
        $result.RowCountDeclared -lt 0) {
        Fail "EXEC_PAPER_R016_FAIL_MANIFEST_VALIDATION" "Manifest validation failed for $($result.ExpectedFileEntryId)."
    }
}

if ($rowValidation.ValidatedFileCount -ne 90 -or
    $rowValidation.AcceptedForReadinessFileCount -ne 90 -or
    $rowValidation.QuarantinedRowValidationFileCount -ne 0 -or
    $rowCounts.MismatchCount -ne 0) {
    Fail "EXEC_PAPER_R016_FAIL_ROW_VALIDATION_COUNTS" "Row validation counts are unexpected."
}
foreach ($result in (As-Array $rowValidation.Results)) {
    if (-not $result.RowValidationAcceptedForReadiness -or
        -not $result.RowCountMatchesManifest -or
        $result.InvalidTimestampRows -ne 0 -or
        $result.InvalidProviderSymbolRows -ne 0 -or
        $result.InvalidBidAskRows -ne 0 -or
        $result.AskLessThanBidRows -ne 0 -or
        $result.RawPayloadSerializedRows -ne 0) {
        Fail "EXEC_PAPER_R016_FAIL_ROW_VALIDATION" "Row validation failed for $($result.ExpectedFileEntryId)."
    }
}

if (@(As-Array $duplicates.Results).Count -ne 90) {
    Fail "EXEC_PAPER_R016_FAIL_DUPLICATE_HANDLING" "Duplicate/out-of-order handling results missing."
}

if ($quote.QuoteWindowReadinessRecords -ne 309 -or
    $quote.ReadyRecords -ne 258 -or
    $close.CloseBenchmarkReadinessRecords -ne 309 -or
    $close.ReadyRecords -ne 258 -or
    $feed.FeedQualityReadinessRecords -ne 90 -or
    $feed.ReadyRecords -ne 83) {
    Fail "EXEC_PAPER_R016_FAIL_READINESS_COUNTS" "Readiness generation counts are unexpected."
}

foreach ($record in (As-Array $quote.Results)) {
    if ([string]$record.TargetCloseTimestampUtc -match "T\d{2}:(06|21|36|51):00Z") {
        Fail "EXEC_PAPER_R016_FAIL_LEGACY_CANONICAL" "Legacy timestamp used as future canonical readiness target close."
    }
}

if ($rebinding.R015StillHeldLineCount -ne 322 -or
    $rebinding.R016ReboundLineCount -ne 258 -or
    $rebinding.StillHeldLineCount -ne 64 -or
    $rebinding.ReadinessBindingsInvented) {
    Fail "EXEC_PAPER_R016_FAIL_REBINDING_COUNTS" "Held-line rebinding counts are inconsistent or invented."
}
foreach ($line in (As-Array $rebinding.Results)) {
    if ($line.ReadinessBindingInvented) {
        Fail "EXEC_PAPER_R016_FAIL_INVENTED_BINDING" "Readiness binding was invented."
    }
    if ($line.ReboundComplete -and
        ([string]::IsNullOrWhiteSpace([string]$line.QuoteWindowReadinessBinding) -or
         [string]::IsNullOrWhiteSpace([string]$line.CloseBenchmarkReadinessBinding) -or
         [string]::IsNullOrWhiteSpace([string]$line.FeedQualityReadinessBinding))) {
        Fail "EXEC_PAPER_R016_FAIL_INCOMPLETE_REBOUND" "Rebound line missing one or more readiness binding ids."
    }
}
if ($stillHeld.StillHeldLineCount -ne 64) {
    Fail "EXEC_PAPER_R016_FAIL_STILL_HELD_COUNT" "Still-held diagnostics count is unexpected."
}

if ($reaggregated.PreviewLineCount -ne 700 -or
    $reaggregated.OriginalR014CompleteLineCount -ne 280 -or
    $reaggregated.R015ReboundLineCount -ne 98 -or
    $reaggregated.R016ReboundLineCount -ne 258 -or
    $reaggregated.ReadinessCompleteLineCount -ne 636 -or
    $reaggregated.StillHeldLineCount -ne 64 -or
    $reaggregated.AllPreviewLinesReadinessComplete) {
    Fail "EXEC_PAPER_R016_FAIL_REAGGREGATED_PREVIEW" "Reaggregated preview status is inconsistent."
}
foreach ($line in (As-Array $reaggregated.Lines)) {
    if (-not $line.NonExecutable -or
        -not $line.NotAnOrder -or
        -not $line.NotSubmitted -or
        -not $line.NoBrokerRoute -or
        -not $line.NoChildSlices -or
        -not $line.NoExecutableSchedule -or
        -not $line.NoFill -or
        -not $line.NoExecutionReport -or
        -not $line.NoRoute -or
        -not $line.NoSubmission -or
        -not $line.NoPaperLedgerCommit) {
        Fail "EXEC_PAPER_R016_FAIL_PREVIEW_LINE_EXECUTABLE" "Preview line is represented as executable/order/fill/route/submission."
    }
}

if ($review.ReadinessCompleteLineCount -ne 636 -or
    $review.StillHeldLineCount -ne 64 -or
    $review.ExecutablePromotionAuthorized -or
    $review.Decision -ne "R009LongRunPaperOnlyPartialMaturityNeedsMoreReadiness") {
    Fail "EXEC_PAPER_R016_FAIL_OPERATOR_REVIEW" "Updated operator review is inconsistent."
}

$expectedClassifications = @(
    "EXEC_PAPER_R016_PARTIAL_HELD_LINE_REBINDING_NO_EXTERNAL",
    "EXEC_PAPER_R016_PASS_STILL_HELD_DIAGNOSTICS_READY_NO_EXTERNAL",
    "EXEC_PAPER_R016_PASS_LONG_RUN_PREVIEW_REAGGREGATION_READY_NO_EXTERNAL",
    "EXEC_PAPER_R016_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
)
if ($decision.Decision -ne "R009LongRunPaperOnlyPartialMaturityNeedsMoreReadiness" -or
    $decision.ReadinessCompleteLineCount -ne 636 -or
    $decision.StillHeldLineCount -ne 64 -or
    $decision.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R016_FAIL_DECISION" "Updated maturity decision is inconsistent or executable."
}
foreach ($classification in $expectedClassifications) {
    if ((As-Array $decision.Classifications) -notcontains $classification) {
        Fail "EXEC_PAPER_R016_FAIL_CLASSIFICATION" "Missing expected classification: $classification"
    }
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R016_FAIL_CANONICAL_POLICY" "Canonical quarter-hour policy was weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R016_FAIL_LEGACY_POLICY" "Legacy compatibility policy was weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or $usdPair.DirectCrossExecutionAllowed) {
    Fail "EXEC_PAPER_R016_FAIL_USD_PAIR_POLICY" "USD-pair-only policy was weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossExecutionDisabled) {
    Fail "EXEC_PAPER_R016_FAIL_DIRECT_CROSS_POLICY" "Direct-cross exclusion was weakened."
}
if ($cost.FiveUsdPerMillionUniversalized) {
    Fail "EXEC_PAPER_R016_FAIL_COST_UNIVERSALIZED" "5 USD/million was universalized."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_PAPER_R016_FAIL_NONMAJOR_CALIBRATION" "Nonmajor calibration requirement was weakened."
}
if (-not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne 4004 -or [string]$usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatWeakened) {
    Fail "EXEC_PAPER_R016_FAIL_USDJPY_CAVEAT" "USDJPY caveat was weakened."
}
if ($lmax.LmaxUsedInThisPhase -or $lmax.LmaxCalledInThisPhase) {
    Fail "EXEC_PAPER_R016_FAIL_LMAX_USED" "LMAX was used."
}

foreach ($auditName in @(
    "phase-exec-paper-r016-no-db-import-audit.json",
    "phase-exec-paper-r016-no-persisted-sanitized-row-audit.json",
    "phase-exec-paper-r016-no-new-pms-cycle-audit.json",
    "phase-exec-paper-r016-no-manualnoexternal-command-run-audit.json",
    "phase-exec-paper-r016-no-new-backtest-audit.json",
    "phase-exec-paper-r016-no-new-simulation-audit.json",
    "phase-exec-paper-r016-no-tca-result-lines-audit.json",
    "phase-exec-paper-r016-no-executable-schedule-audit.json",
    "phase-exec-paper-r016-no-child-slices-audit.json",
    "phase-exec-paper-r016-no-child-orders-audit.json",
    "phase-exec-paper-r016-no-order-created-audit.json",
    "phase-exec-paper-r016-no-real-fill-audit.json",
    "phase-exec-paper-r016-no-execution-report-audit.json",
    "phase-exec-paper-r016-no-route-no-submission-audit.json",
    "phase-exec-paper-r016-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r016-no-polygon-api-call-audit.json",
    "phase-exec-paper-r016-no-lmax-call-audit.json",
    "phase-exec-paper-r016-no-external-api-call-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_PAPER_R016_FAIL_AUDIT" "Forbidden action audit failed: $auditName"
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
    $forbidden.DbImport -or
    $forbidden.PersistedSanitizedRows -or
    $forbidden.BacktestSimulationRun -or
    $forbidden.TcaResultLinesCreated -or
    $forbidden.ExecutableSchedule -or
    $forbidden.ChildSlicesOrOrders -or
    $forbidden.OrdersFillsReportsRoutesSubmissions -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation -or
    $forbidden.R009ExecutablePromotion) {
    Fail "EXEC_PAPER_R016_FAIL_FORBIDDEN_ACTION" "Forbidden action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR016Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R016Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_PAPER_R016_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE" "Build/tests/validator evidence is missing."
}

Write-Output "EXEC_PAPER_R016_VALIDATION_PASSED"

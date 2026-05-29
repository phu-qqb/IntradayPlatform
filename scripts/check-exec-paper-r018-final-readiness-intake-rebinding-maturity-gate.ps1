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
        Fail "EXEC_PAPER_R018_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-paper-r018-summary.md",
    "phase-exec-paper-r018-r017-final-package-reference.json",
    "phase-exec-paper-r018-r014-preview-reference.json",
    "phase-exec-paper-r018-r009-contract-reference.json",
    "phase-exec-paper-r018-file-intake-contract.json",
    "phase-exec-paper-r018-expected-file-entries.json",
    "phase-exec-paper-r018-accepted-file-entries.json",
    "phase-exec-paper-r018-missing-file-diagnostics.json",
    "phase-exec-paper-r018-manifest-validation-results.json",
    "phase-exec-paper-r018-row-level-validation-results.json",
    "phase-exec-paper-r018-row-count-comparison.json",
    "phase-exec-paper-r018-duplicate-out-of-order-handling.json",
    "phase-exec-paper-r018-generated-readiness-results.json",
    "phase-exec-paper-r018-final-held-line-rebinding-results.json",
    "phase-exec-paper-r018-final-still-held-line-diagnostics.json",
    "phase-exec-paper-r018-final-reaggregated-preview-status.json",
    "phase-exec-paper-r018-final-operator-review-report.md",
    "phase-exec-paper-r018-final-operator-review-report.json",
    "phase-exec-paper-r018-final-long-run-maturity-decision.json",
    "phase-exec-paper-r018-next-phase-recommendation.json",
    "phase-exec-paper-r018-if-needed-final-download-command-package.md",
    "phase-exec-paper-r018-if-needed-final-download-command-package.json",
    "phase-exec-paper-r018-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r018-legacy-compatibility-preservation.json",
    "phase-exec-paper-r018-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r018-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r018-cost-guidance-preservation.json",
    "phase-exec-paper-r018-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r018-no-db-import-audit.json",
    "phase-exec-paper-r018-no-persisted-sanitized-row-audit.json",
    "phase-exec-paper-r018-no-new-pms-cycle-audit.json",
    "phase-exec-paper-r018-no-manualnoexternal-command-run-audit.json",
    "phase-exec-paper-r018-no-new-backtest-audit.json",
    "phase-exec-paper-r018-no-new-simulation-audit.json",
    "phase-exec-paper-r018-no-tca-result-lines-audit.json",
    "phase-exec-paper-r018-no-executable-schedule-audit.json",
    "phase-exec-paper-r018-no-child-slices-audit.json",
    "phase-exec-paper-r018-no-child-orders-audit.json",
    "phase-exec-paper-r018-no-order-created-audit.json",
    "phase-exec-paper-r018-no-real-fill-audit.json",
    "phase-exec-paper-r018-no-execution-report-audit.json",
    "phase-exec-paper-r018-no-route-no-submission-audit.json",
    "phase-exec-paper-r018-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r018-no-polygon-api-call-audit.json",
    "phase-exec-paper-r018-no-lmax-call-audit.json",
    "phase-exec-paper-r018-no-external-api-call-audit.json",
    "phase-exec-paper-r018-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r018-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r018-no-external-audit.json",
    "phase-exec-paper-r018-forbidden-actions-audit.json",
    "phase-exec-paper-r018-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_PAPER_R018_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r017Ref = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-r017-final-package-reference.json")
$r014Ref = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-r014-preview-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-r009-contract-reference.json")
$intakeContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-file-intake-contract.json")
$expectedFiles = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-expected-file-entries.json")
$acceptedFiles = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-accepted-file-entries.json")
$missingFiles = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-missing-file-diagnostics.json")
$manifestValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-manifest-validation-results.json")
$rowValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-row-level-validation-results.json")
$rowCounts = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-row-count-comparison.json")
$duplicates = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-duplicate-out-of-order-handling.json")
$readiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-generated-readiness-results.json")
$rebinding = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-held-line-rebinding-results.json")
$stillHeld = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-still-held-line-diagnostics.json")
$reaggregated = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-reaggregated-preview-status.json")
$review = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-operator-review-report.json")
$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-long-run-maturity-decision.json")
$downloadPackage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-if-needed-final-download-command-package.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-build-test-validator-evidence.json")

if ($r017Ref.SourcePhase -ne "EXEC-PAPER-R017" -or
    $r017Ref.R017ReadinessCompleteLineCount -ne 640 -or
    $r017Ref.R017StillHeldLineCount -ne 60 -or
    $r017Ref.R017Decision -ne "R009LongRunPaperOnlyPartialMaturityWithExplicitLocalDataBlocker" -or
    $r017Ref.R017CommandTemplateCount -ne 28 -or
    $r017Ref.R017CommandsExecuted -ne 0 -or
    $r017Ref.R017FilesDownloadedByCodex -ne 0 -or
    -not $r017Ref.ReusedOnly) {
    Fail "EXEC_PAPER_R018_FAIL_R017_REFERENCE" "R017 package reference is invalid."
}

if ($r014Ref.SourcePhase -ne "EXEC-PAPER-R014" -or
    $r014Ref.PreviewLineCount -ne 700 -or
    -not $r014Ref.ReusedExistingPreviewLinesOnly -or
    $r014Ref.ManualNoExternalRunNow) {
    Fail "EXEC_PAPER_R018_FAIL_R014_REFERENCE" "R014 preview reference is invalid."
}

if ($contract.ContractVersion -ne "0.3.0-design-only-candidate" -or
    -not $contract.DesignOnly -or
    -not $contract.PaperOnly -or
    -not $contract.NonExecutable -or
    -not $contract.NotAnOrder -or
    -not $contract.NotSubmitted -or
    -not $contract.NoBrokerRoute -or
    $contract.BrokerReady -or
    $contract.LiveReady -or
    $contract.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R018_FAIL_R009_CONTRACT" "R009 contract is executable or widened."
}

if (-not $intakeContract.ReadLocalFilesOnly -or
    $intakeContract.Provider -ne "PolygonOfflineFile" -or
    $intakeContract.Dataset -ne "HistoricalBboQuotes" -or
    $intakeContract.Format -ne "NDJSON" -or
    -not $intakeContract.ManifestRequired -or
    -not $intakeContract.Sha256MatchRequired -or
    $intakeContract.ExternalApiAllowed -or
    $intakeContract.DownloadsAllowed -or
    $intakeContract.DbImportAllowed -or
    $intakeContract.PersistSanitizedRowsAllowed) {
    Fail "EXEC_PAPER_R018_FAIL_FILE_INTAKE_CONTRACT" "File intake contract is unsafe."
}

if ($expectedFiles.ExpectedFileEntryCount -ne 28 -or
    $acceptedFiles.AcceptedFileEntryCount -ne 28 -or
    $missingFiles.MissingFileEntryCount -ne 0) {
    Fail "EXEC_PAPER_R018_FAIL_FILE_INTAKE_COUNTS" "File intake counts are unexpected."
}

if ($manifestValidation.ManifestValidatedEntryCount -ne 28 -or
    $manifestValidation.AcceptedManifestCount -ne 28 -or
    $manifestValidation.QuarantinedManifestCount -ne 0) {
    Fail "EXEC_PAPER_R018_FAIL_MANIFEST_COUNTS" "Manifest validation counts are unexpected."
}
foreach ($result in (As-Array $manifestValidation.Results)) {
    if (-not $result.ManifestAccepted -or
        $result.Provider -ne "PolygonOfflineFile" -or
        $result.Dataset -ne "HistoricalBboQuotes" -or
        $result.Format -ne "NDJSON" -or
        $result.ContainsSecrets -or
        $result.ContainsRawProviderPayload -or
        -not $result.Sha256Matches -or
        -not $result.FileSizeMatches -or
        $result.RowCountDeclared -lt 0) {
        Fail "EXEC_PAPER_R018_FAIL_MANIFEST_VALIDATION" "Manifest validation failed for $($result.ExpectedFileEntryId)."
    }
}

if ($rowValidation.RowValidatedFileCount -ne $manifestValidation.AcceptedManifestCount -or
    $rowValidation.AcceptedForReadinessFileCount -ne $rowValidation.RowValidatedFileCount) {
    Fail "EXEC_PAPER_R018_FAIL_ROW_VALIDATION_COUNTS" "Row validation counts are inconsistent."
}
foreach ($result in (As-Array $rowValidation.Results)) {
    if (-not $result.RowValidationAcceptedForReadiness -or
        -not $result.RowCountMatchesManifest -or
        $result.InvalidTimestampRows -ne 0 -or
        $result.InvalidProviderSymbolRows -ne 0 -or
        $result.InvalidBidAskRows -ne 0 -or
        $result.AskLessThanBidRows -ne 0 -or
        $result.RawPayloadSerializedRows -ne 0) {
        Fail "EXEC_PAPER_R018_FAIL_ROW_VALIDATION" "Row validation failed for $($result.EntryId)."
    }
}
if ($rowCounts.MismatchCount -ne 0 -or $duplicates.ResultCount -ne 28) {
    Fail "EXEC_PAPER_R018_FAIL_ROW_COUNT_OR_DUPLICATE_SUMMARY" "Row count or duplicate handling summary is inconsistent."
}

if ($readiness.QuoteWindowReadinessRecords -lt $readiness.QuoteWindowReadyRecords -or
    $readiness.CloseBenchmarkReadinessRecords -lt $readiness.CloseBenchmarkReadyRecords -or
    $readiness.FeedQualityReadinessRecords -lt $readiness.FeedQualityReadyRecords -or
    $readiness.QuoteWindowReadyRecords -ne 4 -or
    $readiness.CloseBenchmarkReadyRecords -ne 4) {
    Fail "EXEC_PAPER_R018_FAIL_READINESS_COUNTS" "Generated readiness counts are unexpected."
}
foreach ($record in (As-Array $readiness.QuoteWindowResults)) {
    if ([string]$record.TargetCloseTimestampUtc -match "T\d{2}:(06|21|36|51):00Z") {
        Fail "EXEC_PAPER_R018_FAIL_LEGACY_CANONICAL" "Legacy timestamp used as future canonical readiness target close."
    }
}

if ($rebinding.R017StillHeldLineCount -ne 60 -or
    $rebinding.R018ReboundLineCount -ne 4 -or
    $rebinding.FinalStillHeldLineCount -ne 56 -or
    $rebinding.ReadinessBindingsInvented) {
    Fail "EXEC_PAPER_R018_FAIL_REBINDING_COUNTS" "Final rebinding counts are inconsistent or invented."
}
foreach ($line in (As-Array $rebinding.Results)) {
    if ($line.ReadinessBindingInvented) {
        Fail "EXEC_PAPER_R018_FAIL_INVENTED_BINDING" "Readiness binding was invented."
    }
    if ($line.ReboundComplete -and
        ([string]::IsNullOrWhiteSpace([string]$line.QuoteWindowReadinessBinding) -or
         [string]::IsNullOrWhiteSpace([string]$line.CloseBenchmarkReadinessBinding) -or
         [string]::IsNullOrWhiteSpace([string]$line.FeedQualityReadinessBinding))) {
        Fail "EXEC_PAPER_R018_FAIL_INCOMPLETE_REBOUND" "Rebound line missing one or more readiness binding ids."
    }
}

if ($stillHeld.FinalStillHeldLineCount -ne 56) {
    Fail "EXEC_PAPER_R018_FAIL_STILL_HELD_COUNT" "Final still-held diagnostics count is unexpected."
}
foreach ($held in (As-Array $stillHeld.Lines)) {
    if ($held.HoldReason -notmatch "MissingQuoteWindowReadinessBinding" -or
        $held.HoldReason -notmatch "MissingCloseBenchmarkReadinessBinding") {
        Fail "EXEC_PAPER_R018_FAIL_STILL_HELD_REASON" "Still-held line reason is not isolated to local readiness data."
    }
}

if ($reaggregated.PreviewLineCount -ne 700 -or
    $reaggregated.R017ReadinessCompleteLineCount -ne 640 -or
    $reaggregated.R018ReboundLineCount -ne 4 -or
    $reaggregated.ReadinessCompleteLineCount -ne 644 -or
    $reaggregated.FinalStillHeldLineCount -ne 56 -or
    $reaggregated.AllPreviewLinesReadinessComplete) {
    Fail "EXEC_PAPER_R018_FAIL_REAGGREGATED_PREVIEW" "Final reaggregated preview status is inconsistent."
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
        Fail "EXEC_PAPER_R018_FAIL_PREVIEW_LINE_EXECUTABLE" "Preview line is represented as executable/order/fill/route/submission."
    }
}

if ($review.ExpectedFileEntryCount -ne 28 -or
    $review.AcceptedFileEntryCount -ne 28 -or
    $review.MissingFileEntryCount -ne 0 -or
    $review.ReadinessCompleteLineCount -ne 644 -or
    $review.FinalStillHeldLineCount -ne 56 -or
    $review.ExecutablePromotionAuthorized -or
    $review.Decision -ne "R009LongRunPaperOnlyPartialMaturityWithExplicitReadinessBlocker") {
    Fail "EXEC_PAPER_R018_FAIL_OPERATOR_REVIEW" "Final operator review is inconsistent or executable."
}

$expectedClassifications = @(
    "EXEC_PAPER_R018_PARTIAL_FINAL_HELD_LINE_REBINDING_NO_EXTERNAL",
    "EXEC_PAPER_R018_PASS_FINAL_STILL_HELD_DIAGNOSTICS_READY_NO_EXTERNAL",
    "EXEC_PAPER_R018_PASS_EXPLICIT_READINESS_BLOCKER_READY_NO_EXTERNAL",
    "EXEC_PAPER_R018_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
)
if ($decision.Decision -ne "R009LongRunPaperOnlyPartialMaturityWithExplicitReadinessBlocker" -or
    $decision.ReadinessCompleteLineCount -ne 644 -or
    $decision.FinalStillHeldLineCount -ne 56 -or
    $decision.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R018_FAIL_DECISION" "Final maturity decision is inconsistent or executable."
}
foreach ($classification in $expectedClassifications) {
    if ((As-Array $decision.Classifications) -notcontains $classification) {
        Fail "EXEC_PAPER_R018_FAIL_CLASSIFICATION" "Missing expected classification: $classification"
    }
}

if ($downloadPackage.CommandsExecutedInR018 -ne 0 -or
    $downloadPackage.FilesDownloadedInR018 -ne 0 -or
    $downloadPackage.ExternalApiCalled -or
    (As-Array $downloadPackage.Commands).Count -lt 1) {
    Fail "EXEC_PAPER_R018_FAIL_FINAL_DOWNLOAD_PACKAGE" "Final command package was executed, externalized, or omitted."
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R018_FAIL_CANONICAL_POLICY" "Canonical quarter-hour policy was weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R018_FAIL_LEGACY_POLICY" "Legacy compatibility policy was weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or $usdPair.DirectCrossExecutionAllowed) {
    Fail "EXEC_PAPER_R018_FAIL_USD_PAIR_POLICY" "USD-pair-only policy was weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossExecutionDisabled) {
    Fail "EXEC_PAPER_R018_FAIL_DIRECT_CROSS_POLICY" "Direct-cross exclusion was weakened."
}
if ($cost.FiveUsdPerMillionUniversalized) {
    Fail "EXEC_PAPER_R018_FAIL_COST_UNIVERSALIZED" "5 USD/million was universalized."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_PAPER_R018_FAIL_NONMAJOR_CALIBRATION" "Nonmajor calibration requirement was weakened."
}
if (-not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne 4004 -or [string]$usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatWeakened) {
    Fail "EXEC_PAPER_R018_FAIL_USDJPY_CAVEAT" "USDJPY caveat was weakened."
}
if ($lmax.LmaxUsedInThisPhase -or $lmax.LmaxCalledInThisPhase) {
    Fail "EXEC_PAPER_R018_FAIL_LMAX_USED" "LMAX was used."
}

foreach ($auditName in @(
    "phase-exec-paper-r018-no-db-import-audit.json",
    "phase-exec-paper-r018-no-persisted-sanitized-row-audit.json",
    "phase-exec-paper-r018-no-new-pms-cycle-audit.json",
    "phase-exec-paper-r018-no-manualnoexternal-command-run-audit.json",
    "phase-exec-paper-r018-no-new-backtest-audit.json",
    "phase-exec-paper-r018-no-new-simulation-audit.json",
    "phase-exec-paper-r018-no-tca-result-lines-audit.json",
    "phase-exec-paper-r018-no-executable-schedule-audit.json",
    "phase-exec-paper-r018-no-child-slices-audit.json",
    "phase-exec-paper-r018-no-child-orders-audit.json",
    "phase-exec-paper-r018-no-order-created-audit.json",
    "phase-exec-paper-r018-no-real-fill-audit.json",
    "phase-exec-paper-r018-no-execution-report-audit.json",
    "phase-exec-paper-r018-no-route-no-submission-audit.json",
    "phase-exec-paper-r018-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r018-no-polygon-api-call-audit.json",
    "phase-exec-paper-r018-no-lmax-call-audit.json",
    "phase-exec-paper-r018-no-external-api-call-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_PAPER_R018_FAIL_AUDIT" "Forbidden action audit failed: $auditName"
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
    Fail "EXEC_PAPER_R018_FAIL_FORBIDDEN_ACTION" "Forbidden action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR018Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R018Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_PAPER_R018_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE" "Build/tests/validator evidence is missing."
}

Write-Output "EXEC_PAPER_R018_VALIDATION_PASSED"



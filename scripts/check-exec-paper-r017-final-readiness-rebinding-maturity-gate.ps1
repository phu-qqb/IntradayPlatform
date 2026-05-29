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
        Fail "EXEC_PAPER_R017_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-paper-r017-summary.md",
    "phase-exec-paper-r017-r016-reaggregation-reference.json",
    "phase-exec-paper-r017-r015-missing-readiness-reference.json",
    "phase-exec-paper-r017-r009-contract-reference.json",
    "phase-exec-paper-r017-missing-manifest-recovery-contract.json",
    "phase-exec-paper-r017-missing-manifest-recovery-result.json",
    "phase-exec-paper-r017-recovered-manifest-artifact.json",
    "phase-exec-paper-r017-local-file-validation-results.json",
    "phase-exec-paper-r017-row-level-validation-results.json",
    "phase-exec-paper-r017-generated-readiness-results.json",
    "phase-exec-paper-r017-final-held-line-rebinding-results.json",
    "phase-exec-paper-r017-final-still-held-line-diagnostics.json",
    "phase-exec-paper-r017-final-reaggregated-preview-status.json",
    "phase-exec-paper-r017-final-operator-review-report.md",
    "phase-exec-paper-r017-final-operator-review-report.json",
    "phase-exec-paper-r017-final-long-run-maturity-decision.json",
    "phase-exec-paper-r017-next-phase-recommendation.json",
    "phase-exec-paper-r017-if-needed-final-download-command-package.md",
    "phase-exec-paper-r017-if-needed-final-download-command-package.json",
    "phase-exec-paper-r017-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r017-legacy-compatibility-preservation.json",
    "phase-exec-paper-r017-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r017-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r017-cost-guidance-preservation.json",
    "phase-exec-paper-r017-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r017-no-db-import-audit.json",
    "phase-exec-paper-r017-no-persisted-sanitized-row-audit.json",
    "phase-exec-paper-r017-no-new-pms-cycle-audit.json",
    "phase-exec-paper-r017-no-manualnoexternal-command-run-audit.json",
    "phase-exec-paper-r017-no-new-backtest-audit.json",
    "phase-exec-paper-r017-no-new-simulation-audit.json",
    "phase-exec-paper-r017-no-tca-result-lines-audit.json",
    "phase-exec-paper-r017-no-executable-schedule-audit.json",
    "phase-exec-paper-r017-no-child-slices-audit.json",
    "phase-exec-paper-r017-no-child-orders-audit.json",
    "phase-exec-paper-r017-no-order-created-audit.json",
    "phase-exec-paper-r017-no-real-fill-audit.json",
    "phase-exec-paper-r017-no-execution-report-audit.json",
    "phase-exec-paper-r017-no-route-no-submission-audit.json",
    "phase-exec-paper-r017-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r017-no-polygon-api-call-audit.json",
    "phase-exec-paper-r017-no-lmax-call-audit.json",
    "phase-exec-paper-r017-no-external-api-call-audit.json",
    "phase-exec-paper-r017-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r017-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r017-no-external-audit.json",
    "phase-exec-paper-r017-forbidden-actions-audit.json",
    "phase-exec-paper-r017-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_PAPER_R017_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r016Ref = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-r016-reaggregation-reference.json")
$r015Ref = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-r015-missing-readiness-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-r009-contract-reference.json")
$recoveryContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-missing-manifest-recovery-contract.json")
$recovery = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-missing-manifest-recovery-result.json")
$recoveredManifest = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-recovered-manifest-artifact.json")
$fileValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-local-file-validation-results.json")
$rowValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-row-level-validation-results.json")
$readiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-generated-readiness-results.json")
$rebinding = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-final-held-line-rebinding-results.json")
$stillHeld = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-final-still-held-line-diagnostics.json")
$reaggregated = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-final-reaggregated-preview-status.json")
$review = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-final-operator-review-report.json")
$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-final-long-run-maturity-decision.json")
$downloadPackage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-if-needed-final-download-command-package.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-build-test-validator-evidence.json")

if ($r016Ref.SourcePhase -ne "EXEC-PAPER-R016" -or
    $r016Ref.R016ReadinessCompleteLineCount -ne 636 -or
    $r016Ref.R016StillHeldLineCount -ne 64 -or
    $r016Ref.R016Decision -ne "R009LongRunPaperOnlyPartialMaturityNeedsMoreReadiness" -or
    -not $r016Ref.ReusedOnly) {
    Fail "EXEC_PAPER_R017_FAIL_R016_REFERENCE" "R016 reaggregation reference is invalid."
}

if ($r015Ref.SourcePhase -ne "EXEC-PAPER-R015" -or
    $r015Ref.MissingWindowCount -ne 322 -or
    -not $r015Ref.ReusedOnly) {
    Fail "EXEC_PAPER_R017_FAIL_R015_REFERENCE" "R015 missing-readiness reference is invalid."
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
    Fail "EXEC_PAPER_R017_FAIL_R009_CONTRACT" "R009 contract is executable or widened."
}

if (-not $recoveryContract.RecoveryAllowed -or
    -not $recoveryContract.RecoveryWritesArtifactsOnly -or
    $recoveryContract.DataIncomingOverwriteAllowed -or
    $recoveryContract.ManifestSourceMustBe -ne "LocalRecoveredManifestArtifact" -or
    -not $recoveryContract.MustMarkNotProviderGenerated -or
    $recoveryContract.DownloadsAllowed -or
    $recoveryContract.ExternalApiAllowed) {
    Fail "EXEC_PAPER_R017_FAIL_RECOVERY_CONTRACT" "Missing-manifest recovery contract is unsafe."
}

if (-not $recovery.RecoveryAttempted -or
    -not $recovery.QuoteFileExists -or
    -not $recovery.RecoveredManifestArtifactCreated -or
    $recovery.ManifestSource -ne "LocalRecoveredManifestArtifact" -or
    -not $recovery.NotProviderGenerated -or
    $recovery.WritesDataIncomingFile) {
    Fail "EXEC_PAPER_R017_FAIL_RECOVERY_RESULT" "Recovered manifest result is missing, unsafe, or provider-generated."
}

if ($recoveredManifest.ManifestSource -ne "LocalRecoveredManifestArtifact" -or
    -not $recoveredManifest.NotProviderGenerated -or
    $recoveredManifest.ProviderGenerated -or
    $recoveredManifest.ProviderName -ne "PolygonOfflineFile" -or
    $recoveredManifest.ProviderDatasetType -ne "HistoricalBboQuotes" -or
    $recoveredManifest.FileFormat -ne "NDJSON" -or
    $recoveredManifest.ExecutionTradableSymbol -ne "AUDUSD" -or
    $recoveredManifest.ProviderSymbol -ne "C:AUD-USD" -or
    $recoveredManifest.RowCountDeclared -le 0 -or
    [string]::IsNullOrWhiteSpace([string]$recoveredManifest.FileHash)) {
    Fail "EXEC_PAPER_R017_FAIL_RECOVERED_MANIFEST" "Recovered manifest artifact is invalid or represented as provider-generated."
}

if ($fileValidation.LocalFileValidationCount -lt 1 -or
    $fileValidation.AcceptedLocalFileValidationCount -ne $fileValidation.LocalFileValidationCount) {
    Fail "EXEC_PAPER_R017_FAIL_FILE_VALIDATION_COUNTS" "Local file validation did not accept all locally validated files."
}
foreach ($result in (As-Array $fileValidation.Results)) {
    if (-not $result.ManifestValid -or
        -not $result.Sha256Matches -or
        $result.RowCountDeclared -lt 0) {
        Fail "EXEC_PAPER_R017_FAIL_FILE_VALIDATION" "Local file validation failed for $($result.EntryId)."
    }
}

if ($rowValidation.RowValidatedFileCount -ne $fileValidation.LocalFileValidationCount -or
    $rowValidation.AcceptedForReadinessFileCount -ne $rowValidation.RowValidatedFileCount) {
    Fail "EXEC_PAPER_R017_FAIL_ROW_VALIDATION_COUNTS" "Row validation counts are inconsistent."
}
foreach ($result in (As-Array $rowValidation.Results)) {
    if (-not $result.RowValidationAcceptedForReadiness -or
        -not $result.RowCountMatchesManifest -or
        $result.InvalidTimestampRows -ne 0 -or
        $result.InvalidProviderSymbolRows -ne 0 -or
        $result.InvalidBidAskRows -ne 0 -or
        $result.AskLessThanBidRows -ne 0 -or
        $result.RawPayloadSerializedRows -ne 0) {
        Fail "EXEC_PAPER_R017_FAIL_ROW_VALIDATION" "Row validation failed for $($result.EntryId)."
    }
}

if ($readiness.QuoteWindowReadinessRecords -lt $readiness.QuoteWindowReadyRecords -or
    $readiness.CloseBenchmarkReadinessRecords -lt $readiness.CloseBenchmarkReadyRecords -or
    $readiness.FeedQualityReadinessRecords -lt $readiness.FeedQualityReadyRecords -or
    $readiness.QuoteWindowReadyRecords -ne 4 -or
    $readiness.CloseBenchmarkReadyRecords -ne 4) {
    Fail "EXEC_PAPER_R017_FAIL_READINESS_COUNTS" "Generated readiness counts are unexpected."
}
foreach ($record in (As-Array $readiness.QuoteWindowResults)) {
    if ([string]$record.TargetCloseTimestampUtc -match "T\d{2}:(06|21|36|51):00Z") {
        Fail "EXEC_PAPER_R017_FAIL_LEGACY_CANONICAL" "Legacy timestamp used as future canonical readiness target close."
    }
}

if ($rebinding.R016StillHeldLineCount -ne 64 -or
    $rebinding.R017ReboundLineCount -ne 4 -or
    $rebinding.FinalStillHeldLineCount -ne 60 -or
    $rebinding.ReadinessBindingsInvented) {
    Fail "EXEC_PAPER_R017_FAIL_REBINDING_COUNTS" "Final rebinding counts are inconsistent or invented."
}
foreach ($line in (As-Array $rebinding.Results)) {
    if ($line.ReadinessBindingInvented) {
        Fail "EXEC_PAPER_R017_FAIL_INVENTED_BINDING" "Readiness binding was invented."
    }
    if ($line.ReboundComplete -and
        ([string]::IsNullOrWhiteSpace([string]$line.QuoteWindowReadinessBinding) -or
         [string]::IsNullOrWhiteSpace([string]$line.CloseBenchmarkReadinessBinding) -or
         [string]::IsNullOrWhiteSpace([string]$line.FeedQualityReadinessBinding))) {
        Fail "EXEC_PAPER_R017_FAIL_INCOMPLETE_REBOUND" "Rebound line missing one or more readiness binding ids."
    }
}

if ($stillHeld.FinalStillHeldLineCount -ne 60) {
    Fail "EXEC_PAPER_R017_FAIL_STILL_HELD_COUNT" "Final still-held diagnostics count is unexpected."
}
foreach ($held in (As-Array $stillHeld.Lines)) {
    if ($held.HoldReason -notmatch "MissingQuoteWindowReadinessBinding" -or
        $held.HoldReason -notmatch "MissingCloseBenchmarkReadinessBinding") {
        Fail "EXEC_PAPER_R017_FAIL_STILL_HELD_REASON" "Still-held line reason is not isolated to local readiness data."
    }
}

if ($reaggregated.PreviewLineCount -ne 700 -or
    $reaggregated.R016ReadinessCompleteLineCount -ne 636 -or
    $reaggregated.R017ReboundLineCount -ne 4 -or
    $reaggregated.ReadinessCompleteLineCount -ne 640 -or
    $reaggregated.FinalStillHeldLineCount -ne 60 -or
    $reaggregated.AllPreviewLinesReadinessComplete) {
    Fail "EXEC_PAPER_R017_FAIL_REAGGREGATED_PREVIEW" "Final reaggregated preview status is inconsistent."
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
        Fail "EXEC_PAPER_R017_FAIL_PREVIEW_LINE_EXECUTABLE" "Preview line is represented as executable/order/fill/route/submission."
    }
}

if ($review.ReadinessCompleteLineCount -ne 640 -or
    $review.FinalStillHeldLineCount -ne 60 -or
    $review.ExecutablePromotionAuthorized -or
    $review.Decision -ne "R009LongRunPaperOnlyPartialMaturityWithExplicitLocalDataBlocker") {
    Fail "EXEC_PAPER_R017_FAIL_OPERATOR_REVIEW" "Final operator review is inconsistent or executable."
}

$expectedClassifications = @(
    "EXEC_PAPER_R017_PARTIAL_FINAL_REBINDING_WITH_EXPLICIT_BLOCKER_NO_EXTERNAL",
    "EXEC_PAPER_R017_PASS_FINAL_STILL_HELD_DIAGNOSTICS_READY_NO_EXTERNAL",
    "EXEC_PAPER_R017_PASS_NO_DOWNLOAD_NO_ORDER_GATE_READY_NO_EXTERNAL"
)
if ($decision.Decision -ne "R009LongRunPaperOnlyPartialMaturityWithExplicitLocalDataBlocker" -or
    $decision.ReadinessCompleteLineCount -ne 640 -or
    $decision.FinalStillHeldLineCount -ne 60 -or
    $decision.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R017_FAIL_DECISION" "Final maturity decision is inconsistent or executable."
}
foreach ($classification in $expectedClassifications) {
    if ((As-Array $decision.Classifications) -notcontains $classification) {
        Fail "EXEC_PAPER_R017_FAIL_CLASSIFICATION" "Missing expected classification: $classification"
    }
}

if ($downloadPackage.DownloadCommandsExecuted -or
    $downloadPackage.ExternalApiCalled -or
    (As-Array $downloadPackage.Commands).Count -lt 1) {
    Fail "EXEC_PAPER_R017_FAIL_FINAL_DOWNLOAD_PACKAGE" "Final command package was executed, externalized, or omitted."
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R017_FAIL_CANONICAL_POLICY" "Canonical quarter-hour policy was weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R017_FAIL_LEGACY_POLICY" "Legacy compatibility policy was weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or $usdPair.DirectCrossExecutionAllowed) {
    Fail "EXEC_PAPER_R017_FAIL_USD_PAIR_POLICY" "USD-pair-only policy was weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossExecutionDisabled) {
    Fail "EXEC_PAPER_R017_FAIL_DIRECT_CROSS_POLICY" "Direct-cross exclusion was weakened."
}
if ($cost.FiveUsdPerMillionUniversalized) {
    Fail "EXEC_PAPER_R017_FAIL_COST_UNIVERSALIZED" "5 USD/million was universalized."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_PAPER_R017_FAIL_NONMAJOR_CALIBRATION" "Nonmajor calibration requirement was weakened."
}
if (-not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne 4004 -or [string]$usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatWeakened) {
    Fail "EXEC_PAPER_R017_FAIL_USDJPY_CAVEAT" "USDJPY caveat was weakened."
}
if ($lmax.LmaxUsedInThisPhase -or $lmax.LmaxCalledInThisPhase) {
    Fail "EXEC_PAPER_R017_FAIL_LMAX_USED" "LMAX was used."
}

foreach ($auditName in @(
    "phase-exec-paper-r017-no-db-import-audit.json",
    "phase-exec-paper-r017-no-persisted-sanitized-row-audit.json",
    "phase-exec-paper-r017-no-new-pms-cycle-audit.json",
    "phase-exec-paper-r017-no-manualnoexternal-command-run-audit.json",
    "phase-exec-paper-r017-no-new-backtest-audit.json",
    "phase-exec-paper-r017-no-new-simulation-audit.json",
    "phase-exec-paper-r017-no-tca-result-lines-audit.json",
    "phase-exec-paper-r017-no-executable-schedule-audit.json",
    "phase-exec-paper-r017-no-child-slices-audit.json",
    "phase-exec-paper-r017-no-child-orders-audit.json",
    "phase-exec-paper-r017-no-order-created-audit.json",
    "phase-exec-paper-r017-no-real-fill-audit.json",
    "phase-exec-paper-r017-no-execution-report-audit.json",
    "phase-exec-paper-r017-no-route-no-submission-audit.json",
    "phase-exec-paper-r017-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r017-no-polygon-api-call-audit.json",
    "phase-exec-paper-r017-no-lmax-call-audit.json",
    "phase-exec-paper-r017-no-external-api-call-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_PAPER_R017_FAIL_AUDIT" "Forbidden action audit failed: $auditName"
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
    Fail "EXEC_PAPER_R017_FAIL_FORBIDDEN_ACTION" "Forbidden action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR017Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R017Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_PAPER_R017_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE" "Build/tests/validator evidence is missing."
}

Write-Output "EXEC_PAPER_R017_VALIDATION_PASSED"

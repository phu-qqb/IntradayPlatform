param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Classification, [string]$Message) {
    Write-Error "$Classification $Message"
    exit 1
}

function Assert-True($Condition, [string]$Classification, [string]$Message) {
    if (-not $Condition) {
        Fail $Classification $Message
    }
}

function Read-Json([string]$RelativePath, [string]$Classification) {
    $path = Join-Path $Root $RelativePath
    Assert-True (Test-Path -LiteralPath $path) $Classification "Missing $RelativePath"
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$artifactRoot = 'artifacts/readiness/lmax-runtime-enablement'

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r210-audusd-only-retry-readiness-summary.md")) 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'R210 summary missing.'

$readiness = Read-Json "$artifactRoot/phase-lmax-r210-audusd-only-retry-readiness.json" 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$profile = Read-Json "$artifactRoot/phase-lmax-r210-audusd-profile-readiness.json" 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$logout = Read-Json "$artifactRoot/phase-lmax-r210-logout-reporting-readiness.json" 'LMAX_R210_FAIL_LOGOUT_REPORTING_READINESS_MISSING'
$entries = Read-Json "$artifactRoot/phase-lmax-r210-entries-reporting-readiness.json" 'LMAX_R210_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING'
$state = Read-Json "$artifactRoot/phase-lmax-r210-final-state-evidence-contract-readiness.json" 'LMAX_R210_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING'
$reject = Read-Json "$artifactRoot/phase-lmax-r210-enhanced-reject-reporting-readiness.json" 'LMAX_R210_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING'
$baseline = Read-Json "$artifactRoot/phase-lmax-r210-gbpusd-eurgbp-success-baseline-preservation.json" 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$usdjpy = Read-Json "$artifactRoot/phase-lmax-r210-usdjpy-not-proven-and-caveat-preservation.json" 'LMAX_R210_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$reservation = Read-Json "$artifactRoot/phase-lmax-r210-next-activation-phase-reservation-decision.json" 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID'
$preflight = Read-Json "$artifactRoot/phase-lmax-r210-r211-preflight-checklist.json" 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID'
$expected = Read-Json "$artifactRoot/phase-lmax-r210-r211-expected-evidence-contract.json" 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$universe = Read-Json "$artifactRoot/phase-lmax-r210-approved-universe-preservation.json" 'LMAX_R210_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$caveat = Read-Json "$artifactRoot/phase-lmax-r210-usdjpy-caveat-preservation.json" 'LMAX_R210_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r210-sanitization-audit.json" 'LMAX_R210_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r210-forbidden-actions-audit.json" 'LMAX_R210_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r210-api-worker-fake-gateway-audit.json" 'LMAX_R210_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r210-next-phase-recommendation.json" 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID'
$validation = Read-Json "$artifactRoot/phase-lmax-r210-gate-validation.json" 'LMAX_R210_FAIL_BUILD_OR_TESTS'

foreach ($required in @(
    "$artifactRoot/phase-lmax-r210-r211-operator-approval-template.md",
    "$artifactRoot/phase-lmax-r210-r211-activation-prompt-compact.md")) {
    Assert-True (Test-Path -LiteralPath (Join-Path $Root $required)) 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' "Missing $required"
}

$r207 = Read-Json "$artifactRoot/phase-lmax-r207-activation.json" 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$r208 = Read-Json "$artifactRoot/phase-lmax-r208-partial-remaining-instruments-review.json" 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$r209 = Read-Json "$artifactRoot/phase-lmax-r209-logout-reason-extraction-enrichment.json" 'LMAX_R210_FAIL_LOGOUT_REPORTING_READINESS_MISSING'

Assert-True ($readiness.noExternal -eq $true -and $readiness.newExternalActivationAttempted -eq $false -and $readiness.socketTlsFixMarketDataRuntimeActionDetected -eq $false) 'LMAX_R210_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R210 must remain no-external.'
Assert-True ($forbidden.newExternalActivationAttempted -eq $false -and $forbidden.socketOpened -eq $false -and $forbidden.tlsOpened -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R210_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R210 opened socket/TLS/FIX.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R210_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R210 performed live MarketData action.'

Assert-True ($r207.classification -eq 'LMAX_R207_PASS_PARTIAL_REMAINING_INSTRUMENTS_SEQUENTIAL_ENTRIES_RUNTIME_ACTIVATION_SANITIZED') 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'R207 evidence missing.'
Assert-True ($r208.classification -eq 'LMAX_R208_PASS_PARTIAL_REMAINING_INSTRUMENTS_REVIEW_LOGOUT_REASON_ENRICHMENT_RECOMMENDED_NO_EXTERNAL') 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'R208 evidence missing.'
Assert-True ($r209.classification -eq 'LMAX_R209_PASS_AUDUSD_ONLY_RETRY_READINESS_RECOMMENDED_NO_EXTERNAL') 'LMAX_R210_FAIL_LOGOUT_REPORTING_READINESS_MISSING' 'R209 evidence missing.'

Assert-True ($reservation.targetPhase -eq 'LMAX-R211' -and $reservation.r211OddNumbered -eq $true -and $reservation.r211ExplicitlyReserved -eq $true) 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R211 reservation invalid.'
Assert-True ($reservation.singleExternalActivationAttemptOnly -eq $true -and $reservation.audusdOnly -eq $true -and $reservation.multiInstrumentRetryAllowed -eq $false) 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R211 single-attempt/AUDUSD-only reservation invalid.'

$reservationSource = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
Assert-True ($reservationSource.Contains('"LMAX-R211"')) 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R211 is not in executable reservation source.'

$operationSource = Get-Content -LiteralPath (Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs') -Raw
Assert-True ($operationSource.Contains('IsR211AudUsdOnlyScope') -and $operationSource.Contains('AudUsdInstrument')) 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'R211 AUDUSD-only operation guard missing.'

Assert-True ($profile.audusdOnlyProfileReadiness -eq $true -and $profile.futureRequestAudusdOnly -eq $true -and $profile.futureInstrument -eq 'AUDUSD') 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'AUDUSD-only profile readiness missing.'
Assert-True ($profile.futureRequestIncludesGbpusd -eq $false -and $profile.futureRequestIncludesEurgbp -eq $false -and $profile.futureRequestIncludesUsdjpy -eq $false) 'LMAX_R210_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'Future request includes non-AUDUSD instrument.'
Assert-True ($profile.requestCount -eq 1 -and $profile.securityId -eq '4007' -and $profile.securityIdSource -eq '8') 'LMAX_R210_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'AUDUSD request count or SecurityID/Source invalid.'
Assert-True ($profile.mdReqIdShort -eq $true -and $profile.mdReqIdAlphanumericOnly -eq $true -and $profile.mdReqIdUniquePerRequest -eq $true -and $profile.mdReqIdLengthLessThanOrEqual16 -eq $true) 'LMAX_R210_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'MDReqID repaired shape readiness missing.'
Assert-True ($profile.mdReqIdContainsPhaseLabel -eq $false -and $profile.mdReqIdContainsUnderscore -eq $false -and $profile.mdReqIdContainsPunctuation -eq $false -and $profile.rawMdReqIdSerialized -eq $false) 'LMAX_R210_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'MDReqID shape weakened or raw MDReqID serialization risk.'
Assert-True ($profile.subscriptionRequestType -eq '1' -and $profile.mdUpdateType -eq '0' -and $profile.marketDepth -eq 1 -and $profile.noMdEntryTypes -eq 2 -and $profile.bidAndOfferTogether -eq $true) 'LMAX_R210_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'MarketDataRequest contract weakened.'
Assert-True ($profile.symbolTextPresent -eq $false -and $profile.internalSymbolPresent -eq $false -and $profile.snapshotOnlyPresent -eq $false -and $profile.subscriptionRequestTypeZeroPresent -eq $false) 'LMAX_R210_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'Symbol/InternalSymbol/SnapshotOnly appeared.'

Assert-True ($state.finalStateEvidenceReadiness -eq $true -and $state.classifiedMarketDataResponseCannotEmitAllExplicitStateFieldsFalse -eq $true) 'LMAX_R210_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' 'Final-state evidence readiness missing.'
foreach ($field in @('marketDataRequestWriteAttempted', 'marketDataRequestWriteSucceeded', 'marketDataRequestResponseReadAttempted', 'marketDataRequestReachedBoundedResponseClassification', 'marketDataRequestSentLegacyFlag')) {
    Assert-True ($state.requiredFields -contains $field) 'LMAX_R210_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Final-state field missing: $field"
}

Assert-True ($entries.entriesReportingReadiness -eq $true) 'LMAX_R210_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' 'Entries reporting readiness missing.'
foreach ($field in @('marketDataEntriesObserved', 'marketDataSanitizedEntryCount', 'marketDataEntriesEvidenceCategory', 'marketDataEntriesReportingSource', 'marketDataEntriesNotAvailableReason')) {
    Assert-True ($entries.requiredFields -contains $field) 'LMAX_R210_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Entries field missing: $field"
}
Assert-True ($entries.rawMarketDataPricesAllowed -eq $false -and $entries.rawBidAskValuesAllowed -eq $false -and $entries.rawMarketDataPayloadAllowed -eq $false) 'LMAX_R210_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'Entries reporting allows raw market data.'

Assert-True ($reject.enhancedRejectReportingReadiness -eq $true -and $reject.notAvailableDistinctFromPropagationFailure -eq $true) 'LMAX_R210_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' 'Enhanced reject readiness missing.'
foreach ($field in @('sessionRejectRefTagIdSanitizedCategory', 'sessionRejectReasonSanitizedCategory', 'sessionRejectRefMsgTypeSanitizedCategory', 'rejectReasonExtractionSource')) {
    Assert-True ($reject.requiredFields -contains $field) 'LMAX_R210_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Enhanced reject field missing: $field"
}

Assert-True ($logout.logoutReportingReadiness -eq $true -and $logout.msgType5FixLogoutDistinguished -eq $true -and $logout.notAvailableDistinctFromPropagationFailure -eq $true) 'LMAX_R210_FAIL_LOGOUT_REPORTING_READINESS_MISSING' 'Logout reporting readiness missing.'
foreach ($field in @('logoutObserved', 'logoutSourceCategory', 'logoutReasonSanitizedCategory', 'logoutTextPresentSanitized', 'logoutAfterInstrument', 'logoutAfterSecurityIdSanitized', 'logoutTimingCategory', 'logoutReasonExtractionSource')) {
    Assert-True ($logout.requiredFields -contains $field) 'LMAX_R210_FAIL_LOGOUT_REPORTING_READINESS_MISSING' "Logout field missing: $field"
}
Assert-True ($logout.rawLogoutTextSerialized -eq $false) 'LMAX_R210_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Raw logout text serialization risk.'

Assert-True ($baseline.gbpusdBaselinePreserved -eq $true -and $baseline.gbpusdMarketDataResponseCategory -eq 'Succeeded' -and $baseline.gbpusdEntriesObserved -eq $true -and $baseline.gbpusdSanitizedEntryCount -eq 2) 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'GBPUSD baseline weakened.'
Assert-True ($baseline.eurgbpBaselinePreserved -eq $true -and $baseline.eurgbpSecurityId -eq '4003' -and $baseline.eurgbpSecurityIdSource -eq '8' -and $baseline.eurgbpEntriesObserved -eq $true -and $baseline.eurgbpSanitizedEntryCount -eq 2) 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'EURGBP baseline weakened.'

Assert-True ($usdjpy.usdjpyNotProven -eq $true -and $usdjpy.usdjpyClassifiedAsFailed -eq $false -and $usdjpy.usdJpyCaveatPreserved -eq $true) 'LMAX_R210_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY misclassified or caveat weakened.'
Assert-True ($usdjpy.usdJpySecurityId -eq '4004' -and $usdjpy.usdJpySecurityIdSource -eq '8') 'LMAX_R210_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY SecurityID/Source caveat weakened.'
Assert-True ($caveat.usdJpyNotProven -eq $true -and $caveat.usdJpyClassifiedAsFailed -eq $false -and $caveat.futureR211IncludesUsdJpy -eq $false) 'LMAX_R210_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY R211 status invalid.'

Assert-True ($universe.approvedUniversePreserved -eq $true -and $universe.approvedInstruments -contains 'GBPUSD' -and $universe.approvedInstruments -contains 'EURGBP' -and $universe.approvedInstruments -contains 'AUDUSD' -and $universe.approvedInstruments -contains 'USDJPY') 'LMAX_R210_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'Approved universe weakened.'
Assert-True ($universe.newInstrumentOutsideApprovedUniverseIntroduced -eq $false -and $universe.usdJpyCaveatPreserved -eq $true) 'LMAX_R210_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'Universe/caveat weakened.'

Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true -and $preflight.concreteWeekdayMarketHoursConfirmationRequired -eq $true -and $preflight.exactlyOneBoundedAttemptRequired -eq $true -and $preflight.stopAfterAttempt -eq $true) 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R211 approval/market-hours/attempt constraints missing.'
Assert-True ($expected.expectedEvidenceContractReady -eq $true -and $expected.selectedInstrument -eq 'AUDUSD' -and $expected.securityId -eq '4007' -and $expected.securityIdSource -eq '8') 'LMAX_R210_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'R211 expected evidence contract missing.'
foreach ($field in @('marketDataEntriesObserved', 'marketDataSanitizedEntryCount', 'marketDataEntriesEvidenceCategory', 'marketDataEntriesReportingSource', 'marketDataEntriesNotAvailableReason')) {
    Assert-True ($expected.requiredEntriesFields -contains $field) 'LMAX_R210_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Expected entries field missing: $field"
}
foreach ($field in @('logoutObserved', 'logoutSourceCategory', 'logoutReasonSanitizedCategory', 'logoutTextPresentSanitized', 'logoutAfterInstrument', 'logoutAfterSecurityIdSanitized', 'logoutTimingCategory', 'logoutReasonExtractionSource')) {
    Assert-True ($expected.requiredLogoutFields -contains $field) 'LMAX_R210_FAIL_LOGOUT_REPORTING_READINESS_MISSING' "Expected logout field missing: $field"
}
Assert-True ($expected.rawLogoutTextAllowed -eq $false -and $expected.rawMarketDataPricesPayloadsAllowed -eq $false -and $expected.rawFixAllowed -eq $false -and $expected.rawMdReqIdAllowed -eq $false) 'LMAX_R210_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Expected evidence contract allows raw values.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R210_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawLogoutTextSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false -and $sanitization.rawFixMessagesSerialized -eq $false) 'LMAX_R210_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Raw logout/reject/FIX leak risk.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false -and $sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R210_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'Raw market-data/MDReqID leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false) 'LMAX_R210_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Secret/session/endpoint/TLS leak risk.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R210_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R210_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false -and $forbidden.productionAccountUsed -eq $false) 'LMAX_R210_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay or production account introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R210_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'

Assert-True ($next.supportingClassification -eq 'LMAX_R210_PASS_AUDUSD_ONLY_RETRY_READINESS_NO_EXTERNAL') 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'Next recommendation classification missing.'
Assert-True ($next.r211RequiresFreshExactOperatorApproval -eq $true -and $next.r211RequiresConcreteWeekdayMarketHoursConfirmation -eq $true -and $next.r211RequiresExactlyOneBoundedExternalAttempt -eq $true -and $next.r211MustStopAfterAttempt -eq $true -and $next.r212MustRemainNoExternal -eq $true) 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R211 recommendation constraints missing.'
Assert-True ($next.multiInstrumentLiveRetryAllowedBeforeAudusdIsolationOrReview -eq $false) 'LMAX_R210_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'Next recommendation allows multi-instrument retry.'

Assert-True ($validation.buildStatus -eq 'PASS' -and $validation.focusedTestsStatus -eq 'PASS 128/128' -and $validation.unitTestsStatus -eq 'PASS 1841/1841') 'LMAX_R210_FAIL_BUILD_OR_TESTS' 'Build/test evidence missing.'
Assert-True ($validation.validator -eq 'LMAX_R210_VALIDATION_PASS' -and $validation.validatorStatus -eq 'PASS') 'LMAX_R210_FAIL_BUILD_OR_TESTS' 'Validator evidence missing.'

Write-Output 'LMAX_R210_VALIDATION_PASS'

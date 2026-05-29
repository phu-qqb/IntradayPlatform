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

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r213-audusd-only-retry-readiness-after-tls-inconclusive-summary.md")) 'LMAX_R213_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'R213 summary missing.'

$readiness = Read-Json "$artifactRoot/phase-lmax-r213-audusd-only-retry-readiness-after-tls-inconclusive.json" 'LMAX_R213_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$profile = Read-Json "$artifactRoot/phase-lmax-r213-audusd-profile-readiness.json" 'LMAX_R213_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$tls = Read-Json "$artifactRoot/phase-lmax-r213-tls-boundary-inconclusive-preservation.json" 'LMAX_R213_FAIL_TLS_INCONCLUSIVE_MISREPRESENTED'
$logout = Read-Json "$artifactRoot/phase-lmax-r213-logout-reporting-readiness.json" 'LMAX_R213_FAIL_LOGOUT_REPORTING_READINESS_MISSING'
$entries = Read-Json "$artifactRoot/phase-lmax-r213-entries-reporting-readiness.json" 'LMAX_R213_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING'
$state = Read-Json "$artifactRoot/phase-lmax-r213-final-state-evidence-contract-readiness.json" 'LMAX_R213_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING'
$reject = Read-Json "$artifactRoot/phase-lmax-r213-enhanced-reject-reporting-readiness.json" 'LMAX_R213_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING'
$baseline = Read-Json "$artifactRoot/phase-lmax-r213-gbpusd-eurgbp-success-baseline-preservation.json" 'LMAX_R213_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$usdjpy = Read-Json "$artifactRoot/phase-lmax-r213-usdjpy-not-proven-and-caveat-preservation.json" 'LMAX_R213_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$reservation = Read-Json "$artifactRoot/phase-lmax-r213-next-activation-phase-reservation-decision.json" 'LMAX_R213_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID'
$preflight = Read-Json "$artifactRoot/phase-lmax-r213-r215-preflight-checklist.json" 'LMAX_R213_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID'
$expected = Read-Json "$artifactRoot/phase-lmax-r213-r215-expected-evidence-contract.json" 'LMAX_R213_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$universe = Read-Json "$artifactRoot/phase-lmax-r213-approved-universe-preservation.json" 'LMAX_R213_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$caveat = Read-Json "$artifactRoot/phase-lmax-r213-usdjpy-caveat-preservation.json" 'LMAX_R213_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r213-sanitization-audit.json" 'LMAX_R213_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r213-forbidden-actions-audit.json" 'LMAX_R213_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r213-api-worker-fake-gateway-audit.json" 'LMAX_R213_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r213-next-phase-recommendation.json" 'LMAX_R213_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID'
$validation = Read-Json "$artifactRoot/phase-lmax-r213-gate-validation.json" 'LMAX_R213_FAIL_BUILD_OR_TESTS'

foreach ($required in @(
    "$artifactRoot/phase-lmax-r213-r215-operator-approval-template.md",
    "$artifactRoot/phase-lmax-r213-r215-activation-prompt-compact.md")) {
    Assert-True (Test-Path -LiteralPath (Join-Path $Root $required)) 'LMAX_R213_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' "Missing $required"
}

$r211 = Read-Json "$artifactRoot/phase-lmax-r211-activation.json" 'LMAX_R213_FAIL_TLS_INCONCLUSIVE_MISREPRESENTED'
$r211Boundary = Read-Json "$artifactRoot/phase-lmax-r211-boundary-evidence.json" 'LMAX_R213_FAIL_TLS_INCONCLUSIVE_MISREPRESENTED'
$r212 = Read-Json "$artifactRoot/phase-lmax-r212-audusd-runtime-boundary-inconclusive-review.json" 'LMAX_R213_FAIL_TLS_INCONCLUSIVE_MISREPRESENTED'

Assert-True ($readiness.noExternal -eq $true -and $readiness.newExternalActivationAttempted -eq $false -and $readiness.socketTlsFixMarketDataRuntimeActionDetected -eq $false) 'LMAX_R213_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R213 must remain no-external.'
Assert-True ($forbidden.newExternalActivationAttempted -eq $false -and $forbidden.socketOpened -eq $false -and $forbidden.tlsOpened -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R213_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R213 opened socket/TLS/FIX.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R213_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R213 performed live MarketData action.'

Assert-True ($r211.classification -eq 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' -and $r211.attemptCount -eq 1) 'LMAX_R213_FAIL_TLS_INCONCLUSIVE_MISREPRESENTED' 'R211 evidence missing.'
Assert-True ($r212.classification -eq 'LMAX_R212_PASS_AUDUSD_BOUNDARY_INCONCLUSIVE_REVIEW_RETRY_READINESS_RECOMMENDED_NO_EXTERNAL') 'LMAX_R213_FAIL_TLS_INCONCLUSIVE_MISREPRESENTED' 'R212 evidence missing.'
Assert-True ($tls.r211TlsBoundaryInconclusivePreserved -eq $true -and $tls.r211TlsResultCategory -eq 'HandshakeException' -and $tls.r211TransportResultCategory -eq 'TlsHandshakeBoundaryFailed') 'LMAX_R213_FAIL_TLS_INCONCLUSIVE_MISREPRESENTED' 'TLS inconclusive preservation missing.'
Assert-True ($tls.r211FixLogonAttempted -eq $false -and $tls.r211MarketDataRequestSent -eq $false -and $tls.r211MarketDataResponseRead -eq $false) 'LMAX_R213_FAIL_TLS_INCONCLUSIVE_MISREPRESENTED' 'R211 TLS failure misrepresented.'
Assert-True ($tls.audusdMarketDataFailureClaimed -eq $false -and $tls.audusdLogoutRejectEntriesConclusionClaimed -eq $false) 'LMAX_R213_FAIL_TLS_INCONCLUSIVE_MISREPRESENTED' 'AUDUSD failure or entries/logout/reject conclusion claimed.'
Assert-True ($r211Boundary.tlsSucceeded -eq $false -and $r211Boundary.fixLogonAttempted -eq $false -and $r211Boundary.marketDataBoundaryStatus -eq 'NotAttempted') 'LMAX_R213_FAIL_TLS_INCONCLUSIVE_MISREPRESENTED' 'R211 boundary artifact mismatch.'

Assert-True ($reservation.targetPhase -eq 'LMAX-R215' -and $reservation.r215OddNumbered -eq $true -and $reservation.r215ExplicitlyReserved -eq $true) 'LMAX_R213_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R215 reservation invalid.'
Assert-True ($reservation.singleExternalActivationAttemptOnly -eq $true -and $reservation.audusdOnly -eq $true -and $reservation.r216MustRemainNoExternal -eq $true) 'LMAX_R213_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R215 single-attempt/AUDUSD-only constraints missing.'

$reservationSource = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
Assert-True ($reservationSource.Contains('"LMAX-R215"')) 'LMAX_R213_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R215 is not in executable reservation source.'
$operationSource = Get-Content -LiteralPath (Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs') -Raw
Assert-True ($operationSource.Contains('LMAX-R215') -and $operationSource.Contains('AudUsdInstrument')) 'LMAX_R213_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'R215 AUDUSD-only operation guard missing.'

Assert-True ($profile.audusdOnlyProfileReadiness -eq $true -and $profile.futureRequestAudusdOnly -eq $true -and $profile.futureInstrument -eq 'AUDUSD') 'LMAX_R213_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'AUDUSD-only profile readiness missing.'
Assert-True ($profile.futureRequestIncludesGbpusd -eq $false -and $profile.futureRequestIncludesEurgbp -eq $false -and $profile.futureRequestIncludesUsdjpy -eq $false) 'LMAX_R213_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'Future request includes non-AUDUSD instrument.'
Assert-True ($profile.requestCount -eq 1 -and $profile.securityId -eq '4007' -and $profile.securityIdSource -eq '8') 'LMAX_R213_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'AUDUSD request count or SecurityID/Source invalid.'
Assert-True ($profile.mdReqIdShort -eq $true -and $profile.mdReqIdAlphanumericOnly -eq $true -and $profile.mdReqIdUniquePerRequest -eq $true -and $profile.mdReqIdLengthLessThanOrEqual16 -eq $true) 'LMAX_R213_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'MDReqID repaired shape readiness missing.'
Assert-True ($profile.mdReqIdContainsPhaseLabel -eq $false -and $profile.mdReqIdContainsUnderscore -eq $false -and $profile.mdReqIdContainsPunctuation -eq $false -and $profile.rawMdReqIdSerialized -eq $false) 'LMAX_R213_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'MDReqID shape weakened or raw MDReqID serialization risk.'
Assert-True ($profile.subscriptionRequestType -eq '1' -and $profile.mdUpdateType -eq '0' -and $profile.marketDepth -eq 1 -and $profile.noMdEntryTypes -eq 2 -and $profile.bidAndOfferTogether -eq $true) 'LMAX_R213_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'AUDUSD MarketDataRequest contract weakened.'
Assert-True ($profile.symbolTextPresent -eq $false -and $profile.internalSymbolPresent -eq $false -and $profile.snapshotOnlyPresent -eq $false -and $profile.subscriptionRequestTypeZeroPresent -eq $false) 'LMAX_R213_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'Symbol/InternalSymbol/SnapshotOnly appeared.'

Assert-True ($state.finalStateEvidenceReadiness -eq $true -and $state.classifiedMarketDataResponseCannotEmitAllExplicitStateFieldsFalse -eq $true) 'LMAX_R213_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' 'Final-state evidence readiness missing.'
foreach ($field in @('marketDataRequestWriteAttempted','marketDataRequestWriteSucceeded','marketDataRequestResponseReadAttempted','marketDataRequestReachedBoundedResponseClassification','marketDataRequestSentLegacyFlag')) {
    Assert-True ($state.requiredFields -contains $field) 'LMAX_R213_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Final-state field missing: $field"
}
Assert-True ($entries.entriesReportingReadiness -eq $true) 'LMAX_R213_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' 'Entries reporting readiness missing.'
foreach ($field in @('marketDataEntriesObserved','marketDataSanitizedEntryCount','marketDataEntriesEvidenceCategory','marketDataEntriesReportingSource','marketDataEntriesNotAvailableReason')) {
    Assert-True ($entries.requiredFields -contains $field) 'LMAX_R213_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Entries field missing: $field"
}
Assert-True ($reject.enhancedRejectReportingReadiness -eq $true -and $reject.notAvailableDistinctFromPropagationFailure -eq $true) 'LMAX_R213_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' 'Enhanced reject reporting readiness missing.'
Assert-True ($logout.logoutReportingReadiness -eq $true -and $logout.rawLogoutTextSerialized -eq $false) 'LMAX_R213_FAIL_LOGOUT_REPORTING_READINESS_MISSING' 'Logout reporting readiness missing.'

Assert-True ($baseline.gbpusdBaselinePreserved -eq $true -and $baseline.gbpusdMarketDataResponseCategory -eq 'Succeeded' -and $baseline.gbpusdEntriesObserved -eq $true -and $baseline.gbpusdSanitizedEntryCount -eq 2) 'LMAX_R213_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'GBPUSD baseline weakened.'
Assert-True ($baseline.eurgbpBaselinePreserved -eq $true -and $baseline.eurgbpMarketDataResponseCategory -eq 'Succeeded' -and $baseline.eurgbpEntriesObserved -eq $true -and $baseline.eurgbpSanitizedEntryCount -eq 2) 'LMAX_R213_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'EURGBP baseline weakened.'
Assert-True ($usdjpy.usdjpyNotProven -eq $true -and $usdjpy.usdjpyClassifiedAsFailed -eq $false -and $usdjpy.usdJpyCaveatPreserved -eq $true -and $usdjpy.usdJpySecurityId -eq '4004' -and $usdjpy.usdJpySecurityIdSource -eq '8') 'LMAX_R213_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY misclassified or caveat weakened.'
Assert-True ($universe.approvedUniversePreserved -eq $true -and $universe.approvedInstruments -contains 'GBPUSD' -and $universe.approvedInstruments -contains 'EURGBP' -and $universe.approvedInstruments -contains 'AUDUSD' -and $universe.approvedInstruments -contains 'USDJPY') 'LMAX_R213_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'Approved universe weakened.'
Assert-True ($caveat.usdJpyCaveatPreserved -eq $true -and $caveat.usdJpyNotProven -eq $true -and $caveat.usdJpyClassifiedAsFailed -eq $false) 'LMAX_R213_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY caveat artifact invalid.'

Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true -and $preflight.concreteWeekdayMarketHoursConfirmationRequired -eq $true -and $preflight.exactlyOneBoundedAttemptRequired -eq $true -and $preflight.stopAfterAttempt -eq $true) 'LMAX_R213_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R215 approval/market-hours/attempt constraints missing.'
Assert-True ($expected.expectedEvidenceContractReady -eq $true -and $expected.selectedInstrument -eq 'AUDUSD' -and $expected.securityId -eq '4007' -and $expected.securityIdSource -eq '8') 'LMAX_R213_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'R215 expected evidence contract missing.'
Assert-True ($expected.rawTlsEndpointAllowed -eq $false -and $expected.rawLogoutTextAllowed -eq $false -and $expected.rawMarketDataPricesPayloadsAllowed -eq $false -and $expected.rawFixAllowed -eq $false -and $expected.rawMdReqIdAllowed -eq $false) 'LMAX_R213_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Expected evidence contract allows raw values.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R213_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false) 'LMAX_R213_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Raw TLS/endpoint leak risk.'
Assert-True ($sanitization.rawLogoutTextSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false -and $sanitization.rawFixMessagesSerialized -eq $false) 'LMAX_R213_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Raw logout/reject/FIX leak risk.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false -and $sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R213_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'Raw market-data/MDReqID leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false) 'LMAX_R213_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Secret/session/CompID leak risk.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R213_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R213_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false -and $forbidden.productionAccountUsed -eq $false) 'LMAX_R213_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay or production account introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R213_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'

Assert-True ($next.supportingClassification -eq 'LMAX_R213_PASS_AUDUSD_ONLY_RETRY_READINESS_AFTER_TLS_INCONCLUSIVE_NO_EXTERNAL' -and $next.r215RequiresFreshExactOperatorApproval -eq $true -and $next.r215RequiresConcreteWeekdayMarketHoursConfirmation -eq $true -and $next.r215RequiresExactlyOneBoundedAttempt -eq $true -and $next.r216MustRemainNoExternal -eq $true -and $next.directLiveRetryAllowedFromR213 -eq $false) 'LMAX_R213_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'Next recommendation missing or unsafe.'
Assert-True ($validation.buildStatus -eq 'PASS' -and $validation.focusedTestsStatus -eq 'PASS 130/130' -and $validation.unitTestsStatus -eq 'PASS 1843/1843' -and $validation.validator -eq 'LMAX_R213_VALIDATION_PASS' -and $validation.validatorStatus -eq 'PASS') 'LMAX_R213_FAIL_BUILD_OR_TESTS' 'Build/test/validator evidence missing.'

Write-Output 'LMAX_R213_VALIDATION_PASS'

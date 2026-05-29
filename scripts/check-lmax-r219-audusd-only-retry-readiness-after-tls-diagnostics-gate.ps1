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

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r219-audusd-only-retry-readiness-after-tls-diagnostics-summary.md")) 'LMAX_R219_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'R219 summary missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r219-r221-operator-approval-template.md")) 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R221 approval template missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r219-r221-activation-prompt-compact.md")) 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R221 compact prompt missing.'

$readiness = Read-Json "$artifactRoot/phase-lmax-r219-audusd-only-retry-readiness-after-tls-diagnostics.json" 'LMAX_R219_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$profile = Read-Json "$artifactRoot/phase-lmax-r219-audusd-profile-readiness.json" 'LMAX_R219_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$tls = Read-Json "$artifactRoot/phase-lmax-r219-tls-diagnostics-preservation.json" 'LMAX_R219_FAIL_TLS_DIAGNOSTICS_PRESERVATION_MISSING'
$logout = Read-Json "$artifactRoot/phase-lmax-r219-logout-reporting-readiness.json" 'LMAX_R219_FAIL_LOGOUT_REPORTING_READINESS_MISSING'
$entries = Read-Json "$artifactRoot/phase-lmax-r219-entries-reporting-readiness.json" 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING'
$state = Read-Json "$artifactRoot/phase-lmax-r219-final-state-evidence-contract-readiness.json" 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING'
$reject = Read-Json "$artifactRoot/phase-lmax-r219-enhanced-reject-reporting-readiness.json" 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING'
$baseline = Read-Json "$artifactRoot/phase-lmax-r219-gbpusd-eurgbp-success-baseline-preservation.json" 'LMAX_R219_FAIL_AUDUSD_PROFILE_READINESS_MISSING'
$usdJpy = Read-Json "$artifactRoot/phase-lmax-r219-usdjpy-not-proven-and-caveat-preservation.json" 'LMAX_R219_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$reservation = Read-Json "$artifactRoot/phase-lmax-r219-next-activation-phase-reservation-decision.json" 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID'
$preflight = Read-Json "$artifactRoot/phase-lmax-r219-r221-preflight-checklist.json" 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID'
$contract = Read-Json "$artifactRoot/phase-lmax-r219-r221-expected-evidence-contract.json" 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING'
$universe = Read-Json "$artifactRoot/phase-lmax-r219-approved-universe-preservation.json" 'LMAX_R219_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$usdJpyCaveat = Read-Json "$artifactRoot/phase-lmax-r219-usdjpy-caveat-preservation.json" 'LMAX_R219_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r219-sanitization-audit.json" 'LMAX_R219_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r219-forbidden-actions-audit.json" 'LMAX_R219_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r219-api-worker-fake-gateway-audit.json" 'LMAX_R219_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r219-next-phase-recommendation.json" 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID'
$gate = Read-Json "$artifactRoot/phase-lmax-r219-gate-validation.json" 'LMAX_R219_FAIL_BUILD_OR_TESTS'

Assert-True ($readiness.classification -eq 'LMAX_R219_PASS_AUDUSD_ONLY_RETRY_READINESS_AFTER_TLS_DIAGNOSTICS_NO_EXTERNAL') 'LMAX_R219_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'R219 classification mismatch.'
Assert-True ($readiness.noExternalReadinessOnly -eq $true -and $readiness.externalActivationAttempted -eq $false -and $readiness.socketTlsFixMarketDataRuntimeActionAttempted -eq $false) 'LMAX_R219_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R219 must be no-external.'

Assert-True ($readiness.nextActivationPhase -eq 'LMAX-R221' -and $reservation.nextActivationPhase -eq 'LMAX-R221') 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'Next activation phase must be R221.'
Assert-True ($readiness.nextActivationPhaseOddNumbered -eq $true -and $reservation.nextActivationPhaseOddNumbered -eq $true) 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R221 must be odd-numbered.'
Assert-True ($readiness.nextActivationPhaseExplicitlyReserved -eq $true -and $reservation.nextActivationPhaseExplicitlyReserved -eq $true -and $reservation.reservationImplementedInCode -eq $true) 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R221 reservation missing.'
Assert-True ($reservation.audusdOnlyGuardImplementedForFuturePhase -eq $true) 'LMAX_R219_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'R221 AUDUSD-only guard missing.'

Assert-True ($profile.audusdOnlyProfileReady -eq $true -and $profile.futureRequestAudusdOnly -eq $true -and $profile.futureRequestCount -eq 1) 'LMAX_R219_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'AUDUSD-only profile readiness missing.'
Assert-True ($profile.futureRequestIncludesGbpusd -eq $false -and $profile.futureRequestIncludesEurgbp -eq $false -and $profile.futureRequestIncludesUsdjpy -eq $false) 'LMAX_R219_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'Future request includes non-AUDUSD instrument.'
Assert-True ($profile.securityId -eq '4007' -and $profile.securityIdSource -eq '8') 'LMAX_R219_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'AUDUSD SecurityID/Source missing.'
Assert-True ($profile.mdReqIdShort -eq $true -and $profile.mdReqIdAlphanumericOnly -eq $true -and $profile.mdReqIdUniquePerRequest -eq $true -and $profile.mdReqIdLengthLessThanOrEqual16 -eq $true) 'LMAX_R219_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'Repaired MDReqID shape missing.'
Assert-True ($profile.mdReqIdContainsPhaseLabel -eq $false -and $profile.mdReqIdContainsUnderscore -eq $false -and $profile.mdReqIdContainsPunctuation -eq $false -and $profile.rawMdReqIdSerialized -eq $false) 'LMAX_R219_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'MDReqID serialization or shape risk.'
Assert-True ($profile.subscriptionRequestType -eq '1' -and $profile.mdUpdateType -eq '0' -and $profile.marketDepth -eq 1 -and $profile.noMdEntryTypes -eq 2 -and $profile.bidAndOfferTogether -eq $true) 'LMAX_R219_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'MarketDataRequest contract weakened.'
Assert-True ($profile.symbolTextPresent -eq $false -and $profile.internalSymbolPresent -eq $false -and $profile.snapshotOnlyPresent -eq $false -and $profile.subscriptionRequestTypeZeroPresent -eq $false) 'LMAX_R219_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'Symbol/InternalSymbol/SnapshotOnly appeared.'

Assert-True ($tls.r217ConclusionPreserved -eq $true -and $tls.localProviderConfigReservationAdapterDivergenceFound -eq $false) 'LMAX_R219_FAIL_TLS_DIAGNOSTICS_PRESERVATION_MISSING' 'R217 TLS diagnostics conclusion weakened.'
Assert-True ($tls.audusdMarketDataTested -eq $false -and $tls.audusdClassifiedAsFailed -eq $false) 'LMAX_R219_FAIL_TLS_DIAGNOSTICS_PRESERVATION_MISSING' 'AUDUSD misclassified.'
Assert-True ($tls.rawTlsMaterialSerialized -eq $false -and $tls.rawEndpointValuesSerialized -eq $false) 'LMAX_R219_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Raw TLS/endpoint leak risk.'

Assert-True ($state.finalStateEvidenceReadinessPresent -eq $true) 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' 'Final-state evidence readiness missing.'
foreach ($field in @('marketDataRequestWriteAttempted','marketDataRequestWriteSucceeded','marketDataRequestResponseReadAttempted','marketDataRequestReachedBoundedResponseClassification','marketDataRequestSentLegacyFlag')) {
    Assert-True ($state.requiredFields -contains $field) 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Missing state evidence field $field"
}

Assert-True ($entries.entriesReportingReadinessPresent -eq $true) 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' 'Entries reporting readiness missing.'
foreach ($field in @('marketDataEntriesObserved','marketDataSanitizedEntryCount','marketDataEntriesEvidenceCategory','marketDataEntriesReportingSource','marketDataEntriesNotAvailableReason')) {
    Assert-True ($entries.requiredFields -contains $field) 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Missing entries evidence field $field"
    Assert-True ($contract.requiredEntriesFields -contains $field) 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Expected contract missing entries field $field"
}

Assert-True ($reject.enhancedRejectReportingReadinessPresent -eq $true) 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' 'Enhanced reject reporting readiness missing.'
foreach ($field in @('sessionRejectRefTagIdSanitizedCategory','sessionRejectReasonSanitizedCategory','sessionRejectRefMsgTypeSanitizedCategory','rejectReasonExtractionSource')) {
    Assert-True ($reject.requiredFields -contains $field) 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Missing reject field $field"
    Assert-True ($contract.requiredRejectFields -contains $field) 'LMAX_R219_FAIL_ENTRIES_OR_REJECT_REPORTING_READINESS_MISSING' "Expected contract missing reject field $field"
}

Assert-True ($logout.logoutReportingReadinessPresent -eq $true) 'LMAX_R219_FAIL_LOGOUT_REPORTING_READINESS_MISSING' 'Logout reporting readiness missing.'
foreach ($field in @('logoutObserved','logoutSourceCategory','logoutReasonSanitizedCategory','logoutTextPresentSanitized','logoutAfterInstrument','logoutAfterSecurityIdSanitized','logoutTimingCategory','logoutReasonExtractionSource')) {
    Assert-True ($logout.requiredFields -contains $field) 'LMAX_R219_FAIL_LOGOUT_REPORTING_READINESS_MISSING' "Missing logout field $field"
    Assert-True ($contract.requiredLogoutFields -contains $field) 'LMAX_R219_FAIL_LOGOUT_REPORTING_READINESS_MISSING' "Expected contract missing logout field $field"
}

foreach ($field in @('tcpConnectionAttempted','realSocketOpened','tlsHandshakeAttempted','tlsSucceeded','tlsBoundaryStatus','tlsResultCategory','fixLogonAttempted','fixBoundaryStatus','fixAcknowledgementCategory')) {
    Assert-True ($contract.requiredTlsFixBoundaryFields -contains $field) 'LMAX_R219_FAIL_TLS_DIAGNOSTICS_PRESERVATION_MISSING' "TLS/FIX boundary contract missing $field"
}

Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true -and $preflight.concreteWeekdayMarketHoursConfirmationRequired -eq $true -and $preflight.singleBoundedAttemptOnly -eq $true) 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R221 approval/market-hours/single-attempt requirement missing.'
Assert-True ($preflight.stopAfterAttemptRequired -eq $true -and $preflight.r222NoExternalReviewOnly -eq $true) 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'R221 stop/R222 no-external requirement missing.'

Assert-True ($baseline.gbpusdR203SuccessPreserved -eq $true -and $baseline.eurgbpR207SuccessPreserved -eq $true -and $baseline.baselineWeakened -eq $false) 'LMAX_R219_FAIL_AUDUSD_PROFILE_READINESS_MISSING' 'GBPUSD/EURGBP baseline weakened.'
Assert-True ($usdJpy.usdJpyNotProven -eq $true -and $usdJpy.usdJpyClassifiedAsFailed -eq $false -and $usdJpy.usdJpyCaveatPreserved -eq $true) 'LMAX_R219_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY misclassified or caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004' -and $usdJpy.usdJpySecurityIdSource -eq '8') 'LMAX_R219_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY caveat values missing.'
Assert-True ($usdJpyCaveat.usdJpyCaveatPreserved -eq $true -and $usdJpyCaveat.usdJpySecurityId -eq '4004' -and $usdJpyCaveat.usdJpySecurityIdSource -eq '8') 'LMAX_R219_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY caveat preservation missing.'
Assert-True ($universe.approvedUniversePreserved -eq $true -and $universe.approvedInstruments -contains 'GBPUSD' -and $universe.approvedInstruments -contains 'EURGBP' -and $universe.approvedInstruments -contains 'AUDUSD' -and $universe.approvedInstruments -contains 'USDJPY') 'LMAX_R219_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'Approved universe weakened.'

Assert-True ($contract.rawTlsMaterialSerializationAllowed -eq $false -and $contract.rawEndpointValueSerializationAllowed -eq $false) 'LMAX_R219_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Expected contract allows raw TLS/endpoint serialization.'
Assert-True ($contract.rawFixSerializationAllowed -eq $false -and $contract.rawLogoutTextSerializationAllowed -eq $false -and $contract.rawRejectTextSerializationAllowed -eq $false) 'LMAX_R219_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Expected contract allows raw FIX/logout/reject serialization.'
Assert-True ($contract.rawMdReqIdSerializationAllowed -eq $false -and $contract.rawMarketDataPriceOrPayloadSerializationAllowed -eq $false) 'LMAX_R219_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'Expected contract allows raw MDReqID/market-data serialization.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R219_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false) 'LMAX_R219_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Raw TLS/endpoint leak risk.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawLogoutTextSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false) 'LMAX_R219_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Raw FIX/logout/reject leak risk.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false -and $sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R219_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'Raw MDReqID/market-data leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false) 'LMAX_R219_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Secret/session/CompID leak risk.'

Assert-True ($forbidden.externalActivationAttempted -eq $false -and $forbidden.socketOpened -eq $false -and $forbidden.tlsAttempted -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R219_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R219 external boundary action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R219_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R219 MarketData runtime action detected.'
Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R219_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R219_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false -and $forbidden.productionAccountUsed -eq $false) 'LMAX_R219_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay or production account introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R219_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker gateway regression.'

Assert-True ($gate.buildStatus -eq 'PASS' -and $gate.focusedTestsStatus -eq 'PASS 132/132' -and $gate.validator -eq 'LMAX_R219_VALIDATION_PASS') 'LMAX_R219_FAIL_BUILD_OR_TESTS' 'Build/test/validator evidence missing.'
Assert-True ($next.r221RequiresFreshExactApproval -eq $true -and $next.r221RequiresConcreteWeekdayMarketHoursConfirmation -eq $true -and $next.r221RequiresExactlyOneBoundedAttempt -eq $true) 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'Next phase recommendation missing R221 constraints.'
Assert-True ($next.r222MustRemainNoExternal -eq $true -and $next.liveRetryBeforeFreshR221ApprovalAllowed -eq $false) 'LMAX_R219_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'Next phase recommendation allows premature live retry.'

$artifactText = Get-ChildItem -LiteralPath (Join-Path $Root $artifactRoot) -Filter 'phase-lmax-r219-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @([string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId', 'bid=', 'ask=', 'fix-marketdata.', '.lmax.com')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) 'LMAX_R219_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' "Forbidden serialized token detected in R219 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R219_VALIDATION_PASS'

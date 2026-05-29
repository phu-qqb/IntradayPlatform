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

$summary = Read-Json "$artifactRoot/phase-lmax-r202-entries-reporting-retry-readiness.json" 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING'
$profile = Read-Json "$artifactRoot/phase-lmax-r202-selected-profile-evidence.json" 'LMAX_R202_FAIL_PROFILE_SELECTION_MISSING'
$mdReqId = Read-Json "$artifactRoot/phase-lmax-r202-mdreqid-repaired-shape-readiness.json" 'LMAX_R202_FAIL_MDREQID_REPAIR_READINESS_MISSING'
$entries = Read-Json "$artifactRoot/phase-lmax-r202-entries-reporting-readiness.json" 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING'
$reject = Read-Json "$artifactRoot/phase-lmax-r202-enhanced-reject-reporting-readiness.json" 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING'
$state = Read-Json "$artifactRoot/phase-lmax-r202-final-state-evidence-contract-readiness.json" 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING'
$nonSelected = Read-Json "$artifactRoot/phase-lmax-r202-non-selected-profiles-evidence.json" 'LMAX_R202_FAIL_PROFILE_SELECTION_MISSING'
$reservation = Read-Json "$artifactRoot/phase-lmax-r202-next-activation-phase-reservation-decision.json" 'LMAX_R202_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID'
$preflight = Read-Json "$artifactRoot/phase-lmax-r202-r203-preflight-checklist.json" 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING'
$expected = Read-Json "$artifactRoot/phase-lmax-r202-r203-expected-evidence-contract.json" 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING'
$universe = Read-Json "$artifactRoot/phase-lmax-r202-approved-universe-preservation.json" 'LMAX_R202_FAIL_USDJPY_CAVEAT_WEAKENED'
$usdJpy = Read-Json "$artifactRoot/phase-lmax-r202-usdjpy-caveat-preservation.json" 'LMAX_R202_FAIL_USDJPY_CAVEAT_WEAKENED'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r202-sanitization-audit.json" 'LMAX_R202_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r202-forbidden-actions-audit.json" 'LMAX_R202_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r202-api-worker-fake-gateway-audit.json" 'LMAX_R202_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r202-next-phase-recommendation.json" 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING'

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r202-entries-reporting-retry-readiness-summary.md")) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'Summary missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r202-r203-operator-approval-template.md")) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'Fresh approval template missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r202-r203-activation-prompt-compact.md")) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'Activation prompt missing.'

Assert-True ($summary.noExternal -eq $true) 'LMAX_R202_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R202 must remain no-external.'
Assert-True ($summary.newExternalActivationAttempted -eq $false) 'LMAX_R202_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'New external activation detected.'
Assert-True ($summary.socketTlsFixMarketDataRuntimeActionDetected -eq $false) 'LMAX_R202_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'Socket/TLS/FIX/MarketData runtime action detected.'
Assert-True ($forbidden.socketOpened -eq $false -and $forbidden.tlsOpened -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R202_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'Socket/TLS/FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R202_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'Live MarketData action detected.'

Assert-True ($reservation.reservedNextRealActivationPhase -eq 'LMAX-R203') 'LMAX_R202_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'Next phase must be LMAX-R203.'
Assert-True ($reservation.nextActivationPhaseOddNumbered -eq $true -and $reservation.nextActivationPhaseExplicitlyReserved -eq $true) 'LMAX_R202_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'Next phase reservation invalid.'
$reservationSource = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
Assert-True ($reservationSource.Contains('"LMAX-R203"')) 'LMAX_R202_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID' 'LMAX-R203 is not reserved in source.'

Assert-True ($profile.selectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'LMAX_R202_FAIL_PROFILE_SELECTION_MISSING' 'Selected future profile mismatch.'
Assert-True ($nonSelected.selectedProfileCount -eq 1 -and $nonSelected.priorDiagnosticOrLegacyProfileSelected -eq $false) 'LMAX_R202_FAIL_PROFILE_SELECTION_MISSING' 'More than one profile or legacy profile selected.'
Assert-True ($profile.gbpusdOnly -eq $true -and $profile.singleRequest -eq $true) 'LMAX_R202_FAIL_PROFILE_SELECTION_MISSING' 'Future retry must be GBPUSD-only single request.'
Assert-True ($profile.securityId -eq '4002' -and $profile.securityIdSource -eq '8') 'LMAX_R202_FAIL_PROFILE_SELECTION_MISSING' 'SecurityID contract missing.'
Assert-True ($profile.subscriptionRequestType -eq '1' -and $profile.mdUpdateType -eq '0') 'LMAX_R202_FAIL_PROFILE_SELECTION_MISSING' 'SubscriptionRequestType/MDUpdateType contract missing.'
Assert-True ($profile.marketDepth -eq 1 -and $profile.noMdEntryTypes -eq 2 -and $profile.bidAndOfferTogether -eq $true) 'LMAX_R202_FAIL_PROFILE_SELECTION_MISSING' 'MarketDepth/NoMDEntryTypes/bid+offer contract missing.'
Assert-True ($profile.symbolTextPresent -eq $false -and $profile.internalSymbolPresent -eq $false -and $profile.snapshotOnlyPresent -eq $false -and $profile.subscriptionRequestTypeZeroPresent -eq $false) 'LMAX_R202_FAIL_PROFILE_SELECTION_MISSING' 'Symbol/InternalSymbol/SnapshotOnly contract weakened.'

Assert-True ($mdReqId.mdReqIdRepairReady -eq $true) 'LMAX_R202_FAIL_MDREQID_REPAIR_READINESS_MISSING' 'MDReqID repair readiness missing.'
Assert-True ($mdReqId.futureMdReqIdShape.lengthLessThanOrEqualTo16 -eq $true) 'LMAX_R202_FAIL_MDREQID_REPAIR_READINESS_MISSING' 'MDReqID length contract weakened.'
Assert-True ($mdReqId.futureMdReqIdShape.alphanumericOnly -eq $true -and $mdReqId.futureMdReqIdShape.containsUnderscore -eq $false -and $mdReqId.futureMdReqIdShape.containsPunctuation -eq $false -and $mdReqId.futureMdReqIdShape.containsPhaseLabel -eq $false) 'LMAX_R202_FAIL_MDREQID_REPAIR_READINESS_MISSING' 'MDReqID shape contract weakened.'
Assert-True ($mdReqId.rawMdReqIdSerializationAllowed -eq $false -and $mdReqId.rawMdReqIdSerializationRiskPresent -eq $false) 'LMAX_R202_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Raw MDReqID serialization risk.'

$entriesFields = @(
    'marketDataEntriesObserved',
    'marketDataSanitizedEntryCount',
    'marketDataEntriesEvidenceCategory',
    'marketDataEntriesReportingSource',
    'marketDataEntriesNotAvailableReason'
)
Assert-True ($entries.entriesReportingReadiness -eq $true) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'Entries reporting readiness missing.'
foreach ($field in $entriesFields) {
    Assert-True ($entries.futureEvidenceRequiredFields -contains $field) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' "Entries readiness omits $field."
    Assert-True ($expected.entriesFieldsRequired -contains $field) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' "Expected evidence contract omits $field."
}
Assert-True ($entries.rawMarketDataPricesAllowed -eq $false -and $entries.rawBidAskValuesAllowed -eq $false -and $entries.rawMarketDataPayloadAllowed -eq $false -and $entries.rawFixAllowed -eq $false) 'LMAX_R202_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Entries contract allows raw market-data or FIX.'
Assert-True ($expected.rawPricesAllowed -eq $false -and $expected.rawBidAskValuesAllowed -eq $false -and $expected.rawMarketDataPayloadAllowed -eq $false -and $expected.rawFixAllowed -eq $false) 'LMAX_R202_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Expected evidence allows raw market-data or FIX.'
Assert-True ($expected.rawRejectTextAllowed -eq $false -and $expected.rawMdReqIdAllowed -eq $false) 'LMAX_R202_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Expected evidence allows raw reject or MDReqID.'

Assert-True ($state.finalStateEvidenceContractReady -eq $true -and $state.classifiedMarketDataResponseCannotEmitAllExplicitStateFieldsFalse -eq $true) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'Final-state evidence readiness missing.'
foreach ($field in @('marketDataRequestWriteAttempted', 'marketDataRequestWriteSucceeded', 'marketDataRequestResponseReadAttempted', 'marketDataRequestReachedBoundedResponseClassification', 'marketDataRequestSentLegacyFlag')) {
    Assert-True ($state.requiredFields -contains $field) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' "State field missing: $field."
}

Assert-True ($reject.enhancedSessionRejectDetailReportingReady -eq $true -and $reject.broadSanitizedReasonReportingReady -eq $true) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'Enhanced or broad reject reporting readiness missing.'
foreach ($field in @('sessionRejectRefTagIdSanitizedCategory', 'sessionRejectReasonSanitizedCategory', 'sessionRejectRefMsgTypeSanitizedCategory', 'rejectReasonExtractionSource')) {
    Assert-True ($reject.requiredFields -contains $field) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' "Reject detail field missing: $field."
}
foreach ($field in @('sessionRejectSanitizedReasonCategory', 'sanitizedSessionRejectReasonCategory')) {
    Assert-True ($reject.broadReasonFields -contains $field) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' "Broad reject field missing: $field."
}

Assert-True ($universe.approvedUniversePreserved -eq $true -and $universe.approvedUniverse -contains 'GBPUSD' -and $universe.approvedUniverse -contains 'EURGBP' -and $universe.approvedUniverse -contains 'AUDUSD' -and $universe.approvedUniverse -contains 'USDJPY') 'LMAX_R202_FAIL_USDJPY_CAVEAT_WEAKENED' 'Approved universe weakened.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true -and $usdJpy.usdJpySecurityId -eq '4004' -and $usdJpy.usdJpySecurityIdSource -eq '8') 'LMAX_R202_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'

Assert-True ($preflight.checks.freshExactOperatorApprovalRequired -eq $true -and $preflight.checks.concreteWeekdayMarketHoursConfirmationRequired -eq $true -and $preflight.checks.exactlyOneBoundedAttempt -eq $true) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'Approval, market-hours, or single-attempt constraint missing.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R202_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R202_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw market-data/FIX leak risk.'
Assert-True ($sanitization.rawRejectTextSerialized -eq $false -and $sanitization.rawMdReqIdSerialized -eq $false) 'LMAX_R202_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Raw reject/MDReqID leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsValuesSerialized -eq $false) 'LMAX_R202_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Secret/session/endpoint/TLS leak risk.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R202_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R202_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false) 'LMAX_R202_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.apiWorkerLiveGatewayRegression -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R202_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r199-mdreqid-repaired-activation.json")) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'R199 evidence missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r200-entries-evidence-review.json")) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'R200 evidence missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r201-entries-reporting-enrichment.json")) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'R201 evidence missing.'

Assert-True ($summary.buildEvidence -like '*PASS*' -and $summary.focusedTestsEvidence -like '*PASS*' -and $summary.unitTestsEvidence -like '*PASS*' -and $summary.integrationTestsEvidence -like '*PASS*') 'LMAX_R202_FAIL_BUILD_OR_TESTS' 'Build/test evidence missing.'
Assert-True ($next.r203RequiresFreshExactOperatorApproval -eq $true -and $next.r203RequiresSeparateConcreteWeekdayMarketHoursConfirmation -eq $true -and $next.r203RequiresExactlyOneBoundedAttemptOnly -eq $true) 'LMAX_R202_FAIL_ENTRIES_REPORTING_READINESS_MISSING' 'Next phase recommendation incomplete.'

Write-Output 'LMAX_R202_VALIDATION_PASS'

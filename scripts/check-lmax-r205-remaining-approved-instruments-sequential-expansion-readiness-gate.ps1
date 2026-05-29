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

function Assert-InstrumentReadiness($Artifact, [string]$Instrument, [string]$SecurityId) {
    Assert-True ($Artifact.phase -eq 'LMAX-R205') 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING' "$Instrument phase mismatch."
    Assert-True ($Artifact.instrument -eq $Instrument -and $Artifact.readiness -eq $true) 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING' "$Instrument readiness missing."
    Assert-True ($Artifact.securityId -eq $SecurityId -and $Artifact.securityIdSource -eq '8') 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING' "$Instrument SecurityID/Source mismatch."
    Assert-True ($Artifact.requestCount -eq 1) 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING' "$Instrument must use exactly one request."
    Assert-True ($Artifact.mdReqIdShape -eq 'short-alphanumeric-unique-session-safe-length-lte-16') 'LMAX_R205_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED' "$Instrument MDReqID shape weakened."
    Assert-True ($Artifact.rawMdReqIdSerialized -eq $false) 'LMAX_R205_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED' "$Instrument raw MDReqID serialization risk."
    Assert-True ($Artifact.subscriptionRequestType -eq '1' -and $Artifact.mdUpdateType -eq '0') 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING' "$Instrument SubscriptionRequestType/MDUpdateType weakened."
    Assert-True ($Artifact.marketDepth -eq 1 -and $Artifact.noMdEntryTypes -eq 2 -and $Artifact.bidAndOfferTogether -eq $true) 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING' "$Instrument depth/entry type contract weakened."
    Assert-True ($Artifact.symbolTextPresent -eq $false -and $Artifact.internalSymbolPresent -eq $false) 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING' "$Instrument Symbol/InternalSymbol appeared."
    Assert-True ($Artifact.snapshotOnlyPresent -eq $false -and $Artifact.subscriptionRequestTypeZeroPresent -eq $false) 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING' "$Instrument SnapshotOnly/SubscriptionRequestType=0 appeared."
    Assert-True ($Artifact.rawMarketDataPricesSerialized -eq $false) 'LMAX_R205_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' "$Instrument raw market-data price risk."
}

$artifactRoot = 'artifacts/readiness/lmax-runtime-enablement'

$summaryPath = Join-Path $Root "$artifactRoot/phase-lmax-r205-remaining-approved-instruments-sequential-expansion-readiness-summary.md"
Assert-True (Test-Path -LiteralPath $summaryPath) 'LMAX_R205_FAIL_R203_R204_BASELINE_MISSING' 'R205 summary missing.'

$baseline = Read-Json "$artifactRoot/phase-lmax-r205-r203-r204-baseline-preservation.json" 'LMAX_R205_FAIL_R203_R204_BASELINE_MISSING'
$strategy = Read-Json "$artifactRoot/phase-lmax-r205-accelerated-expansion-strategy.json" 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING'
$eurGbp = Read-Json "$artifactRoot/phase-lmax-r205-eurgbp-profile-readiness.json" 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING'
$audUsd = Read-Json "$artifactRoot/phase-lmax-r205-audusd-profile-readiness.json" 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING'
$usdJpy = Read-Json "$artifactRoot/phase-lmax-r205-usdjpy-profile-readiness-and-caveat.json" 'LMAX_R205_FAIL_USDJPY_CAVEAT_WEAKENED'
$contract = Read-Json "$artifactRoot/phase-lmax-r205-per-instrument-evidence-contract.json" 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING'
$mdReqId = Read-Json "$artifactRoot/phase-lmax-r205-mdreqid-repaired-shape-preservation.json" 'LMAX_R205_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED'
$entries = Read-Json "$artifactRoot/phase-lmax-r205-entries-reporting-readiness.json" 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING'
$reject = Read-Json "$artifactRoot/phase-lmax-r205-enhanced-reject-reporting-readiness.json" 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING'
$state = Read-Json "$artifactRoot/phase-lmax-r205-final-state-evidence-readiness.json" 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING'
$reservation = Read-Json "$artifactRoot/phase-lmax-r205-r207-reservation-and-single-attempt-decision.json" 'LMAX_R205_FAIL_R207_RESERVATION_OR_SINGLE_ATTEMPT_CONSTRAINT_MISSING'
$universe = Read-Json "$artifactRoot/phase-lmax-r205-approved-universe-preservation.json" 'LMAX_R205_FAIL_USDJPY_CAVEAT_WEAKENED'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r205-sanitization-audit.json" 'LMAX_R205_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r205-forbidden-actions-audit.json" 'LMAX_R205_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r205-api-worker-fake-gateway-audit.json" 'LMAX_R205_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r205-next-phase-recommendation.json" 'LMAX_R205_FAIL_R207_RESERVATION_OR_SINGLE_ATTEMPT_CONSTRAINT_MISSING'
$validation = Read-Json "$artifactRoot/phase-lmax-r205-gate-validation.json" 'LMAX_R205_FAIL_BUILD_OR_VALIDATOR'

foreach ($required in @(
    "$artifactRoot/phase-lmax-r205-r207-operator-approval-template.md",
    "$artifactRoot/phase-lmax-r205-r207-activation-prompt-compact.md",
    "$artifactRoot/phase-lmax-r205-r207-preflight-checklist.json")) {
    Assert-True (Test-Path -LiteralPath (Join-Path $Root $required)) 'LMAX_R205_FAIL_R207_RESERVATION_OR_SINGLE_ATTEMPT_CONSTRAINT_MISSING' "Missing $required"
}

Assert-True ($forbidden.newExternalActivationAttempted -eq $false) 'LMAX_R205_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R205 attempted external activation.'
Assert-True ($forbidden.socketOpened -eq $false -and $forbidden.tlsOpened -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R205_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R205 opened socket/TLS/FIX.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R205_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R205 performed live market-data action.'

Assert-True ($baseline.r203BaselinePreserved -eq $true -and $baseline.r204BaselineArchived -eq $true) 'LMAX_R205_FAIL_R203_R204_BASELINE_MISSING' 'R203/R204 baseline missing.'
Assert-True ($baseline.r203Classification -eq 'LMAX_R203_PASS_ENTRIES_REPORTING_RUNTIME_ACTIVATION_SANITIZED') 'LMAX_R205_FAIL_R203_R204_BASELINE_MISSING' 'R203 success classification missing.'
Assert-True ($baseline.r204Classification -eq 'LMAX_R204_PASS_R203_SUCCESS_EVIDENCE_ARCHIVED_NO_EXTERNAL') 'LMAX_R205_FAIL_R203_R204_BASELINE_MISSING' 'R204 archive classification missing.'
Assert-True ($baseline.r203AttemptCount -eq 1 -and $baseline.r203MarketDataResponseCategory -eq 'Succeeded') 'LMAX_R205_FAIL_R203_R204_BASELINE_MISSING' 'R203 success baseline invalid.'

Assert-True ($strategy.targetPhase -eq 'LMAX-R207') 'LMAX_R205_FAIL_R207_RESERVATION_OR_SINGLE_ATTEMPT_CONSTRAINT_MISSING' 'Target phase must be R207.'
Assert-True ($strategy.singleExternalActivationAttemptOnly -eq $true -and $strategy.stopAfterSingleActivationAttempt -eq $true) 'LMAX_R205_FAIL_R207_RESERVATION_OR_SINGLE_ATTEMPT_CONSTRAINT_MISSING' 'R207 single-attempt stop constraint missing.'
Assert-True ($strategy.oneRequestPerInstrument -eq $true -and $strategy.boundedSequentialDiagnosticAllowedInsideSingleAttempt -eq $true) 'LMAX_R205_FAIL_REMAINING_INSTRUMENT_READINESS_MISSING' 'Sequential remaining-instrument diagnostic readiness missing.'
Assert-True ($strategy.pollingAllowed -eq $false -and $strategy.schedulerAllowed -eq $false -and $strategy.serviceAllowed -eq $false -and $strategy.replayAllowed -eq $false -and $strategy.shadowReplayAllowed -eq $false) 'LMAX_R205_FAIL_FORBIDDEN_ACTION_RISK' 'R207 strategy allows forbidden runtime behavior.'

Assert-InstrumentReadiness $eurGbp 'EURGBP' '4003'
Assert-InstrumentReadiness $audUsd 'AUDUSD' '4007'
Assert-InstrumentReadiness $usdJpy 'USDJPY' '4004'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true -and $usdJpy.caveat -eq 'validated_readiness_archive_with_caveat') 'LMAX_R205_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'

Assert-True ($contract.perInstrumentEvidenceContractReady -eq $true) 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING' 'Per-instrument evidence contract not ready.'
foreach ($instrument in @('EURGBP', 'AUDUSD', 'USDJPY')) {
    Assert-True ($contract.instruments -contains $instrument) 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING' "Per-instrument contract missing $instrument."
}
foreach ($field in @(
    'selectedInstrument',
    'securityId',
    'securityIdSource',
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataResponseCategory',
    'marketDataEntriesObserved',
    'marketDataSanitizedEntryCount',
    'marketDataEntriesEvidenceCategory',
    'marketDataEntriesReportingSource',
    'rejectReasonExtractionSource',
    'sessionRejectSanitizedReasonCategory',
    'sanitizedSessionRejectReasonCategory',
    'sessionRejectRefTagIdSanitizedCategory',
    'sessionRejectReasonSanitizedCategory',
    'sessionRejectRefMsgTypeSanitizedCategory',
    'shutdownRevert')) {
    Assert-True ($contract.requiredPerInstrumentFields -contains $field) 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING' "Per-instrument field missing: $field"
}
Assert-True ($contract.rawPricesAllowed -eq $false -and $contract.rawMarketDataPayloadAllowed -eq $false -and $contract.rawFixAllowed -eq $false -and $contract.rawMdReqIdAllowed -eq $false -and $contract.rawRejectTextAllowed -eq $false) 'LMAX_R205_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Per-instrument contract allows raw sensitive values.'

Assert-True ($mdReqId.mdReqIdRepairContractPreservedForR207 -eq $true -and $mdReqId.short -eq $true -and $mdReqId.alphanumericOnly -eq $true -and $mdReqId.uniquePerRequest -eq $true -and $mdReqId.sessionSafe -eq $true -and $mdReqId.lengthLessThanOrEqual16 -eq $true) 'LMAX_R205_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED' 'MDReqID repaired shape readiness missing.'
Assert-True ($mdReqId.containsPhaseLabel -eq $false -and $mdReqId.containsUnderscore -eq $false -and $mdReqId.containsPunctuation -eq $false -and $mdReqId.rawMdReqIdSerializationRiskPresent -eq $false) 'LMAX_R205_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED' 'MDReqID shape or serialization weakened.'

Assert-True ($entries.entriesReportingReadiness -eq $true) 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING' 'Entries reporting readiness missing.'
foreach ($field in @('marketDataEntriesObserved', 'marketDataSanitizedEntryCount', 'marketDataEntriesEvidenceCategory', 'marketDataEntriesReportingSource', 'marketDataEntriesNotAvailableReason')) {
    Assert-True ($entries.requiredFields -contains $field) 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING' "Entries reporting field missing: $field"
}
Assert-True ($entries.rawPricesAllowed -eq $false -and $entries.rawBidAskValuesAllowed -eq $false -and $entries.rawMarketDataPayloadAllowed -eq $false) 'LMAX_R205_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Entries reporting allows raw market-data values.'
Assert-True ($reject.enhancedRejectReportingReadiness -eq $true) 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING' 'Enhanced reject reporting readiness missing.'
foreach ($field in @('rejectReasonExtractionSource', 'sessionRejectSanitizedReasonCategory', 'sanitizedSessionRejectReasonCategory', 'sessionRejectRefTagIdSanitizedCategory', 'sessionRejectReasonSanitizedCategory', 'sessionRejectRefMsgTypeSanitizedCategory')) {
    Assert-True ($reject.requiredFields -contains $field) 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING' "Enhanced reject field missing: $field"
}
Assert-True ($reject.notAvailableDistinctFromPropagationFailure -eq $true -and $reject.rawRejectTextAllowed -eq $false -and $reject.rawFixAllowed -eq $false) 'LMAX_R205_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Enhanced reject reporting contract weakened.'
Assert-True ($state.finalStateEvidenceReadiness -eq $true -and $state.classifiedMarketDataResponseCannotEmitAllExplicitStateFieldsFalse -eq $true) 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING' 'Final-state evidence readiness missing.'
foreach ($field in @('marketDataRequestWriteAttempted', 'marketDataRequestWriteSucceeded', 'marketDataRequestResponseReadAttempted', 'marketDataRequestReachedBoundedResponseClassification', 'marketDataRequestSentLegacyFlag')) {
    Assert-True ($state.requiredFields -contains $field) 'LMAX_R205_FAIL_PER_INSTRUMENT_EVIDENCE_CONTRACT_MISSING' "Final-state field missing: $field"
}

Assert-True ($reservation.targetPhase -eq 'LMAX-R207' -and $reservation.r207OddNumbered -eq $true -and $reservation.r207ExplicitlyReserved -eq $true) 'LMAX_R205_FAIL_R207_RESERVATION_OR_SINGLE_ATTEMPT_CONSTRAINT_MISSING' 'R207 reservation invalid.'
Assert-True ($reservation.singleExternalActivationAttemptOnly -eq $true -and $reservation.stopAfterSingleActivationAttempt -eq $true -and $reservation.pollingAllowed -eq $false -and $reservation.indefiniteLoopAllowed -eq $false) 'LMAX_R205_FAIL_R207_RESERVATION_OR_SINGLE_ATTEMPT_CONSTRAINT_MISSING' 'R207 single-attempt constraint invalid.'

$reservationSource = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
Assert-True ($reservationSource.Contains('"LMAX-R207"')) 'LMAX_R205_FAIL_R207_RESERVATION_OR_SINGLE_ATTEMPT_CONSTRAINT_MISSING' 'R207 is not in executable reservation source.'

Assert-True ($universe.approvedUniversePreserved -eq $true -and $universe.approvedInstruments -contains 'GBPUSD' -and $universe.approvedInstruments -contains 'EURGBP' -and $universe.approvedInstruments -contains 'AUDUSD' -and $universe.approvedInstruments -contains 'USDJPY') 'LMAX_R205_FAIL_USDJPY_CAVEAT_WEAKENED' 'Approved universe weakened.'
Assert-True ($universe.newInstrumentOutsideApprovedUniverseIntroduced -eq $false -and $universe.usdJpyCaveatPreserved -eq $true) 'LMAX_R205_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat or universe weakened.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R205_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false -and $sanitization.rawMdReqIdSerialized -eq $false) 'LMAX_R205_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw FIX/reject/MDReqID serialization risk.'
Assert-True ($sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R205_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw market-data serialization risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false) 'LMAX_R205_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Secret/session/endpoint/TLS serialization risk.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R205_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R205_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false -and $forbidden.productionAccountUsed -eq $false) 'LMAX_R205_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay or production account introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R205_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'

Assert-True ($validation.buildStatus -eq 'PASS' -and $validation.validator -eq 'LMAX_R205_VALIDATION_PASS') 'LMAX_R205_FAIL_BUILD_OR_VALIDATOR' 'Build/validator evidence missing.'
Assert-True ($validation.focusedTestsStatus -like 'PASS*') 'LMAX_R205_FAIL_BUILD_OR_VALIDATOR' 'Focused test evidence missing.'

Assert-True ($next.supportingClassification -eq 'LMAX_R205_PASS_REMAINING_APPROVED_INSTRUMENTS_SEQUENTIAL_EXPANSION_READINESS_NO_EXTERNAL') 'LMAX_R205_FAIL_R207_RESERVATION_OR_SINGLE_ATTEMPT_CONSTRAINT_MISSING' 'Next phase recommendation classification missing.'
Assert-True ($next.r207RequiresFreshExactOperatorApproval -eq $true -and $next.r207RequiresConcreteWeekdayMarketHoursConfirmation -eq $true -and $next.r207RequiresExactlyOneBoundedExternalAttempt -eq $true -and $next.r207MustStopAfterAttempt -eq $true -and $next.r208MustRemainNoExternal -eq $true) 'LMAX_R205_FAIL_R207_RESERVATION_OR_SINGLE_ATTEMPT_CONSTRAINT_MISSING' 'R207 recommendation constraints missing.'

Write-Output 'LMAX_R205_VALIDATION_PASS'

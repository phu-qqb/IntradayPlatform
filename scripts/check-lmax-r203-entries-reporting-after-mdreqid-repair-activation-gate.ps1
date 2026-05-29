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
$profileName = 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument'

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r203-entries-reporting-activation-summary.md")) 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'R203 summary missing.'

$activation = Read-Json "$artifactRoot/phase-lmax-r203-entries-reporting-activation.json" 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE'
$preflight = Read-Json "$artifactRoot/phase-lmax-r203-preflight-result.json" 'LMAX_R203_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED'
$selected = Read-Json "$artifactRoot/phase-lmax-r203-selected-profile-evidence.json" 'LMAX_R203_FAIL_PROFILE_NOT_SELECTED'
$mdReqId = Read-Json "$artifactRoot/phase-lmax-r203-mdreqid-shape-evidence.json" 'LMAX_R203_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED'
$state = Read-Json "$artifactRoot/phase-lmax-r203-state-evidence.json" 'LMAX_R203_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT'
$boundary = Read-Json "$artifactRoot/phase-lmax-r203-boundary-evidence.json" 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE'
$request = Read-Json "$artifactRoot/phase-lmax-r203-marketdata-request-evidence.json" 'LMAX_R203_FAIL_MARKETDATA_RESPONSE_BOUNDARY'
$response = Read-Json "$artifactRoot/phase-lmax-r203-marketdata-response-evidence.json" 'LMAX_R203_FAIL_MARKETDATA_RESPONSE_BOUNDARY'
$entries = Read-Json "$artifactRoot/phase-lmax-r203-entries-evidence.json" 'LMAX_R203_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT'
$reject = Read-Json "$artifactRoot/phase-lmax-r203-reject-evidence.json" 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE'
$sanitized = Read-Json "$artifactRoot/phase-lmax-r203-sanitized-result.json" 'LMAX_R203_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$shutdown = Read-Json "$artifactRoot/phase-lmax-r203-shutdown-revert-evidence.json" 'LMAX_R203_FAIL_SHUTDOWN_REVERT_INCOMPLETE'
$universe = Read-Json "$artifactRoot/phase-lmax-r203-approved-universe-preservation.json" 'LMAX_R203_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$usdJpy = Read-Json "$artifactRoot/phase-lmax-r203-usdjpy-caveat-preservation.json" 'LMAX_R203_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r203-sanitization-audit.json" 'LMAX_R203_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r203-forbidden-actions-audit.json" 'LMAX_R203_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r203-api-worker-fake-gateway-audit.json" 'LMAX_R203_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r203-next-phase-recommendation.json" 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE'
$gate = Read-Json "$artifactRoot/phase-lmax-r203-gate-validation.json" 'LMAX_R203_FAIL_BUILD_OR_TESTS'

Assert-True ($activation.classification -in @(
    'LMAX_R203_PASS_ENTRIES_REPORTING_RUNTIME_ACTIVATION_SANITIZED',
    'LMAX_R203_PASS_RUNTIME_ACTIVATION_NO_REJECT_ENTRIES_NOT_AVAILABLE_SANITIZED',
    'LMAX_R203_FAIL_SESSIONREJECT_WITH_TAG_DETAIL_REPORTED',
    'LMAX_R203_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED',
    'LMAX_R203_FAIL_MARKETDATAREQUESTREJECT_WITH_SANITIZED_REASON_REPORTED',
    'LMAX_R203_FAIL_MARKETDATA_RESPONSE_BOUNDARY',
    'LMAX_R203_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT',
    'LMAX_R203_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT',
    'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE',
    'LMAX_R203_FAIL_SHUTDOWN_REVERT_INCOMPLETE',
    'LMAX_R203_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED',
    'LMAX_R203_FAIL_MARKET_HOURS_CONSTRAINT_MISSING_OR_VIOLATED',
    'LMAX_R203_FAIL_PROFILE_NOT_SELECTED',
    'LMAX_R203_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED',
    'LMAX_R203_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK',
    'LMAX_R203_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK',
    'LMAX_R203_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R203_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R203_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R203_FAIL_BUILD_OR_TESTS')) 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'R203 classification is not allowed.'

Assert-True ($preflight.approvalPresent -eq $true -and $preflight.approvalExactFreshMatch -eq $true) 'LMAX_R203_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'Approval missing or mismatched.'
Assert-True ($preflight.marketHoursConfirmationPresent -eq $true -and $preflight.marketHoursConfirmationConcrete -eq $true) 'LMAX_R203_FAIL_MARKET_HOURS_CONSTRAINT_MISSING_OR_VIOLATED' 'Market-hours confirmation missing or not concrete.'
Assert-True ($preflight.marketHoursWindow -eq 'Tuesday, May 19, 2026 at 13:13 Europe/Paris') 'LMAX_R203_FAIL_MARKET_HOURS_CONSTRAINT_MISSING_OR_VIOLATED' 'Market-hours window mismatch.'

Assert-True ($activation.externalActivationAttempted -eq $true -and $activation.attemptCount -eq 1) 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'R203 must have exactly one external activation attempt.'
Assert-True ($forbidden.externalActivationAttempted -eq $true -and $forbidden.externalAttemptApproved -eq $true -and $forbidden.attemptCount -eq 1) 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Approved attempt audit mismatch.'

Assert-True ($selected.selectedProfile -eq $profileName -and $selected.profileExecuted -eq $true) 'LMAX_R203_FAIL_PROFILE_NOT_SELECTED' 'Selected profile mismatch.'
Assert-True ($selected.selectedDiagnosticProfileCount -eq 1 -and $selected.priorDiagnosticOrLegacyProfileSelected -eq $false) 'LMAX_R203_FAIL_PROFILE_NOT_SELECTED' 'Profile selection count invalid.'
Assert-True ($selected.gbpUsdOnly -eq $true -and $selected.singleRequest -eq $true) 'LMAX_R203_FAIL_PROFILE_NOT_SELECTED' 'Profile is not GBPUSD-only single request.'
Assert-True ($selected.securityId -eq '4002' -and $selected.securityIdSource -eq '8') 'LMAX_R203_FAIL_PROFILE_NOT_SELECTED' 'SecurityID/SecurityIDSource contract weakened.'
Assert-True ($selected.subscriptionRequestType -eq '1' -and $selected.mdUpdateType -eq '0') 'LMAX_R203_FAIL_PROFILE_NOT_SELECTED' 'SubscriptionRequestType/MDUpdateType contract weakened.'
Assert-True ($selected.marketDepth -eq 1 -and $selected.noMdEntryTypes -eq 2 -and $selected.bidAndOfferTogether -eq $true) 'LMAX_R203_FAIL_PROFILE_NOT_SELECTED' 'MarketDepth/NoMDEntryTypes/bid+offer contract weakened.'
Assert-True ($selected.symbolTextPresent -eq $false -and $selected.internalSymbolPresent -eq $false -and $selected.snapshotOnlyPresent -eq $false -and $selected.subscriptionRequestTypeZeroPresent -eq $false) 'LMAX_R203_FAIL_PROFILE_NOT_SELECTED' 'Symbol/InternalSymbol/SnapshotOnly contract weakened.'

Assert-True ($mdReqId.repairedMdReqIdContractPreserved -eq $true -and $selected.repairedMdReqIdShapeUsed -eq $true) 'LMAX_R203_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED' 'Repaired MDReqID shape not used.'
Assert-True ($mdReqId.lengthLessThanOrEqual16 -eq $true -and $selected.mdReqIdMaxLength -le 16) 'LMAX_R203_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED' 'MDReqID length contract weakened.'
Assert-True ($mdReqId.alphanumericOnly -eq $true -and $mdReqId.containsPhaseLabel -eq $false -and $mdReqId.containsUnderscore -eq $false -and $mdReqId.containsPunctuation -eq $false) 'LMAX_R203_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED' 'MDReqID shape contract weakened.'
Assert-True ($mdReqId.rawMdReqIdSerialized -eq $false -and $selected.rawMdReqIdSerialized -eq $false) 'LMAX_R203_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Raw MDReqID serialized.'

Assert-True ($state.marketDataRequestWriteAttempted -eq $true -and $state.marketDataRequestWriteSucceeded -eq $true -and $state.marketDataRequestResponseReadAttempted -eq $true -and $state.marketDataRequestReachedBoundedResponseClassification -eq $true) 'LMAX_R203_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT' 'Explicit state fields missing or false.'
Assert-True ($state.marketDataRequestSentLegacyFlag -eq $false -and $state.stateFieldsConsistent -eq $true -and $state.stateFieldsContradictionDetected -eq $false) 'LMAX_R203_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT' 'State fields inconsistent.'

Assert-True ($boundary.tcpSocket -eq 'Succeeded' -and $boundary.tls -eq 'Succeeded' -and $boundary.fixLogonSession -eq 'Succeeded') 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'TCP/TLS/FIX boundary did not succeed.'
Assert-True ($boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'FIX acknowledgement missing.'
Assert-True ($boundary.marketDataResponseCategory -eq 'Succeeded' -and $response.marketDataResponseCategory -eq 'Succeeded') 'LMAX_R203_FAIL_MARKETDATA_RESPONSE_BOUNDARY' 'MarketDataResponse category mismatch.'
Assert-True ($boundary.marketDataRequestBeforeFixSuccess -eq $false -and $boundary.marketDataResponseReadBeforeMarketDataRequestSuccess -eq $false) 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Boundary order violation.'
Assert-True ($request.requestSent -eq $true -and $request.writeAttempted -eq $true -and $request.writeSucceeded -eq $true) 'LMAX_R203_FAIL_MARKETDATA_RESPONSE_BOUNDARY' 'MarketDataRequest evidence missing.'
Assert-True ($response.responseReadAttempted -eq $true -and $response.boundedResponseClassificationReached -eq $true) 'LMAX_R203_FAIL_MARKETDATA_RESPONSE_BOUNDARY' 'MarketDataResponse read/classification missing.'

Assert-True ($entries.entriesReportingPresent -eq $true) 'LMAX_R203_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT' 'Entries reporting missing.'
Assert-True ($entries.marketDataEntriesObserved -eq $true) 'LMAX_R203_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT' 'Entries observed not reported.'
Assert-True ($entries.marketDataSanitizedEntryCount -eq 2) 'LMAX_R203_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT' 'Sanitized entry count mismatch.'
Assert-True ($entries.marketDataEntriesEvidenceCategory -eq 'EntriesObservedWithSanitizedCount') 'LMAX_R203_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT' 'Entries evidence category mismatch.'
Assert-True ($entries.marketDataEntriesReportingSource -eq 'MarketDataResponseParserClassifierEntryCount') 'LMAX_R203_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT' 'Entries reporting source mismatch.'
Assert-True ($entries.marketDataEntriesNotAvailableReason -eq 'None') 'LMAX_R203_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT' 'Entries not-available reason mismatch.'

Assert-True ($reject.rejectObserved -eq $false -and $reject.rejectReasonExtractionSource -eq 'NoRejectObserved') 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Unexpected reject evidence.'
Assert-True ($response.sessionRejectSanitizedReasonCategory -eq 'None' -and $response.sanitizedSessionRejectReasonCategory -eq 'None') 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Unexpected SessionReject reason.'

Assert-True ($shutdown.shutdownRevertRequired -eq $true -and $shutdown.shutdownRevertCompleted -eq $true -and $shutdown.shutdownRevertStatus -eq 'Succeeded') 'LMAX_R203_FAIL_SHUTDOWN_REVERT_INCOMPLETE' 'Shutdown/revert incomplete.'
Assert-True ($shutdown.attemptStoppedAfterSingleBoundedRun -eq $true) 'LMAX_R203_FAIL_SHUTDOWN_REVERT_INCOMPLETE' 'Did not stop after single attempt.'

Assert-True ($universe.approvedUniversePreserved -eq $true -and $universe.nonApprovedInstrumentRequested -eq $false) 'LMAX_R203_FAIL_FORBIDDEN_ACTION_RISK' 'Approved universe weakened or non-approved instrument requested.'
foreach ($symbol in @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY')) {
    Assert-True ($universe.approvedInstruments -contains $symbol) 'LMAX_R203_FAIL_FORBIDDEN_ACTION_RISK' "Approved instrument missing: $symbol"
}
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true -and $usdJpy.usdJpySecurityId -eq '4004' -and $usdJpy.usdJpySecurityIdSource -eq '8') 'LMAX_R203_FAIL_FORBIDDEN_ACTION_RISK' 'USDJPY caveat weakened.'

Assert-True ($sanitization.credentialValuesReturned -eq $false -and $sanitized.credentialValuesReturned -eq $false) 'LMAX_R203_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $request.rawFixSerialized -eq $false -and $response.rawFixSerialized -eq $false) 'LMAX_R203_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw FIX serialization risk.'
Assert-True ($sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false -and $entries.rawMarketDataPricesSerialized -eq $false -and $entries.rawBidAskValuesSerialized -eq $false) 'LMAX_R203_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw market-data serialization risk.'
Assert-True ($sanitization.rawRejectTextSerialized -eq $false -and $response.rawRejectTextSerialized -eq $false -and $reject.rawRejectTextSerialized -eq $false) 'LMAX_R203_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Raw reject serialization risk.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false -and $request.rawMdReqIdSerialized -eq $false) 'LMAX_R203_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Raw MDReqID serialization risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false) 'LMAX_R203_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Secret/session/endpoint/TLS serialization risk.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R203_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R203_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false) 'LMAX_R203_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R203_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'

Assert-True ($gate.build -like 'PASS*' -and $gate.focusedTests -like 'PASS*' -and $gate.validator -eq 'LMAX_R203_VALIDATION_PASS') 'LMAX_R203_FAIL_BUILD_OR_TESTS' 'Build/test/validator evidence missing.'
Assert-True ($next.recommendedNextPhase -eq 'LMAX-R204' -and $next.mustRemainNoExternal -eq $true) 'LMAX_R203_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Next phase recommendation missing or not no-external.'

$artifactText = Get-ChildItem -LiteralPath (Join-Path $Root $artifactRoot) -Filter 'phase-lmax-r203-*' -File |
    Where-Object { $_.Name -notin @('phase-lmax-r203-operator-approval.txt', 'phase-lmax-r203-expected-operator-approval.txt', 'phase-lmax-r203-operator-approval-note.md') } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '262=', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId', 'LMAX_READONLY_', 'bid=', 'ask=')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) 'LMAX_R203_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' "Forbidden serialized token detected in R203 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R203_VALIDATION_PASS'

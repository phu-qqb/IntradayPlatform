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

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r211-activation-summary.md")) 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'R211 summary missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r211-operator-approval.txt")) 'LMAX_R211_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'R211 operator approval missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r211-expected-operator-approval.txt")) 'LMAX_R211_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'R211 expected approval missing.'

$approval = Get-Content -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r211-operator-approval.txt") -Raw
$expectedApproval = Get-Content -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r211-expected-operator-approval.txt") -Raw
Assert-True ($approval.Trim() -eq $expectedApproval.Trim()) 'LMAX_R211_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'Operator approval mismatch.'
Assert-True ($approval.Contains('LMAX-R211') -and $approval.Contains('AUDUSD') -and $approval.Contains('SecurityID=4007') -and $approval.Contains('SecurityIDSource=8')) 'LMAX_R211_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'Operator approval does not match R211 AUDUSD.'

$activation = Read-Json "$artifactRoot/phase-lmax-r211-activation.json" 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE'
$preflight = Read-Json "$artifactRoot/phase-lmax-r211-preflight-result.json" 'LMAX_R211_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED'
$selected = Read-Json "$artifactRoot/phase-lmax-r211-selected-instrument-evidence.json" 'LMAX_R211_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED'
$boundary = Read-Json "$artifactRoot/phase-lmax-r211-boundary-evidence.json" 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE'
$request = Read-Json "$artifactRoot/phase-lmax-r211-marketdata-request-evidence.json" 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE'
$state = Read-Json "$artifactRoot/phase-lmax-r211-state-evidence.json" 'LMAX_R211_FAIL_AUDUSD_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT'
$response = Read-Json "$artifactRoot/phase-lmax-r211-marketdata-response-evidence.json" 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE'
$entries = Read-Json "$artifactRoot/phase-lmax-r211-entries-evidence.json" 'LMAX_R211_FAIL_AUDUSD_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT'
$reject = Read-Json "$artifactRoot/phase-lmax-r211-reject-evidence.json" 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE'
$logout = Read-Json "$artifactRoot/phase-lmax-r211-logout-evidence.json" 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE'
$sanitized = Read-Json "$artifactRoot/phase-lmax-r211-sanitized-result.json" 'LMAX_R211_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$shutdown = Read-Json "$artifactRoot/phase-lmax-r211-shutdown-revert-evidence.json" 'LMAX_R211_FAIL_SHUTDOWN_REVERT_INCOMPLETE'
$baseline = Read-Json "$artifactRoot/phase-lmax-r211-baseline-preservation.json" 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE'
$universe = Read-Json "$artifactRoot/phase-lmax-r211-approved-universe-preservation.json" 'LMAX_R211_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$usdJpy = Read-Json "$artifactRoot/phase-lmax-r211-usdjpy-caveat-preservation.json" 'LMAX_R211_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r211-sanitization-audit.json" 'LMAX_R211_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r211-forbidden-actions-audit.json" 'LMAX_R211_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r211-api-worker-fake-gateway-audit.json" 'LMAX_R211_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r211-next-phase-recommendation.json" 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE'
$gate = Read-Json "$artifactRoot/phase-lmax-r211-gate-validation.json" 'LMAX_R211_FAIL_BUILD_OR_TESTS'

Assert-True ($activation.classification -in @(
    'LMAX_R211_PASS_AUDUSD_ONLY_ENTRIES_RUNTIME_ACTIVATION_SANITIZED',
    'LMAX_R211_FAIL_AUDUSD_LOGOUT_WITH_SANITIZED_REASON_REPORTED',
    'LMAX_R211_FAIL_AUDUSD_SESSIONREJECT_WITH_TAG_DETAIL_REPORTED',
    'LMAX_R211_FAIL_AUDUSD_MARKETDATAREQUESTREJECT_WITH_SANITIZED_REASON_REPORTED',
    'LMAX_R211_FAIL_AUDUSD_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT',
    'LMAX_R211_FAIL_AUDUSD_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT',
    'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE',
    'LMAX_R211_FAIL_SHUTDOWN_REVERT_INCOMPLETE')) 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'R211 classification is not allowed.'

Assert-True ($preflight.approvalPresent -eq $true -and $preflight.approvalExactFreshMatch -eq $true) 'LMAX_R211_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'Approval missing or mismatched.'
Assert-True ($preflight.marketHoursConfirmationPresent -eq $true -and $preflight.marketHoursConfirmationConcrete -eq $true) 'LMAX_R211_FAIL_MARKET_HOURS_CONSTRAINT_MISSING_OR_VIOLATED' 'Market-hours confirmation missing.'
Assert-True ($preflight.marketHoursWindow -eq 'Tuesday, May 19, 2026 at 17:05 Europe/Paris') 'LMAX_R211_FAIL_MARKET_HOURS_CONSTRAINT_MISSING_OR_VIOLATED' 'Market-hours window mismatch.'

Assert-True ($activation.externalActivationAttempted -eq $true -and $activation.attemptCount -eq 1) 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'R211 must have exactly one attempt.'
Assert-True ($activation.adapterMode -eq 'real-bounded-executable-readonly') 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Adapter mode mismatch.'

Assert-True ($selected.selectedInstrument -eq 'AUDUSD' -and $selected.audusdOnly -eq $true -and $selected.requestCount -eq 1) 'LMAX_R211_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'AUDUSD-only contract missing.'
Assert-True ($selected.securityId -eq '4007' -and $selected.securityIdSource -eq '8') 'LMAX_R211_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'AUDUSD SecurityID/Source mismatch.'
Assert-True ($selected.mdReqIdShort -eq $true -and $selected.mdReqIdAlphanumericOnly -eq $true -and $selected.mdReqIdUniquePerRequest -eq $true -and $selected.mdReqIdLengthLessThanOrEqual16 -eq $true) 'LMAX_R211_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED' 'MDReqID repaired shape missing.'
Assert-True ($selected.mdReqIdContainsPhaseLabel -eq $false -and $selected.mdReqIdContainsUnderscore -eq $false -and $selected.mdReqIdContainsPunctuation -eq $false -and $selected.rawMdReqIdSerialized -eq $false) 'LMAX_R211_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED' 'MDReqID repair contract weakened.'
Assert-True ($selected.subscriptionRequestType -eq '1' -and $selected.mdUpdateType -eq '0' -and $selected.marketDepth -eq 1 -and $selected.noMdEntryTypes -eq 2 -and $selected.bidAndOfferTogether -eq $true) 'LMAX_R211_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'AUDUSD request shape weakened.'
Assert-True ($selected.symbolTextPresent -eq $false -and $selected.internalSymbolPresent -eq $false -and $selected.snapshotOnlyPresent -eq $false -and $selected.subscriptionRequestTypeZeroPresent -eq $false) 'LMAX_R211_FAIL_AUDUSD_ONLY_CONTRACT_WEAKENED' 'Symbol/InternalSymbol/SnapshotOnly appeared.'

Assert-True ($boundary.tcpConnectionAttempted -eq $true -and $boundary.realSocketOpened -eq $true -and $boundary.tlsHandshakeAttempted -eq $true) 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'TCP/TLS attempt evidence missing.'
Assert-True ($boundary.tlsSucceeded -eq $false -and $boundary.tlsBoundaryStatus -eq 'Failed' -and $boundary.transportResultCategory -eq 'TlsHandshakeBoundaryFailed') 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'TLS failure boundary evidence mismatch.'
Assert-True ($boundary.tlsRawMaterialSerialized -eq $false) 'LMAX_R211_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'TLS material serialized.'
Assert-True ($boundary.fixLogonAttempted -eq $false -and $boundary.marketDataBoundaryStatus -eq 'NotAttempted') 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'FIX or MarketData unexpectedly attempted after TLS failure.'

Assert-True ($request.requestConfiguredForAudusdOnly -eq $true -and $request.requestSent -eq $false -and $request.notSentReason -eq 'TlsHandshakeBoundaryFailedBeforeFixAndMarketDataRequest') 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'MarketDataRequest evidence mismatch.'
Assert-True ($request.rawFixSerialized -eq $false -and $request.rawMdReqIdSerialized -eq $false) 'LMAX_R211_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'Raw request value serialized.'

Assert-True ($state.stateFieldsEmitted -eq $true -and $state.stateFieldsConsistent -eq $true -and $state.stateFieldsContradictionDetected -eq $false) 'LMAX_R211_FAIL_AUDUSD_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT' 'State evidence inconsistent.'
Assert-True ($state.marketDataRequestWriteAttempted -eq $false -and $state.marketDataRequestWriteSucceeded -eq $false -and $state.marketDataRequestResponseReadAttempted -eq $false -and $state.marketDataRequestReachedBoundedResponseClassification -eq $false -and $state.marketDataRequestSentLegacyFlag -eq $false) 'LMAX_R211_FAIL_AUDUSD_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT' 'State fields should be false because no MarketDataRequest was attempted.'
Assert-True ($state.classifiedMarketDataResponseObserved -eq $false -and $state.allExplicitStateFieldsFalseAllowedBecauseNoMarketDataRequestWasAttempted -eq $true) 'LMAX_R211_FAIL_AUDUSD_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT' 'State false rationale missing.'

Assert-True ($response.responseReadAttempted -eq $false -and $response.marketDataResponseCategory -eq 'MarketDataNotAttempted') 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'MarketDataResponse should be not attempted.'
Assert-True ($entries.entriesReportingPresent -eq $true -and $entries.entriesCountClaimed -eq $false -and $entries.marketDataEntriesEvidenceCategory -eq 'EntriesEvidenceInconclusiveSafe') 'LMAX_R211_FAIL_AUDUSD_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT' 'Entries evidence should be inconclusive, not claimed.'
Assert-True ($entries.rawMarketDataPricesSerialized -eq $false -and $entries.rawBidAskValuesSerialized -eq $false -and $entries.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R211_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'Raw market-data leak risk.'

Assert-True ($reject.rejectObserved -eq $false -and $reject.sessionRejectObserved -eq $false -and $reject.marketDataRequestRejectObserved -eq $false) 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Unexpected reject observed.'
Assert-True ($reject.rawRejectTextSerialized -eq $false -and $reject.rawFixSerialized -eq $false) 'LMAX_R211_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Raw reject/FIX leak risk.'
Assert-True ($logout.logoutObserved -eq $false -and $logout.logoutReasonSanitizedCategory -eq 'LogoutReasonNotAvailable' -and $logout.rawLogoutTextSerialized -eq $false) 'LMAX_R211_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Logout evidence invalid or raw text risk.'

Assert-True ($shutdown.shutdownRevertCompleted -eq $true -and $shutdown.shutdownRevertStatus -eq 'Succeeded' -and $shutdown.stoppedAfterSingleBoundedAttempt -eq $true -and $shutdown.attemptCount -eq 1) 'LMAX_R211_FAIL_SHUTDOWN_REVERT_INCOMPLETE' 'Shutdown/revert incomplete.'

Assert-True ($baseline.gbpusdR203SuccessPreserved -eq $true -and $baseline.eurgbpR207SuccessPreserved -eq $true) 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'GBPUSD/EURGBP baseline weakened.'
Assert-True ($universe.approvedUniversePreserved -eq $true -and $universe.approvedInstruments -contains 'GBPUSD' -and $universe.approvedInstruments -contains 'EURGBP' -and $universe.approvedInstruments -contains 'AUDUSD' -and $universe.approvedInstruments -contains 'USDJPY') 'LMAX_R211_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Approved universe weakened.'
Assert-True ($usdJpy.usdJpyNotProven -eq $true -and $usdJpy.usdJpyClassifiedAsFailed -eq $false -and $usdJpy.usdJpyCaveatPreserved -eq $true -and $usdJpy.usdJpySecurityId -eq '4004' -and $usdJpy.usdJpySecurityIdSource -eq '8') 'LMAX_R211_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'USDJPY caveat weakened or misclassified.'

Assert-True ($sanitization.credentialValuesReturned -eq $false -and $sanitized.credentialValuesReturned -eq $false) 'LMAX_R211_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawLogoutTextSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false -and $sanitization.rawMdReqIdSerialized -eq $false) 'LMAX_R211_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Raw FIX/logout/reject/MDReqID leak risk.'
Assert-True ($sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R211_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'Raw market-data leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false) 'LMAX_R211_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Secret/session/endpoint/TLS leak risk.'

Assert-True ($forbidden.externalActivationAttempted -eq $true -and $forbidden.externalAttemptApproved -eq $true -and $forbidden.attemptCount -eq 1) 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Approved external attempt audit invalid.'
Assert-True ($forbidden.fixOpened -eq $false -and $forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R211_FAIL_FORBIDDEN_ACTION_RISK' 'Unexpected FIX/MarketData action.'
Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R211_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R211_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false -and $forbidden.productionAccountUsed -eq $false) 'LMAX_R211_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay or production account introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R211_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker gateway regression.'

Assert-True ($gate.buildStatus -eq 'PASS' -and $gate.focusedTestsStatus -eq 'PASS 128/128' -and $gate.validator -eq 'LMAX_R211_VALIDATION_PASS') 'LMAX_R211_FAIL_BUILD_OR_TESTS' 'Build/test/validator evidence missing.'
Assert-True ($next.r212MustRemainNoExternal -eq $true -and $next.liveRetryBeforeR212Allowed -eq $false) 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Next phase recommendation allows premature live retry.'

$artifactText = Get-ChildItem -LiteralPath (Join-Path $Root $artifactRoot) -Filter 'phase-lmax-r211-*' -File |
    Where-Object { $_.Name -notin @('phase-lmax-r211-operator-approval.txt', 'phase-lmax-r211-expected-operator-approval.txt', 'phase-lmax-r211-operator-approval-note.md') } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @([string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId', 'bid=', 'ask=')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) 'LMAX_R211_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' "Forbidden serialized token detected in R211 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R211_VALIDATION_PASS'

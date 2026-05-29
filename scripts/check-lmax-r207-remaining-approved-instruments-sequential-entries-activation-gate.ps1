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

$activation = Read-Json "$artifactRoot/phase-lmax-r207-activation.json" 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE'
$boundary = Read-Json "$artifactRoot/phase-lmax-r207-boundary-evidence.json" 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE'
$state = Read-Json "$artifactRoot/phase-lmax-r207-state-evidence.json" 'LMAX_R207_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT'
$instruments = Read-Json "$artifactRoot/phase-lmax-r207-instrument-evidence.json" 'LMAX_R207_FAIL_PROFILE_OR_INSTRUMENT_CONTRACT_WEAKENED'
$entries = Read-Json "$artifactRoot/phase-lmax-r207-entries-evidence.json" 'LMAX_R207_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT'
$reject = Read-Json "$artifactRoot/phase-lmax-r207-reject-evidence.json" 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE'
$request = Read-Json "$artifactRoot/phase-lmax-r207-marketdata-request-evidence.json" 'LMAX_R207_FAIL_PROFILE_OR_INSTRUMENT_CONTRACT_WEAKENED'
$mdReqId = Read-Json "$artifactRoot/phase-lmax-r207-mdreqid-shape-evidence.json" 'LMAX_R207_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED'
$shutdown = Read-Json "$artifactRoot/phase-lmax-r207-shutdown-revert-evidence.json" 'LMAX_R207_FAIL_SHUTDOWN_REVERT_INCOMPLETE'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r207-sanitization-audit.json" 'LMAX_R207_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r207-forbidden-actions-audit.json" 'LMAX_R207_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r207-api-worker-fake-gateway-audit.json" 'LMAX_R207_FAIL_API_WORKER_GATEWAY_REGRESSION'
$universe = Read-Json "$artifactRoot/phase-lmax-r207-approved-universe-preservation.json" 'LMAX_R207_FAIL_USDJPY_CAVEAT_WEAKENED'
$next = Read-Json "$artifactRoot/phase-lmax-r207-next-phase-recommendation.json" 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE'
$validation = Read-Json "$artifactRoot/phase-lmax-r207-gate-validation.json" 'LMAX_R207_FAIL_BUILD_OR_TESTS'

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r207-activation-summary.md")) 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'R207 summary missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r207-operator-approval.txt")) 'LMAX_R207_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'Operator approval missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r207-expected-operator-approval.txt")) 'LMAX_R207_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'Expected operator approval missing.'

$approval = Get-Content -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r207-operator-approval.txt") -Raw
$expectedApproval = Get-Content -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r207-expected-operator-approval.txt") -Raw
Assert-True ($approval.Trim() -eq $expectedApproval.Trim()) 'LMAX_R207_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'Operator approval mismatch.'
Assert-True ($approval.Contains('LMAX-R207') -and $approval.Contains('EURGBP') -and $approval.Contains('AUDUSD') -and $approval.Contains('USDJPY')) 'LMAX_R207_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'Operator approval does not match R207 remaining instruments.'

Assert-True ($activation.classification -in @(
    'LMAX_R207_PASS_REMAINING_APPROVED_INSTRUMENTS_SEQUENTIAL_ENTRIES_RUNTIME_ACTIVATION_SANITIZED',
    'LMAX_R207_PASS_PARTIAL_REMAINING_INSTRUMENTS_SEQUENTIAL_ENTRIES_RUNTIME_ACTIVATION_SANITIZED',
    'LMAX_R207_FAIL_EURGBP_BOUNDARY_OR_REJECT',
    'LMAX_R207_FAIL_AUDUSD_BOUNDARY_OR_REJECT',
    'LMAX_R207_FAIL_USDJPY_BOUNDARY_OR_REJECT')) 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'R207 classification is not allowed.'
Assert-True ($activation.externalActivationAttempted -eq $true -and $activation.attemptCount -eq 1) 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'R207 must perform exactly one external activation attempt.'
Assert-True ($activation.marketHoursWindow -eq 'Tuesday, May 19, 2026 at 16:12 Europe/Paris') 'LMAX_R207_FAIL_MARKET_HOURS_CONSTRAINT_MISSING_OR_VIOLATED' 'Market-hours confirmation missing or mismatched.'
Assert-True ($activation.adapterMode -eq 'real-bounded-executable-readonly') 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Adapter mode mismatch.'
Assert-True ($activation.diagnosticMode -eq 'SequentialRemainingApprovedInstrumentsInsideSingleBoundedActivation') 'LMAX_R207_FAIL_PROFILE_OR_INSTRUMENT_CONTRACT_WEAKENED' 'Sequential diagnostic mode missing.'

Assert-True ($boundary.tcpBoundary -eq 'Succeeded' -and $boundary.tlsBoundaryStatus -eq 'Succeeded' -and $boundary.fixBoundaryStatus -eq 'Succeeded') 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'TCP/TLS/FIX boundary did not succeed.'
Assert-True ($boundary.fixAcknowledgementCategory -eq 'FixLogonAcknowledged') 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'FIX acknowledgement missing.'

Assert-True ($request.singleExternalActivationAttemptOnly -eq $true -and $request.oneRequestPerInstrument -eq $true) 'LMAX_R207_FAIL_PROFILE_OR_INSTRUMENT_CONTRACT_WEAKENED' 'Single-attempt or one-request-per-instrument contract missing.'
Assert-True ($request.eurgbpSecurityId -eq '4003' -and $request.audusdSecurityId -eq '4007' -and $request.usdjpySecurityId -eq '4004' -and $request.securityIdSource -eq '8') 'LMAX_R207_FAIL_PROFILE_OR_INSTRUMENT_CONTRACT_WEAKENED' 'Instrument SecurityID contract weakened.'
Assert-True ($request.usdJpyCaveatPreserved -eq $true) 'LMAX_R207_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'
Assert-True ($request.subscriptionRequestType -eq '1' -and $request.mdUpdateType -eq '0') 'LMAX_R207_FAIL_PROFILE_OR_INSTRUMENT_CONTRACT_WEAKENED' 'SubscriptionRequestType/MDUpdateType weakened.'
Assert-True ($request.marketDepth -eq 1 -and $request.noMdEntryTypes -eq 2 -and $request.bidAndOfferTogether -eq $true) 'LMAX_R207_FAIL_PROFILE_OR_INSTRUMENT_CONTRACT_WEAKENED' 'Depth/entry-type contract weakened.'
Assert-True ($request.symbolTextPresent -eq $false -and $request.internalSymbolPresent -eq $false -and $request.snapshotOnlyPresent -eq $false -and $request.subscriptionRequestTypeZeroPresent -eq $false) 'LMAX_R207_FAIL_PROFILE_OR_INSTRUMENT_CONTRACT_WEAKENED' 'Symbol/InternalSymbol/SnapshotOnly appeared.'

Assert-True ($mdReqId.mdReqIdRepairContractPreserved -eq $true -and $mdReqId.short -eq $true -and $mdReqId.alphanumericOnly -eq $true -and $mdReqId.uniquePerRequest -eq $true -and $mdReqId.sessionSafe -eq $true -and $mdReqId.lengthLessThanOrEqual16 -eq $true) 'LMAX_R207_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED' 'MDReqID repair contract weakened.'
Assert-True ($mdReqId.containsPhaseLabel -eq $false -and $mdReqId.containsUnderscore -eq $false -and $mdReqId.containsPunctuation -eq $false -and $mdReqId.rawMdReqIdSerialized -eq $false) 'LMAX_R207_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Raw or malformed MDReqID risk.'

Assert-True ($state.stateEvidenceConsistent -eq $true) 'LMAX_R207_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT' 'State evidence inconsistent.'
Assert-True ($state.marketDataRequestWriteAttempted -eq $true -and $state.marketDataRequestWriteSucceeded -eq $true -and $state.marketDataRequestResponseReadAttempted -eq $true -and $state.marketDataRequestReachedBoundedResponseClassification -eq $true) 'LMAX_R207_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT' 'Explicit state fields missing.'
Assert-True ($state.marketDataRequestSentLegacyFlag -eq $false) 'LMAX_R207_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT' 'Legacy flag should remain compatibility-only false.'

Assert-True ($instruments.perInstrumentEvidencePresent -eq $true) 'LMAX_R207_FAIL_PROFILE_OR_INSTRUMENT_CONTRACT_WEAKENED' 'Per-instrument evidence missing.'
foreach ($symbol in @('EURGBP', 'AUDUSD', 'USDJPY')) {
    Assert-True (@($instruments.instruments | Where-Object { $_.selectedInstrument -eq $symbol }).Count -eq 1) 'LMAX_R207_FAIL_PROFILE_OR_INSTRUMENT_CONTRACT_WEAKENED' "Missing per-instrument evidence for $symbol."
}
$eurGbp = $instruments.instruments | Where-Object { $_.selectedInstrument -eq 'EURGBP' } | Select-Object -First 1
$audUsd = $instruments.instruments | Where-Object { $_.selectedInstrument -eq 'AUDUSD' } | Select-Object -First 1
$usdJpy = $instruments.instruments | Where-Object { $_.selectedInstrument -eq 'USDJPY' } | Select-Object -First 1

Assert-True ($eurGbp.securityId -eq '4003' -and $eurGbp.securityIdSource -eq '8' -and $eurGbp.marketDataResponseCategory -eq 'Succeeded') 'LMAX_R207_FAIL_EURGBP_BOUNDARY_OR_REJECT' 'EURGBP did not produce sanitized success.'
Assert-True ($eurGbp.marketDataEntriesObserved -eq $true -and $eurGbp.marketDataSanitizedEntryCount -eq 2 -and $eurGbp.marketDataEntriesEvidenceCategory -eq 'EntriesObservedWithSanitizedCount') 'LMAX_R207_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT' 'EURGBP entries evidence missing.'
Assert-True ($audUsd.securityId -eq '4007' -and $audUsd.securityIdSource -eq '8') 'LMAX_R207_FAIL_AUDUSD_BOUNDARY_OR_REJECT' 'AUDUSD mapping missing.'
Assert-True ($audUsd.marketDataResponseCategory -eq 'LogoutObserved') 'LMAX_R207_FAIL_AUDUSD_BOUNDARY_OR_REJECT' 'AUDUSD boundary/reject evidence missing.'
Assert-True ($usdJpy.securityId -eq '4004' -and $usdJpy.securityIdSource -eq '8' -and $usdJpy.usdJpyCaveatPreserved -eq $true) 'LMAX_R207_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY mapping/caveat missing.'

Assert-True ($entries.entriesReportingPresent -eq $true -and $entries.aggregateMarketDataEntriesObserved -eq $true -and $entries.aggregateMarketDataSanitizedEntryCount -eq 2) 'LMAX_R207_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT' 'Aggregate entries evidence missing.'
Assert-True ($entries.entriesCountSafelyReported -eq $true -and $entries.entriesCountClaimSupportedByRuntimeCli -eq $true) 'LMAX_R207_FAIL_ENTRIES_REPORTING_MISSING_OR_INCONSISTENT' 'Entries count unsupported.'
Assert-True ($entries.rawMarketDataPricesSerialized -eq $false -and $entries.rawBidAskValuesSerialized -eq $false -and $entries.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R207_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw market-data leak risk.'

Assert-True ($reject.rawRejectTextSerialized -eq $false -and $reject.rawFixSerialized -eq $false) 'LMAX_R207_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Raw reject/FIX leak risk.'
Assert-True ($shutdown.shutdownRevertCompleted -eq $true -and $shutdown.shutdownRevertStatus -eq 'Succeeded' -and $shutdown.stoppedAfterSingleBoundedAttempt -eq $true) 'LMAX_R207_FAIL_SHUTDOWN_REVERT_INCOMPLETE' 'Shutdown/revert incomplete.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R207_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false -and $sanitization.rawMdReqIdSerialized -eq $false) 'LMAX_R207_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Raw FIX/reject/MDReqID leak risk.'
Assert-True ($sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R207_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw market-data leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false) 'LMAX_R207_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Secret/session/endpoint/TLS leak risk.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R207_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R207_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false -and $forbidden.productionAccountUsed -eq $false) 'LMAX_R207_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay or production account introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R207_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker gateway regression.'

Assert-True ($universe.approvedUniversePreserved -eq $true -and $universe.approvedInstruments -contains 'GBPUSD' -and $universe.approvedInstruments -contains 'EURGBP' -and $universe.approvedInstruments -contains 'AUDUSD' -and $universe.approvedInstruments -contains 'USDJPY') 'LMAX_R207_FAIL_USDJPY_CAVEAT_WEAKENED' 'Approved universe weakened.'
Assert-True ($universe.newInstrumentOutsideApprovedUniverseIntroduced -eq $false -and $universe.usdJpyCaveatPreserved -eq $true) 'LMAX_R207_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'

Assert-True ($validation.buildStatus -eq 'PASS' -and $validation.focusedTestsStatus -like 'PASS*' -and $validation.validator -eq 'LMAX_R207_VALIDATION_PASS') 'LMAX_R207_FAIL_BUILD_OR_TESTS' 'Build/test/validator evidence missing.'
Assert-True ($next.r208MustRemainNoExternal -eq $true -and $next.liveRetryBeforeR208Allowed -eq $false) 'LMAX_R207_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Next phase recommendation allows premature live retry.'

Write-Output 'LMAX_R207_VALIDATION_PASS'

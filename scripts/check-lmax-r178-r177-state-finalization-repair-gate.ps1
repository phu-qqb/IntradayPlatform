param(
    [string] $ArtifactRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Read-Json($Name) {
    $path = Join-Path $ArtifactRoot $Name
    if (-not (Test-Path $path)) {
        throw "Missing required artifact: $Name"
    }

    Get-Content $path -Raw | ConvertFrom-Json
}

function Assert-True($Condition, [string] $Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-False($Condition, [string] $Message) {
    if ($Condition) {
        throw $Message
    }
}

$review = Read-Json 'phase-lmax-r178-r177-state-finalization-review.json'
$rootCause = Read-Json 'phase-lmax-r178-final-emission-root-cause.json'
$provenance = Read-Json 'phase-lmax-r178-state-field-provenance-contract.json'
$consistency = Read-Json 'phase-lmax-r178-final-consistency-rule.json'
$implementation = Read-Json 'phase-lmax-r178-repair-implementation-evidence.json'
$tests = Read-Json 'phase-lmax-r178-final-artifact-emission-test-evidence.json'
$resolution = Read-Json 'phase-lmax-r178-r177-inconsistency-resolution.json'
$decision = Read-Json 'phase-lmax-r178-next-action-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r178-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r178-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r178-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r178-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r178-gate-validation.json'

foreach ($required in @(
    'phase-lmax-r177-mdupdatetype-required-after-r176-state-propagation-activation.json',
    'phase-lmax-r177-state-propagation-evidence.json',
    'phase-lmax-r177-boundary-evidence.json',
    'phase-lmax-r177-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $ArtifactRoot $required)) "R177 evidence missing: $required"
}

$r177 = Read-Json 'phase-lmax-r177-mdupdatetype-required-after-r176-state-propagation-activation.json'
$r177State = Read-Json 'phase-lmax-r177-state-propagation-evidence.json'
$r177Boundary = Read-Json 'phase-lmax-r177-boundary-evidence.json'

Assert-True ($review.phase -eq 'LMAX-R178') 'R178 review artifact phase mismatch.'
Assert-True ($review.classification -eq 'LMAX_R178_PASS_FINAL_STATE_EVIDENCE_CONTRACT_REPAIR_IMPLEMENTED_NO_EXTERNAL') 'Unexpected R178 classification.'
Assert-True ($review.noExternal -eq $true) 'R178 no-external confirmation missing.'
Assert-False $review.liveActivationPerformed 'R178 must not perform a live activation.'
Assert-True ($review.r177EvidenceReviewed -eq $true) 'R177 evidence review missing.'
Assert-True ($review.r177AttemptCount -eq 1) 'R177 attempt count review missing or invalid.'
Assert-True ($review.r177StateContradictionReviewed -eq $true) 'R177 state contradiction review missing.'
Assert-True ($review.finalEmissionRootCauseIdentified -eq $true) 'Final emission root cause missing.'
Assert-True ($review.repairImplemented -eq $true) 'Repair implementation evidence missing.'
Assert-True ($review.finalEvidenceContractValidated -eq $true) 'Final evidence contract validation missing.'

Assert-True ($r177.attemptCount -eq 1) 'R177 attemptCount must be 1.'
Assert-True ($r177.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R177 observed MarketDataResponse category missing.'
Assert-True ($r177.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R177 sanitized reason mismatch.'
Assert-False $r177State.marketDataRequestWriteAttempted 'R177 write attempted flag should be false in historical inconsistent evidence.'
Assert-False $r177State.marketDataRequestWriteSucceeded 'R177 write succeeded flag should be false in historical inconsistent evidence.'
Assert-False $r177State.marketDataRequestResponseReadAttempted 'R177 response read attempted flag should be false in historical inconsistent evidence.'
Assert-False $r177State.marketDataRequestReachedBoundedResponseClassification 'R177 bounded classification flag should be false in historical inconsistent evidence.'
Assert-True ($r177Boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'R177 FIX acknowledgement missing.'
Assert-True ($r177Boundary.marketDataRequest -eq 'ReachedBoundedResponseClassificationAfterFixSuccess') 'R177 bounded response-classification boundary marker missing.'
Assert-True ($r177Boundary.marketDataResponseRead -eq 'ReachedSanitizedClassification') 'R177 sanitized response-read boundary marker missing.'

Assert-True ($rootCause.responsibleComponent -eq 'LmaxRealReadOnlyMarketDataFrameClient.Sanitize') 'Final emission root cause component mismatch.'
Assert-True ($rootCause.responsibleFile -eq 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyProviderClients.cs') 'Final emission root cause file mismatch.'
foreach ($field in @(
    'MarketDataRequestWriteAttempted',
    'MarketDataRequestWriteSucceeded',
    'MarketDataRequestResponseReadAttempted',
    'MarketDataRequestReachedBoundedResponseClassification')) {
    Assert-True ($rootCause.lostFields -contains $field) "Root cause missing lost field: $field"
}

Assert-True ($provenance.stateFieldProvenanceContractPresent -eq $true) 'State field provenance contract missing.'
Assert-True ($provenance.stateFieldsSourceLayer -ne $null -and $provenance.stateFieldsSourceLayer.Length -gt 0) 'State fields source layer missing.'
Assert-True ($provenance.stateFieldsFinalizedAtLayer -ne $null -and $provenance.stateFieldsFinalizedAtLayer.Length -gt 0) 'State fields finalized layer missing.'
Assert-False $provenance.stateFieldsWereDerivedFromBoundaryEvidence 'State fields must not be silently fabricated from boundary evidence.'
Assert-True ($provenance.stateFieldsContradictionDetected -eq $true) 'R177 contradiction marker missing.'
Assert-True ($provenance.legacyFlagCompatibilityOnly -eq $true) 'Legacy flag compatibility decision missing.'
foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification')) {
    Assert-True ($provenance.authoritativeExplicitStateFields -contains $field) "Provenance contract missing explicit field: $field"
}
Assert-True ($provenance.unknownAllowedWhenWriterEvidenceMissing -eq $true) 'Unknown/derived writer state allowance missing.'
Assert-False $provenance.silentFalseAllowedWhenResponseClassified 'Silent false state must not be allowed when response is classified.'

Assert-True ($consistency.finalConsistencyRulePresent -eq $true) 'Final consistency rule missing.'
Assert-False $consistency.observedResponseCategoryCanEmitAllFalseWithoutExplanation 'Observed response category can still emit all false without explanation.'
Assert-False $consistency.legacyFlagAuthoritative 'Legacy flag must not be authoritative.'

Assert-True ($implementation.repairImplemented -eq $true) 'Repair implementation missing.'
Assert-True ($implementation.providerClientSanitizerCopiesExplicitStateFields -eq $true) 'Provider client sanitizer propagation repair missing.'
Assert-False $implementation.liveRetryExecutedInR178 'R178 repair evidence indicates a live retry.'

$providerClientPath = 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyProviderClients.cs'
Assert-True (Test-Path $providerClientPath) 'Provider client file missing.'
$providerClientText = Get-Content $providerClientPath -Raw
foreach ($assignment in @(
    'MarketDataRequestWriteAttempted = result.MarketDataRequestWriteAttempted',
    'MarketDataRequestWriteSucceeded = result.MarketDataRequestWriteSucceeded',
    'MarketDataRequestResponseReadAttempted = result.MarketDataRequestResponseReadAttempted',
    'MarketDataRequestReachedBoundedResponseClassification = result.MarketDataRequestReachedBoundedResponseClassification')) {
    Assert-True ($providerClientText.Contains($assignment)) "Provider client sanitizer missing assignment: $assignment"
}

$testPath = 'tests/QQ.Production.Intraday.Tests.Unit/LmaxRealReadOnlyProviderClientsTests.cs'
Assert-True (Test-Path $testPath) 'Provider client unit test file missing.'
$testText = Get-Content $testPath -Raw
Assert-True ($testText.Contains('MarketData_client_sanitizer_preserves_request_write_read_and_classification_state_fields')) 'R178 provider-client sanitizer test missing.'

Assert-True ($tests.rejectClassificationCannotEmitAllExplicitStateFieldsFalse -eq $true) 'Final artifact emission test does not guard all-false reject evidence.'
Assert-True ($tests.observedMarketDataResponseCategoryForcesResponseReadAndClassificationConsistency -eq $true) 'Response/category consistency test evidence missing.'
Assert-True ($tests.preExternalBlockedPathKeepsAllFieldsFalseAndNoClassification -eq $true) 'Pre-external blocked path test evidence missing.'
Assert-True ($tests.missingWriterEvidenceRepresentedAsUnknownOrDerivedRatherThanSilentFalse -eq $true) 'Unknown/derived writer-state test evidence missing.'
Assert-True ($tests.rawSensitiveValuesAbsent -eq $true) 'Raw sensitive value test evidence missing.'

Assert-True ($resolution.r177HistoricalEvidencePreserved -eq $true) 'R177 historical evidence preservation missing.'
Assert-True ($resolution.r177ContradictionResolvedForFutureRuns -eq $true) 'R177 future inconsistency resolution missing.'

Assert-False $decision.liveRetryAllowedBeforeR179ReadinessGate 'Decision gate allows live retry before R179.'
Assert-True ($decision.nextPhase -eq 'LMAX-R179') 'Decision gate next phase mismatch.'
Assert-True ($next.recommendedNextPhase -eq 'LMAX-R179') 'Next phase recommendation mismatch.'
Assert-True ($next.r179MustRemainNoExternal -eq $true) 'R179 no-external requirement missing.'
Assert-False $next.liveRetryRecommendedNow 'Next phase recommends a live retry too early.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawFixSerialized -eq $false) 'Raw FIX serialization risk.'
Assert-True ($sanitization.rawRejectTextSerialized -eq $false) 'Raw reject text serialization risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false) 'Raw credential serialization risk.'
Assert-True ($sanitization.rawEndpointSerialized -eq $false) 'Raw endpoint serialization risk.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false) 'Raw TLS serialization risk.'
Assert-True ($sanitization.rawSessionIdentifiersSerialized -eq $false) 'Raw session serialization risk.'
Assert-True ($sanitization.rawCompIdsSerialized -eq $false) 'Raw CompID serialization risk.'
Assert-True ($forbidden.externalActivationAttempted -eq $false) 'External activation attempted in R178.'
Assert-True ($forbidden.socketOpened -eq $false) 'Socket opened in R178.'
Assert-True ($forbidden.tlsOpened -eq $false) 'TLS opened in R178.'
Assert-True ($forbidden.fixOpened -eq $false) 'FIX opened in R178.'
Assert-True ($forbidden.marketDataRuntimeActionPerformed -eq $false) 'MarketData runtime action performed in R178.'
Assert-True ($forbidden.ordersIntroduced -eq $false) 'Order path introduced.'
Assert-True ($forbidden.tradingEnabled -eq $false) 'Trading enablement introduced.'
Assert-True ($forbidden.schedulerIntroduced -eq $false) 'Scheduler introduced.'
Assert-True ($forbidden.pollingLoopIntroduced -eq $false) 'Polling loop introduced.'
Assert-True ($forbidden.serviceIntroduced -eq $false) 'Service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false) 'Replay introduced.'
Assert-True ($forbidden.shadowReplayIntroduced -eq $false) 'Shadow replay introduced.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regression.'
Assert-True ($apiWorker.apiStartupAttempted -eq $false) 'API startup attempted.'
Assert-True ($apiWorker.workerStartupAttempted -eq $false) 'Worker startup attempted.'

Assert-True ($gate.buildResult -like 'PASS*') 'Build evidence missing or failed.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing or failed.'
Assert-True ($gate.unitTests -like 'PASS*') 'Unit test evidence missing or failed.'
Assert-True ($gate.validatorResult -eq 'LMAX_R178_VALIDATION_PASS') 'Validator evidence missing or failed.'

$artifactText = Get-ChildItem $ArtifactRoot -Filter 'phase-lmax-r178-*' -File |
    Where-Object { $_.Name -notlike '*operator-approval*' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = [string]::Join("`n", $artifactText)
foreach ($forbiddenText in @(
    '8=FIX',
    '35=',
    '49=',
    '56=',
    '553=',
    '554=')) {
    Assert-False ($joined.Contains($forbiddenText)) "Forbidden raw/sensitive serialization marker found: $forbiddenText"
}

Write-Host 'LMAX_R178_VALIDATION_PASS'

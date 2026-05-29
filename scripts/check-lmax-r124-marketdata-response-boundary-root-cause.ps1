param(
    [string]$ArtifactRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path $Path)) {
        Fail "Missing artifact: $Path"
    }

    Get-Content $Path -Raw | ConvertFrom-Json
}

function Assert-True($Value, [string]$Message) {
    if ($Value -ne $true) {
        Fail $Message
    }
}

function Assert-False($Value, [string]$Message) {
    if ($Value -ne $false) {
        Fail $Message
    }
}

$required = @(
    "phase-lmax-r124-marketdata-response-boundary-root-cause-report.md",
    "phase-lmax-r124-marketdata-response-boundary-root-cause-summary.json",
    "phase-lmax-r124-r123-boundary-before-after-classification.json",
    "phase-lmax-r124-r123-marketdata-request-review.json",
    "phase-lmax-r124-marketdata-request-send-vs-snapshot-semantics-review.json",
    "phase-lmax-r124-marketdata-response-entries-not-observed-root-cause.json",
    "phase-lmax-r124-marketdata-response-reader-review.json",
    "phase-lmax-r124-marketdata-response-parser-classifier-review.json",
    "phase-lmax-r124-marketdata-bounded-read-wait-review.json",
    "phase-lmax-r124-approved-instrument-response-review.json",
    "phase-lmax-r124-usdjpy-caveat-preservation.json",
    "phase-lmax-r124-marketdata-readonly-safety-review.json",
    "phase-lmax-r124-real-bounded-path-validation.json",
    "phase-lmax-r124-no-external-boundary-attempted.json",
    "phase-lmax-r124-forbidden-actions-audit.json",
    "phase-lmax-r124-api-worker-fake-gateway-audit.json",
    "phase-lmax-r124-no-scheduler-polling-service-audit.json",
    "phase-lmax-r124-credential-endpoint-tls-fix-sanitization-validation.json",
    "phase-lmax-r124-next-phase-recommendation.json",
    "phase-lmax-r124-gate-validation.json"
)

foreach ($name in $required) {
    $path = Join-Path $ArtifactRoot $name
    if (-not (Test-Path $path)) {
        Fail "Required R124 artifact missing: $name"
    }
}

$summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-marketdata-response-boundary-root-cause-summary.json")
$beforeAfter = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-r123-boundary-before-after-classification.json")
$semantics = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-marketdata-request-send-vs-snapshot-semantics-review.json")
$rootCause = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-marketdata-response-entries-not-observed-root-cause.json")
$reader = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-marketdata-response-reader-review.json")
$parser = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-marketdata-response-parser-classifier-review.json")
$bounded = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-marketdata-bounded-read-wait-review.json")
$instruments = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-approved-instrument-response-review.json")
$usdjpy = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-usdjpy-caveat-preservation.json")
$safety = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-marketdata-readonly-safety-review.json")
$noExternal = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-api-worker-fake-gateway-audit.json")
$noScheduler = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-no-scheduler-polling-service-audit.json")
$sanitization = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-credential-endpoint-tls-fix-sanitization-validation.json")
$next = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-next-phase-recommendation.json")
$gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r124-gate-validation.json")

$r123Response = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r123-marketdata-response-evidence.json")
$r123Request = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r123-marketdata-request-evidence.json")

if ($summary.classification -ne "LMAX_R124_PASS_MARKETDATA_RESPONSE_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION") {
    Fail "Unexpected R124 classification: $($summary.classification)"
}

Assert-True $summary.reviewOnly "R124 must be review-only."
Assert-False $summary.externalActivationAttempted "R124 attempted an external activation."
Assert-True $summary.r123FixLogonAcknowledgementSuccessProven "R123 FIX acknowledgement success was not proven."
Assert-True $summary.r123MarketDataRequestSuccessProven "R123 MarketDataRequest success was not proven."
Assert-True $summary.marketDataRequestSentFalseNuanceReviewed "marketDataRequestSent=false nuance was not reviewed."
Assert-False $summary.marketDataResponseEntriesObserved "R124 summary must acknowledge no entries observed."

Assert-False $r123Response.marketDataResponseEntriesObserved "R123 response evidence should show no entries observed."
if ($r123Response.marketDataResponseCategory -ne "MarketDataResponseEntriesNotObserved") {
    Fail "R123 MarketDataResponseEntriesNotObserved evidence missing."
}
Assert-True $r123Request.marketDataRequestAttempted "R123 MarketDataRequest operation evidence missing."
if ($r123Request.marketDataRequestResult -ne "Succeeded") {
    Fail "R123 MarketDataRequest success evidence missing."
}

Assert-True $semantics.marketDataRequestSentFalseNuanceReviewed "Send-vs-snapshot semantics review missing."
Assert-True $semantics.operationLevelRequestAttempted "Operation-level request attempt not acknowledged."
if ($semantics.transportBoundaryStatus -ne "FakeSucceeded") {
    Fail "FakeSucceeded semantics were not represented."
}
Assert-False $semantics.cliMarketDataRequestSentFlag "CLI marketDataRequestSent=false nuance not represented."

Assert-True $rootCause.marketDataResponseEntriesNotObservedAcknowledged "MarketDataResponseEntriesNotObserved not acknowledged."
Assert-False $rootCause.responseReadAttempted "R124 must not claim response read was attempted."
Assert-False $rootCause.responseParserClassifierUsed "R124 must not claim parser/classifier was used."
Assert-False $rootCause.boundedWaitConfigured "R124 must identify missing bounded wait."

Assert-False $reader.approvedManualRealBoundedMarketDataResponseReaderExists "Approved reader should be identified as missing."
Assert-False $parser.approvedManualRealBoundedParserClassifierExists "Approved parser/classifier should be identified as missing."
Assert-False $bounded.boundedReadWaitExistsInApprovedPath "Approved bounded read/wait should be identified as missing."

$expectedInstruments = @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")
$actualInstruments = @($instruments.approvedInstruments)
if (@(Compare-Object $expectedInstruments $actualInstruments).Count -ne 0) {
    Fail "Approved instrument scope differs from GBPUSD/EURGBP/AUDUSD/USDJPY."
}
Assert-True $instruments.nonApprovedInstrumentsRejected "Non-approved instruments must remain rejected."
Assert-True $instruments.usdJpySecurityIdPreserved "USDJPY SecurityID was not preserved."
Assert-True $instruments.usdJpySecurityIdSourcePreserved "USDJPY SecurityIDSource was not preserved."
Assert-True $instruments.usdJpyCaveatPreserved "USDJPY caveat was not preserved."
Assert-True $usdjpy.caveatPreserved "USDJPY caveat artifact missing or weakened."
Assert-False $usdjpy.weakened "USDJPY caveat weakened."

Assert-True $safety.marketDataReadOnlyOnly "MarketData path must remain read-only."
Assert-False $safety.orderMessagesSupported "Order messages were introduced."
Assert-False $safety.newOrderSingleSupported "NewOrderSingle was introduced."
Assert-False $safety.cancelReplaceSupported "Cancel/replace was introduced."
Assert-False $safety.executionReportFillOrderLifecycleParsingIntroduced "Execution report/fill/order lifecycle parsing introduced."
Assert-False $safety.tradingStateMutationIntroduced "Trading state mutation introduced."

Assert-False $noExternal.externalActivationAttempted "External boundary attempted during R124."
Assert-False $noExternal.tcpSocketAttempted "TCP attempted during R124."
Assert-False $noExternal.tlsAttempted "TLS attempted during R124."
Assert-False $noExternal.fixLogonAttempted "FIX attempted during R124."
Assert-False $noExternal.marketDataRequestAttempted "MarketDataRequest attempted during R124."
Assert-False $noExternal.marketDataResponseReadAttempted "MarketDataResponse read attempted during R124."

if ($forbidden.result -ne "PASS") { Fail "Forbidden action audit did not pass." }
Assert-False $forbidden.ordersIntroduced "Orders introduced."
Assert-False $forbidden.schedulerPollingIntroduced "Scheduler/polling introduced."
Assert-False $forbidden.replayIntroduced "Replay introduced."
Assert-False $forbidden.shadowReplayIntroduced "Shadow replay introduced."
Assert-False $forbidden.externalBoundaryAttemptedDuringR124 "External boundary attempted according to forbidden audit."

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGateway -ne "FakeLmaxGatewayOnly") {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}
Assert-False $noScheduler.schedulerIntroduced "Scheduler introduced."
Assert-False $noScheduler.pollingLoopIntroduced "Polling loop introduced."

Assert-False $sanitization.credentialValuesReturned "credentialValuesReturned must be false."
Assert-False $sanitization.rawCredentialsSerialized "Raw credentials serialized."
Assert-False $sanitization.rawEndpointValuesSerialized "Raw endpoint values serialized."
Assert-False $sanitization.rawTlsMaterialSerialized "Raw TLS material serialized."
Assert-False $sanitization.rawFixSerialized "Raw FIX serialized."
Assert-False $sanitization.rawCompIdSessionValuesSerialized "Raw CompID/session values serialized."
Assert-False $sanitization.productionAccountAllowed "Production account/config allowed."

if ([string]::IsNullOrWhiteSpace($next.nextRecommendedPhase)) {
    Fail "Next-phase recommendation absent."
}
if ($next.nextRecommendedPhase -notmatch "LMAX-R125") {
    Fail "Next phase must recommend LMAX-R125."
}

if ($gate.buildEvidence -notmatch "PASS") {
    Fail "Build evidence missing."
}
if ($gate.testEvidence.unitTests -notmatch "PASS" -or $gate.testEvidence.integrationTests -notmatch "PASS") {
    Fail "Test evidence missing."
}

$artifactText = Get-ChildItem $ArtifactRoot -Filter "phase-lmax-r124-*" -File |
    ForEach-Object { Get-Content $_.FullName -Raw }

$forbiddenPatterns = @(
    "password\s*=",
    "username\s*=",
    "553\s*=",
    "554\s*=",
    "35\s*=",
    "49\s*=",
    "56\s*=",
    "BEGIN CERTIFICATE",
    "PRIVATE KEY",
    "RawFix\s"
)

foreach ($pattern in $forbiddenPatterns) {
    if ($artifactText -match $pattern) {
        Fail "Sensitive or forbidden raw value pattern found in R124 artifacts: $pattern"
    }
}

Write-Output "LMAX_R124_VALIDATION_PASS"

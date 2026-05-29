$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts/readiness/lmax-runtime-enablement'

function Read-Json($path) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required artifact: $path"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Require-True($value, $message) {
    if ($value -ne $true) { throw $message }
}

function Require-False($value, $message) {
    if ($value -ne $false) { throw $message }
}

function Require-Equal($actual, $expected, $message) {
    if ($actual -ne $expected) {
        throw "$message Expected '$expected' but found '$actual'."
    }
}

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-workspace-operator-approval-summary.json')
$carry = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-r103-preflight-carryforward-review.json')
$approval = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-future-approval-requirement.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-api-worker-fake-gateway-audit.json')
$noLive = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-no-live-default-validation.json')
$instruments = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-approved-instrument-scope-validation.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-usdjpy-caveat-preservation.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-secret-sanitization-requirements.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-no-external-boundary-attempted.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r104-gate-validation.json')
$r103 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-qq-workspace-preflight-summary.json')

$allowedClassifications = @(
    'LMAX_R104_PASS_WORKSPACE_OPERATOR_APPROVAL_PACKAGE_READY_NO_EXTERNAL_ACTIVATION',
    'LMAX_R104_DECISION_READY_FOR_R105_OPERATOR_APPROVAL_NO_EXTERNAL_ACTIVATION',
    'LMAX_R104_DECISION_NEED_ADDITIONAL_WORKSPACE_PREFLIGHT_NO_EXTERNAL_ACTIVATION'
)

if ($allowedClassifications -notcontains $summary.classification) {
    throw "Unexpected R104 classification: $($summary.classification)"
}

Require-Equal $r103.classification 'LMAX_R103_PASS_QQ_WORKSPACE_PREFLIGHT_EVIDENCE_PACK_READY_NO_EXTERNAL_ACTIVATION' 'R103 success evidence missing or mismatched.'
Require-Equal $summary.classification 'LMAX_R104_PASS_WORKSPACE_OPERATOR_APPROVAL_PACKAGE_READY_NO_EXTERNAL_ACTIVATION' 'R104 classification mismatch.'
Require-False $summary.activationPerformed 'R104 performed activation.'
Require-False $summary.tcpTlsFixMarketDataBoundaryAttempted 'R104 attempted external boundary.'
Require-Equal $summary.approvalPackageStatus 'READY' 'Approval package status mismatch.'
Require-True $summary.futureApprovalPhraseCreated 'Future approval phrase missing.'
Require-False $summary.futureApprovalTreatedAsGranted 'Future approval treated as already granted.'
Require-False $summary.priorApprovalsReusable 'Prior approvals reusable.'
Require-False $summary.r104AuthorizesActivation 'R104 authorizes activation.'
Require-True $summary.futureActivationRequiresFreshExplicitApproval 'Fresh approval requirement missing.'
Require-Equal $summary.futureActivationPhase 'LMAX-R105' 'Future activation phase must be R105.'
Require-Equal $summary.forbiddenActionsAudit 'PASS' 'Forbidden actions audit failed.'
Require-Equal $summary.apiWorkerFakeGatewayAudit 'PASS' 'API/Worker audit failed.'
Require-Equal $summary.noLiveDefaultValidation 'PASS' 'No-live-default validation failed.'
Require-Equal $summary.approvedInstrumentScopeValidation 'PASS' 'Approved instrument validation failed.'
Require-True $summary.usdJpyCaveatPreserved 'USDJPY caveat missing.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must be false.'
Require-False $summary.sensitiveValuesPrintedStoredSerialized 'Sensitive values serialized.'

Require-Equal $carry.r103Classification 'LMAX_R103_PASS_QQ_WORKSPACE_PREFLIGHT_EVIDENCE_PACK_READY_NO_EXTERNAL_ACTIVATION' 'R103 carryforward classification mismatch.'
Require-True $carry.r103SuccessEvidencePresent 'R103 success evidence missing.'
Require-True $carry.r103PreflightDecisionCarriedForward 'R103 decision not carried forward.'
Require-True $carry.cliToolReadinessCarriedForward 'CLI readiness not carried forward.'
Require-True $carry.demoEndpointSanitizedReadinessCarriedForward 'Demo endpoint readiness not carried forward.'
Require-True $carry.credentialSourceSanitizedReadinessCarriedForward 'Credential readiness not carried forward.'
Require-True $carry.apiWorkerFakeGatewayAuditCarriedForward 'API/Worker audit not carried forward.'
Require-True $carry.noLiveDefaultValidationCarriedForward 'No-live default not carried forward.'
Require-False $carry.activationPerformedInR104 'Activation performed in R104.'

Require-True $approval.futureApprovalPhraseCreated 'Future approval phrase missing.'
Require-False $approval.futureApprovalTreatedAsGranted 'Future approval treated as granted.'
Require-False $approval.priorApprovalsReusable 'Prior approvals reusable.'
Require-True $approval.futureActivationRequiresFreshExplicitApproval 'Future fresh approval missing.'
Require-Equal $approval.approvalMustBeProvidedInLaterPhase 'LMAX-R105' 'Approval phase mismatch.'
Require-True $approval.approvalMustBeExact 'Exact approval requirement missing.'
Require-True $approval.approvalMustNameQqWorkspace 'QQ Workspace approval requirement missing.'
Require-True $approval.approvalMustPreserveUsdJpyCaveat 'USDJPY caveat approval requirement missing.'
Require-False $approval.r104AuthorizesActivation 'R104 authorizes activation.'

Require-Equal $forbidden.result 'PASS' 'Forbidden action audit failed.'
Require-False $forbidden.activationPerformed 'Activation performed.'
Require-False $forbidden.socketOpened 'Socket opened.'
Require-False $forbidden.tlsAttempted 'TLS attempted.'
Require-False $forbidden.fixAttempted 'FIX attempted.'
Require-False $forbidden.marketDataRequestAttempted 'MarketDataRequest attempted.'
Require-False $forbidden.ordersSubmitted 'Orders submitted.'
Require-False $forbidden.newOrderSingleUsed 'NewOrderSingle used.'
Require-False $forbidden.cancelReplaceUsed 'Cancel/replace used.'
Require-False $forbidden.tradingEnabled 'Trading enabled.'
Require-False $forbidden.tradingStateMutated 'Trading state mutated.'
Require-False $forbidden.productionAccountAllowed 'Production account/config allowed.'
Require-False $forbidden.productionAccountUsed 'Production account used.'
Require-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Require-False $forbidden.pollingIntroduced 'Polling introduced.'
Require-False $forbidden.replayIntroduced 'Replay introduced.'
Require-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'

Require-Equal $apiWorker.result 'PASS' 'API/Worker audit failed.'
Require-True $apiWorker.apiWorkerFakeLmaxGatewayOnly 'API/Worker changed away from FakeLmaxGatewayOnly.'
Require-False $apiWorker.apiWorkerGatewayChanged 'API/Worker gateway changed.'
Require-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup 'Manual CLI reachable from API/Worker default startup.'
Require-False $apiWorker.realAdapterGlobalDefault 'Real adapter global/default.'

Require-Equal $noLive.result 'PASS' 'No-live-default validation failed.'
Require-True $noLive.noExternalDefaultPreserved 'No-external default not preserved.'
Require-False $noLive.liveGatewayDefaultIntroduced 'Live gateway default introduced.'
Require-False $noLive.realAdapterDefaultIntroduced 'Real adapter default introduced.'
Require-False $noLive.appsettingsLiveEnablementIntroduced 'Appsettings live enablement introduced.'
Require-False $noLive.manualCliDefaultStartupIntroduced 'Manual CLI default startup introduced.'
Require-False $noLive.schedulerOrHostedServiceIntroduced 'Scheduler/hosted service introduced.'

$expectedInstruments = @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY')
Require-Equal $instruments.result 'PASS' 'Instrument validation failed.'
if (@($instruments.approvedInstruments).Count -ne 4) {
    throw 'Approved instrument count mismatch.'
}
foreach ($instrument in $expectedInstruments) {
    if ($instruments.approvedInstruments -notcontains $instrument) {
        throw "Approved instrument missing: $instrument"
    }
}
Require-False $instruments.approvedInstrumentScopeChanged 'Approved instrument scope changed.'
Require-False $instruments.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'

Require-True $usdJpy.caveatPreserved 'USDJPY caveat not preserved.'
Require-Equal $usdJpy.securityId '4004' 'USDJPY SecurityID mismatch.'
Require-Equal $usdJpy.securityIdSource '8' 'USDJPY SecurityIDSource mismatch.'
Require-Equal $usdJpy.caveat 'prior failed-safe root cause remains unproven' 'USDJPY caveat weakened.'

Require-Equal $sanitize.result 'PASS' 'Secret sanitization failed.'
Require-False $sanitize.credentialValuesReturned 'Credential values returned.'
Require-False $sanitize.rawCredentialsSerialized 'Raw credentials serialized.'
Require-False $sanitize.rawEndpointValuesSerialized 'Raw endpoint values serialized.'
Require-False $sanitize.rawTlsMaterialSerialized 'Raw TLS material serialized.'
Require-False $sanitize.rawCertificateDetailsSerialized 'Raw certificate details serialized.'
Require-False $sanitize.rawFixMessagesSerialized 'Raw FIX messages serialized.'
Require-False $sanitize.rawSensitiveFixLogsSerialized 'Raw sensitive FIX logs serialized.'
Require-False $sanitize.rawAccountIdentifiersSerialized 'Raw account identifiers serialized.'
Require-False $sanitize.credentialDerivedValuesSerialized 'Credential-derived values serialized.'
Require-True $sanitize.sanitizedOutputOnlyRequiredForFutureActivation 'Sanitized output requirement missing.'

Require-False $noExternal.activationPerformed 'Activation performed.'
Require-False $noExternal.manualCliActivationRun 'Manual CLI activation run.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted.'
Require-False $noExternal.tlsAttempted 'TLS attempted.'
Require-False $noExternal.fixLogonAttempted 'FIX attempted.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted.'
Require-False $noExternal.futureApprovalPhraseTreatedAsGranted 'Future approval phrase treated as granted.'

Require-Equal $next.nextRecommendedPhase 'LMAX-R105' 'Next phase must be R105.'
Require-Equal $next.nextRecommendedTitle 'Operator-Approved Single Temporary QQ Workspace Demo Read-Only Activation Retry' 'Next phase title mismatch.'
Require-True $next.nextPhaseMayActivateOnlyAfterFreshExactApproval 'R105 approval requirement missing.'
Require-True $next.nextPhaseRequiresPhilippeApprovalPhrase 'Philippe approval requirement missing.'
Require-True $next.r104DoesNotAuthorizeActivation 'R104 authorization block missing.'
Require-True $next.activationRetryPhaseIsOdd 'Odd activation phase requirement missing.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.r103SuccessEvidencePresent 'R103 evidence missing from gate.'
Require-True $gate.r103ClassificationMatches 'R103 classification mismatch in gate.'
Require-True $gate.approvalPackageReportPresent 'Approval package report missing.'
Require-True $gate.futureApprovalPhrasePresent 'Future approval phrase missing.'
Require-False $gate.futureApprovalPhraseTreatedAsGranted 'Future approval phrase treated as granted in gate.'
Require-False $gate.r104AuthorizesActivation 'R104 authorizes activation in gate.'
Require-True $gate.nextPhaseIsR105 'Next phase is not R105.'
Require-False $gate.priorApprovalsReusable 'Prior approvals reusable in gate.'
Require-True $gate.noExternalBoundaryAttempted 'External boundary attempted during R104.'
Require-True $gate.forbiddenActionsAuditPass 'Forbidden actions audit missing/failing.'
Require-True $gate.apiWorkerFakeGatewayAuditPass 'API/Worker audit missing/failing.'
Require-True $gate.noLiveDefaultValidationPass 'No-live-default validation missing/failing.'
Require-True $gate.approvedInstrumentScopeValidationPass 'Instrument validation missing/failing.'
Require-True $gate.usdJpyCaveatPreserved 'USDJPY caveat gate missing.'
Require-True $gate.secretSanitizationPass 'Secret sanitization gate missing.'
Require-True $gate.buildEvidencePresent 'Build evidence missing.'
Require-True $gate.testEvidencePresent 'Test evidence missing.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing.'

$phrase = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r104-future-approval-phrase.md') -Raw
if ($phrase -notmatch 'I, Philippe, explicitly approve Phase LMAX-R105') {
    throw 'Future approval phrase missing exact R105 approval text.'
}
if ($phrase -notmatch 'not approved in R104' -or $phrase -notmatch 'Do not execute this in R104') {
    throw 'Future approval phrase is not clearly marked as not granted.'
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r104-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }

$forbiddenPatterns = @(
    'password\s*[:=]',
    'username\s*[:=]',
    'session\s*token',
    'sendercompid\s*[:=]',
    'targetcompid\s*[:=]',
    '35=A',
    '8=FIX',
    '10=\d{3}',
    'BEGIN CERTIFICATE',
    'END CERTIFICATE',
    'subject=',
    'issuer=',
    'private key',
    'fix-marketdata',
    'london-demo',
    'lmax\.com'
)

foreach ($pattern in $forbiddenPatterns) {
    if ($artifactText -match $pattern) {
        throw "Forbidden sensitive endpoint, TLS, credential, or FIX pattern found in R104 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R104_VALIDATION_PASS'

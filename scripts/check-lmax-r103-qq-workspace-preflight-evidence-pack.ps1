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

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-qq-workspace-preflight-summary.json')
$carry = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-r102-carryforward-review.json')
$build = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-workspace-build-test-readiness.json')
$cli = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-cli-tool-build-readiness.json')
$artifactDir = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-artifact-directory-readiness.json')
$endpoint = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-demo-endpoint-sanitized-readiness.json')
$credential = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-credential-source-sanitized-readiness.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-api-worker-fake-gateway-audit.json')
$noLive = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-no-live-default-validation.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-forbidden-actions-audit.json')
$instruments = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-approved-instrument-scope-validation.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-usdjpy-caveat-preservation.json')
$approval = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-operator-approval-requirement-review.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-no-external-boundary-attempted.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-secret-sanitization-validation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r103-gate-validation.json')
$r102 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-qq-workspace-preparation-summary.json')

$allowedClassifications = @(
    'LMAX_R103_PASS_QQ_WORKSPACE_PREFLIGHT_EVIDENCE_PACK_READY_NO_EXTERNAL_ACTIVATION',
    'LMAX_R103_DECISION_READY_FOR_WORKSPACE_OPERATOR_APPROVAL_PREP_NO_EXTERNAL_ACTIVATION',
    'LMAX_R103_DECISION_NEED_WORKSPACE_CONFIG_FIX_NO_EXTERNAL_ACTIVATION',
    'LMAX_R103_DECISION_NEED_WORKSPACE_SECRET_SOURCE_FIX_NO_EXTERNAL_ACTIVATION',
    'LMAX_R103_DECISION_NEED_WORKSPACE_BUILD_TEST_FIX_NO_EXTERNAL_ACTIVATION'
)

if ($allowedClassifications -notcontains $summary.classification) {
    throw "Unexpected R103 classification: $($summary.classification)"
}

Require-Equal $r102.classification 'LMAX_R102_PASS_QQ_WORKSPACE_DEPLOYMENT_CONNECTIVITY_PREPARATION_PACK_READY_NO_EXTERNAL_ACTIVATION' 'R102 success evidence missing or mismatched.'
Require-Equal $summary.classification 'LMAX_R103_PASS_QQ_WORKSPACE_PREFLIGHT_EVIDENCE_PACK_READY_NO_EXTERNAL_ACTIVATION' 'R103 classification mismatch.'
Require-False $summary.activationPerformed 'R103 performed activation.'
Require-False $summary.tcpTlsFixMarketDataBoundaryAttempted 'R103 attempted external boundary.'
Require-Equal $summary.workspacePreflightDecision 'ReadyForWorkspaceOperatorApprovalPackagePrep' 'Workspace preflight decision mismatch.'
Require-Equal $summary.buildTestReadiness 'PASS' 'Build/test readiness missing.'
Require-Equal $summary.cliToolReadiness 'PASS' 'CLI readiness missing.'
Require-Equal $summary.artifactDirectoryReadiness 'PASS' 'Artifact directory readiness missing.'
Require-Equal $summary.demoEndpointSanitizedReadiness 'PASS' 'Demo endpoint readiness missing.'
Require-Equal $summary.credentialSourceSanitizedReadiness 'PASS' 'Credential source readiness missing.'
Require-Equal $summary.apiWorkerFakeGatewayAudit 'PASS' 'API/Worker audit failed.'
Require-Equal $summary.noLiveDefaultValidation 'PASS' 'No-live-default validation failed.'
Require-Equal $summary.forbiddenActionsAudit 'PASS' 'Forbidden actions audit failed.'
Require-Equal $summary.approvedInstrumentScopeValidation 'PASS' 'Approved instrument validation failed.'
Require-True $summary.usdJpyCaveatPreserved 'USDJPY caveat missing.'
Require-True $summary.operatorApprovalRequirementPrepared 'Operator approval requirement missing.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must be false.'
Require-False $summary.sensitiveValuesPrintedStoredSerialized 'Sensitive values serialized.'

Require-Equal $carry.r102Classification 'LMAX_R102_PASS_QQ_WORKSPACE_DEPLOYMENT_CONNECTIVITY_PREPARATION_PACK_READY_NO_EXTERNAL_ACTIVATION' 'R102 carryforward classification mismatch.'
Require-True $carry.r102SuccessEvidencePresent 'R102 success evidence missing.'
Require-True $carry.r102DecisionCarriedForward 'R102 decision not carried forward.'
Require-True $carry.futureActivationRequiresFreshExplicitApproval 'Fresh approval carryforward missing.'
Require-False $carry.activationPerformedInR103 'Activation performed in R103.'

Require-Equal $build.workspaceBuildTestReadiness 'PASS' 'Workspace build/test readiness failed.'
Require-True $build.repositoryPathAvailable 'Repository path unavailable.'
Require-True $build.solutionAvailable 'Solution unavailable.'
Require-True $build.dotnetSdkRuntimeAvailable 'dotnet SDK/runtime unavailable.'
Require-True $build.dotnetBuildPasses 'dotnet build missing.'
Require-True $build.unitTestsPass 'Unit tests missing.'
Require-True $build.integrationTestsPass 'Integration tests missing.'
Require-False $build.activationPerformed 'Activation performed during build/test readiness.'

Require-Equal $cli.cliToolReadiness 'PASS' 'CLI readiness failed.'
Require-True $cli.cliProjectAvailable 'CLI project unavailable.'
Require-Equal $cli.requiredTool 'QQ.Production.Intraday.Tools.LmaxReadOnlyActivation' 'CLI tool mismatch.'
Require-Equal $cli.requiredAdapterMode 'real-bounded-executable-readonly' 'Adapter mode mismatch.'
Require-True $cli.cliBuildsWithSolution 'CLI does not build with solution.'
Require-False $cli.cliActivationExecuted 'CLI activation executed.'
Require-True $cli.futureUseRequiresFreshOperatorApproval 'Future CLI approval requirement missing.'

Require-Equal $artifactDir.artifactDirectoryReadiness 'PASS' 'Artifact directory readiness failed.'
Require-True $artifactDir.artifactDirectoryExists 'Artifact directory missing.'
Require-True $artifactDir.artifactDirectoryWritable 'Artifact directory not writable.'
Require-False $artifactDir.rawSensitiveArtifactsAllowed 'Raw sensitive artifacts allowed.'
Require-True $artifactDir.sanitizedEvidenceOnly 'Sanitized evidence only missing.'

Require-Equal $endpoint.demoEndpointSanitizedReadiness 'PASS' 'Demo endpoint readiness failed.'
Require-Equal $endpoint.endpointMode 'Demo' 'Endpoint mode mismatch.'
Require-True $endpoint.endpointPresent 'Endpoint not present.'
Require-True $endpoint.hostPresent 'Host not present.'
Require-True $endpoint.hostConcreteBinding 'Host concrete binding missing.'
Require-False $endpoint.hostWasPlaceholder 'Placeholder host used.'
Require-True $endpoint.portPresent 'Port not present.'
Require-True $endpoint.portConcreteBinding 'Port concrete binding missing.'
Require-True $endpoint.productionExcluded 'Production not excluded.'
Require-True $endpoint.endpointApproved 'Endpoint not approved.'
Require-False $endpoint.rawEndpointValuesSerialized 'Raw endpoint values serialized.'

Require-Equal $credential.credentialSourceSanitizedReadiness 'PASS' 'Credential source readiness failed.'
Require-True $credential.credentialSourcePresent 'Credential source missing.'
Require-True $credential.demoReadOnlyOnly 'Credential source not Demo/read-only.'
Require-False $credential.productionCredentialSourceAllowed 'Production credential source allowed.'
Require-True $credential.inMemoryOnlySecretUseRequired 'In-memory-only secret use missing.'
Require-False $credential.credentialValuesReturned 'Credential values returned.'
Require-False $credential.rawCredentialsSerialized 'Raw credentials serialized.'
Require-False $credential.rawAccountIdentifiersSerialized 'Raw account identifiers serialized.'
Require-False $credential.rawFixFieldsSerialized 'Raw FIX fields serialized.'

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
Require-False $noLive.schedulerOrHostedServiceIntroduced 'Scheduler or hosted service introduced.'

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

$expectedInstruments = @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY')
Require-Equal $instruments.result 'PASS' 'Approved instrument validation failed.'
if (@($instruments.approvedInstruments).Count -ne 4) {
    throw 'Approved instrument scope count mismatch.'
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

Require-True $approval.operatorApprovalRequirementPrepared 'Operator approval requirement missing.'
Require-True $approval.freshExplicitApprovalRequiredForFutureActivation 'Fresh approval requirement missing.'
Require-False $approval.priorApprovalsReusable 'Prior approvals reusable.'
Require-True $approval.approvalMustNameWorkspaceEnvironment 'Workspace environment approval requirement missing.'
Require-True $approval.approvalMustNameFutureActivationPhase 'Future phase approval requirement missing.'
Require-True $approval.approvalMustPreserveDemoReadOnlyNoOrdersNoTrading 'Read-only/no-orders approval requirement missing.'
Require-True $approval.approvalMustPreserveApprovedInstrumentScope 'Instrument scope approval requirement missing.'
Require-True $approval.approvalMustPreserveUsdJpyCaveat 'USDJPY caveat approval requirement missing.'
Require-False $approval.activationAuthorizedByR103 'R103 authorized activation.'

Require-False $noExternal.activationPerformed 'Activation performed.'
Require-False $noExternal.manualCliActivationRun 'Manual CLI activation run.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted.'
Require-False $noExternal.tlsAttempted 'TLS attempted.'
Require-False $noExternal.fixLogonAttempted 'FIX attempted.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted.'
Require-False $noExternal.pollingStarted 'Polling started.'
Require-False $noExternal.schedulerStarted 'Scheduler started.'

Require-Equal $sanitize.result 'PASS' 'Secret sanitization failed.'
Require-False $sanitize.credentialValuesReturned 'Credential values returned.'
Require-False $sanitize.rawCredentialsSerialized 'Raw credentials serialized.'
Require-False $sanitize.rawEndpointValuesSerialized 'Raw endpoint values serialized.'
Require-False $sanitize.rawTlsMaterialSerialized 'Raw TLS material serialized.'
Require-False $sanitize.rawCertificateDetailsSerialized 'Raw certificate details serialized.'
Require-False $sanitize.rawTlsExceptionDetailsSerialized 'Raw TLS exception details serialized.'
Require-False $sanitize.rawFixMessagesSerialized 'Raw FIX messages serialized.'
Require-False $sanitize.rawSensitiveFixLogsSerialized 'Raw sensitive FIX logs serialized.'
Require-False $sanitize.fullAccountIdentifiersSerialized 'Full account identifiers serialized.'
Require-False $sanitize.credentialDerivedValuesSerialized 'Credential-derived values serialized.'

Require-Equal $next.nextRecommendedPhase 'LMAX-R104' 'Next phase recommendation missing.'
Require-Equal $next.nextRecommendedTitle 'QQ Workspace Operator Approval Package for First Workspace Read-Only Activation Retry' 'Next phase title mismatch.'
Require-False $next.nextPhaseShouldPerformActivation 'R104 should not perform activation.'
Require-True $next.activationRetryShouldUseLaterOddNumberedApprovedPhase 'Later odd activation phase requirement missing.'
Require-True $next.futureActivationRequiresFreshExplicitApproval 'Future activation approval requirement missing.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.r102SuccessEvidencePresent 'R102 evidence missing from gate.'
Require-True $gate.r102ClassificationMatches 'R102 classification mismatch in gate.'
Require-True $gate.workspacePreflightReportPresent 'Workspace preflight report missing from gate.'
Require-True $gate.buildTestReadinessPresent 'Build/test readiness missing from gate.'
Require-True $gate.cliReadinessPresent 'CLI readiness missing from gate.'
Require-True $gate.artifactDirectoryReadinessPresent 'Artifact directory readiness missing from gate.'
Require-True $gate.demoEndpointSanitizedReadinessPresent 'Demo endpoint readiness missing from gate.'
Require-True $gate.credentialSourceSanitizedReadinessPresent 'Credential readiness missing from gate.'
Require-True $gate.apiWorkerFakeGatewayAuditPass 'API/Worker audit missing/failing in gate.'
Require-True $gate.noLiveDefaultValidationPass 'No-live-default validation missing/failing in gate.'
Require-True $gate.forbiddenActionsAuditPass 'Forbidden audit missing/failing in gate.'
Require-True $gate.approvedInstrumentScopeValidationPass 'Instrument scope validation missing/failing in gate.'
Require-True $gate.usdJpyCaveatPreserved 'USDJPY caveat gate missing.'
Require-True $gate.operatorApprovalRequirementPresent 'Operator approval gate missing.'
Require-True $gate.noExternalBoundaryAttempted 'External boundary attempted during R103.'
Require-True $gate.secretSanitizationValidationPass 'Secret sanitization gate missing/failing.'
Require-True $gate.buildEvidencePresent 'Build evidence missing.'
Require-True $gate.testEvidencePresent 'Test evidence missing.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r103-*' -File |
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
        throw "Forbidden sensitive endpoint, TLS, credential, or FIX pattern found in R103 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R103_VALIDATION_PASS'

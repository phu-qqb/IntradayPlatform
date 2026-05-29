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

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-qq-workspace-preparation-summary.json')
$carry = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-local-evidence-carryforward-review.json')
$codePath = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-workspace-code-path-readiness.json')
$cli = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-workspace-cli-tool-readiness.json')
$config = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-workspace-config-prerequisites.json')
$endpoint = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-demo-endpoint-binding-prerequisites.json')
$credential = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-credential-source-prerequisites.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-api-worker-fake-gateway-audit.json')
$noLive = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-no-live-default-validation.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-forbidden-actions-audit.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-secret-sanitization-requirements.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r102-gate-validation.json')
$r101 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-workspace-tls-environment-readiness-summary.json')

$allowedClassifications = @(
    'LMAX_R102_PASS_QQ_WORKSPACE_DEPLOYMENT_CONNECTIVITY_PREPARATION_PACK_READY_NO_EXTERNAL_ACTIVATION',
    'LMAX_R102_DECISION_READY_FOR_WORKSPACE_PREFLIGHT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R102_DECISION_NEED_WORKSPACE_CONFIG_SETUP_NO_EXTERNAL_ACTIVATION',
    'LMAX_R102_DECISION_NEED_WORKSPACE_SECRET_SOURCE_SETUP_NO_EXTERNAL_ACTIVATION'
)

if ($allowedClassifications -notcontains $summary.classification) {
    throw "Unexpected R102 classification: $($summary.classification)"
}

Require-Equal $r101.classification 'LMAX_R101_PASS_WORKSPACE_TLS_ENVIRONMENT_READINESS_REVIEW_NO_EXTERNAL_ACTIVATION' 'R101 success evidence missing or mismatched.'
Require-Equal $summary.classification 'LMAX_R102_PASS_QQ_WORKSPACE_DEPLOYMENT_CONNECTIVITY_PREPARATION_PACK_READY_NO_EXTERNAL_ACTIVATION' 'R102 classification mismatch.'
Require-False $summary.activationPerformed 'R102 performed activation.'
Require-False $summary.tcpTlsFixMarketDataBoundaryAttempted 'R102 attempted an external boundary.'
Require-Equal $summary.workspacePreparationDecision 'ReadyForWorkspacePreflightEvidencePack' 'Workspace preparation decision mismatch.'
Require-True $summary.localEvidenceCarryforwardComplete 'Local evidence carryforward missing.'
Require-True $summary.workspaceCodePathReadinessPrepared 'Code path readiness missing.'
Require-True $summary.workspaceCliToolReadinessPrepared 'CLI readiness missing.'
Require-True $summary.workspaceConfigPrerequisitesPrepared 'Config prerequisites missing.'
Require-True $summary.demoEndpointPrerequisitesPrepared 'Demo endpoint prerequisites missing.'
Require-True $summary.credentialSourcePrerequisitesPrepared 'Credential source prerequisites missing.'
Require-Equal $summary.apiWorkerFakeGatewayAudit 'PASS' 'API/Worker audit not pass.'
Require-Equal $summary.noLiveDefaultValidation 'PASS' 'No-live-default validation not pass.'
Require-Equal $summary.forbiddenActionsAudit 'PASS' 'Forbidden actions audit not pass.'
Require-True $summary.secretSanitizationRequirementsPrepared 'Secret sanitization requirements missing.'
Require-True $summary.workspacePreflightChecklistCreated 'Workspace preflight checklist missing.'
Require-True $summary.operatorApprovalRequirementCreated 'Operator approval requirement missing.'
Require-True $summary.futureActivationRequiresFreshExplicitApproval 'Future activation approval requirement missing.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must be false.'

Require-True $carry.localEvidenceCarryforwardComplete 'Carryforward review missing.'
Require-True $carry.manualCliExistsAndApproved 'Manual CLI evidence missing.'
Require-True $carry.realBoundedExecutableReadOnlyAdapterPathExists 'Adapter path evidence missing.'
Require-True $carry.concreteDemoEndpointBindingExists 'Demo endpoint evidence missing.'
Require-True $carry.socketConnectorExists 'Socket connector evidence missing.'
Require-True $carry.tcpSocketSuccessProvenRepeatedly 'TCP success evidence missing.'
Require-True $carry.tlsBoundaryReached 'TLS boundary evidence missing.'
Require-True $carry.tlsSuccessProvenInR81R85 'R81/R85 TLS success evidence missing.'
Require-True $carry.laterLocalHandshakeExceptionEvidenceRecorded 'Later local HandshakeException evidence missing.'
Require-True $carry.fixCredentialMaterialBindingExists 'FIX credential material binding missing.'
Require-True $carry.fixSessionLogonOnlyFrameWriterExists 'FIX frame writer evidence missing.'
Require-True $carry.apiWorkerRemainFakeLmaxGatewayOnly 'API/Worker fake gateway carryforward missing.'
Require-False $carry.orderTradingPathIntroduced 'Order/trading path introduced.'

Require-True $codePath.workspaceCodePathReadinessPrepared 'Code path readiness missing.'
Require-True $codePath.apiWorkerStartupMustNotUseManualPath 'API/Worker manual path restriction missing.'
Require-False $codePath.defaultLiveEnablementAllowed 'Default live enablement allowed.'
Require-False $codePath.ordersTradingPathAllowed 'Orders/trading path allowed.'

Require-True $cli.workspaceCliToolReadinessPrepared 'CLI readiness missing.'
Require-Equal $cli.requiredTool 'QQ.Production.Intraday.Tools.LmaxReadOnlyActivation' 'Required CLI tool mismatch.'
Require-Equal $cli.requiredAdapterMode 'real-bounded-executable-readonly' 'Required adapter mode mismatch.'
Require-True $cli.manualCliMustBeInvokedOnlyAfterFreshApproval 'Fresh approval CLI restriction missing.'
Require-True $cli.manualCliMustNotBeInvokedByApiWorkerDefaultStartup 'API/Worker CLI restriction missing.'
Require-False $cli.r102InvokedManualCli 'R102 invoked manual CLI.'

Require-True $config.workspaceConfigPrerequisitesPrepared 'Config prerequisites missing.'
Require-True $config.osDotnetRuntimeVersionCheckRequired 'OS/.NET check missing.'
Require-True $config.repoBuildTestExpected 'Build/test expectation missing.'
Require-True $config.artifactOutputDirectoryRequired 'Artifact directory requirement missing.'
Require-True $config.operatorApprovalCaptureLocationRequired 'Operator approval capture requirement missing.'
Require-True $config.shutdownRevertExpectationsRequired 'Shutdown/revert requirement missing.'
Require-True $config.rollbackAbortRuleRequired 'Rollback/abort rule missing.'
Require-True $config.firewallProxyTlsInspectionAwarenessRequired 'Firewall/proxy/TLS inspection awareness missing.'
Require-True $config.dnsReachabilityStrategySanitized 'Sanitized DNS/reachability strategy missing.'
Require-False $config.productionAccountConfigAllowed 'Production account/config allowed.'
Require-False $config.rawEndpointValuesAllowedInArtifacts 'Raw endpoint values allowed.'

Require-True $endpoint.demoEndpointPrerequisitesPrepared 'Demo endpoint prerequisites missing.'
Require-Equal $endpoint.endpointModeRequired 'Demo' 'Endpoint mode mismatch.'
Require-True $endpoint.endpointPresentRequired 'Endpoint presence requirement missing.'
Require-True $endpoint.hostConcreteBindingRequired 'Host concrete binding missing.'
Require-False $endpoint.hostPlaceholderAllowed 'Placeholder host allowed.'
Require-True $endpoint.portConcreteBindingRequired 'Port concrete binding missing.'
Require-True $endpoint.productionExcludedRequired 'Production exclusion missing.'
Require-True $endpoint.endpointApprovedRequired 'Endpoint approval missing.'
Require-False $endpoint.rawEndpointValuesSerialized 'Raw endpoint values serialized.'

Require-True $credential.credentialSourcePrerequisitesPrepared 'Credential source prerequisites missing.'
Require-True $credential.credentialSourceRequired 'Credential source requirement missing.'
Require-True $credential.demoReadOnlyOnly 'Demo/read-only credential restriction missing.'
Require-False $credential.productionCredentialSourceAllowed 'Production credential source allowed.'
Require-True $credential.inMemoryOnlySecretUseRequired 'In-memory-only secret use missing.'
Require-False $credential.credentialValuesReturned 'Credential values returned.'
Require-False $credential.rawCredentialLoggingAllowed 'Raw credential logging allowed.'
Require-False $credential.rawCredentialSerializationAllowed 'Raw credential serialization allowed.'
Require-False $credential.rawFixFieldSerializationAllowed 'Raw FIX field serialization allowed.'

Require-Equal $apiWorker.result 'PASS' 'API/Worker audit failed.'
Require-True $apiWorker.apiWorkerFakeLmaxGatewayOnly 'API/Worker gateway changed away from FakeLmaxGatewayOnly.'
Require-False $apiWorker.apiWorkerGatewayChanged 'API/Worker gateway changed.'
Require-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup 'Manual CLI reachable from API/Worker default startup.'
Require-False $apiWorker.realAdapterGlobalDefault 'Real adapter global default.'

Require-Equal $noLive.result 'PASS' 'No-live-default validation failed.'
Require-True $noLive.noExternalDefaultPreserved 'No-external default not preserved.'
Require-False $noLive.liveGatewayDefaultIntroduced 'Live gateway default introduced.'
Require-False $noLive.realAdapterDefaultIntroduced 'Real adapter default introduced.'
Require-False $noLive.appsettingsLiveEnablementIntroduced 'Appsettings live enablement introduced.'
Require-False $noLive.manualCliDefaultStartupIntroduced 'Manual CLI default startup introduced.'
Require-False $noLive.apiWorkerLivePathIntroduced 'API/Worker live path introduced.'

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
Require-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Require-False $forbidden.pollingIntroduced 'Polling introduced.'
Require-False $forbidden.replayIntroduced 'Replay introduced.'
Require-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'

Require-True $sanitize.secretSanitizationRequirementsPrepared 'Secret sanitization requirements missing.'
Require-False $sanitize.credentialValuesReturned 'Credential values returned.'
Require-True $sanitize.rawCredentialsMustNotBePrintedStoredSerialized 'Raw credential sanitization requirement missing.'
Require-True $sanitize.rawEndpointValuesMustNotBeStoredIfSensitive 'Raw endpoint sanitization requirement missing.'
Require-True $sanitize.rawTlsMaterialCertDumpsMustNotBeStored 'TLS/cert sanitization requirement missing.'
Require-True $sanitize.rawFixMessagesLogsMustNotBeStored 'FIX sanitization requirement missing.'
Require-True $sanitize.sanitizedBooleansAndCategoriesOnly 'Sanitized-only evidence requirement missing.'

Require-Equal $next.nextRecommendedPhase 'LMAX-R103' 'Next phase recommendation missing.'
Require-Equal $next.nextRecommendedTitle 'QQ Workspace Preflight Evidence Pack' 'Next phase title mismatch.'
Require-False $next.nextPhaseShouldPerformActivation 'R103 should not activate by default.'
Require-True $next.futureActivationRequiresFreshExplicitApproval 'Future activation approval requirement missing.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.r101SuccessEvidencePresent 'R101 evidence missing from gate.'
Require-True $gate.r101ClassificationMatches 'R101 classification mismatch in gate.'
Require-True $gate.workspacePreparationReportPresent 'Workspace report missing from gate.'
Require-True $gate.localEvidenceCarryforwardReviewPresent 'Carryforward review missing from gate.'
Require-True $gate.cliToolReadinessPresent 'CLI readiness missing from gate.'
Require-True $gate.configPrerequisitesPresent 'Config prerequisites missing from gate.'
Require-True $gate.demoEndpointPrerequisitesPresent 'Demo endpoint prerequisites missing from gate.'
Require-True $gate.credentialSourcePrerequisitesPresent 'Credential prerequisites missing from gate.'
Require-True $gate.apiWorkerFakeGatewayAuditPass 'API/Worker audit missing/failing in gate.'
Require-True $gate.noLiveDefaultValidationPass 'No-live-default validation missing/failing in gate.'
Require-True $gate.forbiddenActionsAuditPass 'Forbidden actions audit missing/failing in gate.'
Require-True $gate.secretSanitizationRequirementsPresent 'Secret sanitization requirements missing from gate.'
Require-True $gate.workspacePreflightChecklistPresent 'Workspace preflight checklist missing from gate.'
Require-True $gate.operatorApprovalRequirementPresent 'Operator approval requirement missing from gate.'
Require-True $gate.noExternalBoundaryAttempted 'External boundary attempted during R102.'
Require-True $gate.buildEvidencePresent 'Build evidence missing.'
Require-True $gate.testEvidencePresent 'Test evidence missing.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r102-*' -File |
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
        throw "Forbidden sensitive endpoint, TLS, credential, or FIX pattern found in R102 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R102_VALIDATION_PASS'

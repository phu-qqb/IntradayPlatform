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

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-workspace-tls-environment-readiness-summary.json')
$closure = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-local-laptop-cycle-closure.json')
$preserve = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-local-evidence-preservation-review.json')
$workspace = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-workspace-next-step-decision.json')
$noRetry = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-no-local-retry-decision.json')
$approval = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-future-workspace-activation-approval-requirement.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-api-worker-fake-gateway-audit.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-forbidden-actions-audit.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-credential-endpoint-tls-fix-sanitization-validation.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-usdjpy-caveat-preservation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r101-gate-validation.json')

$allowedClassifications = @(
    'LMAX_R101_PASS_WORKSPACE_TLS_ENVIRONMENT_READINESS_REVIEW_NO_EXTERNAL_ACTIVATION',
    'LMAX_R101_DECISION_CLOSE_LOCAL_LAPTOP_TLS_CYCLE_NO_EXTERNAL_ACTIVATION',
    'LMAX_R101_DECISION_DEFER_TLS_CONNECTIVITY_CHECKS_TO_QQ_WORKSPACE_NO_EXTERNAL_ACTIVATION'
)

if ($allowedClassifications -notcontains $summary.classification) {
    throw "Unexpected R101 classification: $($summary.classification)"
}

Require-Equal $summary.classification 'LMAX_R101_PASS_WORKSPACE_TLS_ENVIRONMENT_READINESS_REVIEW_NO_EXTERNAL_ACTIVATION' 'R101 classification mismatch.'
Require-False $summary.activationPerformed 'R101 performed activation.'
Require-False $summary.tcpTlsFixMarketDataBoundaryAttempted 'R101 attempted external boundary.'
Require-True $summary.localLaptopCycleClosed 'Local laptop cycle closure missing.'
Require-True $summary.furtherLaptopTlsChecksDeferred 'Laptop TLS checks not deferred.'
Require-True $summary.futureTlsChecksAssignedToQqWorkspace 'Workspace TLS assignment missing.'
Require-True $summary.localEvidencePreservedAsArchitectureReadinessEvidence 'Local evidence preservation missing.'
Require-True $summary.workspacePhaseMustStartFromExistingProvenRuntimeChain 'Workspace chain continuity missing.'
Require-True $summary.futureWorkspaceActivationRequiresFreshExplicitApproval 'Fresh approval requirement missing.'
Require-True $summary.apiWorkerMustRemainFakeLmaxGatewayOnly 'API/Worker fake gateway invariant missing.'
Require-False $summary.ordersTradingPathAllowed 'Orders/trading path allowed.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must be false.'
Require-False $summary.credentialEndpointTlsFixSensitiveValuesPrintedStoredSerialized 'Sensitive values serialized.'

Require-True $closure.localLaptopCycleClosed 'Laptop cycle not closed.'
Require-Equal $closure.operatorProductDecision 'DoNotContinueDiagnosingLaptopLocalTlsBehavior' 'Operator/product decision mismatch.'
Require-False $closure.furtherLaptopLocalTlsRetriesDefaultRecommended 'Laptop-local retry recommended by default.'
Require-False $closure.furtherLaptopTlsEnvironmentChecksRequiredNow 'Laptop-local checks required unexpectedly.'
Require-False $closure.activationPerformed 'Activation performed in closure.'
Require-False $closure.tcpTlsFixMarketDataBoundaryAttempted 'Boundary attempted in closure.'

Require-True $preserve.localEvidencePreserved 'Local evidence not preserved.'
Require-True $preserve.preservedEvidence.manualCliExecutionSurfaceApproved 'Manual CLI evidence missing.'
Require-True $preserve.preservedEvidence.realBoundedExecutableReadOnlyAdapterWorks 'Real bounded adapter evidence missing.'
Require-True $preserve.preservedEvidence.concreteDemoEndpointBindingExists 'Demo endpoint evidence missing.'
Require-True $preserve.preservedEvidence.socketConnectorBindingExists 'Socket connector evidence missing.'
Require-True $preserve.preservedEvidence.tcpSocketSucceededRepeatedly 'TCP success evidence missing.'
Require-True $preserve.preservedEvidence.tlsBoundaryReached 'TLS boundary evidence missing.'
Require-True $preserve.preservedEvidence.tlsSucceededInR81R85 'R81/R85 TLS success evidence missing.'
Require-True $preserve.preservedEvidence.r93R95R97RepeatedHandshakeExceptionLocalEvidence 'Repeated handshake evidence missing.'
Require-True $preserve.preservedEvidence.fixBlockedAfterTlsNonSuccess 'FIX block evidence missing.'
Require-True $preserve.preservedEvidence.marketDataBlockedWithoutFixSuccess 'MarketData block evidence missing.'
Require-True $preserve.useAsArchitectureReadinessEvidence 'Architecture/readiness evidence preservation missing.'
Require-True $preserve.workspaceShouldNotRestartFromScratch 'Workspace restart-from-scratch risk.'

Require-Equal $workspace.futureTlsConnectivityChecksAssignedTo 'QQWorkspaceEnvironment' 'Workspace assignment mismatch.'
Require-Equal $workspace.nextMeaningfulValidationLocation 'QQWorkspaceDeploymentRuntimeTarget' 'Next validation target mismatch.'
Require-Equal $workspace.nextStep 'PrepareWorkspaceDeploymentConnectivityPrerequisites' 'Workspace next step mismatch.'
Require-True $workspace.localLaptopChecksDeferred 'Laptop checks not deferred.'
Require-True $workspace.workspacePreparationRequiredBeforeActivation 'Workspace preparation requirement missing.'
Require-False $workspace.activationAllowedInR101 'Activation allowed in R101.'

Require-True $noRetry.noFurtherLaptopLocalActivationRetriesByDefault 'Laptop retry still default.'
Require-True $noRetry.noFurtherLaptopTlsEnvironmentChecksRequiredNow 'Laptop checks still required.'
Require-False $noRetry.localRetryRecommendedWithoutOperatorDecision 'Local retry recommended without operator decision.'
Require-True $noRetry.futureWorkspaceRetryRequiresSeparateApproval 'Future workspace approval missing.'

Require-True $approval.futureWorkspaceActivationRequiresFreshExplicitApproval 'Future workspace approval requirement missing.'
Require-False $approval.priorApprovalsReusable 'Prior approvals reusable.'
Require-True $approval.approvalMustNameWorkspaceEnvironment 'Workspace environment approval specificity missing.'
Require-True $approval.approvalMustPreserveDemoReadOnlyNoOrdersNoTrading 'Read-only/no-orders approval constraint missing.'
Require-True $approval.approvalMustPreserveUsdJpyCaveat 'USDJPY approval caveat constraint missing.'
Require-False $approval.activationPerformedInR101 'Activation performed in R101 approval review.'

Require-Equal $apiWorker.result 'PASS' 'API/Worker audit failed.'
Require-True $apiWorker.apiWorkerFakeLmaxGatewayOnly 'API/Worker gateway changed away from FakeLmaxGatewayOnly.'
Require-False $apiWorker.apiWorkerGatewayChanged 'API/Worker gateway changed.'
Require-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup 'Manual CLI reachable from API/Worker default startup.'
Require-False $apiWorker.realAdapterGlobalDefault 'Real adapter global default.'

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

Require-Equal $sanitize.result 'PASS' 'Sanitization validation failed.'
Require-False $sanitize.credentialValuesReturned 'Credential values returned.'
Require-False $sanitize.rawCredentialsSerialized 'Raw credentials serialized.'
Require-False $sanitize.rawEndpointValuesSerialized 'Raw endpoint values serialized.'
Require-False $sanitize.rawTlsMaterialSerialized 'Raw TLS material serialized.'
Require-False $sanitize.rawCertificateDetailsSerialized 'Raw certificate details serialized.'
Require-False $sanitize.rawFixMessagesSerialized 'Raw FIX messages serialized.'
Require-False $sanitize.rawSensitiveFixLogsSerialized 'Raw sensitive FIX logs serialized.'

Require-True $usdJpy.caveatPreserved 'USDJPY caveat not preserved.'
Require-Equal $usdJpy.securityId '4004' 'USDJPY SecurityID mismatch.'
Require-Equal $usdJpy.securityIdSource '8' 'USDJPY SecurityIDSource mismatch.'
Require-Equal $usdJpy.caveat 'prior failed-safe root cause remains unproven' 'USDJPY caveat weakened.'

Require-Equal $next.nextRecommendedPhase 'LMAX-R102' 'Next phase recommendation missing.'
Require-Equal $next.nextRecommendedTitle 'QQ Workspace Deployment/Connectivity Preparation Pack' 'Next phase title mismatch.'
Require-False $next.nextPhaseShouldPerformActivation 'R102 should not activate by default.'
Require-True $next.freshApprovalRequiredForFutureActivation 'Future activation approval requirement missing.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.workspaceReadinessDecisionPresent 'Workspace readiness decision missing.'
Require-True $gate.localLaptopCycleClosurePresent 'Laptop cycle closure missing.'
Require-True $gate.noLaptopLocalRetryDefault 'No-laptop-retry default missing.'
Require-True $gate.workspaceNextStepDecisionPresent 'Workspace next step decision missing.'
Require-True $gate.futureWorkspaceApprovalRequirementPresent 'Workspace approval requirement missing.'
Require-True $gate.apiWorkerFakeGatewayAuditPresent 'API/Worker audit missing.'
Require-True $gate.forbiddenActionsAuditPresent 'Forbidden actions audit missing.'
Require-True $gate.sanitizationEvidencePresent 'Sanitization evidence missing.'
Require-True $gate.usdJpyCaveatPreserved 'USDJPY caveat gate missing.'
Require-True $gate.buildEvidencePresent 'Build evidence missing.'
Require-True $gate.testEvidencePresent 'Test evidence missing.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r101-*' -File |
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
        throw "Forbidden sensitive endpoint, TLS, credential, or FIX pattern found in R101 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R101_VALIDATION_PASS'

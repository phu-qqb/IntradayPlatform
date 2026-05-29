$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$writerPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualFixLogonFrameWriter.cs'
$apiPath = Join-Path $root 'src/QQ.Production.Intraday.Api/Program.cs'
$workerPath = Join-Path $root 'src/QQ.Production.Intraday.Worker/Program.cs'

function Read-Json($name) {
    $path = Join-Path $artifactRoot $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing artifact: $name"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Assert-True($condition, $message) {
    if (-not $condition) {
        throw $message
    }
}

$requiredArtifacts = @(
    'phase-lmax-r117-fix-logon-session-identifier-fix-report.md',
    'phase-lmax-r117-fix-logon-session-identifier-summary.json',
    'phase-lmax-r117-r116-root-cause-before-after-classification.json',
    'phase-lmax-r117-sender-compid-binding-validation.json',
    'phase-lmax-r117-target-compid-binding-validation.json',
    'phase-lmax-r117-session-identifier-material-validation.json',
    'phase-lmax-r117-placeholder-label-elimination-validation.json',
    'phase-lmax-r117-username-password-binding-carryforward-validation.json',
    'phase-lmax-r117-sequence-reset-policy-validation.json',
    'phase-lmax-r117-session-logon-only-safety-validation.json',
    'phase-lmax-r117-order-message-exclusion-validation.json',
    'phase-lmax-r117-marketdata-block-until-fix-success-validation.json',
    'phase-lmax-r117-raw-fix-and-session-sanitization-validation.json',
    'phase-lmax-r117-production-config-exclusion-validation.json',
    'phase-lmax-r117-real-bounded-path-validation.json',
    'phase-lmax-r117-no-external-boundary-attempted.json',
    'phase-lmax-r117-forbidden-actions-audit.json',
    'phase-lmax-r117-api-worker-fake-gateway-audit.json',
    'phase-lmax-r117-usdjpy-caveat-preservation.json',
    'phase-lmax-r117-next-phase-recommendation.json',
    'phase-lmax-r117-gate-validation.json'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

$r116 = Read-Json 'phase-lmax-r116-fix-session-boundary-root-cause-summary.json'
$summary = Read-Json 'phase-lmax-r117-fix-logon-session-identifier-summary.json'
$beforeAfter = Read-Json 'phase-lmax-r117-r116-root-cause-before-after-classification.json'
$sender = Read-Json 'phase-lmax-r117-sender-compid-binding-validation.json'
$target = Read-Json 'phase-lmax-r117-target-compid-binding-validation.json'
$sessionMaterial = Read-Json 'phase-lmax-r117-session-identifier-material-validation.json'
$placeholder = Read-Json 'phase-lmax-r117-placeholder-label-elimination-validation.json'
$userPass = Read-Json 'phase-lmax-r117-username-password-binding-carryforward-validation.json'
$sequence = Read-Json 'phase-lmax-r117-sequence-reset-policy-validation.json'
$sessionOnly = Read-Json 'phase-lmax-r117-session-logon-only-safety-validation.json'
$orders = Read-Json 'phase-lmax-r117-order-message-exclusion-validation.json'
$marketData = Read-Json 'phase-lmax-r117-marketdata-block-until-fix-success-validation.json'
$sanitize = Read-Json 'phase-lmax-r117-raw-fix-and-session-sanitization-validation.json'
$production = Read-Json 'phase-lmax-r117-production-config-exclusion-validation.json'
$pathValidation = Read-Json 'phase-lmax-r117-real-bounded-path-validation.json'
$noExternal = Read-Json 'phase-lmax-r117-no-external-boundary-attempted.json'
$forbidden = Read-Json 'phase-lmax-r117-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r117-api-worker-fake-gateway-audit.json'
$usdjpy = Read-Json 'phase-lmax-r117-usdjpy-caveat-preservation.json'
$next = Read-Json 'phase-lmax-r117-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r117-gate-validation.json'

Assert-True ($r116.classification -eq 'LMAX_R116_PASS_FIX_COMPID_OR_SESSION_IDENTIFIER_SUSPECT_NO_EXTERNAL_ACTIVATION') 'R116 success evidence is missing or mismatched.'
Assert-True ($r116.compIdSessionIdentifierReview.actualInMemoryFrameUsesSanitizedPlaceholderLabels -eq $true) 'R116 placeholder-label root cause is not acknowledged.'
Assert-True ($r116.usernamePasswordTags553554PresentBySanitizedEvidence -eq $true) 'R116 username/password carryforward evidence is missing.'
Assert-True ($r116.externalActivationAttempted -eq $false) 'R116 must be no-external.'

Assert-True ($summary.classification -eq 'LMAX_R117_PASS_FIX_LOGON_SESSION_IDENTIFIER_BINDING_READY_NO_EXTERNAL_ACTIVATION') 'Unexpected R117 classification.'
Assert-True ($summary.senderCompIdBindingReady -eq $true -and $sender.senderCompIdBoundFromApprovedMaterial -eq $true) 'SenderCompID binding from approved in-memory material is not provable.'
Assert-True ($summary.targetCompIdBindingReady -eq $true -and $target.targetCompIdBoundFromApprovedDemoConfig -eq $true) 'TargetCompID binding from approved Demo config/material is not provable.'
Assert-True ($summary.sessionIdentifierMaterialBindingReady -eq $true -and $sessionMaterial.sessionIdentifierMaterialBound -eq $true) 'Session identifier material binding is not provable.'
Assert-True ($sessionMaterial.missingSessionIdentifierMaterialRejected -eq $true -and $sessionMaterial.placeholderFallbackAllowed -eq $false) 'Missing session identifier material must be rejected without placeholder fallback.'
Assert-True ($summary.placeholderLabelsEliminatedFromActualInMemoryLogonFramePath -eq $true) 'Placeholder-label elimination is not proven.'
Assert-True ($placeholder.actualInMemoryFrameUsesSanitizedPlaceholderLabels -eq $false) 'Actual in-memory Logon frame path still uses sanitized placeholder labels.'
Assert-True ($placeholder.senderCompIdWasPlaceholder -eq $false -and $placeholder.targetCompIdWasPlaceholder -eq $false) 'SenderCompID/TargetCompID placeholder evidence regressed.'

Assert-True ($beforeAfter.before.actualInMemoryFrameUsesSanitizedPlaceholderLabels -eq $true) 'Before classification does not preserve R116 root cause.'
Assert-True ($beforeAfter.after.senderCompIdBoundFromApprovedMaterial -eq $true) 'After classification missing SenderCompID binding.'
Assert-True ($beforeAfter.after.targetCompIdBoundFromApprovedDemoConfig -eq $true) 'After classification missing TargetCompID binding.'
Assert-True ($beforeAfter.after.actualInMemoryFrameUsesSanitizedPlaceholderLabels -eq $false) 'After classification still allows placeholder labels.'

Assert-True ($userPass.usernamePasswordBindingCarryforwardReady -eq $true) 'Username/password carryforward review missing.'
Assert-True ($userPass.usernameTag553Bound -eq $true -and $userPass.passwordTag554Bound -eq $true) 'Username/password tag binding regressed.'
Assert-True ($userPass.usernameValueSerialized -eq $false -and $userPass.passwordValueSerialized -eq $false) 'Username/password values were serialized.'
Assert-True ($sequence.resetSeqNumFlagYRemainsPresent -eq $true -and $sequence.resetSeqNumFlagYAlignedWithMarketDataDocs -eq $true) 'ResetSeqNumFlag=Y policy regressed.'
Assert-True ($sequence.sequenceResetPolicyChanged -eq $false) 'Sequence/reset policy was changed in R117.'

Assert-True ($sessionOnly.sessionLogonOnly -eq $true -and $sessionOnly.fixLogonBuilderReady -eq $true -and $sessionOnly.fixFrameWriterReady -eq $true) 'Writer/builder must remain session/logon-only.'
Assert-True ($orders.orderMessagesUnsupported -eq $true -and $orders.newOrderSingleSupported -eq $false -and $orders.cancelReplaceSupported -eq $false) 'Order-capable FIX writer was introduced.'
Assert-True ($orders.executionReportsSupported -eq $false -and $orders.fillsSupported -eq $false -and $orders.orderLifecycleSupported -eq $false) 'Execution/fill/order lifecycle parsing was introduced.'
Assert-True ($orders.tradingMutationSupported -eq $false) 'Order/trading path was introduced.'
Assert-True ($marketData.marketDataRequestBlockedUntilFixSuccess -eq $true -and $marketData.marketDataAllowedWithoutFixSessionAcknowledgement -eq $false) 'MarketDataRequest can be attempted without FIX success.'

Assert-True ($sanitize.rawSenderCompIdSerialized -eq $false -and $sanitize.rawTargetCompIdSerialized -eq $false -and $sanitize.rawSessionIdSerialized -eq $false) 'Raw CompID/session identifiers were serialized.'
Assert-True ($sanitize.rawFixFrameSerialized -eq $false -and $sanitize.rawFixMessagesSerialized -eq $false) 'Raw FIX frame/messages were serialized.'
Assert-True ($sanitize.rawCredentialsSerialized -eq $false -and $sanitize.credentialValuesReturned -eq $false) 'Credential material was serialized or returned.'
Assert-True ($summary.credentialValuesReturned -eq $false -and $sessionMaterial.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($production.productionAccountAllowed -eq $false -and $production.productionConfigAllowed -eq $false) 'Production account/config was allowed.'
Assert-True ($production.productionAccountConfigExcluded -eq $true) 'Production exclusion evidence missing.'
Assert-True ($pathValidation.manualRealBoundedPathValidated -eq $true -and $pathValidation.noExternalDefaultPreserved -eq $true -and $pathValidation.apiWorkerReachable -eq $false) 'Real-bounded path or no-external/default validation regressed.'

Assert-True ($noExternal.activationAttempted -eq $false -and $noExternal.tcpAttempted -eq $false -and $noExternal.tlsAttempted -eq $false -and $noExternal.fixLogonAttempted -eq $false -and $noExternal.marketDataRequestAttempted -eq $false) 'R117 attempted an external boundary.'
Assert-True ($noExternal.fixFrameWrittenLive -eq $false) 'R117 wrote a live FIX frame.'
Assert-True ($forbidden.result -eq 'PASS') 'Forbidden actions audit failed.'
Assert-True ($forbidden.orders -eq $false -and $forbidden.newOrderSingle -eq $false -and $forbidden.cancelReplace -eq $false) 'Order/trading path was introduced.'
Assert-True ($forbidden.hostedBackgroundService -eq $false -and $forbidden.scheduler -eq $false -and $forbidden.polling -eq $false -and $forbidden.replay -eq $false -and $forbidden.shadowReplay -eq $false) 'Hosted service/scheduler/polling/replay path introduced.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true -and $apiWorker.defaultLiveEnablementIntroduced -eq $false) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($usdjpy.caveatPreserved -eq $true -and $usdjpy.securityId -eq '4004' -and $usdjpy.securityIdSource -eq '8') 'USDJPY caveat is missing or weakened.'
Assert-True ($next.nextRecommendedPhase -eq 'Phase LMAX-R119 - Operator-Approved Single Temporary QQ Workspace Demo Read-Only Activation Retry After FIX Session Identifier Binding Fix') 'Next phase recommendation is missing or wrong.'
Assert-True ($gate.buildResult -like 'PASS*') 'Build evidence is missing.'
Assert-True ($gate.focusedTests -like 'PASS*' -and $gate.unitTests -like 'PASS*' -and $gate.integrationTests -like 'PASS*') 'Test evidence is missing.'

$writerSource = Get-Content -LiteralPath $writerPath -Raw
Assert-True ($writerSource -match 'LMAX_DEMO_SENDER_COMP_ID') 'Builder does not read approved Demo SenderCompID material.'
Assert-True ($writerSource -match 'LMAX_DEMO_TARGET_COMP_ID') 'Builder does not read approved Demo TargetCompID material.'
Assert-True ($writerSource -match 'Field\("49", sessionMaterial\.SenderCompId\)') 'Builder does not bind SenderCompID from in-memory session material.'
Assert-True ($writerSource -match 'Field\("56", sessionMaterial\.TargetCompId\)') 'Builder does not bind TargetCompID from in-memory session material.'
Assert-True ($writerSource -match 'SessionIdentifierCredentialMaterialMissing') 'Builder does not reject missing session identifier material.'
Assert-True ($writerSource -notmatch 'Field\("49", options\.SenderCompIdLabel\)') 'Builder still binds SenderCompID from placeholder label.'
Assert-True ($writerSource -notmatch 'Field\("56", options\.TargetCompIdLabel\)') 'Builder still binds TargetCompID from placeholder label.'
Assert-True ($writerSource -notmatch 'NewOrderSingleSupported: true') 'NewOrderSingle support introduced.'
Assert-True ($writerSource -notmatch 'CancelReplaceSupported: true') 'Cancel/replace support introduced.'

$apiWorkerSource = (Get-Content -LiteralPath $apiPath -Raw) + "`n" + (Get-Content -LiteralPath $workerPath -Raw)
Assert-True ($apiWorkerSource -notmatch 'LmaxReadOnlyActivationManualFixLogonFrameBuilder') 'Manual builder is reachable from API/Worker startup.'
Assert-True ($apiWorkerSource -match 'FakeLmaxGateway') 'API/Worker FakeLmaxGatewayOnly evidence missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r117-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$forbiddenPatterns = @(
    'password=',
    'username=',
    'session token',
    'BEGIN CERTIFICATE',
    'PRIVATE KEY',
    '35=A',
    '35=D',
    '35=F',
    '35=G',
    '35=8',
    '49=',
    '56=',
    '553=',
    '554=',
    'SenderCompID=',
    'TargetCompID=',
    'r117-synthetic',
    'r114-synthetic',
    'DemoReadOnlySenderCompId',
    'DemoReadOnlyTargetCompId',
    'lmax.com',
    'fix-marketdata'
)

foreach ($pattern in $forbiddenPatterns) {
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or raw protocol material found in R117 artifacts: $pattern"
}

Write-Host 'LMAX_R117_VALIDATION_PASS'

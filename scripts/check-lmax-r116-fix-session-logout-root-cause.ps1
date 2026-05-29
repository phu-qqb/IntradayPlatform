$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'

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

$r115Summary = Read-Json 'phase-lmax-r115-temporary-readonly-activation-retry-summary.json'
$summary = Read-Json 'phase-lmax-r116-fix-session-boundary-root-cause-summary.json'
$beforeAfter = Read-Json 'phase-lmax-r116-r115-boundary-before-after-classification.json'
$logout = Read-Json 'phase-lmax-r116-r115-fix-logout-review.json'
$logoutReason = Read-Json 'phase-lmax-r116-fix-logout-reason-sanitized-review.json'
$fieldPresence = Read-Json 'phase-lmax-r116-fix-logon-field-presence-review.json'
$userPass = Read-Json 'phase-lmax-r116-fix-username-password-binding-review.json'
$compId = Read-Json 'phase-lmax-r116-fix-compid-session-identifier-review.json'
$sequence = Read-Json 'phase-lmax-r116-fix-sequence-reset-policy-review.json'
$entitlement = Read-Json 'phase-lmax-r116-fix-entitlement-or-session-permission-review.json'
$marketData = Read-Json 'phase-lmax-r116-marketdata-block-after-fix-failure-review.json'
$noExternal = Read-Json 'phase-lmax-r116-no-external-boundary-attempted.json'
$forbidden = Read-Json 'phase-lmax-r116-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r116-api-worker-fake-gateway-audit.json'
$sanitization = Read-Json 'phase-lmax-r116-credential-endpoint-tls-fix-sanitization-validation.json'
$usdjpy = Read-Json 'phase-lmax-r116-usdjpy-caveat-preservation.json'
$next = Read-Json 'phase-lmax-r116-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r116-gate-validation.json'

$requiredArtifacts = @(
    'phase-lmax-r116-fix-session-boundary-root-cause-report.md',
    'phase-lmax-r116-fix-session-boundary-root-cause-summary.json',
    'phase-lmax-r116-r115-boundary-before-after-classification.json',
    'phase-lmax-r116-r115-fix-logout-review.json',
    'phase-lmax-r116-fix-logout-reason-sanitized-review.json',
    'phase-lmax-r116-fix-logon-field-presence-review.json',
    'phase-lmax-r116-fix-username-password-binding-review.json',
    'phase-lmax-r116-fix-compid-session-identifier-review.json',
    'phase-lmax-r116-fix-sequence-reset-policy-review.json',
    'phase-lmax-r116-fix-entitlement-or-session-permission-review.json',
    'phase-lmax-r116-marketdata-block-after-fix-failure-review.json',
    'phase-lmax-r116-real-bounded-path-validation.json',
    'phase-lmax-r116-no-external-boundary-attempted.json',
    'phase-lmax-r116-forbidden-actions-audit.json',
    'phase-lmax-r116-api-worker-fake-gateway-audit.json',
    'phase-lmax-r116-no-scheduler-polling-service-audit.json',
    'phase-lmax-r116-credential-endpoint-tls-fix-sanitization-validation.json',
    'phase-lmax-r116-usdjpy-caveat-preservation.json',
    'phase-lmax-r116-next-phase-recommendation.json',
    'phase-lmax-r116-gate-validation.json'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

Assert-True ($r115Summary.classification -eq 'LMAX_R115_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY') 'R115 FIX boundary evidence is missing or mismatched.'
Assert-True ($r115Summary.fixAcknowledgementCategory -eq 'FixLogoutReceived') 'R115 FixLogoutReceived evidence is missing.'
Assert-True ($r115Summary.usernameTag553Bound -eq $true -and $r115Summary.passwordTag554Bound -eq $true) 'R115 username/password binding evidence is missing.'
Assert-True ($r115Summary.rawFixFrameOrMessageSerialized -eq $false) 'R115 raw FIX serialization evidence regressed.'

Assert-True ($summary.classification -eq 'LMAX_R116_PASS_FIX_COMPID_OR_SESSION_IDENTIFIER_SUSPECT_NO_EXTERNAL_ACTIVATION') 'Unexpected R116 classification.'
Assert-True ($summary.reviewOnly -eq $true -and $summary.newActivationPerformed -eq $false -and $summary.externalActivationAttempted -eq $false) 'R116 must be review-only with no activation.'
Assert-True ($summary.r115AttemptSequenceCleanlyReviewed -eq $true -and $summary.r115ExternalAttemptCount -eq 1) 'R115 attempt sequence was not reviewed cleanly.'
Assert-True ($summary.r115TcpSuccessProven -eq $true -and $summary.r115TlsSuccessProven -eq $true) 'R115 TCP/TLS success not proven.'
Assert-True ($summary.r115FixLogonAttemptProven -eq $true) 'R115 FIX logon attempt not proven.'
Assert-True ($summary.usernamePasswordTags553554PresentBySanitizedEvidence -eq $true) 'R115 553/554 evidence not reviewed.'
Assert-True ($summary.r115FixAcknowledgementReaderParserClassifierProven -eq $true) 'R115 acknowledgement reader/classifier not proven.'
Assert-True ($summary.fixLogoutReceivedAcknowledged -eq $true) 'FixLogoutReceived must be acknowledged.'
Assert-True ($summary.logoutReasonAvailable -eq $false -and $summary.logoutReasonCategory -eq 'LogoutReasonNotAvailable') 'Logout reason availability/category mismatch.'

Assert-True ($beforeAfter.r115FixAcknowledgementCategory -eq 'FixLogoutReceived') 'Before/after classification does not acknowledge FixLogoutReceived.'
Assert-True ($logout.r115FixLogoutReceivedAcknowledged -eq $true -and $logout.fixAcknowledgementReaderParserClassifierUsed -eq $true) 'FIX Logout review missing.'
Assert-True ($logoutReason.logoutReasonReviewPresent -eq $true) 'Logout reason sanitized review is missing.'
Assert-True ($logoutReason.currentParserCapturesMessageType -eq $true -and $logoutReason.currentParserCapturesSanitizedLogoutTextCategory -eq $false) 'Logout parser behavior review mismatch.'
Assert-True ($logoutReason.rawLogoutTextSerialized -eq $false) 'Raw Logout text must not be serialized.'

Assert-True ($fieldPresence.beginStringFix44Present -eq $true -and $fieldPresence.msgTypeLogonPresent -eq $true) 'FIX Logon field presence review missing.'
Assert-True ($fieldPresence.senderCompIdPresent -eq $true -and $fieldPresence.targetCompIdPresent -eq $true) 'CompID field presence review missing.'
Assert-True ($fieldPresence.usernameTag553Present -eq $true -and $fieldPresence.passwordTag554Present -eq $true) 'Username/password field presence review missing.'
Assert-True ($fieldPresence.rawFixFrameSerialized -eq $false) 'Raw FIX frame must not be serialized.'
Assert-True ($userPass.usernamePasswordTagBindingReviewed -eq $true -and $userPass.usernameTag553Bound -eq $true -and $userPass.passwordTag554Bound -eq $true) 'Username/password binding review missing.'
Assert-True ($userPass.usernameValueSerialized -eq $false -and $userPass.passwordValueSerialized -eq $false) 'Username/password values must not be serialized.'

Assert-True ($compId.compIdSessionIdentifierReviewPresent -eq $true) 'CompID/session identifier review missing.'
Assert-True ($compId.actualInMemoryFrameUsesSanitizedPlaceholderLabels -eq $true) 'CompID placeholder-label evidence missing.'
Assert-True ($compId.compIdOrSessionIdentifierMismatchSuspected -eq $true) 'CompID/session identifier suspect classification missing.'
Assert-True ($compId.rawCompIdOrSessionIdentifiersSerialized -eq $false) 'Raw CompID/session identifiers must not be serialized.'
Assert-True ($sequence.sequenceResetPolicyReviewPresent -eq $true -and $sequence.resetSeqNumFlagYAlignedWithMarketDataDocs -eq $true) 'Sequence/reset policy review missing.'
Assert-True ($entitlement.entitlementOrSessionPermissionReviewPresent -eq $true -and $entitlement.entitlementOrSessionPermissionProven -eq $false) 'Entitlement/session permission review missing.'
Assert-True ($marketData.marketDataRequestBlockedAfterFixFailure -eq $true -and $marketData.marketDataAllowedWithoutFixSuccess -eq $false) 'MarketDataRequest must remain blocked after FIX failure.'

Assert-True ($noExternal.activationAttempted -eq $false -and $noExternal.tcpAttempted -eq $false -and $noExternal.tlsAttempted -eq $false -and $noExternal.fixLogonAttempted -eq $false -and $noExternal.marketDataRequestAttempted -eq $false) 'R116 attempted an external boundary.'
Assert-True ($forbidden.result -eq 'PASS') 'Forbidden action audit failed.'
Assert-True ($forbidden.orders -eq $false -and $forbidden.newOrderSingle -eq $false -and $forbidden.cancelReplace -eq $false) 'Order/trading path was introduced.'
Assert-True ($forbidden.scheduler -eq $false -and $forbidden.polling -eq $false -and $forbidden.replay -eq $false -and $forbidden.shadowReplay -eq $false) 'Scheduler/polling/replay path introduced.'
Assert-True ($forbidden.executionReportFillOrderLifecycleParsingIntroduced -eq $false) 'Execution report/fill/order lifecycle parsing introduced.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true -and $apiWorker.defaultLiveEnablementIntroduced -eq $false) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($sanitization.result -eq 'PASS' -and $sanitization.credentialValuesReturned -eq $false) 'Sanitization validation failed.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false -and $sanitization.rawEndpointSerialized -eq $false) 'Sensitive material serialized.'
Assert-True ($usdjpy.caveatPreserved -eq $true -and $usdjpy.securityId -eq '4004' -and $usdjpy.securityIdSource -eq '8') 'USDJPY caveat missing or weakened.'
Assert-True ($next.nextRecommendedPhase -eq 'Phase LMAX-R117 - Targeted FIX Logon Session Parameter Fix') 'Next phase recommendation missing or wrong.'
Assert-True ($gate.buildResult -like 'PASS*' -and $gate.unitTests -like 'PASS*' -and $gate.integrationTests -like 'PASS*') 'Build/test evidence missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r116-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$forbiddenPatterns = @(
    'password=',
    'password:',
    'username=',
    'session token',
    'BEGIN CERTIFICATE',
    'PRIVATE KEY',
    '35=A',
    '35=D',
    '35=F',
    '35=G',
    '35=8',
    '553=',
    '554=',
    'SenderCompID=',
    'TargetCompID='
)

foreach ($pattern in $forbiddenPatterns) {
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or raw protocol material found in R116 artifacts: $pattern"
}

Write-Host 'LMAX_R116_VALIDATION_PASS'

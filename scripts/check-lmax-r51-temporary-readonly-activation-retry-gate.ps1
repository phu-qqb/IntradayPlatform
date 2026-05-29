param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r51-gate-validation.json"
$approvalText = "I, Philippe, explicitly approve Phase LMAX-R51 for one temporary Demo read-only runtime market-data activation retry after the R50 final pre-external consolidation fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
$r50Decision = "LMAX_R50_PASS_FINAL_PRE_EXTERNAL_APPROVAL_COMPOSITION_CONSOLIDATION_NO_EXTERNAL_ACTIVATION"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

$allowedClassifications = @(
    "LMAX_R51_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R51_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R51_FAIL_R50_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R51_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R51_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R51_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R51_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R51_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R51_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R51_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R51_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R51_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R51_FAIL_CREDENTIAL_CONFIG_BOUNDARY",
    "LMAX_R51_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R51_FAIL_TLS_BOUNDARY",
    "LMAX_R51_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R51_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R51_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R51_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R51_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R51_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R51_FAIL_BUILD_OR_TESTS"
)

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing required file: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-True($Condition, [string]$Message) {
    if ($Condition -ne $true) {
        throw $Message
    }
}

function Assert-False($Value, [string]$Message) {
    if ($Value -ne $false) {
        throw $Message
    }
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected' but got '$Actual'."
    }
}

$required = @(
    "phase-lmax-r51-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r51-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r51-operator-approval-note.md",
    "phase-lmax-r51-preflight-result.json",
    "phase-lmax-r51-preflight-trace.json",
    "phase-lmax-r51-boundary-evidence.json",
    "phase-lmax-r51-marketdata-sanitized-result.json",
    "phase-lmax-r51-forbidden-actions-audit.json",
    "phase-lmax-r51-api-worker-fake-gateway-audit.json",
    "phase-lmax-r51-usdjpy-caveat-preservation.json",
    "phase-lmax-r51-shutdown-revert-evidence.json",
    "phase-lmax-r51-next-phase-recommendation.json",
    "phase-lmax-r51-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R51 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$r50 = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-gate-validation.json")
Assert-True $r50.passed "R50 gate validation did not pass."
Assert-Equal $r50.classification $r50Decision "R50 classification"
Assert-True $r50.nextRetryPhaseExplicitlySupported "R50 did not explicitly support next retry phase."
Assert-True $r50.arbitraryUnapprovedPhasesRejected "R50 did not reject arbitrary phases."
Assert-False $r50.additionalKnownPreExternalBlockerFound "R50 still had known pre-external blocker."
$checks.r50SuccessEvidencePresent = $true

$approvalNote = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r51-operator-approval-note.md") -Raw
Assert-True ($approvalNote.IndexOf($approvalText, [StringComparison]::Ordinal) -ge 0) "R51 exact operator approval text missing or mismatched."
$checks.operatorApprovalExact = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-temporary-readonly-activation-retry-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R51 classification not allowed: $($summary.classification)"
Assert-Equal $summary.r50Classification $r50Decision "Summary R50 classification"
Assert-True $summary.r50SuccessEvidencePresent "R50 success evidence missing from summary."
Assert-True $summary.r50PreExternalConsolidationValid "R50 consolidation not valid in summary."
Assert-True $summary.r51ExplicitlyReserved "R51 phase reservation not proven."
Assert-True $summary.arbitraryUnapprovedPhasesRejected "Arbitrary phase rejection not proven."
Assert-True $summary.r42AdapterExecutablePathValid "R42 adapter path regressed."
Assert-True $summary.r44BoundedRuntimeCompositionProvable "R44 bounded runtime composition regressed."
Assert-True $summary.r46ExecutableBoundaryOperationCompositionProvable "R46 boundary operation composition regressed."
Assert-True $summary.r48ExternalBoundaryProviderExecutionCompositionProvable "R48 provider execution composition regressed."
Assert-Equal $summary.adapterMode "ApprovedBoundedExecutableReadOnly" "AdapterMode"
Assert-True $summary.boundedExecutorApproved "BoundedExecutorApproved missing."
Assert-True $summary.runtimeDelegateBindingApproved "RuntimeDelegateBindingApproved missing."
Assert-True ([int]$summary.attemptCount -le 1) "More than one activation attempt occurred."
Assert-False $summary.productionAccountUsed "Production account used."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must be false."
Assert-False $summary.realCredentialValuesRead "Real credential values were read."
Assert-True $summary.outputSanitized "Output not sanitized."
Assert-True $summary.buildRepresented "Build evidence missing."
Assert-True $summary.testsRepresented "Test evidence missing."
if ($summary.classification -eq "LMAX_R51_FAIL_CREDENTIAL_CONFIG_MISSING") {
    Assert-False $summary.credentialConfigPresent "Credential/config missing classification requires credentialConfigPresent=false."
    Assert-False $summary.credentialConfigApproved "Credential/config missing classification requires credentialConfigApproved=false."
    Assert-False $summary.externalActivationAttempted "Credential/config missing classification must not attempt external activation."
    Assert-True ([int]$summary.attemptCount -eq 0) "Credential/config missing classification must have attemptCount=0."
}
$checks.summaryPassed = $true

$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-preflight-result.json")
Assert-Equal $preflight.r50Classification $r50Decision "Preflight R50 classification"
Assert-True $preflight.operatorApprovalExact "Preflight operator approval exact"
Assert-True $preflight.r51ExplicitlyReserved "Preflight R51 reservation missing."
Assert-True $preflight.r42AdapterExecutablePathValid "Preflight R42 path regressed."
Assert-True $preflight.r44BoundedRuntimeCompositionProvable "Preflight R44 proof missing."
Assert-True $preflight.r46ExecutableBoundaryOperationCompositionProvable "Preflight R46 proof missing."
Assert-True $preflight.r48ExternalBoundaryProviderExecutionCompositionProvable "Preflight R48 proof missing."
Assert-Equal $preflight.adapterMode "ApprovedBoundedExecutableReadOnly" "Preflight adapter mode"
Assert-True $preflight.boundedExecutorApproved "Preflight bounded executor approval"
Assert-True $preflight.runtimeDelegateBindingApproved "Preflight delegate approval"
Assert-True $preflight.approvedInstrumentsExact "Preflight instrument scope"
Assert-True $preflight.usdJpyCaveatPreserved "Preflight USDJPY caveat"
Assert-False $preflight.productionAccountUsed "Preflight production account"
Assert-False $preflight.credentialValuesReturned "Preflight credentialValuesReturned"
Assert-False $preflight.realCredentialValuesRead "Preflight read real credential values"
Assert-False $preflight.externalBoundaryAttempted "Preflight attempted external boundary"
Assert-True ($preflight.passed -eq $true -or ($preflight.abortedBeforeExternalAction -eq $true -and $preflight.externalBoundaryAttempted -eq $false)) "Preflight neither passed nor aborted safely."
$checks.preflightPassedOrSafelyAborted = $true

$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-preflight-trace.json")
Assert-Equal $trace.classification $summary.classification "Trace classification"
Assert-False $trace.externalBoundaryAttempted "Trace indicates external boundary attempt."
Assert-True ([int]$trace.attemptCount -le 1) "Trace attempt count exceeds one."
$credentialTraceFailures = @($trace.trace | Where-Object { $_.gate -eq "Credential/config execution source" -and $_.result -eq "FAIL" })
Assert-True ($credentialTraceFailures.Count -eq 1) "Missing credential/config execution source trace failure."
$checks.preflightTracePresent = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-boundary-evidence.json")
foreach ($field in @("credentialConfig","tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries","shutdownRevert")) {
    Assert-True (@("NotAttempted","Attempted","Succeeded","FailedSafe","FailedExternal","FailedValidation","Aborted") -contains [string]$boundary.$field.status) "Invalid boundary status for $field."
}
if ($summary.externalActivationAttempted -eq $false) {
    foreach ($field in @("credentialConfig","tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries")) {
        Assert-Equal $boundary.$field.status "NotAttempted" "Boundary status for $field"
    }
}
Assert-Equal $boundary.shutdownRevert.status "Succeeded" "Shutdown/revert status"
$checks.boundaryEvidenceChecked = $true

$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-marketdata-sanitized-result.json")
Assert-False $marketData.credentialValuesReturned "MarketData credentialValuesReturned must be false."
Assert-True $marketData.outputSanitized "MarketData output was not sanitized."
Assert-True $marketData.approvedInstrumentsOnly "MarketData result contains unapproved instruments."
$checks.marketDataResultChecked = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-forbidden-actions-audit.json")
foreach ($field in @(
    "orderSubmissionExecuted",
    "orderPathIntroduced",
    "orderPathTouched",
    "tradingEnablementExecuted",
    "tradingStateMutated",
    "productionAccountUsed",
    "apiStarted",
    "workerStarted",
    "hostedServiceAdded",
    "backgroundServiceAdded",
    "schedulerStarted",
    "pollingStarted",
    "replayExecuted",
    "shadowReplayExecuted",
    "shadowReplaySubmitted",
    "credentialPrinted",
    "credentialStored",
    "credentialValuesReturned",
    "realCredentialValuesRead",
    "rawSensitiveFixLogsStored",
    "runtimeEnablementPersisted",
    "externalBoundaryAttempted"
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
Assert-True $forbidden.passed "Forbidden action audit failed."
$checks.forbiddenActionsPassed = $true

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
$checks.apiWorkerFakeGatewayPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat preservation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-shutdown-revert-evidence.json")
Assert-True $shutdown.present "Shutdown/revert evidence missing."
Assert-True $shutdown.completed "Shutdown/revert not completed."
Assert-False $shutdown.runtimeEnablementPersisted "Runtime enablement persisted."
$checks.shutdownRevertPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-next-phase-recommendation.json")
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R52*") "Missing R52 recommendation."
Assert-False $next.r52Executed "R52 was executed."
$checks.nextPhasePassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r51-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R51 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R51 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R51 artifacts contain forbidden raw sensitive FIX message type."
$checks.sanitizationPassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("ApprovedBoundedExecutableReadOnly", "phase-lmax-r51", "LmaxTemporaryReadOnlyActivationExecutor")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R51 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R51 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R51 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R51"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r51-temporary-readonly-activation-retry-gate.ps1"
    passed = $true
    classification = $summary.classification
    externalActivationAttempted = $summary.externalActivationAttempted
    attemptCount = $summary.attemptCount
    credentialValuesReturned = $summary.credentialValuesReturned
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R51 gate validation passed: $gatePath"

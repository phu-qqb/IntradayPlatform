param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r50-gate-validation.json"
$classification = "LMAX_R50_PASS_FINAL_PRE_EXTERNAL_APPROVAL_COMPOSITION_CONSOLIDATION_NO_EXTERNAL_ACTIVATION"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

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

$allowedClassifications = @(
    "LMAX_R50_PASS_FINAL_PRE_EXTERNAL_APPROVAL_COMPOSITION_CONSOLIDATION_NO_EXTERNAL_ACTIVATION",
    "LMAX_R50_FAIL_R49_PHASE_RESERVATION_STILL_MISSING",
    "LMAX_R50_FAIL_NEXT_RETRY_PHASE_RESERVATION_NOT_PROVABLE",
    "LMAX_R50_FAIL_ADDITIONAL_PRE_EXTERNAL_BLOCKER_FOUND",
    "LMAX_R50_FAIL_COMPOSITION_CHAIN_REGRESSION",
    "LMAX_R50_FAIL_APPROVAL_GATES_WEAKENED",
    "LMAX_R50_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R50_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_INTRODUCED",
    "LMAX_R50_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R50_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R50_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R50_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R50_FAIL_BUILD_OR_TESTS"
)

$required = @(
    "phase-lmax-r50-final-pre-external-consolidation-report.md",
    "phase-lmax-r50-final-pre-external-consolidation-summary.json",
    "phase-lmax-r50-r49-blocker-before-after-classification.json",
    "phase-lmax-r50-known-pre-external-blocker-sweep.json",
    "phase-lmax-r50-phase-reservation-validation.json",
    "phase-lmax-r50-composition-chain-validation.json",
    "phase-lmax-r50-bounded-executor-validation.json",
    "phase-lmax-r50-forbidden-actions-audit.json",
    "phase-lmax-r50-api-worker-fake-gateway-audit.json",
    "phase-lmax-r50-no-live-launcher-audit.json",
    "phase-lmax-r50-no-external-boundary-attempted.json",
    "phase-lmax-r50-usdjpy-caveat-preservation.json",
    "phase-lmax-r50-next-phase-recommendation.json",
    "phase-lmax-r50-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R50 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$r49 = Read-Json (Join-Path $artifactRoot "phase-lmax-r49-temporary-readonly-activation-retry-summary.json")
Assert-Equal $r49.classification "LMAX_R49_FAIL_R42_CONCRETE_ADAPTER_REGRESSION" "R49 classification"
Assert-Equal $r49.concreteBlocker "ConcreteAdapterApprovedBoundedExecutableReadOnlyPhaseReservationMissingR49" "R49 blocker"
Assert-False $r49.externalActivationAttempted "R49 unexpectedly attempted activation."
$checks.r49EvidencePresent = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-final-pre-external-consolidation-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R50 classification not allowed: $($summary.classification)"
Assert-Equal $summary.classification $classification "R50 classification"
Assert-True $summary.r49PhaseReservationBlockerCleared "R49 phase reservation blocker remains."
Assert-True $summary.nextRetryPhaseExplicitlySupported "Next retry phase not explicitly supported."
Assert-Equal $summary.nextRetryPhase "LMAX-R51" "Next retry phase"
Assert-True $summary.arbitraryUnapprovedPhasesRejected "Arbitrary phases are not rejected."
Assert-True $summary.r42AdapterExecutablePathValid "R42 adapter path not valid."
Assert-True $summary.r44BoundedRuntimeCompositionValid "R44 composition not valid."
Assert-True $summary.r46ExecutableBoundaryOperationCompositionValid "R46 composition not valid."
Assert-True $summary.r48ExternalBoundaryProviderExecutionCompositionValid "R48 composition not valid."
Assert-False $summary.additionalKnownPreExternalBlockerFound "Additional pre-external blocker found."
Assert-False $summary.externalActivationAttempted "R50 attempted activation."
Assert-False $summary.externalBoundaryAttempted "R50 attempted external boundary."
Assert-False $summary.realCredentialValuesRead "R50 read real credentials."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must be false."
Assert-True $summary.buildRepresented "Build evidence missing."
Assert-True $summary.testsRepresented "Test evidence missing."
$checks.summaryPassed = $true

$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-r49-blocker-before-after-classification.json")
Assert-True $beforeAfter.before.present "Before blocker should be present."
Assert-False $beforeAfter.after.present "After blocker should be cleared."
Assert-True $beforeAfter.after.cleared "After blocker not cleared."
$checks.beforeAfterPassed = $true

$sweep = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-known-pre-external-blocker-sweep.json")
Assert-True $sweep.passed "Known pre-external blocker sweep failed."
Assert-False $sweep.additionalKnownPreExternalBlockerFound "Additional known blocker found."
foreach ($blocker in $sweep.knownBlockers) {
    Assert-False $blocker.presentAfterR50 "Known blocker remains: $($blocker.code)"
}
$checks.blockerSweepPassed = $true

$phase = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-phase-reservation-validation.json")
Assert-True $phase.passed "Phase reservation validation failed."
Assert-True $phase.r49Supported "R49 is not supported."
Assert-True $phase.r51Supported "R51 is not supported."
Assert-False $phase.arbitraryPhaseSupported "Arbitrary phase accepted."
Assert-True (($phase.explicitReservedPhases -join ",") -eq "LMAX-R43,LMAX-R45,LMAX-R47,LMAX-R49,LMAX-R51") "Explicit reserved phases mismatch."
$checks.phaseReservationPassed = $true

$chain = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-composition-chain-validation.json")
Assert-True $chain.passed "Composition chain validation failed."
foreach ($field in @("r42AdapterExecutablePathValid","r44BoundedRuntimeCompositionValid","r46ExecutableBoundaryOperationCompositionValid","r48ExternalBoundaryProviderExecutionCompositionValid","approvalGatesPreserved","noKnownAdditionalPreExternalBlocker")) {
    Assert-True $chain.$field "Composition chain flag failed: $field"
}
$checks.compositionChainPassed = $true

$bounded = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-bounded-executor-validation.json")
Assert-True $bounded.passed "Bounded executor validation failed."
Assert-True ([int]$bounded.maxAttemptCount -eq 1) "maxAttemptCount must be one."
Assert-True ([int]$bounded.retryCount -eq 0) "retryCount must be zero."
Assert-False $bounded.batchMode "batchMode enabled."
Assert-False $bounded.loopMode "loopMode enabled."
Assert-False $bounded.externalActionExecuted "Bounded validation executed external action."
Assert-False $bounded.externalBoundaryAttempted "Bounded validation attempted external boundary."
$checks.boundedExecutorPassed = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-forbidden-actions-audit.json")
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
    "credentialValuesRead",
    "credentialValuesReturned",
    "credentialPrinted",
    "credentialStored",
    "rawSensitiveFixLogsStored",
    "runtimeEnablementPersisted"
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
Assert-True $forbidden.passed "Forbidden action audit failed."
$checks.forbiddenActionsPassed = $true

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
$checks.apiWorkerFakeGatewayPassed = $true

$launcher = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-no-live-launcher-audit.json")
Assert-True $launcher.passed "No-live-launcher audit failed."
foreach ($field in @("liveLauncherCreated","consoleAppCreated","scriptCreated","hostedServiceAdded","backgroundServiceAdded","schedulerAdded","pollingAdded","apiEndpointAdded")) {
    Assert-False $launcher.$field "Launcher/service flag was true: $field"
}
$checks.noLiveLauncherPassed = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-no-external-boundary-attempted.json")
Assert-True $boundary.passed "External boundary audit failed."
foreach ($field in @("credentialConfig","tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries")) {
    Assert-Equal $boundary.boundaryStatuses.$field "NotAttempted" "Boundary status for $field"
}
$checks.noExternalBoundaryPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat preservation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-next-phase-recommendation.json")
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R51*") "Missing R51 recommendation."
Assert-False $next.r51Executed "R51 was executed."
$checks.nextPhasePassed = $true

$reservationSource = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs") -Raw
foreach ($phaseName in @("LMAX-R43", "LMAX-R45", "LMAX-R47", "LMAX-R49", "LMAX-R51")) {
    Assert-True ($reservationSource -match [regex]::Escape($phaseName)) "Reservation source missing $phaseName."
}
Assert-True ($reservationSource -notmatch 'LMAX-R999|StartsWith|Contains\("LMAX-R"|Regex') "Reservation source appears to accept arbitrary phases."
foreach ($file in @(
    "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter.cs",
    "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxConcreteBoundedRuntimeActivationComposition.cs",
    "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxExecutableBoundaryOperationComposition.cs",
    "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxExternalBoundaryProviderExecutionComposition.cs"
)) {
    $text = Get-Content -LiteralPath (Join-Path $Root $file) -Raw
    Assert-True ($text -match "LmaxApprovedBoundedExecutableRetryPhaseReservations") "$file does not use consolidated reservations."
}
$checks.sourceReservationPassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r50-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R50 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R50 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R50 artifacts contain forbidden raw sensitive FIX message type."
$checks.sanitizationPassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("LmaxApprovedBoundedExecutableRetryPhaseReservations", "ApprovedBoundedExecutableReadOnly", "phase-lmax-r50")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R50 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R50 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R50 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R50"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r50-final-pre-external-consolidation-fix.ps1"
    passed = $true
    classification = $summary.classification
    r49PhaseReservationBlockerCleared = $summary.r49PhaseReservationBlockerCleared
    nextRetryPhaseExplicitlySupported = $summary.nextRetryPhaseExplicitlySupported
    arbitraryUnapprovedPhasesRejected = $summary.arbitraryUnapprovedPhasesRejected
    additionalKnownPreExternalBlockerFound = $summary.additionalKnownPreExternalBlockerFound
    externalActivationAttempted = $summary.externalActivationAttempted
    credentialValuesReturned = $summary.credentialValuesReturned
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R50 gate validation passed: $gatePath"

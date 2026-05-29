param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r52-gate-validation.json"
$classification = "LMAX_R52_PASS_CREDENTIAL_CONFIG_SOURCE_BINDING_FIXED_NO_EXTERNAL_ACTIVATION"
$r51Cause = "NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

$allowedClassifications = @(
    "LMAX_R52_PASS_CREDENTIAL_CONFIG_SOURCE_BINDING_FIXED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R52_FAIL_R51_CREDENTIAL_CONFIG_BINDING_STILL_MISSING",
    "LMAX_R52_FAIL_CREDENTIAL_CONFIG_SOURCE_NOT_PROVABLE",
    "LMAX_R52_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R52_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R52_FAIL_BOUNDED_PATH_REGRESSION",
    "LMAX_R52_FAIL_COMPOSITION_CHAIN_REGRESSION",
    "LMAX_R52_FAIL_APPROVAL_GATES_WEAKENED",
    "LMAX_R52_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R52_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_INTRODUCED",
    "LMAX_R52_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R52_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R52_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R52_FAIL_BUILD_OR_TESTS"
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
    "phase-lmax-r52-credential-config-source-binding-fix-report.md",
    "phase-lmax-r52-credential-config-source-binding-summary.json",
    "phase-lmax-r52-r51-blocker-before-after-classification.json",
    "phase-lmax-r52-credential-config-binding-validation.json",
    "phase-lmax-r52-secret-sanitization-validation.json",
    "phase-lmax-r52-bounded-path-validation.json",
    "phase-lmax-r52-composition-chain-validation.json",
    "phase-lmax-r52-approved-instrument-scope-validation.json",
    "phase-lmax-r52-forbidden-actions-audit.json",
    "phase-lmax-r52-api-worker-fake-gateway-audit.json",
    "phase-lmax-r52-no-live-launcher-audit.json",
    "phase-lmax-r52-no-external-boundary-attempted.json",
    "phase-lmax-r52-usdjpy-caveat-preservation.json",
    "phase-lmax-r52-next-phase-recommendation.json",
    "phase-lmax-r52-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R52 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$r51 = Read-Json (Join-Path $artifactRoot "phase-lmax-r51-temporary-readonly-activation-retry-summary.json")
Assert-Equal $r51.classification "LMAX_R51_FAIL_CREDENTIAL_CONFIG_MISSING" "R51 classification"
Assert-Equal $r51.concreteCause $r51Cause "R51 concrete cause"
Assert-False $r51.externalActivationAttempted "R51 unexpectedly attempted external activation."
$checks.r51BlockerEvidencePresent = $true

$r50 = Read-Json (Join-Path $artifactRoot "phase-lmax-r50-gate-validation.json")
Assert-True $r50.passed "R50 gate validation did not pass."
Assert-Equal $r50.classification "LMAX_R50_PASS_FINAL_PRE_EXTERNAL_APPROVAL_COMPOSITION_CONSOLIDATION_NO_EXTERNAL_ACTIVATION" "R50 classification"
Assert-True $r50.nextRetryPhaseExplicitlySupported "R50 next retry phase not explicit."
Assert-True $r50.arbitraryUnapprovedPhasesRejected "R50 arbitrary phase rejection missing."
$checks.r50GateEvidencePresent = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-credential-config-source-binding-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R52 classification not allowed: $($summary.classification)"
Assert-Equal $summary.classification $classification "R52 classification"
Assert-False $summary.noApprovedR51CredentialConfigOperationBindingForSecretValueLoad "R51 credential/config binding blocker remains true."
Assert-True $summary.approvedDemoReadOnlyCredentialConfigSourceBindingProvable "Approved Demo/read-only credential/config source binding not provable."
Assert-True $summary.sourcePresent "Credential/config source not present."
Assert-True $summary.sourceApproved "Credential/config source not approved."
Assert-True $summary.sourceReachableOnlyThroughBoundedPath "Credential/config source not restricted to bounded path."
Assert-True $summary.sourceStructurallyLoadable "Credential/config source not structurally loadable."
Assert-True $summary.credentialConfigOperationBindingPresent "Credential/config operation binding missing."
Assert-Equal $summary.adapterMode "ApprovedBoundedExecutableReadOnly" "Adapter mode"
Assert-True $summary.boundedExecutorApproved "Bounded executor approval missing."
Assert-True $summary.runtimeDelegateBindingApproved "Runtime delegate binding approval missing."
Assert-True $summary.r42AdapterExecutablePathValid "R42 gate regressed."
Assert-True $summary.r44BoundedRuntimeCompositionValid "R44 gate regressed."
Assert-True $summary.r46ExecutableBoundaryOperationCompositionValid "R46 gate regressed."
Assert-True $summary.r48ExternalBoundaryProviderExecutionCompositionValid "R48 gate regressed."
Assert-True $summary.r50PreExternalConsolidationValid "R50 gate regressed."
Assert-True $summary.arbitraryUnapprovedPhasesRejected "Arbitrary phases are not rejected."
Assert-False $summary.productionAccountAllowed "Production account/config allowed."
Assert-False $summary.productionAccountUsed "Production account/config used."
Assert-False $summary.externalActivationAttempted "External activation attempted."
Assert-False $summary.externalBoundaryAttempted "External boundary attempted."
Assert-Equal ([int]$summary.attemptCount) 0 "Attempt count"
Assert-False $summary.credentialValuesRead "Credential values read."
Assert-False $summary.credentialValuesReturned "Credential values returned."
Assert-False $summary.credentialValuesPrinted "Credential values printed."
Assert-False $summary.credentialValuesStored "Credential values stored."
Assert-False $summary.credentialValuesSerialized "Credential values serialized."
Assert-False $summary.realCredentialValuesRead "Real credential values read."
Assert-True $summary.outputSanitized "Output not sanitized."
Assert-True $summary.buildRepresented "Build evidence missing."
Assert-True $summary.testsRepresented "Test evidence missing."
Assert-True ([string]$summary.buildResult -like "PASS*") "Build result is not PASS."
Assert-True ([string]$summary.focusedTestResult -like "PASS*") "Focused test result is not PASS."
Assert-True ([string]$summary.fullTestResult -like "PASS*") "Full test result is not PASS."
$checks.summaryPassed = $true

$binding = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-credential-config-binding-validation.json")
Assert-True $binding.passed "Credential/config binding validation failed."
Assert-False $binding.noApprovedR51CredentialConfigOperationBindingForSecretValueLoad "Binding validation still has R51 blocker."
Assert-True $binding.approvedDemoReadOnlyCredentialConfigSourceBindingProvable "Binding validation not provable."
Assert-True $binding.sourcePresent "Binding source missing."
Assert-True $binding.sourceApproved "Binding source not approved."
Assert-True $binding.sourceReachableOnlyThroughBoundedPath "Binding source reachable outside bounded path."
Assert-True $binding.sourceStructurallyLoadable "Binding source not structurally loadable."
Assert-Equal $binding.credentialConfigBoundary "ValidationOnly" "Credential/config boundary status"
Assert-False $binding.realCredentialValuesRead "Binding read real credential values."
Assert-False $binding.credentialValuesReturned "Binding returned credential values."
Assert-False $binding.credentialValuesPrinted "Binding printed credential values."
Assert-False $binding.credentialValuesStored "Binding stored credential values."
Assert-False $binding.credentialValuesSerialized "Binding serialized credential values."
Assert-False $binding.productionAccountAllowed "Binding allowed production account."
Assert-False $binding.productionAccountUsed "Binding used production account."
Assert-False $binding.externalBoundaryAttempted "Binding attempted external boundary."
foreach ($field in $binding.requiredFieldPresence) {
    Assert-True $field.bindingPresent "Required field binding missing: $($field.fieldLabel)"
    Assert-False $field.valueReturned "Required field value returned: $($field.fieldLabel)"
}
$checks.credentialConfigBindingPassed = $true

$secret = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-secret-sanitization-validation.json")
Assert-True $secret.passed "Secret sanitization validation failed."
Assert-True $secret.outputSanitized "Secret output not sanitized."
foreach ($field in @("credentialValuesRead","credentialValuesReturned","credentialValuesPrinted","credentialValuesStored","credentialValuesSerialized","realCredentialValuesRead","rawCredentialsInArtifacts","rawSensitiveFixLogsInArtifacts","credentialDerivedValuesInArtifacts","fullAccountIdentifiersInArtifacts","productionEndpointInArtifacts")) {
    Assert-False $secret.$field "Secret sanitization flag was true: $field"
}
$checks.secretSanitizationPassed = $true

$bounded = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-bounded-path-validation.json")
Assert-True $bounded.passed "Bounded path validation failed."
Assert-Equal $bounded.adapterMode "ApprovedBoundedExecutableReadOnly" "Bounded path adapter mode"
Assert-True $bounded.boundedExecutorApproved "Bounded path executor approval missing."
Assert-True $bounded.runtimeDelegateBindingApproved "Bounded path runtime delegate approval missing."
Assert-True $bounded.sourceReachableOnlyThroughBoundedPath "Bounded path source restriction missing."
Assert-True $bounded.notReachableFromApiWorkerDefaultStartup "Source reachable from API/Worker startup."
Assert-False $bounded.externalBoundaryAttempted "Bounded path attempted external boundary."
Assert-Equal $bounded.credentialConfigBoundary "ValidationOnly" "Bounded path credential boundary"
foreach ($field in @("tcpSocket","tls","fixLogonSession","marketDataRequest")) {
    Assert-Equal $bounded.$field "NotAttempted" "Bounded path boundary status for $field"
}
$checks.boundedPathPassed = $true

$composition = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-composition-chain-validation.json")
Assert-True $composition.passed "Composition chain validation failed."
foreach ($field in @("r42AdapterExecutablePathValid","r44BoundedRuntimeCompositionValid","r46ExecutableBoundaryOperationCompositionValid","r48ExternalBoundaryProviderExecutionCompositionValid","r50PreExternalConsolidationValid","r51CredentialConfigBindingBlockerCleared","arbitraryUnapprovedPhasesRejected")) {
    Assert-True $composition.$field "Composition chain flag missing: $field"
}
foreach ($field in @("approvalGatesWeakened","apiWorkerGatewayRegression","externalBoundaryAttempted")) {
    Assert-False $composition.$field "Composition chain regression flag true: $field"
}
$checks.compositionChainPassed = $true

$instrument = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-approved-instrument-scope-validation.json")
Assert-True $instrument.passed "Instrument scope validation failed."
Assert-True $instrument.approvedInstrumentsExact "Approved instruments not exact."
Assert-False $instrument.nonApprovedInstrumentConfigured "Non-approved instrument configured."
$symbols = @($instrument.approvedInstruments | ForEach-Object { $_.symbol } | Sort-Object)
Assert-Equal ($symbols -join ",") "AUDUSD,EURGBP,GBPUSD,USDJPY" "Approved instrument symbols"
$checks.instrumentScopePassed = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-forbidden-actions-audit.json")
Assert-True $forbidden.passed "Forbidden actions audit failed."
foreach ($field in @(
    "orderSubmissionExecuted",
    "newOrderSingleTouched",
    "orderPathIntroduced",
    "orderPathTouched",
    "tradingEnablementExecuted",
    "tradingStateMutated",
    "productionAccountUsed",
    "productionAccountAllowed",
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
    "credentialSerialized",
    "rawSensitiveFixLogsStored",
    "runtimeEnablementPersisted",
    "externalActivationAttempted",
    "externalBoundaryAttempted"
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
$checks.forbiddenActionsPassed = $true

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker FakeLmaxGateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
Assert-False $api.appsettingsLiveEnablementIntroduced "Appsettings live enablement introduced."
Assert-True $api.requireFakeExecutionGateway "RequireFakeExecutionGateway not true."
Assert-False $api.allowExternalConnections "AllowExternalConnections not false."
Assert-False $api.boundedCredentialConfigBindingWiredIntoApiWorker "Credential binding wired into API/Worker."
$checks.apiWorkerFakeGatewayPassed = $true

$launcher = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-no-live-launcher-audit.json")
Assert-True $launcher.passed "No-live-launcher audit failed."
foreach ($field in @("liveLauncherCreated","mainAdded","consoleAppAdded","hostedServiceAdded","backgroundServiceAdded","schedulerAdded","pollingLoopAdded","apiEndpointAdded","workerStartupAdded")) {
    Assert-False $launcher.$field "Launcher/background flag was true: $field"
}
$checks.noLiveLauncherPassed = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-no-external-boundary-attempted.json")
Assert-True $boundary.passed "No-external-boundary validation failed."
Assert-False $boundary.externalActivationAttempted "External activation attempted."
Assert-False $boundary.externalBoundaryAttempted "External boundary attempted."
Assert-Equal ([int]$boundary.attemptCount) 0 "Boundary attempt count"
Assert-True (@("NotAttempted","ValidationOnly") -contains [string]$boundary.credentialConfig) "Credential/config boundary must be NotAttempted or ValidationOnly."
foreach ($field in @("tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries")) {
    Assert-Equal $boundary.$field "NotAttempted" "Boundary status for $field"
}
Assert-False $boundary.credentialValuesReturned "Boundary evidence returned credential values."
Assert-False $boundary.realCredentialValuesRead "Boundary evidence read real credential values."
$checks.noExternalBoundaryPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat preservation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.lineage "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY lineage"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-True $usd.caveatPreserved "USDJPY caveat not preserved."
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-next-phase-recommendation.json")
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R53*") "Missing R53 recommendation."
Assert-False $next.r53Executed "R53 was executed."
$checks.nextPhasePassed = $true

$sourcePath = Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxCredentialConfigSourceBinding.cs"
$testPath = Join-Path $Root "tests/QQ.Production.Intraday.Tests.Unit/LmaxCredentialConfigSourceBindingTests.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($needle in @(
    "NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad",
    "ApprovedDemoReadOnlyCredentialConfigSourceBindingProvable",
    "CreateApprovedOperation",
    "CredentialSecretMaterialLoadNotAllowedInR52",
    "CredentialValuesReturnedOrExposed"
)) {
    Assert-True ($source -match [regex]::Escape($needle)) "R52 source missing proof token: $needle"
}
foreach ($needle in @(
    "R51_credential_config_binding_blocker_is_cleared",
    "Production_credential_config_source_is_rejected",
    "Credential_values_are_not_allowed",
    "Binding_is_not_reachable_outside_the_bounded_path"
)) {
    Assert-True ($tests -match [regex]::Escape($needle)) "R52 tests missing proof token: $needle"
}
$checks.sourceAndTestsPresent = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("LmaxCredentialConfigSourceBinding", "phase-lmax-r52", "ApprovedBoundedExecutableReadOnly", "LmaxTemporaryReadOnlyActivationExecutor")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R52 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R52 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R52 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r52-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R52 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R52 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R52 artifacts contain forbidden raw sensitive FIX message type."
$checks.artifactSanitizationPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R52"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r52-credential-config-source-binding-fix.ps1"
    passed = $true
    classification = $classification
    noApprovedR51CredentialConfigOperationBindingForSecretValueLoad = $false
    approvedDemoReadOnlyCredentialConfigSourceBindingProvable = $true
    credentialValuesReturned = $false
    externalActivationAttempted = $false
    attemptCount = 0
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R52 gate validation passed: $gatePath"

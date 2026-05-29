param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing required file: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message. Expected '$Expected' but got '$Actual'."
    }
}

function Assert-True($Actual, [string]$Message) {
    if ($Actual -ne $true) {
        throw "$Message. Expected true but got '$Actual'."
    }
}

function Assert-False($Actual, [string]$Message) {
    if ($Actual -ne $false) {
        throw "$Message. Expected false but got '$Actual'."
    }
}

function Assert-NoSensitiveContent([string]$Path) {
    $text = Get-Content -LiteralPath $Path -Raw
    $patterns = @(
        '(?i)password\s*[:=]\s*[^,\s\}\]]+',
        '(?i)api[_-]?key\s*[:=]\s*[^,\s\}\]]+',
        '(?i)secret\s*[:=]\s*[^,\s\}\]]+',
        '(?i)sessionpassword\s*[:=]\s*[^,\s\}\]]+'
    )

    foreach ($pattern in $patterns) {
        if ($text -match $pattern) {
            throw "Sensitive-content marker found in $Path"
        }
    }
}

$readiness = Join-Path $RepoRoot "artifacts\readiness\lmax-runtime-enablement"
$usdJpy = Join-Path $RepoRoot "artifacts\readiness\usdjpy-troubleshooting"

$requiredR4 = @(
    "phase-lmax-r4-r3-preflight-abort-root-cause-review.json",
    "phase-lmax-r4-narrow-runtime-path-requirements.json",
    "phase-lmax-r4-component-remediation-design.json",
    "phase-lmax-r4-future-r5-implementation-plan.json",
    "phase-lmax-r4-hard-block-validation-design.json",
    "phase-lmax-r4-remediation-decision-gate.json",
    "phase-lmax-r4-non-run-validation.json",
    "phase-lmax-r4-preflight-abort-remediation-pack.md",
    "phase-lmax-r4-operator-note.md"
)

$requiredR1 = @(
    "phase-lmax-r1-runtime-enablement-boundary-map.json",
    "phase-lmax-r1-component-impact-review.json",
    "phase-lmax-r1-readonly-safety-model.json",
    "phase-lmax-r1-future-r2-preconditions.json",
    "phase-lmax-r1-design-only-decision-gate.json"
)

$requiredR2 = @(
    "phase-lmax-r2-runtime-readonly-preflight-checklist.json",
    "phase-lmax-r2-future-r3-operator-approval-model.json",
    "phase-lmax-r2-future-r3-activation-boundary-design.json",
    "phase-lmax-r2-hard-block-matrix.json",
    "phase-lmax-r2-future-r3-evidence-schema.json",
    "phase-lmax-r2-test-validator-plan.json",
    "phase-lmax-r2-preflight-decision-gate.json"
)

$requiredR3 = @(
    "phase-lmax-r3-operator-approval-record.json",
    "phase-lmax-r3-preflight-gate.json",
    "phase-lmax-r3-temporary-runtime-activation-record.json",
    "phase-lmax-r3-approved-instrument-status-record.json",
    "phase-lmax-r3-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r3-forbidden-action-validation.json",
    "phase-lmax-r3-shutdown-revert-record.json",
    "phase-lmax-r3-post-attempt-non-mutation-validation.json",
    "phase-lmax-r3-rail-isolation-validation.json",
    "phase-lmax-r3-decision-gate.json"
)

Write-Host "LMAX-R4 Preflight Abort Remediation Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime activation, or config mutation."

foreach ($file in ($requiredR1 + $requiredR2 + $requiredR3 + $requiredR4)) {
    $path = Join-Path $readiness $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required artifact: $path"
    }
    Assert-NoSensitiveContent $path
}

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) {
    throw "Missing USDJPY T7 closure gate: $t7GatePath"
}
Assert-NoSensitiveContent $t7GatePath

$phase7nGatePath = Join-Path $RepoRoot "artifacts\readiness\phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
if (-not (Test-Path -LiteralPath $phase7nGatePath)) {
    throw "Missing Phase 7N closure gate: $phase7nGatePath"
}
Assert-NoSensitiveContent $phase7nGatePath

$r1Gate = Read-Json (Join-Path $readiness "phase-lmax-r1-design-only-decision-gate.json")
$r2Gate = Read-Json (Join-Path $readiness "phase-lmax-r2-preflight-decision-gate.json")
$r3Gate = Read-Json (Join-Path $readiness "phase-lmax-r3-decision-gate.json")
$r4RootCause = Read-Json (Join-Path $readiness "phase-lmax-r4-r3-preflight-abort-root-cause-review.json")
$r4Requirements = Read-Json (Join-Path $readiness "phase-lmax-r4-narrow-runtime-path-requirements.json")
$r4Components = Read-Json (Join-Path $readiness "phase-lmax-r4-component-remediation-design.json")
$r4Plan = Read-Json (Join-Path $readiness "phase-lmax-r4-future-r5-implementation-plan.json")
$r4Blocks = Read-Json (Join-Path $readiness "phase-lmax-r4-hard-block-validation-design.json")
$r4Gate = Read-Json (Join-Path $readiness "phase-lmax-r4-remediation-decision-gate.json")
$r4NonRun = Read-Json (Join-Path $readiness "phase-lmax-r4-non-run-validation.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r1Gate.finalDecision "LMAX_R1_RUNTIME_ENABLEMENT_DESIGN_REVIEW_COMPLETE_NO_ENABLEMENT" "R1 decision"
Assert-Equal $r2Gate.finalDecision "LMAX_R2_READONLY_RUNTIME_PREFLIGHT_READY_NO_ACTIVATION" "R2 decision"
Assert-Equal $r3Gate.finalDecision "LMAX_R3_FAIL_PREFLIGHT_ABORTED" "R3 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

Assert-Equal $r4Gate.phase "LMAX-R4" "R4 phase"
Assert-Equal $r4Gate.finalDecision "LMAX_R4_PREFLIGHT_ABORT_REMEDIATION_READY_NO_ACTIVATION" "R4 decision"
Assert-True $r4Gate.r3AbortReviewed "R3 abort reviewed"
Assert-True $r4Gate.r3AbortWasSafetySuccess "R3 abort safety success"
Assert-False $r4Gate.r3AbortWasLmaxConnectivityFailure "R3 abort was not connectivity failure"
Assert-True $r4Gate.missingNarrowRuntimePathIdentified "Missing path identified"
Assert-True $r4Gate.remediationPackCompleted "Remediation pack completed"
Assert-False $r4Gate.r5Authorized "R5 authorized"

Assert-True $r4RootCause.r3OperatorApprovalValid "R3 approval valid"
Assert-True $r4RootCause.r3PreflightWorkedCorrectly "R3 preflight worked"
Assert-False $r4RootCause.connectionAttemptedInR3 "R3 connection attempted"
Assert-Equal $r4RootCause.abortClassification "SafetySuccessNotLmaxConnectivityFailure" "R3 abort classification"
Assert-True $r4Requirements.pathRequired "Narrow path required"

$componentNames = @($r4Components.componentClassification | ForEach-Object { $_.component })
foreach ($requiredComponent in @("gateway registration", "LMAX read-only runtime adapter", "Runtime activation scope object", "Instrument allowlist", "Safety flags", "Operator approval artifact", "Audit events", "Sanitization layer", "Shutdown/revert mechanism", "Non-mutation validator", "Rail isolation validator", "Config override mechanism", "Test harness")) {
    if (-not (@($componentNames | Where-Object { $_ -like "*$requiredComponent*" }).Count -gt 0)) {
        throw "Component remediation design missing: $requiredComponent"
    }
}

Assert-False $r4Plan.r5AuthorizedByR4 "R5 authorized by R4"
foreach ($forbidden in @("Connect to LMAX", "Start API/Worker", "Perform snapshots", "Send MarketDataRequests", "Create a live launcher", "Change default gateway registration")) {
    if (-not ($r4Plan.forbiddenR5Actions -contains $forbidden)) {
        throw "R5 plan missing forbidden action: $forbidden"
    }
}

foreach ($block in @("OrderGatewayRegistration", "TradingGatewayRegistration", "AllowOrderSubmission", "AllowLiveTrading", "IsTradingEnabled", "SchedulerAutoStart", "PollingAutoStart", "ReplayEnabled", "ShadowReplaySubmitEnabled", "ProductionAccount", "NonApprovedInstruments", "UsdJpyWithoutCaveat", "PermanentDefaultRuntimeEnablement", "MissingOperatorApproval", "MissingSanitization", "MissingShutdownRevertPlan")) {
    if (-not (@($r4Blocks.hardBlocks | Where-Object { $_.block -eq $block }).Count -eq 1)) {
        throw "Hard-block design missing: $block"
    }
}

foreach ($property in @(
    "externalRunExecuted",
    "snapshotExecuted",
    "replayExecuted",
    "postEndpointInvoked",
    "realSocketOpened",
    "tcpConnectionAttempted",
    "tlsHandshakeAttempted",
    "fixLogonAttempted",
    "marketDataRequestSent",
    "orderSubmissionExecuted",
    "tradingStateMutated",
    "schedulerStarted",
    "pollingStarted",
    "shadowReplaySubmitted",
    "apiWorkerStarted",
    "runtimePoweredUp",
    "retryExecuted",
    "batchExecuted",
    "loopExecuted",
    "runtimeEnablementExecuted",
    "tradingEnablementExecuted",
    "schedulerEnablementExecuted",
    "orderPathEnablementExecuted",
    "defaultGatewayRegistrationChanged",
    "liveConnectionScriptCreated",
    "r5Authorized"
)) {
    Assert-False $r4Gate.$property "R4 gate $property"
    Assert-False $r4NonRun.$property "R4 non-run $property"
}

Assert-Equal $r4Gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "R4 gate gateway mode"
Assert-Equal $r4NonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "R4 non-run gateway mode"
Assert-False $r4Gate.evidenceArchivesModified "Evidence archives modified"
Assert-True $r4Gate.usdJpyT7ClosureIntact "USDJPY T7 closure intact"
Assert-True $r4Gate.validatedRailsArchivesIntact "Validated rails intact"

$apiProgram = Get-Content -LiteralPath (Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs") -Raw
$workerProgram = Get-Content -LiteralPath (Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs") -Raw
if ($apiProgram -notmatch "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>") {
    throw "API FakeLmaxGateway registration not found."
}
if ($workerProgram -notmatch "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>") {
    throw "Worker FakeLmaxGateway registration not found."
}
if ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*Lmax" -or $workerProgram -match "AddSingleton<IVenueExecutionGateway,\s*Lmax") {
    throw "Real LMAX execution gateway registration detected."
}

$appSettings = Read-Json (Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json")
Assert-False $appSettings.Safety.AllowExternalConnections "Safety:AllowExternalConnections"
Assert-False $appSettings.Safety.AllowLiveTrading "Safety:AllowLiveTrading"
Assert-True $appSettings.Safety.RequireFakeExecutionGateway "Safety:RequireFakeExecutionGateway"
Assert-False $appSettings.LmaxReadOnlyRuntime.Enabled "LmaxReadOnlyRuntime:Enabled"
Assert-Equal $appSettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "LmaxReadOnlyRuntime:ImplementationMode"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowExternalConnections "LmaxReadOnlyRuntime:AllowExternalConnections"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowOrderSubmission "LmaxReadOnlyRuntime:AllowOrderSubmission"
Assert-False $appSettings.LmaxReadOnlyRuntime.SchedulerEnabled "LmaxReadOnlyRuntime:SchedulerEnabled"
Assert-False $appSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "LmaxReadOnlyRuntime:SubmitToShadowReplay"

$report = Get-Content -LiteralPath (Join-Path $readiness "phase-lmax-r4-preflight-abort-remediation-pack.md") -Raw
foreach ($marker in @(
    "Executive summary",
    "R3 preflight abort summary",
    "Why the abort was correct",
    "Scope and non-run guarantees",
    "Missing narrow runtime path requirements",
    "Component remediation design",
    "Future R5 implementation plan",
    "Hard-block validation design",
    "What R4 allows",
    "What remains forbidden",
    "Decision",
    "Recommended next phase"
)) {
    if ($report -notmatch [regex]::Escape($marker)) {
        throw "Report missing marker: $marker"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R4"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    validator = "scripts/check-lmax-r4-preflight-abort-remediation-gate.ps1"
    allRequiredArtifactsExist = $true
    r1Decision = $r1Gate.finalDecision
    r2Decision = $r2Gate.finalDecision
    r3Decision = $r3Gate.finalDecision
    r4Decision = $r4Gate.finalDecision
    externalRunExecuted = $false
    snapshotExecuted = $false
    replayExecuted = $false
    postEndpointInvoked = $false
    realSocketOpened = $false
    tcpConnectionAttempted = $false
    tlsHandshakeAttempted = $false
    fixLogonAttempted = $false
    marketDataRequestSent = $false
    orderSubmissionExecuted = $false
    tradingStateMutated = $false
    schedulerStarted = $false
    pollingStarted = $false
    shadowReplaySubmitted = $false
    apiWorkerStarted = $false
    runtimePoweredUp = $false
    runtimeEnablementExecuted = $false
    tradingEnablementExecuted = $false
    schedulerEnablementExecuted = $false
    orderPathEnablementExecuted = $false
    defaultGatewayRegistrationChanged = $false
    liveConnectionScriptCreated = $false
    r5Authorized = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    finalDecision = $r4Gate.finalDecision
    result = "PASS"
}

$validationPath = Join-Path $readiness "phase-lmax-r4-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "FinalDecision: $($r4Gate.finalDecision)"
Write-Host "Report: $validationPath"

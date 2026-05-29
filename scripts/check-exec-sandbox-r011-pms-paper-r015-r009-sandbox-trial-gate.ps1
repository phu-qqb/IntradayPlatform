param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts/readiness/execution-sandbox"

function Fail([string]$Message) {
    Write-Error "EXEC_SANDBOX_R011_GATE_FAIL: $Message"
    exit 1
}

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact: $Name"
    }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$required = @(
    "phase-exec-sandbox-r011-summary.md",
    "phase-exec-sandbox-r011-r010-reference.json",
    "phase-exec-sandbox-r011-pms-paper-r015-intake.json",
    "phase-exec-sandbox-r011-q4e-bootstrap-historical-only-confirmation.json",
    "phase-exec-sandbox-r011-exec-algo-field-completion.json",
    "phase-exec-sandbox-r011-missing-exec-algo-field-diagnostics.json",
    "phase-exec-sandbox-r011-side-derivation-evidence.json",
    "phase-exec-sandbox-r011-broker-symbol-mapping.json",
    "phase-exec-sandbox-r011-sandbox-risk-gate-result.json",
    "phase-exec-sandbox-r011-r009-sandbox-decision.json",
    "phase-exec-sandbox-r011-sandbox-order-intent.json",
    "phase-exec-sandbox-r011-sandbox-route.json",
    "phase-exec-sandbox-r011-sandbox-submission-result.json",
    "phase-exec-sandbox-r011-sandbox-ack-reject.json",
    "phase-exec-sandbox-r011-sandbox-execution-report.json",
    "phase-exec-sandbox-r011-sandbox-fill-report.json",
    "phase-exec-sandbox-r011-flatten-order-intent.json",
    "phase-exec-sandbox-r011-flatten-submission-result.json",
    "phase-exec-sandbox-r011-flatten-execution-report.json",
    "phase-exec-sandbox-r011-flatten-fill-report.json",
    "phase-exec-sandbox-r011-final-reconciliation.json",
    "phase-exec-sandbox-r011-trial-decision.json",
    "phase-exec-sandbox-r011-no-secret-persistence-audit.json",
    "phase-exec-sandbox-r011-no-production-broker-audit.json",
    "phase-exec-sandbox-r011-no-production-order-audit.json",
    "phase-exec-sandbox-r011-no-production-route-audit.json",
    "phase-exec-sandbox-r011-no-production-fill-report-audit.json",
    "phase-exec-sandbox-r011-no-production-ledger-audit.json",
    "phase-exec-sandbox-r011-no-paper-ledger-commit-audit.json",
    "phase-exec-sandbox-r011-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r011-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r011-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r011-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r011-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r011-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r011-cost-guidance-preservation.json",
    "phase-exec-sandbox-r011-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r011-forbidden-actions-audit.json",
    "phase-exec-sandbox-r011-next-phase-recommendation.json",
    "phase-exec-sandbox-r011-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactDir $file))) {
        Fail "Missing required artifact: $file"
    }
}

$summary = Get-Content -LiteralPath (Join-Path $ArtifactDir "phase-exec-sandbox-r011-summary.md") -Raw
foreach ($classification in @(
    "EXEC_SANDBOX_R011_PASS_PMS_PAPER_R015_INTAKE_READY",
    "EXEC_SANDBOX_R011_PASS_EXEC_ALGO_FIELD_COMPLETION_READY",
    "EXEC_SANDBOX_R011_INCONCLUSIVE_SAFE_SANDBOX_ORDER_REJECTED_NO_POSITION",
    "EXEC_SANDBOX_R011_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
)) {
    if ($summary -notmatch [regex]::Escape($classification)) {
        Fail "Missing classification in summary: $classification"
    }
}

$q4e = Read-Json "phase-exec-sandbox-r011-q4e-bootstrap-historical-only-confirmation.json"
if ($q4e.q4eBootstrapUsedAsActiveState -or $q4e.qubes4eWorkflowRun -or $q4e.stratTakenWorkflowRun) {
    Fail "Old Qubes 4E bootstrap treated as active or run"
}

$side = Read-Json "phase-exec-sandbox-r011-side-derivation-evidence.json"
if (-not $side.sideDerived -or $side.sideInvented -or $side.status -ne "Ready") {
    Fail "Side was not safely derived from PMS evidence"
}

$risk = Read-Json "phase-exec-sandbox-r011-sandbox-risk-gate-result.json"
if ($risk.status -ne "PassedBeforeSubmission" -or
    -not $risk.existingLmaxDemoProfile -or
    -not $risk.sandboxOnly -or
    -not $risk.sandboxFixLogonConfirmedBeforeOrder -or
    -not $risk.sandboxKillSwitchOpen -or
    $risk.productionVenueAllowed -or
    $risk.productionLedgerCommitAllowed -or
    $risk.paperLedgerCommitAllowed -or
    -not $risk.symbolWhitelisted -or
    -not $risk.sideValidAndEvidenced -or
    -not $risk.quantityValid -or
    -not $risk.canonicalTargetCloseQuarterHour -or
    -not $risk.idempotencyKeyPresent -or
    $risk.directCross -or
    $risk.nonmajor) {
    Fail "Sandbox risk gate is not clean"
}

$route = Read-Json "phase-exec-sandbox-r011-sandbox-route.json"
if (-not $route.sandboxOnly -or $route.productionRoute -or $route.nonSandboxBrokerRoute -or $route.productionCredentialsUsed -or $route.brokerVenue -ne "ExistingLmaxDemoProfile") {
    Fail "Sandbox route is unsafe"
}

$submission = Read-Json "phase-exec-sandbox-r011-sandbox-submission-result.json"
if (-not $submission.sandboxOnly -or $submission.productionSubmission -or $submission.submittedOrderCount -ne 1 -or -not $submission.orderSentAfterLogonConfirmed) {
    Fail "Submission count/status violates R011 guardrails"
}

$flatten = Read-Json "phase-exec-sandbox-r011-flatten-submission-result.json"
if ($flatten.flattenSubmittedOrderCount -gt 1 -or $flatten.productionSubmission) {
    Fail "Flatten submission violates R011 guardrails"
}

$recon = Read-Json "phase-exec-sandbox-r011-final-reconciliation.json"
if ($recon.productionLedgerMutation -or $recon.paperLedgerCommit -or $recon.productionStateMutation -or -not $recon.sandboxOnly) {
    Fail "Reconciliation indicates forbidden mutation"
}
if ($recon.actualContractOrderCount -ne 1 -or $recon.flattenSubmittedCount -gt 1) {
    Fail "Reconciliation order counts violate R011 limits"
}

$forbidden = Read-Json "phase-exec-sandbox-r011-forbidden-actions-audit.json"
foreach ($property in $forbidden.PSObject.Properties) {
    if ($property.Name -in @("phase", "status", "sandboxArtifactsMarkedSandboxOnly", "existingLmaxDemoProfileMarked")) {
        continue
    }
    if ($property.Value -eq $true) {
        Fail "Forbidden action marked true: $($property.Name)"
    }
}
if (-not $forbidden.sandboxArtifactsMarkedSandboxOnly -or -not $forbidden.existingLmaxDemoProfileMarked) {
    Fail "Sandbox artifacts are not clearly marked"
}

$secret = Read-Json "phase-exec-sandbox-r011-no-secret-persistence-audit.json"
if ($secret.credentialValuesWrittenToArtifacts -or -not $secret.credentialValuesRedacted -or $secret.productionCredentialDetected) {
    Fail "Secret persistence audit failed"
}

$whitelist = Read-Json "phase-exec-sandbox-r011-usd-pair-whitelist-preservation.json"
if (-not $whitelist.submittedSymbolWhitelisted -or $whitelist.unsupportedSymbolSubmitted -or $whitelist.quantityViolatesValidatedSandboxRule -or $whitelist.audusdMisclassified) {
    Fail "USD-pair whitelist preservation failed"
}

$legacy = Read-Json "phase-exec-sandbox-r011-legacy-compatibility-preservation.json"
if ($legacy.legacy06UsedAsFutureCanonical -or $legacy.legacy21UsedAsFutureCanonical -or $legacy.legacy36UsedAsFutureCanonical -or $legacy.legacy51UsedAsFutureCanonical) {
    Fail "Legacy timestamp accepted as future canonical"
}

$usdjpy = Read-Json "phase-exec-sandbox-r011-usdjpy-caveat-preservation.json"
if ($usdjpy.caveatWeakened) {
    Fail "USDJPY caveat weakened"
}

$evidence = Read-Json "phase-exec-sandbox-r011-build-test-validator-evidence.json"
if ($evidence.build.result -ne "Passed" -or $evidence.focusedTests.result -ne "Passed" -or -not $evidence.validator.script) {
    Fail "Build/tests/validator evidence missing"
}

$combined = Get-ChildItem -LiteralPath $ArtifactDir -Filter "phase-exec-sandbox-r011-*" |
    Where-Object { -not $_.PSIsContainer } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw } |
    Out-String

$forbiddenPatterns = @(
    '"productionOrder"\s*:\s*true',
    '"productionRoute"\s*:\s*true',
    '"productionFill"\s*:\s*true',
    '"productionExecutionReport"\s*:\s*true',
    '"paperLedgerCommit"\s*:\s*true',
    '"productionLedgerCommitOccurred"\s*:\s*true',
    '"productionStateMutationOccurred"\s*:\s*true',
    '"directCrossExecutionAllowed"\s*:\s*true',
    '"legacy06AcceptedAsFutureCanonical"\s*:\s*true',
    '"audusdMisclassified"\s*:\s*true',
    '"credentialValuesWrittenToArtifacts"\s*:\s*true',
    '"q4eBootstrapUsedAsActiveState"\s*:\s*true',
    'LMAX_DEMO_FIX_PASSWORD"\s*:\s*"[^"]+',
    'LMAX_DEMO_FIX_USERNAME"\s*:\s*"[^"]+',
    'LMAX_DEMO_SENDER_COMP_ID"\s*:\s*"[^"]+',
    'LMAX_DEMO_TARGET_COMP_ID"\s*:\s*"[^"]+',
    'TargetCompId\s*[:=]\s*[A-Za-z0-9]+',
    'SenderCompId\s*[:=]\s*[A-Za-z0-9]+',
    'password\s*[:=]',
    '554='
)

foreach ($pattern in $forbiddenPatterns) {
    if ($combined -match $pattern) {
        Fail "Forbidden artifact pattern found: $pattern"
    }
}

Write-Output "EXEC_SANDBOX_R011_GATE_PASS_INCONCLUSIVE_SAFE"

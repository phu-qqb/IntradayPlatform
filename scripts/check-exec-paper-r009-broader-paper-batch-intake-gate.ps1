param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$classification, [string]$message) {
    Write-Error "$classification $message"
    exit 1
}

function Read-Json([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_PAPER_R009_FAIL_BUILD_OR_TESTS" "Missing required artifact: $path"
    }

    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-paper-r009-summary.md",
    "phase-exec-paper-r009-r057-plan-reference.json",
    "phase-exec-paper-r009-r056-preview-reference.json",
    "phase-exec-paper-r009-r009-contract-reference.json",
    "phase-exec-paper-r009-fixture-directory-inventory.json",
    "phase-exec-paper-r009-fixture-validation-results.json",
    "phase-exec-paper-r009-batch-manifest-template.json",
    "phase-exec-paper-r009-batch-manifest-validation.json",
    "phase-exec-paper-r009-accepted-batch-entries.json",
    "phase-exec-paper-r009-missing-inputs-diagnostics.json",
    "phase-exec-paper-r009-manual-noexternal-command-plan.md",
    "phase-exec-paper-r009-manual-noexternal-command-plan.json",
    "phase-exec-paper-r009-expected-output-artifacts.json",
    "phase-exec-paper-r009-next-operator-action-package.json",
    "phase-exec-paper-r009-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r009-legacy-compatibility-preservation.json",
    "phase-exec-paper-r009-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r009-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r009-cost-guidance-preservation.json",
    "phase-exec-paper-r009-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r009-no-broker-activation-audit.json",
    "phase-exec-paper-r009-no-live-marketdata-audit.json",
    "phase-exec-paper-r009-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r009-no-executable-schedule-audit.json",
    "phase-exec-paper-r009-no-child-slices-audit.json",
    "phase-exec-paper-r009-no-child-orders-audit.json",
    "phase-exec-paper-r009-no-order-created-audit.json",
    "phase-exec-paper-r009-no-real-fill-audit.json",
    "phase-exec-paper-r009-no-execution-report-audit.json",
    "phase-exec-paper-r009-no-route-no-submission-audit.json",
    "phase-exec-paper-r009-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r009-no-polygon-api-call-audit.json",
    "phase-exec-paper-r009-no-lmax-call-audit.json",
    "phase-exec-paper-r009-no-external-api-call-audit.json",
    "phase-exec-paper-r009-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r009-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r009-no-external-audit.json",
    "phase-exec-paper-r009-forbidden-actions-audit.json",
    "phase-exec-paper-r009-next-phase-recommendation.json",
    "phase-exec-paper-r009-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsRoot $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_PAPER_R009_FAIL_BUILD_OR_TESTS" "Missing required artifact: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-r009-contract-reference.json")
$inventory = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-fixture-directory-inventory.json")
$fixtureValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-fixture-validation-results.json")
$manifestTemplate = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-batch-manifest-template.json")
$manifestValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-batch-manifest-validation.json")
$accepted = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-accepted-batch-entries.json")
$missing = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-missing-inputs-diagnostics.json")
$commands = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-manual-noexternal-command-plan.json")
$expectedOutputs = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-expected-output-artifacts.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-cost-guidance-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r009-build-test-validator-evidence.json")

if ($noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.FilesDownloaded) {
    Fail "EXEC_PAPER_R009_FAIL_API_CALL_DETECTED" "No-external audit reports external activity."
}

if ($forbidden.ForbiddenActionsDetected -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.PMSCycleRun -or
    $forbidden.BacktestRun -or
    $forbidden.SimulationRun -or
    $forbidden.TcaResultLinesCreated -or
    $forbidden.ExecutableSchedulesCreated -or
    $forbidden.ChildSlicesCreated -or
    $forbidden.ChildOrdersCreated -or
    $forbidden.OrdersCreated -or
    $forbidden.FillsCreated -or
    $forbidden.ExecutionReportsCreated -or
    $forbidden.RoutesCreated -or
    $forbidden.SubmissionsCreated -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation -or
    $forbidden.CommandsExecuted) {
    Fail "EXEC_PAPER_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action audit reports a blocked action."
}

if (-not $contract.NonExecutable -or -not $contract.NotAnOrder -or -not $contract.NoBrokerRoute -or $contract.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R009_FAIL_R009_PROMOTED_TO_EXECUTABLE" "R009 contract reference weakens non-executable status."
}

if ($commands.CommandsExecutedByR009) {
    Fail "EXEC_PAPER_R009_FAIL_PMS_CYCLE_RUN" "Command plan reports a command execution."
}

foreach ($template in $commands.Templates) {
    if ($template.CommandLine -notmatch "--mode ManualNoExternal") {
        Fail "EXEC_PAPER_R009_FAIL_COMMAND_TEMPLATE_INVALID" "Command template omits ManualNoExternal mode."
    }

    if ($template.CommandLine -notmatch "--no-paper-ledger-commit true") {
        Fail "EXEC_PAPER_R009_FAIL_COMMAND_TEMPLATE_OMITS_NO_LEDGER_COMMIT" "Command template omits --no-paper-ledger-commit true."
    }

    if ($template.CommandLine -match "--mode no-external-paper-cycle" -or $template.CommandLine -match "\s--output\s") {
        Fail "EXEC_PAPER_R009_FAIL_COMMAND_TEMPLATE_INVALID" "Command template uses deprecated mode or output argument."
    }

    if (-not $template.OperatorRunOnly) {
        Fail "EXEC_PAPER_R009_FAIL_COMMAND_TEMPLATE_INVALID" "Command template is not operator-run only."
    }
}

if ($legacy.Legacy06UsedAsFutureCanonical -or -not $legacy.LegacyTimestampsCompatibilityOnly -or $canonical.Legacy06UsedAsFutureCanonical -or -not $canonical.FutureTimestampsUseCanonicalQuarterHour) {
    Fail "EXEC_PAPER_R009_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical/legacy timestamp policy is weakened."
}

if ($directCross.DirectCrossExecutionEnabled -or -not $directCross.NettingFirst) {
    Fail "EXEC_PAPER_R009_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion is weakened."
}

if ($cost.FiveUsdPerMillionUniversalized -or -not $cost.FiveUsdPerMillionBestCaseMajorOnly) {
    Fail "EXEC_PAPER_R009_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million guidance is universalized."
}

if (-not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8" -or $usdjpy.USDJPYCaveatWeakened) {
    Fail "EXEC_PAPER_R009_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat is weakened."
}

if (-not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_PAPER_R009_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD is misclassified."
}

if ($expectedOutputs.DisallowedOutputs -notcontains "paper ledger commits" -or $expectedOutputs.DisallowedOutputs -notcontains "orders") {
    Fail "EXEC_PAPER_R009_FAIL_EXPECTED_OUTPUTS_WEAKENED" "Expected output artifact requirements omit disallowed order/ledger outputs."
}

if (-not $manifestTemplate.LegacyTimestampsCompatibilityOnly -or $manifestTemplate.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R009_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Batch manifest template weakens legacy compatibility policy."
}

$status = [string]$missing.Status
if ($status -eq "NeedsOperatorFixtures") {
    if ($inventory.FixtureFileCount -ne 0 -or $fixtureValidation.AcceptedFixtureCount -ne 0 -or $accepted.AcceptedEntryCount -ne 0) {
        Fail "EXEC_PAPER_R009_FAIL_BATCH_INTAKE_INCONSISTENT" "NeedsOperatorFixtures status is inconsistent with fixture or accepted-entry counts."
    }
}
elseif ($status -eq "PartialBatchNeedsTargetCloses") {
    if ($fixtureValidation.AcceptedFixtureCount -lt 1 -or $manifestValidation.ValidBatchEntryCount -ne 0) {
        Fail "EXEC_PAPER_R009_FAIL_BATCH_INTAKE_INCONSISTENT" "Partial target-close status is inconsistent with fixture/manifest counts."
    }
}
else {
    if (-not $accepted.AcceptedBatchReady -or $accepted.AcceptedEntryCount -lt 1) {
        Fail "EXEC_PAPER_R009_FAIL_BATCH_INTAKE_INCONSISTENT" "Complete batch status lacks accepted entries."
    }
}

if ($evidence.DotnetBuild -ne "Passed" -or $evidence.FocusedR009Tests -ne "Passed" -or $evidence.UnitTests -ne "Passed" -or $evidence.R009Validator -ne "Passed") {
    Fail "EXEC_PAPER_R009_FAIL_BUILD_OR_TESTS" "Build/tests/validator evidence is missing or not passed."
}

if ($status -eq "NeedsOperatorFixtures") {
    Write-Output "EXEC_PAPER_R009_NEEDS_OPERATOR_FIXTURES_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R009_PASS_BATCH_TEMPLATE_READY_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R009_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
}
elseif ($status -eq "PartialBatchNeedsTargetCloses") {
    Write-Output "EXEC_PAPER_R009_PARTIAL_BATCH_NEEDS_TARGET_CLOSE_MANIFEST_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R009_PASS_MANUAL_NOEXTERNAL_COMMAND_TEMPLATE_READY_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R009_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
}
else {
    Write-Output "EXEC_PAPER_R009_PASS_BROADER_PAPER_BATCH_INTAKE_READY_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R009_PASS_MANUAL_NOEXTERNAL_COMMAND_PLAN_READY_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R009_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
}

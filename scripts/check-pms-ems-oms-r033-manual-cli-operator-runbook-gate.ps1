param(
    [string]$ArtifactDirectory = "artifacts/readiness/pms-ems-oms-integration"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param([string]$Classification, [string]$Message)
    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-JsonArtifact {
    param([string]$Path, [string]$MissingClassification)
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail-Gate $MissingClassification "Missing required artifact: $Path"
    }

    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { Fail-Gate $MissingClassification "Artifact is not valid JSON: $Path" }
}

function Require-True {
    param([bool]$Value, [string]$Classification, [string]$Message)
    if (-not $Value) { Fail-Gate $Classification $Message }
}

function Require-False {
    param([bool]$Value, [string]$Classification, [string]$Message)
    if ($Value) { Fail-Gate $Classification $Message }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot $ArtifactDirectory

$requiredArtifacts = @{
    "phase-pms-ems-oms-r033-summary.md" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-operator-runbook.md" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-operator-runbook.json" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-cli-invocation-examples.md" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-valid-cli-example.json" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-invalid-cli-examples.json" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-preflight-interpretation-guide.md" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-output-interpretation-guide.md" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-operator-decision-guide.md" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-forbidden-actions-checklist.md" = "PMS_EMS_OMS_R033_FAIL_FORBIDDEN_ACTIONS_CHECKLIST_MISSING"
    "phase-pms-ems-oms-r033-troubleshooting-guide.md" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-handoff-checklist.md" = "PMS_EMS_OMS_R033_FAIL_HANDOFF_CHECKLIST_MISSING"
    "phase-pms-ems-oms-r033-idempotency-guide.json" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-lineage-requirements.json" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R033_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r033-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r033-no-cli-execution-audit.json" = "PMS_EMS_OMS_R033_FAIL_CLI_EXECUTED"
    "phase-pms-ems-oms-r033-no-cycle-run-audit.json" = "PMS_EMS_OMS_R033_FAIL_NEW_CYCLE_RAN"
    "phase-pms-ems-oms-r033-no-qubes-ingest-audit.json" = "PMS_EMS_OMS_R033_FAIL_NEW_QUBES_BATCH_INGESTED"
    "phase-pms-ems-oms-r033-no-scheduler-service-polling-audit.json" = "PMS_EMS_OMS_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
    "phase-pms-ems-oms-r033-no-automatic-execution-audit.json" = "PMS_EMS_OMS_R033_FAIL_AUTOMATIC_EXECUTION_INTRODUCED"
    "phase-pms-ems-oms-r033-no-paper-ledger-commit-audit.json" = "PMS_EMS_OMS_R033_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
    "phase-pms-ems-oms-r033-no-order-fill-report-route-audit.json" = "PMS_EMS_OMS_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r033-no-external-audit.json" = "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r033-next-phase-recommendation.json" = "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
    "phase-pms-ems-oms-r033-build-test-validator-evidence.json" = "PMS_EMS_OMS_R033_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$runbook = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-operator-runbook.json") "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
$valid = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-valid-cli-example.json") "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
$invalid = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-invalid-cli-examples.json") "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-idempotency-guide.json") "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-lineage-requirements.json") "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R033_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$cliAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-no-cli-execution-audit.json") "PMS_EMS_OMS_R033_FAIL_CLI_EXECUTED"
$cycleAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-no-cycle-run-audit.json") "PMS_EMS_OMS_R033_FAIL_NEW_CYCLE_RAN"
$qubesAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-no-qubes-ingest-audit.json") "PMS_EMS_OMS_R033_FAIL_NEW_QUBES_BATCH_INGESTED"
$schedulerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-no-scheduler-service-polling-audit.json") "PMS_EMS_OMS_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
$automaticAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-no-automatic-execution-audit.json") "PMS_EMS_OMS_R033_FAIL_AUTOMATIC_EXECUTION_INTRODUCED"
$paperLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-no-paper-ledger-commit-audit.json") "PMS_EMS_OMS_R033_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-no-order-fill-report-route-audit.json") "PMS_EMS_OMS_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-no-external-audit.json") "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r033-build-test-validator-evidence.json") "PMS_EMS_OMS_R033_FAIL_BUILD_OR_TESTS"

foreach ($path in @(
    "phase-pms-ems-oms-r033-operator-runbook.md",
    "phase-pms-ems-oms-r033-cli-invocation-examples.md",
    "phase-pms-ems-oms-r033-preflight-interpretation-guide.md",
    "phase-pms-ems-oms-r033-output-interpretation-guide.md",
    "phase-pms-ems-oms-r033-operator-decision-guide.md",
    "phase-pms-ems-oms-r033-forbidden-actions-checklist.md",
    "phase-pms-ems-oms-r033-troubleshooting-guide.md",
    "phase-pms-ems-oms-r033-handoff-checklist.md"
)) {
    $fullPath = Join-Path $artifactRoot $path
    $content = Get-Content -LiteralPath $fullPath -Raw
    if ([string]::IsNullOrWhiteSpace($content)) {
        Fail-Gate "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Artifact is empty: $path"
    }
}

Require-True ([bool]$runbook.operatorRunbookCreated) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Runbook missing."
Require-True ([string]$runbook.tool -eq "QQ.Production.Intraday.Tools.ManualPaperCycle") "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Tool wrong."
Require-True ([string]$runbook.command -eq "run-manual-paper-cycle") "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Command wrong."
Require-True ([bool]$runbook.operatorTriggeredOnly) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Operator-only missing."
Require-True ([bool]$runbook.runsAtMostOneCycle) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "One-cycle guidance missing."
Require-True ([bool]$runbook.notSchedulerServicePolling) "PMS_EMS_OMS_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling allowed."
Require-True ([bool]$runbook.forbiddenActionsDocumented) "PMS_EMS_OMS_R033_FAIL_FORBIDDEN_ACTIONS_CHECKLIST_MISSING" "Forbidden actions missing."
Require-True ([bool]$runbook.preflightInterpretationIncluded) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Preflight guide missing."
Require-True ([bool]$runbook.outputInterpretationIncluded) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Output guide missing."
Require-True ([bool]$runbook.troubleshootingIncluded) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Troubleshooting missing."
Require-True ([bool]$runbook.lineageRequirementsIncluded) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Lineage missing."

Require-True ([bool]$valid.validCliExampleCreated) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Valid example missing."
Require-True ([string]$valid.mode -eq "ManualNoExternal") "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Valid mode wrong."
Require-True ([int]$valid.expectedCadenceMinutes -eq 15) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Valid cadence wrong."
Require-True ([bool]$valid.noPaperLedgerCommit) "PMS_EMS_OMS_R033_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "No paper commit missing."
Require-True ([bool]$invalid.invalidCliExamplesCreated) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Invalid examples missing."
Require-True ([bool]$invalid.allInvalidExamplesBlockBeforeCycle) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Invalid examples don't block."
Require-True (@($invalid.examples).Count -ge 9) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Too few invalid examples."

Require-True ([bool]$idempotency.idempotencyGuideCreated) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Idempotency missing."
Require-False ([bool]$idempotency.forcesSecondCycle) "PMS_EMS_OMS_R033_FAIL_NEW_CYCLE_RAN" "Idempotency forces second cycle."
Require-False ([bool]$idempotency.commitsPaperLedger) "PMS_EMS_OMS_R033_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Idempotency commits paper ledger."
Require-True ([bool]$lineage.lineageRequirementsCreated) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Lineage requirements missing."
Require-True ([bool]$lineage.qubesLineageRequired) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Qubes lineage not required."
Require-True ([bool]$lineage.cycleLineageRequired) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Cycle lineage not required."
Require-True ([bool]$lineage.paperBaselineLineageRequired) "PMS_EMS_OMS_R033_FAIL_RUNBOOK_MISSING" "Baseline lineage not required."

Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "PMS_EMS_OMS_R033_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY missing."
Require-True ([string]$usdjpy.securityId -eq "4004") "PMS_EMS_OMS_R033_FAIL_USDJPY_CAVEAT_WEAKENED" "SecurityID wrong."
Require-True ([string]$usdjpy.securityIdSource -eq "8") "PMS_EMS_OMS_R033_FAIL_USDJPY_CAVEAT_WEAKENED" "SecurityIDSource wrong."
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "PMS_EMS_OMS_R033_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-False ([bool]$usdjpy.weakened) "PMS_EMS_OMS_R033_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY weakened."
Require-True ([bool]$lmax.referenceOnly) "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.brokerCalled) "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker called."

Require-False ([bool]$cliAudit.cliExecuted) "PMS_EMS_OMS_R033_FAIL_CLI_EXECUTED" "CLI executed."
Require-True ([int]$cliAudit.cliInvocationCount -eq 0) "PMS_EMS_OMS_R033_FAIL_CLI_EXECUTED" "CLI invocation count nonzero."
Require-False ([bool]$cycleAudit.newCycleRan) "PMS_EMS_OMS_R033_FAIL_NEW_CYCLE_RAN" "New cycle ran."
Require-True ([int]$cycleAudit.paperCycleExecutionCount -eq 0) "PMS_EMS_OMS_R033_FAIL_NEW_CYCLE_RAN" "Cycle execution count nonzero."
Require-False ([bool]$qubesAudit.newQubesBatchIngested) "PMS_EMS_OMS_R033_FAIL_NEW_QUBES_BATCH_INGESTED" "Qubes batch ingested."
Require-True ([int]$qubesAudit.qubesIngestCount -eq 0) "PMS_EMS_OMS_R033_FAIL_NEW_QUBES_BATCH_INGESTED" "Qubes ingest count nonzero."
Require-False ([bool]$schedulerAudit.schedulerIntroduced) "PMS_EMS_OMS_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler introduced."
Require-False ([bool]$schedulerAudit.serviceIntroduced) "PMS_EMS_OMS_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Service introduced."
Require-False ([bool]$schedulerAudit.pollingIntroduced) "PMS_EMS_OMS_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Polling introduced."
Require-False ([bool]$schedulerAudit.timerIntroduced) "PMS_EMS_OMS_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Timer introduced."
Require-False ([bool]$schedulerAudit.backgroundJobIntroduced) "PMS_EMS_OMS_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Background job introduced."
Require-False ([bool]$automaticAudit.automaticExecutionIntroduced) "PMS_EMS_OMS_R033_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution introduced."
Require-False ([bool]$paperLedgerAudit.paperLedgerCommitOccurred) "PMS_EMS_OMS_R033_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateMutated) "PMS_EMS_OMS_R033_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger mutated."
Require-False ([bool]$orderAudit.ordersCreated) "PMS_EMS_OMS_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$orderAudit.executableOrdersCreated) "PMS_EMS_OMS_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable orders created."
Require-False ([bool]$orderAudit.fillsCreated) "PMS_EMS_OMS_R033_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$orderAudit.executionReportsCreated) "PMS_EMS_OMS_R033_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$orderAudit.routesCreated) "PMS_EMS_OMS_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$orderAudit.submissionsCreated) "PMS_EMS_OMS_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."

Require-False ([bool]$noExternal.brokerActivation) "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker activation."
Require-False ([bool]$noExternal.socketTlsFixRuntimeAction) "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Socket/TLS/FIX runtime."
Require-False ([bool]$noExternal.liveMarketRuntimeAction) "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market runtime."
Require-False ([bool]$noExternal.apiWorkerLiveGatewayEnabled) "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API/Worker live gateway."
Require-False ([bool]$noExternal.schedulerPollingServiceTimerBackgroundJob) "PMS_EMS_OMS_R033_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling."
Require-False ([bool]$noExternal.automaticExecution) "PMS_EMS_OMS_R033_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution."
Require-False ([bool]$noExternal.cliExecuted) "PMS_EMS_OMS_R033_FAIL_CLI_EXECUTED" "CLI executed."
Require-False ([bool]$noExternal.newCycleRan) "PMS_EMS_OMS_R033_FAIL_NEW_CYCLE_RAN" "New cycle ran."
Require-False ([bool]$noExternal.newQubesBatchIngested) "PMS_EMS_OMS_R033_FAIL_NEW_QUBES_BATCH_INGESTED" "Qubes ingested."
Require-False ([bool]$noExternal.paperLedgerCommit) "PMS_EMS_OMS_R033_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit."
Require-False ([bool]$noExternal.ordersCreated) "PMS_EMS_OMS_R033_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$noExternal.fillsCreated) "PMS_EMS_OMS_R033_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$noExternal.executionReportsCreated) "PMS_EMS_OMS_R033_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$noExternal.liveBrokerProductionTradingMutation) "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "State mutation."
Require-False ([bool]$noExternal.replayOrShadowReplay) "PMS_EMS_OMS_R033_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Replay introduced."

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R033_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "PMS_EMS_OMS_R033_FAIL_BUILD_OR_TESTS" "Build failed."
Require-True ([string]$evidence.focusedTests -like "PASS*") "PMS_EMS_OMS_R033_FAIL_BUILD_OR_TESTS" "Focused tests failed."
Require-True ([string]$evidence.unitTests -like "PASS*") "PMS_EMS_OMS_R033_FAIL_BUILD_OR_TESTS" "Unit tests failed."
Require-True ([string]$evidence.validator -eq "PASS") "PMS_EMS_OMS_R033_FAIL_BUILD_OR_TESTS" "Validator evidence missing."

Write-Host "PMS_EMS_OMS_R033_PASS_OPERATOR_RUNBOOK_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R033_PASS_MANUAL_CLI_HANDOFF_READY_NO_EXTERNAL"

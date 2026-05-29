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
    "phase-pms-ems-oms-r034-summary.md" = "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
    "phase-pms-ems-oms-r034-operational-acceptance.json" = "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
    "phase-pms-ems-oms-r034-manual-cli-handoff-closure.md" = "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING"
    "phase-pms-ems-oms-r034-manual-cli-handoff-closure.json" = "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING"
    "phase-pms-ems-oms-r034-accepted-operating-mode.json" = "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
    "phase-pms-ems-oms-r034-runbook-acceptance.json" = "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
    "phase-pms-ems-oms-r034-forbidden-actions-final-checklist.md" = "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r034-future-scope-controls.json" = "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
    "phase-pms-ems-oms-r034-qubes-source-contract-preservation.json" = "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
    "phase-pms-ems-oms-r034-lineage-requirements-preservation.json" = "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
    "phase-pms-ems-oms-r034-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R034_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r034-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r034-no-cli-execution-audit.json" = "PMS_EMS_OMS_R034_FAIL_CLI_EXECUTED"
    "phase-pms-ems-oms-r034-no-cycle-run-audit.json" = "PMS_EMS_OMS_R034_FAIL_NEW_CYCLE_RAN"
    "phase-pms-ems-oms-r034-no-qubes-ingest-audit.json" = "PMS_EMS_OMS_R034_FAIL_NEW_QUBES_BATCH_INGESTED"
    "phase-pms-ems-oms-r034-no-scheduler-service-polling-audit.json" = "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
    "phase-pms-ems-oms-r034-no-automatic-execution-audit.json" = "PMS_EMS_OMS_R034_FAIL_AUTOMATIC_EXECUTION_INTRODUCED"
    "phase-pms-ems-oms-r034-no-paper-ledger-commit-audit.json" = "PMS_EMS_OMS_R034_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
    "phase-pms-ems-oms-r034-no-order-fill-report-route-audit.json" = "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r034-no-external-audit.json" = "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r034-branch-closure-recommendation.json" = "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING"
    "phase-pms-ems-oms-r034-build-test-validator-evidence.json" = "PMS_EMS_OMS_R034_FAIL_BUILD_OR_VALIDATOR"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

foreach ($path in @(
    "phase-pms-ems-oms-r034-summary.md",
    "phase-pms-ems-oms-r034-manual-cli-handoff-closure.md",
    "phase-pms-ems-oms-r034-forbidden-actions-final-checklist.md"
)) {
    $content = Get-Content -LiteralPath (Join-Path $artifactRoot $path) -Raw
    if ([string]::IsNullOrWhiteSpace($content)) {
        Fail-Gate "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Artifact is empty: $path"
    }
}

$acceptance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-operational-acceptance.json") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
$closure = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-manual-cli-handoff-closure.json") "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING"
$mode = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-accepted-operating-mode.json") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
$runbook = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-runbook-acceptance.json") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
$scope = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-future-scope-controls.json") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-qubes-source-contract-preservation.json") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-lineage-requirements-preservation.json") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R034_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$cliAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-no-cli-execution-audit.json") "PMS_EMS_OMS_R034_FAIL_CLI_EXECUTED"
$cycleAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-no-cycle-run-audit.json") "PMS_EMS_OMS_R034_FAIL_NEW_CYCLE_RAN"
$qubesAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-no-qubes-ingest-audit.json") "PMS_EMS_OMS_R034_FAIL_NEW_QUBES_BATCH_INGESTED"
$schedulerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-no-scheduler-service-polling-audit.json") "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
$automaticAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-no-automatic-execution-audit.json") "PMS_EMS_OMS_R034_FAIL_AUTOMATIC_EXECUTION_INTRODUCED"
$paperLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-no-paper-ledger-commit-audit.json") "PMS_EMS_OMS_R034_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-no-order-fill-report-route-audit.json") "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-no-external-audit.json") "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$closureRecommendation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-branch-closure-recommendation.json") "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r034-build-test-validator-evidence.json") "PMS_EMS_OMS_R034_FAIL_BUILD_OR_VALIDATOR"

Require-True ([bool]$acceptance.operationalAcceptanceCreated) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Operational acceptance missing."
Require-True ([bool]$acceptance.acceptedForControlledOperatorUse) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Controlled operator acceptance missing."
Require-True ([string]$acceptance.tool -eq "QQ.Production.Intraday.Tools.ManualPaperCycle") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Tool mismatch."
Require-True ([string]$acceptance.command -eq "run-manual-paper-cycle") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Command mismatch."
Require-True ([string]$acceptance.requiredMode -eq "ManualNoExternal") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "ManualNoExternal not required."
Require-True ([bool]$acceptance.operatorTriggeredOnly) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Operator-only missing."
Require-True ([bool]$acceptance.oneCyclePerInvocationOnly) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "One-cycle acceptance missing."
Require-True ([bool]$acceptance.futureExpansionRequiresNewExplicitPhase) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Future scope control missing."
Require-False ([bool]$acceptance.schedulerAuthorized) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler authorized."
Require-False ([bool]$acceptance.serviceAuthorized) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Service authorized."
Require-False ([bool]$acceptance.pollingAuthorized) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Polling authorized."
Require-False ([bool]$acceptance.automaticExecutionAuthorized) "PMS_EMS_OMS_R034_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution authorized."
Require-False ([bool]$acceptance.brokerAccessAuthorized) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker access authorized."
Require-False ([bool]$acceptance.liveMarketDataAuthorized) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market data authorized."
Require-False ([bool]$acceptance.ordersAuthorized) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders authorized."
Require-False ([bool]$acceptance.fillsAuthorized) "PMS_EMS_OMS_R034_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills authorized."
Require-False ([bool]$acceptance.executionReportsAuthorized) "PMS_EMS_OMS_R034_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports authorized."
Require-False ([bool]$acceptance.paperLedgerCommitAuthorized) "PMS_EMS_OMS_R034_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit authorized."

Require-True ([bool]$closure.handoffClosureCreated) "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING" "Handoff closure missing."
Require-True ([string]$closure.branchClosureStatus -eq "ClosedNoExternalManualOnly") "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING" "Branch closure status mismatch."
Require-True ([bool]$closure.manualCliAcceptedForControlledUse) "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING" "Manual CLI closure missing."
Require-False ([bool]$closure.paperLedgerCommitAuthorized) "PMS_EMS_OMS_R034_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Closure authorizes paper commit."
Require-False ([bool]$closure.ordersFillsReportsRoutesSubmissionsAuthorized) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Closure authorizes order/fill/report/route/submission."
Require-False ([bool]$closure.brokerOrLiveMarketDataAuthorized) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Closure authorizes external data."
Require-False ([bool]$closure.schedulerServicePollingAutomaticAuthorized) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Closure authorizes automation."

Require-True ([bool]$mode.acceptedOperatingModeCreated) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Operating mode missing."
Require-True ([string]$mode.runMode -eq "ManualNoExternal") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Operating mode mismatch."
Require-True ([int]$mode.expectedCadenceMinutes -eq 15) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Cadence mismatch."
Require-True ([bool]$mode.requiresPreflight) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Preflight not required."
Require-True ([bool]$mode.noPaperLedgerCommit) "PMS_EMS_OMS_R034_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "No paper commit not preserved."
Require-True ([bool]$mode.noScheduler -and [bool]$mode.noService -and [bool]$mode.noPolling) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling not forbidden."

Require-True ([bool]$runbook.runbookAcceptanceCreated) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Runbook acceptance missing."
Require-True ([bool]$runbook.r033OperatorRunbookAccepted) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "R033 runbook not accepted."
Require-True ([bool]$runbook.r033HandoffChecklistAccepted) "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING" "R033 handoff checklist not accepted."
Require-True ([bool]$runbook.acceptedForControlledManualNoExternalUse) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Runbook controlled use missing."
Require-True ([bool]$runbook.acceptanceDoesNotExecuteCli) "PMS_EMS_OMS_R034_FAIL_CLI_EXECUTED" "Runbook acceptance executes CLI."
Require-True ([bool]$runbook.acceptanceDoesNotRunCycle) "PMS_EMS_OMS_R034_FAIL_NEW_CYCLE_RAN" "Runbook acceptance runs cycle."
Require-True ([bool]$runbook.acceptanceDoesNotIngestQubes) "PMS_EMS_OMS_R034_FAIL_NEW_QUBES_BATCH_INGESTED" "Runbook acceptance ingests Qubes."

Require-True ([bool]$scope.futureScopeControlsCreated) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Future scope controls missing."
Require-True ([bool]$scope.futureExpansionRequiresNewExplicitPhase) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Future expansion not gated."
Require-True ([bool]$scope.manualCliAcceptanceDoesNotAuthorizeScheduler) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler future scope weakened."
Require-True ([bool]$scope.manualCliAcceptanceDoesNotAuthorizePaperLedgerCommit) "PMS_EMS_OMS_R034_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper commit future scope weakened."

Require-True ([bool]$qubes.qubesSourceContractPreserved) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Qubes source contract missing."
Require-True ([string]$qubes.sourceFormat -eq "<BloombergTicker>;<weight>") "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Qubes source format changed."
Require-False ([bool]$qubes.newQubesBatchIngestedInR034) "PMS_EMS_OMS_R034_FAIL_NEW_QUBES_BATCH_INGESTED" "Qubes ingested."
Require-False ([bool]$qubes.rawPayloadSerializedBeyondSafeSummary) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Raw payload serialized."
Require-True ([bool]$lineage.lineageRequirementsPreserved) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Lineage missing."
Require-True ([bool]$lineage.qubesLineagePreserved) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Qubes lineage missing."
Require-True ([bool]$lineage.paperBaselineLineagePreserved) "PMS_EMS_OMS_R034_FAIL_OPERATIONAL_ACCEPTANCE_MISSING" "Baseline lineage missing."

Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "PMS_EMS_OMS_R034_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.securityId -eq "4004") "PMS_EMS_OMS_R034_FAIL_USDJPY_CAVEAT_WEAKENED" "SecurityID wrong."
Require-True ([string]$usdjpy.securityIdSource -eq "8") "PMS_EMS_OMS_R034_FAIL_USDJPY_CAVEAT_WEAKENED" "SecurityIDSource wrong."
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "PMS_EMS_OMS_R034_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-False ([bool]$usdjpy.weakened) "PMS_EMS_OMS_R034_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY weakened."
Require-True ([bool]$lmax.referenceOnly) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.brokerCalled) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker called."

Require-False ([bool]$cliAudit.cliExecuted) "PMS_EMS_OMS_R034_FAIL_CLI_EXECUTED" "CLI executed."
Require-True ([int]$cliAudit.cliInvocationCount -eq 0) "PMS_EMS_OMS_R034_FAIL_CLI_EXECUTED" "CLI invocation count nonzero."
Require-False ([bool]$cycleAudit.newCycleRan) "PMS_EMS_OMS_R034_FAIL_NEW_CYCLE_RAN" "New cycle ran."
Require-True ([int]$cycleAudit.paperCycleExecutionCount -eq 0) "PMS_EMS_OMS_R034_FAIL_NEW_CYCLE_RAN" "Cycle count nonzero."
Require-False ([bool]$qubesAudit.newQubesBatchIngested) "PMS_EMS_OMS_R034_FAIL_NEW_QUBES_BATCH_INGESTED" "New Qubes batch ingested."
Require-True ([int]$qubesAudit.qubesIngestCount -eq 0) "PMS_EMS_OMS_R034_FAIL_NEW_QUBES_BATCH_INGESTED" "Qubes ingest count nonzero."
Require-False ([bool]$schedulerAudit.schedulerIntroduced) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler introduced."
Require-False ([bool]$schedulerAudit.serviceIntroduced) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Service introduced."
Require-False ([bool]$schedulerAudit.pollingIntroduced) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Polling introduced."
Require-False ([bool]$schedulerAudit.timerIntroduced) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Timer introduced."
Require-False ([bool]$schedulerAudit.backgroundJobIntroduced) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Background job introduced."
Require-False ([bool]$automaticAudit.automaticExecutionIntroduced) "PMS_EMS_OMS_R034_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution introduced."
Require-False ([bool]$paperLedgerAudit.paperLedgerCommitOccurred) "PMS_EMS_OMS_R034_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateMutated) "PMS_EMS_OMS_R034_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger mutated."
Require-False ([bool]$orderAudit.ordersCreated) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$orderAudit.executableOrdersCreated) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable orders created."
Require-False ([bool]$orderAudit.omsParentOrdersCreated) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS parent orders created."
Require-False ([bool]$orderAudit.omsChildOrdersCreated) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS child orders created."
Require-False ([bool]$orderAudit.brokerOrdersCreated) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker orders created."
Require-False ([bool]$orderAudit.fillsCreated) "PMS_EMS_OMS_R034_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$orderAudit.executionReportsCreated) "PMS_EMS_OMS_R034_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$orderAudit.routesCreated) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$orderAudit.submissionsCreated) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-False ([bool]$orderAudit.liveTradingPathIntroduced) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading path."

Require-False ([bool]$noExternal.brokerActivation) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker activation."
Require-False ([bool]$noExternal.socketTlsFixRuntimeAction) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Socket/TLS/FIX runtime."
Require-False ([bool]$noExternal.liveMarketRuntimeAction) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market runtime."
Require-False ([bool]$noExternal.marketDataRequestAttempted) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "MarketDataRequest attempted."
Require-False ([bool]$noExternal.marketDataResponseRead) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "MarketDataResponse read."
Require-False ([bool]$noExternal.apiWorkerLiveGatewayEnabled) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API/Worker live gateway."
Require-False ([bool]$noExternal.schedulerPollingServiceTimerBackgroundJob) "PMS_EMS_OMS_R034_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling."
Require-False ([bool]$noExternal.automaticExecution) "PMS_EMS_OMS_R034_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution."
Require-False ([bool]$noExternal.cliExecuted) "PMS_EMS_OMS_R034_FAIL_CLI_EXECUTED" "CLI executed."
Require-False ([bool]$noExternal.newCycleRan) "PMS_EMS_OMS_R034_FAIL_NEW_CYCLE_RAN" "New cycle ran."
Require-False ([bool]$noExternal.newQubesBatchIngested) "PMS_EMS_OMS_R034_FAIL_NEW_QUBES_BATCH_INGESTED" "Qubes ingested."
Require-False ([bool]$noExternal.paperLedgerCommit) "PMS_EMS_OMS_R034_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit."
Require-False ([bool]$noExternal.ordersCreated) "PMS_EMS_OMS_R034_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$noExternal.fillsCreated) "PMS_EMS_OMS_R034_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$noExternal.executionReportsCreated) "PMS_EMS_OMS_R034_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$noExternal.livePositionMutation) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live position mutation."
Require-False ([bool]$noExternal.brokerPositionMutation) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker position mutation."
Require-False ([bool]$noExternal.productionLedgerMutation) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Production ledger mutation."
Require-False ([bool]$noExternal.tradingStateMutation) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Trading state mutation."
Require-False ([bool]$noExternal.replayOrShadowReplay) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Replay introduced."
Require-False ([bool]$noExternal.secretOrRawPayloadSerialization) "PMS_EMS_OMS_R034_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Secret/raw payload serialization."

Require-True ([bool]$closureRecommendation.branchClosureRecommendationCreated) "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING" "Branch closure recommendation missing."
Require-True ([bool]$closureRecommendation.futureExpansionRequiresNewExplicitScope) "PMS_EMS_OMS_R034_FAIL_HANDOFF_CLOSURE_MISSING" "New scope recommendation missing."

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R034_FAIL_BUILD_OR_VALIDATOR" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "PMS_EMS_OMS_R034_FAIL_BUILD_OR_VALIDATOR" "Build failed."
Require-True ([string]$evidence.focusedStaticTests -like "PASS*") "PMS_EMS_OMS_R034_FAIL_BUILD_OR_VALIDATOR" "Focused/static tests failed."
Require-True ([string]$evidence.unitTests -like "PASS*") "PMS_EMS_OMS_R034_FAIL_BUILD_OR_VALIDATOR" "Unit tests failed."
Require-True ([string]$evidence.validator -eq "PASS") "PMS_EMS_OMS_R034_FAIL_BUILD_OR_VALIDATOR" "Validator evidence missing."

Write-Host "PMS_EMS_OMS_R034_PASS_MANUAL_CLI_OPERATIONAL_ACCEPTANCE_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R034_PASS_HANDOFF_CLOSURE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R034_PASS_CURRENT_PAPER_CYCLE_BRANCH_CLOSED_NO_EXTERNAL"

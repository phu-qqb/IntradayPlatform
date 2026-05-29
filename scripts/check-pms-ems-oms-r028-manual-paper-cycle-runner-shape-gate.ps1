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
    "phase-pms-ems-oms-r028-summary.md" = "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
    "phase-pms-ems-oms-r028-manual-runner-contract.json" = "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
    "phase-pms-ems-oms-r028-manual-run-request-shape.json" = "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
    "phase-pms-ems-oms-r028-preflight-contract.json" = "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r028-preflight-example.json" = "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r028-required-preconditions.json" = "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r028-expected-output-shape.json" = "PMS_EMS_OMS_R028_FAIL_EXPECTED_OUTPUT_SHAPE_MISSING"
    "phase-pms-ems-oms-r028-idempotency-contract.json" = "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
    "phase-pms-ems-oms-r028-safety-gate.json" = "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
    "phase-pms-ems-oms-r028-r027-continuity-preservation.json" = "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
    "phase-pms-ems-oms-r028-paper-baseline-lineage-preservation.json" = "PMS_EMS_OMS_R028_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r028-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R028_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r028-no-cycle-run-audit.json" = "PMS_EMS_OMS_R028_FAIL_NEW_CYCLE_RAN"
    "phase-pms-ems-oms-r028-no-qubes-ingest-audit.json" = "PMS_EMS_OMS_R028_FAIL_NEW_QUBES_BATCH_INGESTED"
    "phase-pms-ems-oms-r028-no-paper-ledger-mutation-audit.json" = "PMS_EMS_OMS_R028_FAIL_PAPER_LEDGER_MUTATED"
    "phase-pms-ems-oms-r028-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R028_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r028-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R028_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r028-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R028_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r028-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R028_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r028-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R028_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r028-no-order-created-audit.json" = "PMS_EMS_OMS_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r028-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r028-instrument-universe-handling.json" = "PMS_EMS_OMS_R028_FAIL_AUDUSD_MISCLASSIFIED"
    "phase-pms-ems-oms-r028-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R028_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r028-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r028-no-external-audit.json" = "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r028-forbidden-actions-audit.json" = "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r028-next-phase-recommendation.json" = "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
    "phase-pms-ems-oms-r028-build-test-validator-evidence.json" = "PMS_EMS_OMS_R028_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-manual-runner-contract.json") "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
$request = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-manual-run-request-shape.json") "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
$preflightContract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-preflight-contract.json") "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING"
$preflight = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-preflight-example.json") "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING"
$preconditions = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-required-preconditions.json") "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING"
$expected = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-expected-output-shape.json") "PMS_EMS_OMS_R028_FAIL_EXPECTED_OUTPUT_SHAPE_MISSING"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-idempotency-contract.json") "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
$safety = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-safety-gate.json") "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
$continuity = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-r027-continuity-preservation.json") "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING"
$baseline = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-paper-baseline-lineage-preservation.json") "PMS_EMS_OMS_R028_FAIL_QUBES_LINEAGE_WEAKENED"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-qubes-lineage-preservation.json") "PMS_EMS_OMS_R028_FAIL_QUBES_LINEAGE_WEAKENED"
$noCycle = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-cycle-run-audit.json") "PMS_EMS_OMS_R028_FAIL_NEW_CYCLE_RAN"
$noQubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-qubes-ingest-audit.json") "PMS_EMS_OMS_R028_FAIL_NEW_QUBES_BATCH_INGESTED"
$paperLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-paper-ledger-mutation-audit.json") "PMS_EMS_OMS_R028_FAIL_PAPER_LEDGER_MUTATED"
$liveAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R028_FAIL_LIVE_POSITION_MUTATION"
$brokerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R028_FAIL_BROKER_POSITION_MUTATION"
$productionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R028_FAIL_PRODUCTION_LEDGER_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R028_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R028_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-order-created-audit.json") "PMS_EMS_OMS_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-route-no-submission-audit.json") "PMS_EMS_OMS_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-instrument-universe-handling.json") "PMS_EMS_OMS_R028_FAIL_AUDUSD_MISCLASSIFIED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R028_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-no-external-audit.json") "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-forbidden-actions-audit.json") "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r028-build-test-validator-evidence.json") "PMS_EMS_OMS_R028_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.manualRunnerContractCreated) "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING" "Manual runner contract missing."
foreach ($property in @("shapeOnly", "manualOperatorTriggered", "runModeManualNoExternal", "doesNotExecuteRunner", "doesNotStartSchedulerServicePolling", "doesNotIngestQubes", "doesNotMutatePaperLedger", "preservesR027ContinuityDecision", "preservesR025BaselineLineage", "preservesQubesLineage")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING" "Contract flag missing: $property"
}

Require-True ([bool]$request.manualRunRequestShapeCreated) "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING" "Run request shape missing."
Require-True ([string]$request.requestedCycleRunId -eq "cycle-r028-manual-paper-shape") "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING" "Wrong requested cycle id."
Require-True ([string]$request.qubesRunId -eq "qubes-r028-manual-fixture") "PMS_EMS_OMS_R028_FAIL_QUBES_LINEAGE_WEAKENED" "Wrong QubesRunId."
Require-True ([int]$request.expectedCadenceMinutes -eq 15) "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING" "Cadence not 15."
Require-True ([string]$request.runMode -eq "ManualNoExternal") "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING" "Wrong run mode."
Require-True ([bool]$request.qubesInputIsFixtureNoExternal) "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING" "Qubes input not fixture."

Require-True ([bool]$preflightContract.preflightContractCreated) "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING" "Preflight contract missing."
Require-True ([bool]$preflight.preflightExampleCreated) "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING" "Preflight example missing."
Require-True ([bool]$preflight.preconditionsSatisfied) "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING" "Preconditions not satisfied."
Require-True ([string]$preflight.preflightStatus -eq "ReadyNoExternal") "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING" "Preflight not ready."
foreach ($property in @("executesCycle", "ingestsNewQubesBatch", "mutatesPaperLedgerState", "startsSchedulerOrService", "callsBrokerOrLiveMarketData", "createsOrders", "createsFills", "createsExecutionReports", "createsRoutes", "submitsOrders")) {
    Require-False ([bool]$preflight.$property) "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Preflight unsafe flag: $property"
}

Require-True ([bool]$preconditions.requiredPreconditionsCreated) "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING" "Preconditions missing."
foreach ($property in @("requiresPriorPaperContinuityReady", "requiresPriorPaperLedgerBaseline", "requiresQubesRunId", "requiresFixtureNoExternalQubesInput", "requiresFifteenMinuteCadence", "requiresNoSchedulerServicePolling", "requiresNoLiveBrokerMarketData")) {
    Require-True ([bool]$preconditions.$property) "PMS_EMS_OMS_R028_FAIL_PREFLIGHT_CONTRACT_MISSING" "Precondition flag missing: $property"
}

Require-True ([bool]$expected.expectedOutputShapeCreated) "PMS_EMS_OMS_R028_FAIL_EXPECTED_OUTPUT_SHAPE_MISSING" "Expected output shape missing."
foreach ($property in @("cycleRun", "qubesLineage", "paperBaselineInput", "targetPortfolio", "targetVsCurrentDiff", "theoreticalPnl", "reconciliation", "theoreticalVsReal", "nonExecutableRebalanceIntents", "operatorReport")) {
    Require-True ([bool]$expected.$property) "PMS_EMS_OMS_R028_FAIL_EXPECTED_OUTPUT_SHAPE_MISSING" "Expected output flag missing: $property"
}
Require-False ([bool]$expected.executesCycle) "PMS_EMS_OMS_R028_FAIL_NEW_CYCLE_RAN" "Expected output executes cycle."

Require-True ([bool]$idempotency.idempotencyContractCreated) "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.duplicateRequestedCycleRunIdBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING" "Duplicate cycle behavior wrong."
Require-False ([bool]$idempotency.duplicatesExecuteCycle) "PMS_EMS_OMS_R028_FAIL_NEW_CYCLE_RAN" "Duplicate executes cycle."
Require-False ([bool]$idempotency.duplicatesIngestQubes) "PMS_EMS_OMS_R028_FAIL_NEW_QUBES_BATCH_INGESTED" "Duplicate ingests Qubes."
Require-False ([bool]$idempotency.duplicatesMutatePaperLedger) "PMS_EMS_OMS_R028_FAIL_PAPER_LEDGER_MUTATED" "Duplicate mutates paper ledger."

Require-True ([bool]$safety.safetyGateCreated) "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING" "Safety gate missing."
foreach ($property in @("noExternal", "noBroker", "noLiveMarketData", "noSchedulerServicePolling", "noCycleExecution", "noQubesIngest", "noPaperLedgerMutation", "noLivePositionMutation", "noBrokerPositionMutation", "noProductionLedgerMutation", "noTradingStateMutation", "noOrder", "noFill", "noExecutionReport", "noRoute", "noSubmission", "noReplayOrShadowReplay")) {
    Require-True ([bool]$safety.$property) "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Safety gate missing: $property"
}

Require-True ([bool]$continuity.r027ContinuityPreservationCreated) "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING" "R027 continuity missing."
Require-True ([string]$continuity.priorContinuityStatus -eq "PaperContinuityReadyNoExternal") "PMS_EMS_OMS_R028_FAIL_MANUAL_RUNNER_CONTRACT_MISSING" "Continuity status wrong."
Require-True ([bool]$baseline.paperBaselineLineagePreservationCreated) "PMS_EMS_OMS_R028_FAIL_QUBES_LINEAGE_WEAKENED" "Baseline lineage missing."
Require-True ([string]$baseline.nextCycleBaselineType -eq "PaperLedgerFixture") "PMS_EMS_OMS_R028_FAIL_QUBES_LINEAGE_WEAKENED" "Baseline type wrong."
Require-False ([bool]$baseline.baselineIsProduction) "PMS_EMS_OMS_R028_FAIL_PRODUCTION_LEDGER_MUTATION" "Baseline production."
Require-False ([bool]$baseline.baselineIsBroker) "PMS_EMS_OMS_R028_FAIL_BROKER_POSITION_MUTATION" "Baseline broker."
Require-False ([bool]$baseline.baselineIsLiveTrading) "PMS_EMS_OMS_R028_FAIL_TRADING_STATE_MUTATION" "Baseline live trading."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R028_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([bool]$qubes.qubesRunIdRequired) "PMS_EMS_OMS_R028_FAIL_QUBES_LINEAGE_WEAKENED" "QubesRunId requirement missing."
Require-True ([bool]$qubes.sourceFormatPreserved) "PMS_EMS_OMS_R028_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source format missing."

Require-True ([bool]$noCycle.noCycleRunAuditCreated) "PMS_EMS_OMS_R028_FAIL_NEW_CYCLE_RAN" "No-cycle audit missing."
Require-False ([bool]$noCycle.newCycleRan) "PMS_EMS_OMS_R028_FAIL_NEW_CYCLE_RAN" "New cycle ran."
Require-False ([bool]$noCycle.runnerExecuted) "PMS_EMS_OMS_R028_FAIL_NEW_CYCLE_RAN" "Runner executed."
Require-True ([bool]$noQubes.noQubesIngestAuditCreated) "PMS_EMS_OMS_R028_FAIL_NEW_QUBES_BATCH_INGESTED" "No-Qubes audit missing."
Require-False ([bool]$noQubes.newQubesBatchIngested) "PMS_EMS_OMS_R028_FAIL_NEW_QUBES_BATCH_INGESTED" "Qubes ingested."

foreach ($audit in @($paperLedgerAudit, $liveAudit, $brokerAudit, $productionAudit, $tradingAudit, $fillAudit, $orderAudit, $routeAudit)) {
    Require-True ([bool]$audit.auditCreated) "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Mutation/safety audit missing."
}
Require-False ([bool]$paperLedgerAudit.paperLedgerStateMutated) "PMS_EMS_OMS_R028_FAIL_PAPER_LEDGER_MUTATED" "Paper ledger mutated."
Require-False ([bool]$liveAudit.livePositionStateMutated) "PMS_EMS_OMS_R028_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-False ([bool]$brokerAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R028_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-False ([bool]$productionAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R028_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R028_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$fillAudit.fillCreated) "PMS_EMS_OMS_R028_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill created."
Require-False ([bool]$fillAudit.executionReportCreated) "PMS_EMS_OMS_R028_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution report created."
Require-False ([bool]$orderAudit.executableOrderCreated) "PMS_EMS_OMS_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable order created."
Require-False ([bool]$orderAudit.omsOrderCreated) "PMS_EMS_OMS_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS order created."
Require-False ([bool]$routeAudit.brokerRouteCreated) "PMS_EMS_OMS_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker route created."
Require-False ([bool]$routeAudit.ordersSubmitted) "PMS_EMS_OMS_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders submitted."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R028_FAIL_AUDUSD_MISCLASSIFIED" "Universe missing."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R028_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksManualRunnerShape) "PMS_EMS_OMS_R028_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks R028."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R028_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R028_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID wrong."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R028_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource wrong."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R028_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY failed."
Require-False ([bool]$usdjpy.usdJpyCaveatWeakened) "PMS_EMS_OMS_R028_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX reference missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."

foreach ($property in @("externalBrokerActivationDetected", "boundaryRuntimeActionDetected", "liveMarketDataAttempted", "apiStarted", "workerStarted", "schedulerPollingServiceTimerBackgroundJobStartedOrIntroduced", "liveGatewayEnabled", "newCycleRan", "newQubesBatchIngested", "paperLedgerMutated", "replayOrShadowReplayIntroduced", "secretsOrCredentialsSerialized", "rawFixSerialized", "rawEndpointTlsValuesSerialized", "sessionIdsSerialized", "compIdsSerialized", "rawMdReqIdSerialized", "rawBrokerMarketDataPayloadsOrPricesSerialized", "rawMarketDataFixturePayloadsSerializedBeyondApprovedSafeSummaries")) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}
foreach ($property in @("brokerActivation", "socketTlsFix", "liveMarketData", "apiWorkerSchedulerService", "timersPollingBackgroundJobs", "ordersRoutesSubmissions", "fillsExecutionReports", "liveTradingPath", "livePositionMutation", "brokerPositionMutation", "productionLedgerMutation", "tradingStateMutation", "paperLedgerMutation", "newCycleRun", "newQubesBatchIngest", "replayShadowReplay", "secretOrRawPayloadSerialization")) {
    Require-False ([bool]$forbidden.$property) "PMS_EMS_OMS_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $property"
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R028_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "PMS_EMS_OMS_R028_FAIL_BUILD_OR_TESTS" "Build did not pass."
Require-True ([string]$evidence.focusedTests -like "PASS*") "PMS_EMS_OMS_R028_FAIL_BUILD_OR_TESTS" "Focused tests did not pass."
Require-True ([string]$evidence.unitTests -like "PASS*") "PMS_EMS_OMS_R028_FAIL_BUILD_OR_TESTS" "Unit tests did not pass."

Write-Host "PMS_EMS_OMS_R028_PASS_MANUAL_PAPER_CYCLE_RUNNER_CONTRACT_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R028_PASS_ROLLING_PAPER_CYCLE_PREFLIGHT_SHAPE_READY_NO_EXTERNAL"

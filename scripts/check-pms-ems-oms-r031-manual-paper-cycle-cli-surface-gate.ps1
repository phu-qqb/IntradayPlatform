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
    "phase-pms-ems-oms-r031-summary.md" = "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
    "phase-pms-ems-oms-r031-cli-contract.json" = "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
    "phase-pms-ems-oms-r031-cli-arguments-contract.json" = "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
    "phase-pms-ems-oms-r031-cli-preflight-contract.json" = "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
    "phase-pms-ems-oms-r031-cli-valid-request-example.json" = "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED"
    "phase-pms-ems-oms-r031-cli-rejected-request-examples.json" = "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED"
    "phase-pms-ems-oms-r031-cli-output-contract.json" = "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
    "phase-pms-ems-oms-r031-manual-cycle-run-result-example.json" = "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
    "phase-pms-ems-oms-r031-non-executable-intent-audit.json" = "PMS_EMS_OMS_R031_FAIL_EXECUTABLE_ORDER_CREATED"
    "phase-pms-ems-oms-r031-no-scheduler-service-polling-audit.json" = "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
    "phase-pms-ems-oms-r031-no-automatic-execution-audit.json" = "PMS_EMS_OMS_R031_FAIL_AUTOMATIC_EXECUTION_INTRODUCED"
    "phase-pms-ems-oms-r031-no-paper-ledger-commit-audit.json" = "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
    "phase-pms-ems-oms-r031-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R031_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r031-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R031_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r031-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R031_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r031-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R031_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r031-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r031-no-order-created-audit.json" = "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r031-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r031-idempotency-evidence.json" = "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED"
    "phase-pms-ems-oms-r031-lineage-preservation.json" = "PMS_EMS_OMS_R031_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r031-instrument-universe-handling.json" = "PMS_EMS_OMS_R031_FAIL_AUDUSD_MISCLASSIFIED"
    "phase-pms-ems-oms-r031-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R031_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r031-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r031-no-external-audit.json" = "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r031-forbidden-actions-audit.json" = "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r031-next-phase-recommendation.json" = "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
    "phase-pms-ems-oms-r031-build-test-validator-evidence.json" = "PMS_EMS_OMS_R031_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-cli-contract.json") "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
$arguments = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-cli-arguments-contract.json") "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
$preflight = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-cli-preflight-contract.json") "PMS_EMS_OMS_R031_FAIL_PREFLIGHT_WEAKENED"
$valid = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-cli-valid-request-example.json") "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED"
$rejected = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-cli-rejected-request-examples.json") "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED"
$output = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-cli-output-contract.json") "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
$example = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-manual-cycle-run-result-example.json") "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING"
$intentAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-non-executable-intent-audit.json") "PMS_EMS_OMS_R031_FAIL_EXECUTABLE_ORDER_CREATED"
$schedulerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-scheduler-service-polling-audit.json") "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
$automaticAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-automatic-execution-audit.json") "PMS_EMS_OMS_R031_FAIL_AUTOMATIC_EXECUTION_INTRODUCED"
$paperLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-paper-ledger-commit-audit.json") "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
$liveAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R031_FAIL_LIVE_POSITION_MUTATION"
$brokerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R031_FAIL_BROKER_POSITION_MUTATION"
$productionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R031_FAIL_PRODUCTION_LEDGER_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R031_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-order-created-audit.json") "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-route-no-submission-audit.json") "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-idempotency-evidence.json") "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-lineage-preservation.json") "PMS_EMS_OMS_R031_FAIL_QUBES_LINEAGE_WEAKENED"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-instrument-universe-handling.json") "PMS_EMS_OMS_R031_FAIL_AUDUSD_MISCLASSIFIED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R031_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-no-external-audit.json") "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-forbidden-actions-audit.json") "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r031-build-test-validator-evidence.json") "PMS_EMS_OMS_R031_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.cliContractCreated) "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING" "CLI contract missing."
Require-True ([string]$contract.command -eq "run-manual-paper-cycle") "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING" "Wrong command."
Require-True ([bool]$contract.explicitManualInvocationRequired) "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "Manual invocation not required."
Require-True ([bool]$contract.requiresManualNoExternalMode) "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "ManualNoExternal not required."
Require-True ([bool]$contract.callsPreflightBeforeCycle) "PMS_EMS_OMS_R031_FAIL_PREFLIGHT_WEAKENED" "Preflight not required."
Require-True ([bool]$contract.runsAtMostOneCyclePerInvocation) "PMS_EMS_OMS_R031_FAIL_MULTIPLE_CYCLES_ALLOWED" "Multiple cycles allowed."
Require-False ([bool]$contract.automaticExecutionIntroduced) "PMS_EMS_OMS_R031_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution introduced."
Require-False ([bool]$contract.schedulerServicePollingIntroduced) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling introduced."
Require-False ([bool]$contract.paperLedgerCommitAllowed) "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit allowed."
Require-False ([bool]$contract.brokerOrLiveMarketInputAllowed) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live boundary allowed."
Require-False ([bool]$contract.orderTradingModeAllowed) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order/trading mode allowed."

Require-True ([bool]$arguments.cliArgumentsContractCreated) "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING" "Argument contract missing."
Require-True (@($arguments.requiredArguments) -contains "--mode ManualNoExternal") "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "Mode argument missing."
Require-True (@($arguments.requiredArguments) -contains "--expected-cadence-minutes 15") "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "Cadence argument missing."
Require-True ([bool]$arguments.noOptionEnablesLiveBroker) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live broker option enabled."
Require-True ([bool]$arguments.noOptionEnablesSchedulerServicePolling) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler option enabled."
Require-True ([bool]$arguments.noOptionEnablesTradingOrdersFillsReports) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Trading/order option enabled."
Require-True ([bool]$arguments.noOptionEnablesPaperLedgerCommit) "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit option enabled."

Require-True ([bool]$preflight.cliPreflightContractCreated) "PMS_EMS_OMS_R031_FAIL_PREFLIGHT_WEAKENED" "Preflight contract missing."
Require-True ([bool]$preflight.requiresPriorPaperContinuityReadyNoExternal) "PMS_EMS_OMS_R031_FAIL_PREFLIGHT_WEAKENED" "Continuity gate not required."
Require-True ([bool]$preflight.requiresPriorPaperLedgerBaseline) "PMS_EMS_OMS_R031_FAIL_PREFLIGHT_WEAKENED" "Baseline not required."
Require-True ([bool]$preflight.requiresQubesRunId) "PMS_EMS_OMS_R031_FAIL_PREFLIGHT_WEAKENED" "QubesRunId not required."
Require-True ([bool]$preflight.requiresFifteenMinuteCadence) "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "15-minute cadence not required."
Require-True ([bool]$preflight.rejectsSchedulerServicePollingMode) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler not rejected."
Require-True ([bool]$preflight.rejectsLiveBrokerOrLiveMarketInputMode) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live boundary not rejected."
Require-True ([bool]$preflight.rejectsOrderOrTradingMode) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order/trading not rejected."
Require-True ([bool]$preflight.rejectsPaperLedgerCommit) "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper commit not rejected."

Require-True ([bool]$valid.validRequestExampleCreated) "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "Valid request missing."
Require-True ([string]$valid.mode -eq "ManualNoExternal") "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "Valid request mode wrong."
Require-True ([int]$valid.expectedCadenceMinutes -eq 15) "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "Valid request cadence wrong."
Require-True ([string]$valid.preflightStatus -eq "ReadyNoExternal") "PMS_EMS_OMS_R031_FAIL_PREFLIGHT_WEAKENED" "Valid preflight wrong."
Require-True ([bool]$valid.canRunExactlyOneCycle) "PMS_EMS_OMS_R031_FAIL_MULTIPLE_CYCLES_ALLOWED" "One-cycle gate wrong."
Require-True ([bool]$rejected.rejectedRequestExamplesCreated) "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "Rejected examples missing."
Require-True (($rejected.examples | Where-Object { $_.reason -eq "UnsafeSchedulerServicePolling" }).cycleExecuted -eq $false) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Unsafe scheduler example runs."
Require-True (($rejected.examples | Where-Object { $_.reason -eq "UnsafeLiveBoundary" }).cycleExecuted -eq $false) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Unsafe live example runs."
Require-True (($rejected.examples | Where-Object { $_.reason -eq "UnsafeOrderOrTradingMode" }).cycleExecuted -eq $false) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Unsafe order example runs."

Require-True ([bool]$output.cliOutputContractCreated) "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING" "Output contract missing."
Require-True ([bool]$output.includesPreflightResult) "PMS_EMS_OMS_R031_FAIL_PREFLIGHT_WEAKENED" "Output omits preflight."
Require-True ([bool]$output.includesTargetVsCurrentDiff) "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING" "Output omits diff."
Require-True ([bool]$output.includesNonExecutableRebalanceIntents) "PMS_EMS_OMS_R031_FAIL_EXECUTABLE_ORDER_CREATED" "Output omits intents."
Require-True ([bool]$output.serializesSafeSummariesOnly) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Unsafe serialization."
Require-False ([bool]$output.serializesRawBrokerPayloads) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Raw broker payload serialized."
Require-False ([bool]$output.serializesRawMarketInputPayloads) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Raw market input serialized."

Require-True ([bool]$example.manualCycleRunResultExampleCreated) "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING" "Result example missing."
Require-True ([string]$example.cliStatus -eq "CompletedNoExternal") "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "CLI result not completed."
Require-True ([int]$example.manualCycleExecutionCount -eq 1) "PMS_EMS_OMS_R031_FAIL_MULTIPLE_CYCLES_ALLOWED" "Wrong cycle count."
Require-False ([bool]$example.multipleCyclesRun) "PMS_EMS_OMS_R031_FAIL_MULTIPLE_CYCLES_ALLOWED" "Multiple cycles run."
Require-False ([bool]$example.paperLedgerCommitted) "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger committed."
Require-False ([bool]$example.ordersCreated) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order created."
Require-False ([bool]$example.fillsCreated) "PMS_EMS_OMS_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill created."
Require-False ([bool]$example.executionReportsCreated) "PMS_EMS_OMS_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Report created."
Require-False ([bool]$example.routesCreated) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Route created."
Require-False ([bool]$example.ordersSubmitted) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order submitted."

Require-True ([bool]$intentAudit.auditCreated) "PMS_EMS_OMS_R031_FAIL_EXECUTABLE_ORDER_CREATED" "Intent audit missing."
Require-True ([bool]$intentAudit.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R031_FAIL_EXECUTABLE_ORDER_CREATED" "Intent executable."
Require-False ([bool]$intentAudit.executableIntentCreated) "PMS_EMS_OMS_R031_FAIL_EXECUTABLE_ORDER_CREATED" "Executable intent created."
Require-False ([bool]$intentAudit.orderPathIntroduced) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order path introduced."

Require-False ([bool]$schedulerAudit.schedulerIntroduced) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler introduced."
Require-False ([bool]$schedulerAudit.serviceIntroduced) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Service introduced."
Require-False ([bool]$schedulerAudit.pollingIntroduced) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Polling introduced."
Require-False ([bool]$schedulerAudit.timerIntroduced) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Timer introduced."
Require-False ([bool]$schedulerAudit.backgroundJobIntroduced) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Background job introduced."
Require-False ([bool]$automaticAudit.automaticExecutionIntroduced) "PMS_EMS_OMS_R031_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution introduced."
Require-True ([bool]$automaticAudit.manualInvocationRequired) "PMS_EMS_OMS_R031_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Manual invocation not required."
Require-True ([bool]$automaticAudit.runsAtMostOneCyclePerInvocation) "PMS_EMS_OMS_R031_FAIL_MULTIPLE_CYCLES_ALLOWED" "More than one cycle per invocation."

Require-False ([bool]$paperLedgerAudit.paperLedgerCommitOccurred) "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit occurred."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateMutated) "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger mutated."
Require-False ([bool]$liveAudit.livePositionStateMutated) "PMS_EMS_OMS_R031_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-False ([bool]$brokerAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R031_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-False ([bool]$productionAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R031_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R031_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$fillAudit.fillsCreated) "PMS_EMS_OMS_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$fillAudit.executionReportsCreated) "PMS_EMS_OMS_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$orderAudit.ordersCreated) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$orderAudit.executableOrdersCreated) "PMS_EMS_OMS_R031_FAIL_EXECUTABLE_ORDER_CREATED" "Executable orders created."
Require-False ([bool]$routeAudit.brokerRoutesCreated) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$routeAudit.ordersSubmitted) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders submitted."

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "Idempotency missing."
Require-True ([string]$idempotency.duplicateRequestedCycleRunIdBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "Duplicate behavior wrong."
Require-False ([bool]$idempotency.duplicateRunsMoreThanOneCycle) "PMS_EMS_OMS_R031_FAIL_MULTIPLE_CYCLES_ALLOWED" "Duplicate runs second cycle."
Require-False ([bool]$idempotency.duplicateCommitsPaperLedger) "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Duplicate commits paper ledger."

Require-True ([bool]$lineage.lineagePreservationCreated) "PMS_EMS_OMS_R031_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage missing."
Require-True ([bool]$lineage.qubesLineagePreserved) "PMS_EMS_OMS_R031_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
Require-True ([bool]$lineage.r028ContractLineagePreserved) "PMS_EMS_OMS_R031_FAIL_QUBES_LINEAGE_WEAKENED" "R028 lineage missing."
Require-True ([bool]$lineage.r029RunnerLineagePreserved) "PMS_EMS_OMS_R031_FAIL_QUBES_LINEAGE_WEAKENED" "R029 lineage missing."
Require-True ([bool]$lineage.r030ArchiveLineagePreserved) "PMS_EMS_OMS_R031_FAIL_QUBES_LINEAGE_WEAKENED" "R030 lineage missing."
Require-True ([bool]$lineage.r025PaperBaselineLineagePreserved) "PMS_EMS_OMS_R031_FAIL_QUBES_LINEAGE_WEAKENED" "Paper baseline lineage missing."
Require-True ([bool]$lineage.riskLineagePreserved) "PMS_EMS_OMS_R031_FAIL_QUBES_LINEAGE_WEAKENED" "Risk lineage missing."
Require-True ([bool]$lineage.lotSizingLineagePreserved) "PMS_EMS_OMS_R031_FAIL_QUBES_LINEAGE_WEAKENED" "Lot-sizing lineage missing."

Require-False ([bool]$universe.audusdMisclassifiedFailed) "PMS_EMS_OMS_R031_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([string]$universe.audusdStatus -eq "PausedTlsBoundaryInconclusiveNotFailed") "PMS_EMS_OMS_R031_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD caveat weakened."
Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "PMS_EMS_OMS_R031_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.securityId -eq "4004") "PMS_EMS_OMS_R031_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID wrong."
Require-True ([string]$usdjpy.securityIdSource -eq "8") "PMS_EMS_OMS_R031_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource wrong."
Require-False ([bool]$usdjpy.weakened) "PMS_EMS_OMS_R031_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY weakened."
Require-True ([bool]$lmax.referenceOnly) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.brokerCalled) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker called."

Require-False ([bool]$noExternal.brokerActivation) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker activation."
Require-False ([bool]$noExternal.socketTlsFixRuntimeAction) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Socket/TLS/FIX runtime action."
Require-False ([bool]$noExternal.liveMarketRuntimeAction) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market runtime action."
Require-False ([bool]$noExternal.apiWorkerLiveGatewayEnabled) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API/Worker live gateway."
Require-False ([bool]$noExternal.schedulerPollingServiceTimerBackgroundJob) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling."
Require-False ([bool]$noExternal.automaticExecution) "PMS_EMS_OMS_R031_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution."
Require-False ([bool]$noExternal.paperLedgerCommit) "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit."
Require-False ([bool]$noExternal.liveBrokerProductionTradingMutation) "PMS_EMS_OMS_R031_FAIL_TRADING_STATE_MUTATION" "Live/broker/production/trading mutation."

Require-False ([bool]$forbidden.newExternalActionDetected) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden external action."
Require-False ([bool]$forbidden.schedulerOrServiceIntroduced) "PMS_EMS_OMS_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden scheduler/service."
Require-False ([bool]$forbidden.automaticExecutionIntroduced) "PMS_EMS_OMS_R031_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Forbidden automatic execution."
Require-False ([bool]$forbidden.cliGuardWeakened) "PMS_EMS_OMS_R031_FAIL_CLI_GUARD_WEAKENED" "CLI guard weakened."
Require-False ([bool]$forbidden.multipleCyclesAllowed) "PMS_EMS_OMS_R031_FAIL_MULTIPLE_CYCLES_ALLOWED" "Multiple cycles allowed."
Require-False ([bool]$forbidden.preflightWeakened) "PMS_EMS_OMS_R031_FAIL_PREFLIGHT_WEAKENED" "Preflight weakened."
Require-False ([bool]$forbidden.paperLedgerCommitOccurred) "PMS_EMS_OMS_R031_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit."
Require-False ([bool]$forbidden.orderOrTradingPathIntroduced) "PMS_EMS_OMS_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order/trading path."
Require-False ([bool]$forbidden.secretOrRawPayloadSerializationRisk) "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Secret/raw payload risk."

$sourceFiles = @(
    (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/ManualPaperCycleCliSurface.cs"),
    (Join-Path $repoRoot "tools/QQ.Production.Intraday.Tools.ManualPaperCycle/Program.cs")
)
$forbiddenPatterns = @(
    "TcpClient",
    "SslStream",
    "MarketDataRequest",
    "MarketDataResponse",
    "FixSession",
    "IHostedService",
    "BackgroundService",
    "PeriodicTimer",
    "Task.Delay",
    "System.Threading.Timer",
    "SubmitOrder",
    "SendOrderAsync"
)
foreach ($file in $sourceFiles) {
    if (-not (Test-Path -LiteralPath $file)) {
        Fail-Gate "PMS_EMS_OMS_R031_FAIL_CLI_CONTRACT_MISSING" "Missing source file: $file"
    }
    $text = Get-Content -LiteralPath $file -Raw
    foreach ($pattern in $forbiddenPatterns) {
        if ($text.Contains($pattern)) {
            Fail-Gate "PMS_EMS_OMS_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden source token '$pattern' in $file"
        }
    }
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R031_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "PMS_EMS_OMS_R031_FAIL_BUILD_OR_TESTS" "Build failed."
Require-True ([string]$evidence.focusedTests -like "PASS*") "PMS_EMS_OMS_R031_FAIL_BUILD_OR_TESTS" "Focused tests failed."
Require-True ([string]$evidence.unitTests -like "PASS*") "PMS_EMS_OMS_R031_FAIL_BUILD_OR_TESTS" "Unit tests failed."
Require-True ([string]$evidence.validator -eq "PASS") "PMS_EMS_OMS_R031_FAIL_BUILD_OR_TESTS" "Validator evidence missing."

Write-Host "PMS_EMS_OMS_R031_PASS_MANUAL_PAPER_CYCLE_CLI_SURFACE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R031_PASS_GUARDED_ONE_CYCLE_CLI_GATE_READY_NO_EXTERNAL"

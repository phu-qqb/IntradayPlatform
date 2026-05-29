param(
    [string]$ArtifactDirectory = "artifacts/readiness/pms-ems-oms-integration"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param(
        [string]$Classification,
        [string]$Message
    )

    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-JsonArtifact {
    param(
        [string]$Path,
        [string]$MissingClassification
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Fail-Gate $MissingClassification "Missing required artifact: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        Fail-Gate $MissingClassification "Artifact is not valid JSON: $Path"
    }
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
    "phase-pms-ems-oms-r004b-qubes-db-persistence-summary.md" = "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING"
    "phase-pms-ems-oms-r004b-qubes-db-persistence.json" = "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING"
    "phase-pms-ems-oms-r004b-existing-persistence-map.json" = "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING"
    "phase-pms-ems-oms-r004b-raw-qubes-row-persistence.json" = "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING"
    "phase-pms-ems-oms-r004b-normalized-weight-persistence.json" = "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING"
    "phase-pms-ems-oms-r004b-modelweightbatch-targetweight-linkage.json" = "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING"
    "phase-pms-ems-oms-r004b-qubesrunid-lineage.json" = "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING"
    "phase-pms-ems-oms-r004b-idempotency-and-duplicate-handling.json" = "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING"
    "phase-pms-ems-oms-r004b-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R004B_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r004b-no-external-audit.json" = "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r004b-forbidden-actions-audit.json" = "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r004b-next-phase-recommendation.json" = "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING"
    "phase-pms-ems-oms-r004b-build-test-validator-evidence.json" = "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    $path = Join-Path $artifactRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$persistence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-qubes-db-persistence.json") "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING"
$map = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-existing-persistence-map.json") "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING"
$raw = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-raw-qubes-row-persistence.json") "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING"
$normalized = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-normalized-weight-persistence.json") "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING"
$linkage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-modelweightbatch-targetweight-linkage.json") "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-qubesrunid-lineage.json") "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-idempotency-and-duplicate-handling.json") "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R004B_FAIL_USDJPY_CAVEAT_WEAKENED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-no-external-audit.json") "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-forbidden-actions-audit.json") "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-next-phase-recommendation.json") "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004b-build-test-validator-evidence.json") "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$persistence.qubesDbPersistenceCreated) "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING" "Qubes DB persistence marker is missing."
Require-True ([bool]$persistence.qubesDbPersistenceVerified) "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING" "Qubes DB persistence was not verified."
Require-True ([bool]$persistence.qubesDbPersistenceRepaired) "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING" "Qubes raw persistence repair marker is missing."
Require-True ([bool]$persistence.rawQubesRowsPersisted) "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING" "Raw Qubes rows are not persisted."
Require-True ([bool]$persistence.normalizedUsdQuoteWeightsPersisted) "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING" "Normalized USD-quote rows are not persisted."
Require-True ([bool]$persistence.qubesRunIdLineagePersisted) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "QubesRunId lineage is not persisted."
Require-True ([bool]$persistence.idempotencyImplemented) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "Idempotency is missing."
Require-True ([bool]$persistence.usesModelWeightBatch) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "ModelWeightBatch usage is missing."
Require-True ([bool]$persistence.usesModelRun) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "ModelRun usage is missing."
Require-True ([bool]$persistence.usesTargetWeight) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "TargetWeight usage is missing."
Require-True ([string]$persistence.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "Qubes source system is missing."
Require-True ([int]$persistence.cadenceMinutes -eq 15) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "15-minute cadence is missing."
Require-False ([bool]$persistence.ordersCreated) "PMS_EMS_OMS_R004B_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders were created."
Require-False ([bool]$persistence.brokerGatewayCalled) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker gateway was called."

Require-True ([bool]$map.persistenceMapCreated) "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING" "Persistence map is missing."
Require-True ([string]$map.existingBeforeR004B.modelWeightBatches.table -eq "ModelWeightBatches") "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING" "Existing ModelWeightBatches map is missing."
Require-True ([string]$map.existingBeforeR004B.modelWeightRows.table -eq "ModelWeightRows") "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING" "Existing ModelWeightRows map is missing."
Require-True ([string]$map.existingBeforeR004B.modelRuns.table -eq "ModelRuns") "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING" "Existing ModelRuns map is missing."
Require-True ([string]$map.existingBeforeR004B.targetWeights.table -eq "TargetWeights") "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING" "Existing TargetWeights map is missing."
Require-False ([bool]$map.gapFoundBeforeRepair.rawQubesSemicolonRowsPersisted) "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING" "R004B did not record the raw-row persistence gap."
Require-True ([string]$map.addedInR004B.auditBatchTable -eq "QubesWeightAuditBatches") "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING" "Audit batch table is missing."

Require-True ([bool]$raw.rawQubesRowPersistenceCreated) "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING" "Raw row persistence artifact is missing."
Require-True ([bool]$raw.rawQubesRowPersistenceSupported) "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING" "Raw row persistence is unsupported."
Require-True (($raw.samplePersistedRows | Measure-Object).Count -ge 2) "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING" "Raw row sample evidence is missing."
Require-True ([bool]$raw.rawRowsAreModelInputsNotBrokerSecrets) "PMS_EMS_OMS_R004B_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw Qubes rows are not identified as model inputs."
Require-False ([bool]$raw.brokerPayloadsSerialized) "PMS_EMS_OMS_R004B_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Broker payloads are serialized."
Require-False ([bool]$raw.secretsSerialized) "PMS_EMS_OMS_R004B_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Secrets are serialized."

Require-True ([bool]$normalized.normalizedWeightPersistenceCreated) "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING" "Normalized persistence artifact is missing."
Require-True ([bool]$normalized.normalizedWeightPersistenceSupported) "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING" "Normalized persistence is unsupported."
Require-True ([bool]$normalized.storesUsdQuoteNormalizedTargets) "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING" "USD-quote normalized target storage is missing."
Require-False ([bool]$normalized.usdUsdEmitted) "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING" "USDUSD was emitted."
Require-True (($normalized.sampleNormalizedRows | Measure-Object).Count -ge 4) "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING" "Normalized row sample evidence is missing."
Require-False ([bool]$normalized.lmaxValidationScopeUsedAsPersistenceGate) "PMS_EMS_OMS_R004B_FAIL_AUDUSD_MISCLASSIFIED" "LMAX validation scope gates Qubes persistence."

Require-True ([bool]$linkage.modelWeightBatchTargetWeightLinkageCreated) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "Linkage artifact is missing."
Require-True ([bool]$linkage.modelWeightBatchLinkagePresent) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "ModelWeightBatch linkage is missing."
Require-True ([bool]$linkage.modelRunLinkagePresent) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "ModelRun linkage is missing."
Require-True ([bool]$linkage.targetWeightLinkagePresent) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "TargetWeight linkage is missing."
Require-True ([bool]$linkage.usesExistingTargetWeightInfrastructure) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "Existing TargetWeight infrastructure was not used."
Require-False ([bool]$linkage.duplicateTablesForModelRunOrTargetWeightCreated) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "Duplicate ModelRun/TargetWeight tables were created."
Require-False ([bool]$linkage.executableOrdersCreated) "PMS_EMS_OMS_R004B_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable orders were created."

Require-True ([bool]$lineage.qubesRunIdLineageCreated) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "QubesRunId lineage artifact is missing."
Require-True ([bool]$lineage.qubesRunIdHandlingPresent) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "QubesRunId handling is missing."
Require-True ([string]$lineage.qubesRunId -ne "") "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "QubesRunId is empty."
Require-True ([string]$lineage.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "Qubes source lineage is missing."
Require-True ([int]$lineage.cadenceMinutes -eq 15) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "15-minute cadence lineage is missing."
Require-True ([int]$lineage.rawRowCount -gt 0) "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING" "Raw row count is missing."
Require-True ([int]$lineage.normalizedRowCount -gt 0) "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING" "Normalized row count is missing."
Require-True ([bool]$lineage.lineage.qubesRunIdToAuditBatch) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "QubesRunId to audit batch linkage missing."
Require-True ([bool]$lineage.lineage.auditBatchToRawRows) "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING" "Audit batch to raw rows linkage missing."
Require-True ([bool]$lineage.lineage.auditBatchToNormalizedRows) "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING" "Audit batch to normalized rows linkage missing."
Require-True ([bool]$lineage.lineage.auditBatchToModelWeightBatch) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "Audit batch to ModelWeightBatch linkage missing."
Require-True ([bool]$lineage.lineage.auditBatchToPromotedModelRun) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "Audit batch to ModelRun linkage missing."
Require-True ([bool]$lineage.lineage.normalizedRowsToTargetWeightInstrument) "PMS_EMS_OMS_R004B_FAIL_MODELWEIGHT_TARGETWEIGHT_LINKAGE_MISSING" "Normalized row to TargetWeight linkage missing."
Require-False ([bool]$lineage.missingRunIdPersistsValidBatch) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "Missing run id can persist a valid batch."
Require-False ([bool]$lineage.malformedTickerPersistsPromotedTargetWeights) "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING" "Malformed ticker can persist promoted target weights."

Require-True ([bool]$idempotency.idempotencyAndDuplicateHandlingCreated) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "Idempotency artifact is missing."
Require-True ([bool]$idempotency.idempotencyImplemented) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "Idempotency is missing."
Require-True ([bool]$idempotency.duplicateHandlingImplemented) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "Duplicate handling is missing."
Require-True ([string]$idempotency.idempotencyKey -eq "QubesRunId") "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "Idempotency key is not QubesRunId."
Require-True ([bool]$idempotency.serviceBehavior.repeatedIngestionSameQubesRunId.returnsExistingAuditBatch) "PMS_EMS_OMS_R004B_FAIL_QUBESRUNID_LINEAGE_MISSING" "Repeated ingestion does not return existing audit batch."
Require-True ([bool]$idempotency.serviceBehavior.repeatedIngestionSameQubesRunId.doesNotDuplicateRawRows) "PMS_EMS_OMS_R004B_FAIL_RAW_QUBES_ROW_PERSISTENCE_MISSING" "Repeated ingestion duplicates raw rows."
Require-True ([bool]$idempotency.serviceBehavior.repeatedIngestionSameQubesRunId.doesNotDuplicateNormalizedRows) "PMS_EMS_OMS_R004B_FAIL_NORMALIZED_WEIGHT_PERSISTENCE_MISSING" "Repeated ingestion duplicates normalized rows."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R004B_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat artifact is missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R004B_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID caveat is missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R004B_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource caveat is missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R004B_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven status is missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R004B_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY is classified as failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R004B_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS-boundary inconclusive status is missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R004B_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD is classified as failed."
Require-False ([bool]$usdjpy.lmaxLiveValidationGapsBlockQubesPersistence) "PMS_EMS_OMS_R004B_FAIL_AUDUSD_MISCLASSIFIED" "LMAX live gaps block Qubes persistence."

foreach ($property in @(
    "externalBrokerActivationDetected",
    "socketTlsFixMarketDataRuntimeActionDetected",
    "marketDataRequestAttempted",
    "liveMarketDataResponseRead",
    "apiStarted",
    "workerStarted",
    "schedulerPollingServiceStarted",
    "liveGatewayEnabled",
    "orderSubmissionIntroduced",
    "executableOrderCreated",
    "liveTradingPathIntroduced",
    "liveTradingStateMutated",
    "replayOrShadowReplayIntroduced",
    "secretsOrCredentialsSerialized",
    "rawFixSerialized",
    "rawEndpointTlsValuesSerialized",
    "sessionIdsSerialized",
    "compIdsSerialized",
    "rawMdReqIdSerialized",
    "rawBrokerMarketDataPayloadsSerialized",
    "rawBrokerMarketDataPricesSerialized",
    "lmaxOrBrokerCalled",
    "lmaxLiveValidationGapsBlockQubesPersistence",
    "rebalanceIntentExecutable"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "order|trading|rebalance|executable") {
            Fail-Gate "PMS_EMS_OMS_R004B_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING" "Next phase recommendation is missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R005") "PMS_EMS_OMS_R004B_FAIL_PERSISTENCE_MAP_MISSING" "Next phase is not R005."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase is not marked no-external."
Require-True ([bool]$nextPhase.mustNotUseLiveMarketData) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase permits live market data."
Require-True ([bool]$nextPhase.mustNotGenerateExecutableOrders) "PMS_EMS_OMS_R004B_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits executable orders."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R004B_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading is enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R004B_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading is enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections are enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections are enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R004B_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake execution gateway is not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R004B_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake execution gateway is not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime is enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime allows external connections."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R004B_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX read-only runtime allows order submission."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only scheduler is enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime submits to shadow replay."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R004B_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "LMAX runtime persists raw FIX messages."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market-data bars are enabled."
Require-False ([bool]$workerSettings.ModelWeights.PromoteReadyBatches) "PMS_EMS_OMS_R004B_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker model-weight promotion is enabled for background processing."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r004b-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
$unsafePatterns = @(
    "\u0001",
    "35=",
    "MDReqID\s*[:=]",
    "SenderCompID\s*[:=]",
    "TargetCompID\s*[:=]",
    "BeginString\s*[:=]",
    "SocketHost\s*[:=]",
    "TlsHost\s*[:=]",
    "Password\s*[:=]",
    "ApiKey\s*[:=]",
    "Secret\s*[:=]",
    "Bearer\s+[A-Za-z0-9_\.-]+"
)

foreach ($pattern in $unsafePatterns) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R004B_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesWeightPersistence.cs",
    "src/QQ.Production.Intraday.Infrastructure.SqlServer/Migrations/20260520090000_AddQubesWeightAuditPersistence.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesWeightPersistenceTests.cs",
    "tests/QQ.Production.Intraday.Tests.Integration/SqlServerLocalDbTests.cs"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS" "Required implementation/test file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesWeightPersistence.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "ParentOrder", "ChildOrder", "FixSession", "Lmax")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R004B_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Qubes persistence source contains forbidden runtime pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesWeightPersistenceTests.cs") -Raw
foreach ($requiredTestName in @(
    "Valid_qubes_raw_fixture_persists_a_qubes_audit_batch",
    "Raw_input_rows_are_persisted_and_retrievable",
    "Normalized_usd_quote_weights_are_persisted",
    "Qubes_run_id_links_raw_rows_to_normalized_rows",
    "Normalized_rows_link_to_modelweightbatch_modelrun_and_targetweight_after_promotion",
    "Cadence_fifteen_minutes_is_persisted_and_validated",
    "Missing_run_id_does_not_persist_valid_batch",
    "Malformed_ticker_does_not_persist_promoted_target_weights",
    "Duplicate_or_repeated_ingestion_is_idempotent",
    "Audusd_and_usdjpy_live_validation_gaps_do_not_block_persistence",
    "Usdjpy_caveat_remains_preserved",
    "No_order_trading_or_broker_path_is_introduced"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS" "Build evidence is missing or not PASS."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS" "Focused test evidence is missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.integrationTests.status -eq "PASS") "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS" "Integration test evidence is missing or not PASS."
Require-True ([int]$evidence.integrationTests.failed -eq 0) "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS" "Integration tests have failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence marker is missing."
Require-True (Test-Path -LiteralPath (Join-Path $repoRoot "scripts/check-pms-ems-oms-r004b-qubes-db-persistence-gate.ps1")) "PMS_EMS_OMS_R004B_FAIL_BUILD_OR_TESTS" "Validator script is missing."

Write-Host "PMS_EMS_OMS_R004B_PASS_QUBES_DB_PERSISTENCE_VERIFIED_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R004B_PASS_QUBES_DB_PERSISTENCE_REPAIRED_NO_EXTERNAL"

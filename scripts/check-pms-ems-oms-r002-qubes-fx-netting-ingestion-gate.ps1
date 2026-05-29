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
    param(
        [bool]$Value,
        [string]$Classification,
        [string]$Message
    )

    if (-not $Value) {
        Fail-Gate $Classification $Message
    }
}

function Require-False {
    param(
        [bool]$Value,
        [string]$Classification,
        [string]$Message
    )

    if ($Value) {
        Fail-Gate $Classification $Message
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot $ArtifactDirectory

$requiredArtifacts = @{
    "phase-pms-ems-oms-r002-summary.md" = "PMS_EMS_OMS_R002_FAIL_QUBES_FIXTURE_MISSING"
    "phase-pms-ems-oms-r002-qubes-fx-fixture-ingestion.json" = "PMS_EMS_OMS_R002_FAIL_QUBES_FIXTURE_MISSING"
    "phase-pms-ems-oms-r002-raw-qubes-fixture-contract.json" = "PMS_EMS_OMS_R002_FAIL_QUBES_FIXTURE_MISSING"
    "phase-pms-ems-oms-r002-fx-netting-contract.json" = "PMS_EMS_OMS_R002_FAIL_FX_NETTING_CONTRACT_MISSING"
    "phase-pms-ems-oms-r002-usd-quote-normalized-target-weights.json" = "PMS_EMS_OMS_R002_FAIL_USDQUOTE_NORMALIZATION_MISSING"
    "phase-pms-ems-oms-r002-validation-rules.json" = "PMS_EMS_OMS_R002_FAIL_CADENCE_VALIDATION_MISSING"
    "phase-pms-ems-oms-r002-modelweightbatch-mapping.json" = "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING"
    "phase-pms-ems-oms-r002-promotion-evidence.json" = "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING"
    "phase-pms-ems-oms-r002-instrument-universe-handling.json" = "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION"
    "phase-pms-ems-oms-r002-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R002_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r002-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION"
    "phase-pms-ems-oms-r002-no-external-audit.json" = "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r002-forbidden-actions-audit.json" = "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r002-next-phase-recommendation.json" = "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING"
    "phase-pms-ems-oms-r002-build-test-validator-evidence.json" = "PMS_EMS_OMS_R002_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    $path = Join-Path $artifactRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$ingestion = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-qubes-fx-fixture-ingestion.json") "PMS_EMS_OMS_R002_FAIL_QUBES_FIXTURE_MISSING"
$rawContract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-raw-qubes-fixture-contract.json") "PMS_EMS_OMS_R002_FAIL_QUBES_FIXTURE_MISSING"
$netting = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-fx-netting-contract.json") "PMS_EMS_OMS_R002_FAIL_FX_NETTING_CONTRACT_MISSING"
$normalized = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-usd-quote-normalized-target-weights.json") "PMS_EMS_OMS_R002_FAIL_USDQUOTE_NORMALIZATION_MISSING"
$validation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-validation-rules.json") "PMS_EMS_OMS_R002_FAIL_CADENCE_VALIDATION_MISSING"
$mapping = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-modelweightbatch-mapping.json") "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING"
$promotion = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-promotion-evidence.json") "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-instrument-universe-handling.json") "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R002_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-no-external-audit.json") "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-forbidden-actions-audit.json") "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-next-phase-recommendation.json") "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r002-build-test-validator-evidence.json") "PMS_EMS_OMS_R002_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$ingestion.implemented) "PMS_EMS_OMS_R002_FAIL_QUBES_FIXTURE_MISSING" "Qubes fixture ingestion marker is missing."
Require-True ([string]$ingestion.qubesRunId -ne "") "PMS_EMS_OMS_R002_FAIL_QUBES_RUNID_OR_SOURCE_MISSING" "QubesRunId is missing."
Require-True ([string]$ingestion.sourceSystem -eq "Qubes") "PMS_EMS_OMS_R002_FAIL_QUBES_RUNID_OR_SOURCE_MISSING" "Qubes source system is missing."
Require-True ([int]$ingestion.cadenceMinutes -eq 15) "PMS_EMS_OMS_R002_FAIL_CADENCE_VALIDATION_MISSING" "15-minute cadence evidence is missing."
Require-True ([int]$ingestion.rawInputRowCount -gt 0) "PMS_EMS_OMS_R002_FAIL_QUBES_FIXTURE_MISSING" "Raw fixture row count is missing."
Require-True ([int]$ingestion.normalizedOutputRowCount -gt 0) "PMS_EMS_OMS_R002_FAIL_USDQUOTE_NORMALIZATION_MISSING" "Normalized output row count is missing."
Require-True ([bool]$ingestion.modelWeightBatchRequestCreated) "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "ModelWeightBatch request evidence is missing."
Require-True ([bool]$ingestion.noExternal) "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external marker is missing."

Require-True ([bool]$rawContract.rawFixtureContractCreated) "PMS_EMS_OMS_R002_FAIL_QUBES_FIXTURE_MISSING" "Raw fixture contract is missing."
Require-True ([string]$rawContract.delimiter -eq ";") "PMS_EMS_OMS_R002_FAIL_QUBES_FIXTURE_MISSING" "Raw fixture delimiter is not semicolon."
Require-True ([bool]$rawContract.weightParsing.finiteNumericRequired) "PMS_EMS_OMS_R002_FAIL_QUBES_FIXTURE_MISSING" "Finite numeric weight validation is missing."
Require-False ([bool]$rawContract.rawBrokerOrMarketDataPayloadAllowed) "PMS_EMS_OMS_R002_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker or market-data payloads are allowed."

Require-True ([bool]$netting.fxNettingContractCreated) "PMS_EMS_OMS_R002_FAIL_FX_NETTING_CONTRACT_MISSING" "FX netting contract is missing."
Require-True ([string]$netting.rule.baseExposureDelta -eq "+weight") "PMS_EMS_OMS_R002_FAIL_EXPOSURE_NETTING_INCORRECT" "Base exposure rule is incorrect."
Require-True ([string]$netting.rule.quoteExposureDelta -eq "-weight") "PMS_EMS_OMS_R002_FAIL_EXPOSURE_NETTING_INCORRECT" "Quote exposure rule is incorrect."
Require-True ([bool]$netting.zeroSumValidation.required) "PMS_EMS_OMS_R002_FAIL_EXPOSURE_NETTING_INCORRECT" "Zero-sum validation is missing."
Require-True ([decimal]$netting.zeroSumValidation.sampleTotalCurrencyExposure -eq 0) "PMS_EMS_OMS_R002_FAIL_EXPOSURE_NETTING_INCORRECT" "Sample total exposure is not zero."

Require-True ([bool]$normalized.usdQuoteNormalizationCreated) "PMS_EMS_OMS_R002_FAIL_USDQUOTE_NORMALIZATION_MISSING" "USD-quote normalization contract is missing."
Require-True ([bool]$normalized.usdResidualPreserved) "PMS_EMS_OMS_R002_FAIL_USDQUOTE_NORMALIZATION_MISSING" "USD residual preservation is missing."
Require-False ([bool]$normalized.usdusdEmitted) "PMS_EMS_OMS_R002_FAIL_USDQUOTE_NORMALIZATION_MISSING" "USDUSD was emitted."
Require-True (($normalized.normalizedRows | Measure-Object).Count -eq 13) "PMS_EMS_OMS_R002_FAIL_USDQUOTE_NORMALIZATION_MISSING" "Sample normalized row count is unexpected."

$expectedWeights = @{
    "AUDUSD Curncy" = 0.086178
    "CADUSD Curncy" = 0.017426
    "CHFUSD Curncy" = 0.002553
    "CNHUSD Curncy" = 1.186292
    "EURUSD Curncy" = 0.134196
    "GBPUSD Curncy" = -0.460092
    "JPYUSD Curncy" = -0.008443
    "MXNUSD Curncy" = 0.148627
    "NOKUSD Curncy" = 0.160180
    "NZDUSD Curncy" = -0.560724
    "SEKUSD Curncy" = -0.261092
    "SGDUSD Curncy" = -0.335555
    "ZARUSD Curncy" = -0.396527
}

foreach ($row in $normalized.normalizedRows) {
    if (-not $expectedWeights.ContainsKey([string]$row.ticker)) {
        Fail-Gate "PMS_EMS_OMS_R002_FAIL_USDQUOTE_NORMALIZATION_MISSING" "Unexpected normalized ticker: $($row.ticker)"
    }

    if ([decimal]$row.weight -ne [decimal]$expectedWeights[[string]$row.ticker]) {
        Fail-Gate "PMS_EMS_OMS_R002_FAIL_EXPOSURE_NETTING_INCORRECT" "Unexpected normalized weight for $($row.ticker)."
    }
}

$ruleNames = @($validation.rules | ForEach-Object { [string]$_.name })
foreach ($requiredRule in @("QubesRunIdRequired", "CadenceMustBeFifteenMinutes", "BloombergFxTickerShape", "TotalCurrencyExposureZeroSum", "UnknownInstrumentHandling")) {
    if ($ruleNames -notcontains $requiredRule) {
        Fail-Gate "PMS_EMS_OMS_R002_FAIL_CADENCE_VALIDATION_MISSING" "Validation rule missing: $requiredRule"
    }
}
Require-True ([int]$validation.cadenceMinutes -eq 15) "PMS_EMS_OMS_R002_FAIL_CADENCE_VALIDATION_MISSING" "Cadence validation evidence is not 15 minutes."

Require-True ([bool]$mapping.modelWeightBatchMappingCreated) "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "ModelWeightBatch mapping artifact is missing."
Require-True ([bool]$mapping.usesExistingModelWeightBatch) "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "Existing ModelWeightBatch is not used."
Require-True ([bool]$mapping.usesExistingPromotionInfrastructure) "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "Existing promotion infrastructure is not used."
Require-True ([string]$mapping.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R002_FAIL_QUBES_RUNID_OR_SOURCE_MISSING" "ModelWeightSourceSystem.Qubes mapping is missing."
Require-False ([bool]$mapping.tradingStateMutated) "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "ModelWeightBatch mapping mutates trading state."
Require-False ([bool]$mapping.ordersGenerated) "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "ModelWeightBatch mapping generated orders."

Require-True ([bool]$promotion.promotionEvidenceCreated) "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "Promotion evidence is missing."
Require-True ([bool]$promotion.validPromotionScenario.promotionUsesExistingModelWeightPromotionService) "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "Existing promotion service is not used."
Require-True ([bool]$promotion.validPromotionScenario.promotesToModelRun) "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "ModelRun promotion evidence is missing."
Require-True ([bool]$promotion.validPromotionScenario.promotesToExistingTargetWeights) "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "TargetWeight promotion evidence is missing."
Require-True ([int]$promotion.validPromotionScenario.orderCountForPromotedRun -eq 0) "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion created orders."
Require-False ([bool]$promotion.validPromotionScenario.executableOrderPathIntroduced) "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable order path was introduced."
Require-True ([bool]$promotion.sampleBroadUniverseScenario.unknownInstrumentHandlingSafe) "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "Unknown instrument handling is not safe."
Require-False ([bool]$promotion.sampleBroadUniverseScenario.lmaxValidationGapsBlockIngestion) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "LMAX gaps block ingestion."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "Instrument universe handling artifact is missing."
Require-True ([bool]$universe.qubesUniverseIndependentFromLmaxValidatedScope) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "Qubes universe is not independent from LMAX scope."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsTargetUniverseGate) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "LMAX read-only scope is used as a target-universe gate."
Require-False ([bool]$universe.audusdUsdJpyLiveValidationGapsBlockQubesIngestion) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "AUDUSD/USDJPY gaps block Qubes ingestion."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat artifact is missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven caveat is missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY is classified as failed."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID caveat is missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource caveat is missing."
Require-False ([bool]$usdjpy.q2TargetIngestionDependsOnUsdJpyLiveValidation) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "Qubes ingestion depends on USDJPY live validation."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "LMAX baseline reference artifact is missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX was used in this phase."
Require-True ([bool]$lmax.baseline.GBPUSD.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "GBPUSD read-only baseline is missing."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "GBPUSD sanitized entry count is missing."
Require-True ([bool]$lmax.baseline.EURGBP.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "EURGBP read-only baseline is missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R002_FAIL_LMAX_GAP_BLOCKS_QUBES_INGESTION" "EURGBP sanitized entry count is missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R002_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD is classified as failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY is classified as failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityID caveat is missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityIDSource caveat is missing."

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
    "liveTradingPathIntroduced",
    "tradingStateMutated",
    "replayOrShadowReplayIntroduced",
    "secretsOrCredentialsSerialized",
    "rawFixSerialized",
    "rawEndpointTlsValuesSerialized",
    "sessionIdsSerialized",
    "compIdsSerialized",
    "rawMdReqIdSerialized",
    "rawMarketDataPayloadsSerialized",
    "rawMarketDataPricesSerialized",
    "lmaxOrBrokerCalled",
    "lmaxLiveValidationGapsBlockQubesIngestion",
    "rebalanceIntentExecutable"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "order|trading|rebalance") {
            Fail-Gate "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "Next-phase recommendation is missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R003") "PMS_EMS_OMS_R002_FAIL_MODELWEIGHT_MAPPING_MISSING" "Next-phase recommendation is not R003."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase is not marked no-external."
Require-True ([bool]$nextPhase.mustNotGenerateExecutableOrders) "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase does not preserve non-executable order gate."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading is enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading is enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections are enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections are enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake execution gateway is not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake execution gateway is not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime is enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime allows external connections."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX read-only runtime allows order submission."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only scheduler is enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime submits to shadow replay."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R002_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "LMAX runtime persists raw FIX messages."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r002-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
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
        Fail-Gate "PMS_EMS_OMS_R002_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$ingestionSource = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesFxWeightsIngestion.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "Socket", "FixSession")) {
    if ($ingestionSource -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R002 ingestion source contains forbidden runtime pattern: $pattern"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R002_FAIL_BUILD_OR_TESTS" "Build evidence is missing or not PASS."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R002_FAIL_BUILD_OR_TESTS" "Focused test evidence is missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R002_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R002_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence marker is missing."
Require-True (Test-Path -LiteralPath (Join-Path $repoRoot "scripts/check-pms-ems-oms-r002-qubes-fx-netting-ingestion-gate.ps1")) "PMS_EMS_OMS_R002_FAIL_BUILD_OR_TESTS" "Validator script is missing."

Write-Host "PMS_EMS_OMS_R002_PASS_QUBES_FX_NETTING_INGESTION_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R002_PASS_QUBES_USDQUOTE_MODELWEIGHT_PROMOTION_READY_NO_EXTERNAL"

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
    "phase-pms-ems-oms-r003-summary.md" = "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING"
    "phase-pms-ems-oms-r003-theoretical-portfolio-diff.json" = "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING"
    "phase-pms-ems-oms-r003-target-portfolio-snapshot.json" = "PMS_EMS_OMS_R003_FAIL_TARGET_PORTFOLIO_SNAPSHOT_MISSING"
    "phase-pms-ems-oms-r003-current-portfolio-fixture.json" = "PMS_EMS_OMS_R003_FAIL_CURRENT_PORTFOLIO_FIXTURE_MISSING"
    "phase-pms-ems-oms-r003-rebalance-intents.json" = "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING"
    "phase-pms-ems-oms-r003-rebalance-intent-contract.json" = "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING"
    "phase-pms-ems-oms-r003-non-executable-intent-audit.json" = "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING"
    "phase-pms-ems-oms-r003-qubes-metadata-preservation.json" = "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED"
    "phase-pms-ems-oms-r003-instrument-universe-handling.json" = "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF"
    "phase-pms-ems-oms-r003-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r003-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF"
    "phase-pms-ems-oms-r003-no-external-audit.json" = "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r003-forbidden-actions-audit.json" = "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r003-next-phase-recommendation.json" = "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING"
    "phase-pms-ems-oms-r003-build-test-validator-evidence.json" = "PMS_EMS_OMS_R003_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    $path = Join-Path $artifactRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$diff = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-theoretical-portfolio-diff.json") "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING"
$target = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-target-portfolio-snapshot.json") "PMS_EMS_OMS_R003_FAIL_TARGET_PORTFOLIO_SNAPSHOT_MISSING"
$current = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-current-portfolio-fixture.json") "PMS_EMS_OMS_R003_FAIL_CURRENT_PORTFOLIO_FIXTURE_MISSING"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-rebalance-intents.json") "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING"
$intentContract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-rebalance-intent-contract.json") "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING"
$intentAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-non-executable-intent-audit.json") "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING"
$metadata = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-qubes-metadata-preservation.json") "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-instrument-universe-handling.json") "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-no-external-audit.json") "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-forbidden-actions-audit.json") "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-next-phase-recommendation.json") "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r003-build-test-validator-evidence.json") "PMS_EMS_OMS_R003_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$target.targetPortfolioSnapshotCreated) "PMS_EMS_OMS_R003_FAIL_TARGET_PORTFOLIO_SNAPSHOT_MISSING" "Target portfolio snapshot marker is missing."
Require-True ([string]$target.stateSource -eq "Theoretical") "PMS_EMS_OMS_R003_FAIL_TARGET_PORTFOLIO_SNAPSHOT_MISSING" "Target snapshot is not theoretical."
Require-True ([bool]$target.modelWeightBatchMappingReused) "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED" "ModelWeightBatch mapping is not reused."
Require-True ([bool]$target.targetWeightLinkage.compatibleWithExistingTargetWeights) "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED" "TargetWeight linkage is missing."
Require-True (($target.positions | Measure-Object).Count -eq 13) "PMS_EMS_OMS_R003_FAIL_TARGET_PORTFOLIO_SNAPSHOT_MISSING" "Target snapshot row count is incorrect."
Require-False ([bool]$target.liveMarketDataMarksUsed) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Target snapshot used live market data marks."
Require-False ([bool]$target.liveBrokerStateUsed) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Target snapshot used live broker state."

Require-True ([bool]$current.currentPortfolioFixtureCreated) "PMS_EMS_OMS_R003_FAIL_CURRENT_PORTFOLIO_FIXTURE_MISSING" "Current portfolio fixture marker is missing."
Require-True ([string]$current.fixtureType -eq "flat-zero-exposure") "PMS_EMS_OMS_R003_FAIL_CURRENT_PORTFOLIO_FIXTURE_MISSING" "Current portfolio fixture is not flat zero exposure."
Require-True ([string]$current.stateSource -eq "Simulated") "PMS_EMS_OMS_R003_FAIL_CURRENT_PORTFOLIO_FIXTURE_MISSING" "Current fixture is not simulated."
Require-True (($current.positions | Measure-Object).Count -eq 0) "PMS_EMS_OMS_R003_FAIL_CURRENT_PORTFOLIO_FIXTURE_MISSING" "Current fixture should have zero positions."
Require-False ([bool]$current.usesLiveBrokerState) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Current fixture uses live broker state."
Require-False ([bool]$current.brokerPositionProviderCalled) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker position provider was called."
Require-False ([bool]$current.marketDataMarksUsed) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Current fixture uses market-data marks."
Require-False ([bool]$current.rawBrokerPayloadsSerialized) "PMS_EMS_OMS_R003_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker payloads were serialized."
Require-False ([bool]$current.rawMarketDataPricesSerialized) "PMS_EMS_OMS_R003_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw market-data prices were serialized."

Require-True ([bool]$diff.theoreticalDiffCreated) "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING" "Theoretical diff marker is missing."
Require-True ([bool]$diff.modelWeightBatchMappingReused) "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED" "Diff does not reuse ModelWeightBatch mapping."
Require-True ([bool]$diff.targetWeightLinkagePresent) "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED" "Diff target-weight linkage is missing."
Require-True ([int]$diff.rowCount -eq 13) "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING" "Theoretical diff row count is incorrect."
Require-False ([bool]$diff.liveBrokerStateUsed) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Diff used live broker state."
Require-False ([bool]$diff.liveMarketDataMarksUsed) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Diff used live market data marks."

foreach ($row in $diff.diffRows) {
    if ([decimal]$row.currentWeight -ne 0) {
        Fail-Gate "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING" "Flat fixture current weight should be zero for $($row.symbol)."
    }

    if ([decimal]$row.deltaWeight -ne [decimal]$row.targetWeight) {
        Fail-Gate "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING" "Delta weight does not equal target weight for flat fixture row $($row.symbol)."
    }

    if ([string]$row.category -eq "") {
        Fail-Gate "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING" "Diff category is missing for $($row.symbol)."
    }
}

Require-True ([bool]$intentContract.rebalanceIntentContractCreated) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING" "Rebalance intent contract is missing."
Require-True ([bool]$intentContract.isExecutableAlwaysFalse) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent contract allows executable intents."
Require-False ([bool]$intentContract.orderSubmissionSupported) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Intent contract supports order submission."
Require-False ([bool]$intentContract.omsSubmissionSupported) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Intent contract supports OMS submission."
Require-False ([bool]$intentContract.brokerGatewaySupported) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Intent contract supports broker gateway."

Require-True ([bool]$intents.rebalanceIntentsCreated) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING" "Rebalance intents marker is missing."
Require-True ([int]$intents.intentCount -eq 13) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING" "Intent count is incorrect."
Require-True ([bool]$intents.allIntentsNonExecutable) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENT_EXECUTABLE" "Not all intents are non-executable."
Require-True ([bool]$intents.allIntentsBlockedNoOms) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING" "Not all intents are blocked with no OMS."
Require-False ([bool]$intents.orderSubmissionIntroduced) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order submission was introduced."
Require-False ([bool]$intents.executableOrderCreated) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable order was created."

foreach ($intent in $intents.intents) {
    Require-False ([bool]$intent.isExecutable) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent found for $($intent.symbol)."
    $statuses = @($intent.intentStatuses | ForEach-Object { [string]$_ })
    foreach ($requiredStatus in @("TheoreticalOnly", "NotExecutable", "BlockedNoOMS")) {
        if ($statuses -notcontains $requiredStatus) {
            Fail-Gate "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING" "Intent status $requiredStatus is missing for $($intent.symbol)."
        }
    }
}

Require-True ([bool]$intentAudit.nonExecutableIntentAuditCreated) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING" "Non-executable intent audit is missing."
Require-True ([bool]$intentAudit.allIntentLinesHaveTheoreticalOnlyStatus) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING" "TheoreticalOnly status audit failed."
Require-True ([bool]$intentAudit.allIntentLinesHaveNotExecutableStatus) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING" "NotExecutable status audit failed."
Require-True ([bool]$intentAudit.allIntentLinesHaveBlockedNoOmsStatus) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENTS_MISSING" "BlockedNoOMS status audit failed."
Require-False ([bool]$intentAudit.allIntentLinesAreExecutable) "PMS_EMS_OMS_R003_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent audit says lines are executable."
Require-False ([bool]$intentAudit.executableOrderCreated) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Intent audit detected executable order."
Require-False ([bool]$intentAudit.omsOrderCreated) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Intent audit detected OMS order."
Require-False ([bool]$intentAudit.orderSubmissionIntroduced) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Intent audit detected order submission."
Require-False ([bool]$intentAudit.brokerGatewayCalled) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Intent audit detected broker gateway call."
Require-False ([bool]$intentAudit.liveTradingPathIntroduced) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Intent audit detected live trading path."
Require-False ([bool]$intentAudit.liveTradingStateMutated) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Intent audit detected live trading state mutation."

Require-True ([bool]$metadata.qubesMetadataPreservationCreated) "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED" "Qubes metadata artifact is missing."
Require-True ([string]$metadata.qubesRunId -ne "") "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED" "QubesRunId is missing."
Require-True ([string]$metadata.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED" "Qubes source system is missing."
Require-True ([int]$metadata.cadenceMinutes -eq 15) "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED" "15-minute cadence is missing."
Require-True ([bool]$metadata.r002NormalizedWeightsConsumed) "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED" "R002 normalized weights were not consumed."
Require-True ([bool]$metadata.modelWeightBatchMappingReused) "PMS_EMS_OMS_R003_FAIL_QUBES_METADATA_WEAKENED" "ModelWeightBatch mapping reuse is missing."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF" "Instrument universe artifact is missing."
Require-True ([bool]$universe.qubesTargetUniverseMayBeBroaderThanLmaxValidatedScope) "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF" "Qubes target universe independence is missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsTheoreticalDiffGate) "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF" "LMAX scope gates theoretical diff."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockTheoreticalDiff) "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF" "LMAX gaps block theoretical diff."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksTheoreticalDiff) "PMS_EMS_OMS_R003_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks theoretical diff."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksTheoreticalDiff) "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks theoretical diff."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat artifact is missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven caveat is missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY is classified as failed."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID caveat is missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource caveat is missing."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R003_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS-boundary inconclusive status is missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R003_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD is classified as failed."
Require-False ([bool]$usdjpy.theoreticalDiffDependsOnUsdJpyLiveValidation) "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF" "Theoretical diff depends on USDJPY live validation."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF" "LMAX baseline reference artifact is missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX was used in this phase."
Require-True ([bool]$lmax.baseline.GBPUSD.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF" "GBPUSD read-only baseline is missing."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF" "GBPUSD sanitized count is missing."
Require-True ([bool]$lmax.baseline.EURGBP.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF" "EURGBP read-only baseline is missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R003_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_DIFF" "EURGBP sanitized count is missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R003_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD is classified as failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY is classified as failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityID caveat is missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R003_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityIDSource caveat is missing."

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
    "rawMarketDataPayloadsSerialized",
    "rawMarketDataPricesSerialized",
    "lmaxOrBrokerCalled",
    "lmaxLiveValidationGapsBlockTheoreticalDiff",
    "rebalanceIntentExecutable"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "order|trading|rebalance|executable") {
            Fail-Gate "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING" "Next-phase recommendation is missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R004") "PMS_EMS_OMS_R003_FAIL_THEORETICAL_DIFF_MISSING" "Next phase is not R004."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase is not marked no-external."
Require-True ([bool]$nextPhase.mustNotUseLiveMarketData) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase permits live market data."
Require-True ([bool]$nextPhase.mustNotGenerateExecutableOrders) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits executable orders."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading is enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading is enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections are enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections are enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake execution gateway is not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake execution gateway is not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime is enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime allows external connections."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX read-only runtime allows order submission."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only scheduler is enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime submits to shadow replay."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R003_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "LMAX runtime persists raw FIX messages."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r003-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
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
        Fail-Gate "PMS_EMS_OMS_R003_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$diffSource = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesTheoreticalPortfolioDiff.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "Socket", "FixSession")) {
    if ($diffSource -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R003 diff source contains forbidden runtime pattern: $pattern"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R003_FAIL_BUILD_OR_TESTS" "Build evidence is missing or not PASS."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R003_FAIL_BUILD_OR_TESTS" "Focused test evidence is missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R003_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R003_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence marker is missing."
Require-True (Test-Path -LiteralPath (Join-Path $repoRoot "scripts/check-pms-ems-oms-r003-theoretical-portfolio-diff-gate.ps1")) "PMS_EMS_OMS_R003_FAIL_BUILD_OR_TESTS" "Validator script is missing."

Write-Host "PMS_EMS_OMS_R003_PASS_THEORETICAL_PORTFOLIO_DIFF_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R003_PASS_NONEXECUTABLE_REBALANCE_INTENTS_READY_NO_EXTERNAL"

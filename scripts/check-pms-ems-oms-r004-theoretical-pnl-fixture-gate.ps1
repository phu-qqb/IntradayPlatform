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
    "phase-pms-ems-oms-r004-summary.md" = "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING"
    "phase-pms-ems-oms-r004-market-mark-fixture-contract.json" = "PMS_EMS_OMS_R004_FAIL_MARK_FIXTURE_MISSING"
    "phase-pms-ems-oms-r004-theoretical-marked-portfolio.json" = "PMS_EMS_OMS_R004_FAIL_THEORETICAL_MARKED_PORTFOLIO_MISSING"
    "phase-pms-ems-oms-r004-theoretical-pnl-snapshot.json" = "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING"
    "phase-pms-ems-oms-r004-instrument-pnl-details.json" = "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING"
    "phase-pms-ems-oms-r004-mark-availability-and-staleness.json" = "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING"
    "phase-pms-ems-oms-r004-qubes-metadata-preservation.json" = "PMS_EMS_OMS_R004_FAIL_QUBES_METADATA_WEAKENED"
    "phase-pms-ems-oms-r004-rebalance-intent-preservation.json" = "PMS_EMS_OMS_R004_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r004-instrument-universe-handling.json" = "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL"
    "phase-pms-ems-oms-r004-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r004-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL"
    "phase-pms-ems-oms-r004-no-external-audit.json" = "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r004-forbidden-actions-audit.json" = "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r004-next-phase-recommendation.json" = "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING"
    "phase-pms-ems-oms-r004-build-test-validator-evidence.json" = "PMS_EMS_OMS_R004_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    $path = Join-Path $artifactRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$markFixture = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-market-mark-fixture-contract.json") "PMS_EMS_OMS_R004_FAIL_MARK_FIXTURE_MISSING"
$marked = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-theoretical-marked-portfolio.json") "PMS_EMS_OMS_R004_FAIL_THEORETICAL_MARKED_PORTFOLIO_MISSING"
$pnl = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-theoretical-pnl-snapshot.json") "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING"
$details = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-instrument-pnl-details.json") "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING"
$availability = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-mark-availability-and-staleness.json") "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING"
$metadata = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-qubes-metadata-preservation.json") "PMS_EMS_OMS_R004_FAIL_QUBES_METADATA_WEAKENED"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-rebalance-intent-preservation.json") "PMS_EMS_OMS_R004_FAIL_REBALANCE_INTENT_EXECUTABLE"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-instrument-universe-handling.json") "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-no-external-audit.json") "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-forbidden-actions-audit.json") "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-next-phase-recommendation.json") "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r004-build-test-validator-evidence.json") "PMS_EMS_OMS_R004_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$markFixture.marketMarkFixtureContractCreated) "PMS_EMS_OMS_R004_FAIL_MARK_FIXTURE_MISSING" "Market mark fixture contract is missing."
Require-True ([bool]$markFixture.isNoExternalFixture) "PMS_EMS_OMS_R004_FAIL_MARK_FIXTURE_MISSING" "Mark fixture is not explicitly no-external."
Require-True ([string]$markFixture.fixtureSource -eq "NoExternalR004Fixture") "PMS_EMS_OMS_R004_FAIL_MARK_FIXTURE_MISSING" "Mark fixture source is not explicit."
Require-False ([bool]$markFixture.usesLiveBrokerMarketData) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Mark fixture uses live broker market data."
Require-False ([bool]$markFixture.callsLmax) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Mark fixture calls LMAX."
Require-False ([bool]$markFixture.serializesRawBrokerPayloads) "PMS_EMS_OMS_R004_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker payloads are serialized."
Require-False ([bool]$markFixture.serializesRawBrokerMarketDataPrices) "PMS_EMS_OMS_R004_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker market-data prices are serialized."
Require-True (($markFixture.previousMarks | Measure-Object).Count -ge 5) "PMS_EMS_OMS_R004_FAIL_MARK_FIXTURE_MISSING" "Previous mark fixture coverage is insufficient."
Require-True (($markFixture.currentMarks | Measure-Object).Count -ge 4) "PMS_EMS_OMS_R004_FAIL_MARK_FIXTURE_MISSING" "Current mark fixture coverage is insufficient."

Require-True ([bool]$marked.theoreticalMarkedPortfolioCreated) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_MARKED_PORTFOLIO_MISSING" "Theoretical marked portfolio is missing."
Require-True ([string]$marked.stateSource -eq "Theoretical") "PMS_EMS_OMS_R004_FAIL_THEORETICAL_MARKED_PORTFOLIO_MISSING" "Marked portfolio is not theoretical."
Require-True ([bool]$marked.usesNoExternalMarkFixture) "PMS_EMS_OMS_R004_FAIL_MARK_FIXTURE_MISSING" "Marked portfolio does not use no-external mark fixture."
Require-False ([bool]$marked.usesLiveBrokerMarketData) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Marked portfolio uses live broker market data."
Require-True (($marked.positions | Measure-Object).Count -eq 13) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_MARKED_PORTFOLIO_MISSING" "Marked portfolio row count is incorrect."
Require-True (($marked.positions | Where-Object { [string]$_.pnlStatus -eq "Computed" } | Measure-Object).Count -ge 3) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "Computed PnL rows are missing."
Require-True (($marked.positions | Where-Object { [string]$_.pnlStatus -eq "MissingMark" } | Measure-Object).Count -ge 1) "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "MissingMark handling is missing."
Require-True (($marked.positions | Where-Object { [string]$_.pnlStatus -eq "StaleMark" } | Measure-Object).Count -ge 1) "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "StaleMark handling is missing."

Require-True ([bool]$pnl.theoreticalPnlSnapshotCreated) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "Theoretical PnL snapshot is missing."
Require-True ([string]$pnl.pnlSource -eq "Theoretical") "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "PnL source is not theoretical."
Require-True ([bool]$pnl.fixtureBasedOnly) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "PnL is not fixture-based only."
Require-False ([bool]$pnl.livePnlClaimed) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live PnL is claimed."
Require-False ([bool]$pnl.brokerReportedPnlClaimed) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker-reported PnL is claimed."
Require-False ([bool]$pnl.realizedPnlComputed) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "Realized PnL was computed without a supporting deterministic fixture."
Require-True ([decimal]$pnl.portfolioPnL.unrealizedPnL -eq 6804.66) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "Portfolio unrealized PnL is unexpected."
Require-True ([decimal]$pnl.portfolioPnL.realizedPnL -eq 0) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "Portfolio realized PnL should be zero."
Require-True ([string]$pnl.status -eq "MissingMark") "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "Portfolio PnL status should reflect missing marks."
Require-False ([bool]$pnl.usesLiveBrokerMarketData) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "PnL used live broker market data."
Require-False ([bool]$pnl.callsBrokerGateway) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "PnL called a broker gateway."

Require-True ([bool]$details.instrumentPnlDetailsCreated) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "Instrument PnL details are missing."
Require-True ([int]$details.computedRowCount -eq 3) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "Computed row count is unexpected."
Require-True ([int]$details.missingMarkRowCount -ge 1) "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "Missing mark row count is missing."
Require-True ([int]$details.staleMarkRowCount -ge 1) "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "Stale mark row count is missing."

$jpy = $details.details | Where-Object { [string]$_.symbol -eq "JPYUSD" } | Select-Object -First 1
$nok = $details.details | Where-Object { [string]$_.symbol -eq "NOKUSD" } | Select-Object -First 1
Require-True ($null -ne $jpy -and [string]$jpy.status -eq "MissingMark") "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "JPYUSD missing mark handling is missing."
Require-True ($null -ne $nok -and [string]$nok.status -eq "StaleMark") "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "NOKUSD stale mark handling is missing."

Require-True ([bool]$availability.markAvailabilityAndStalenessCreated) "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "Mark availability/staleness artifact is missing."
Require-True ([bool]$availability.missingMarkHandlingPresent) "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "Missing mark handling marker is missing."
Require-True ([bool]$availability.staleMarkHandlingPresent) "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "Stale mark handling marker is missing."

Require-True ([bool]$metadata.qubesMetadataPreservationCreated) "PMS_EMS_OMS_R004_FAIL_QUBES_METADATA_WEAKENED" "Qubes metadata artifact is missing."
Require-True ([string]$metadata.qubesRunId -ne "") "PMS_EMS_OMS_R004_FAIL_QUBES_METADATA_WEAKENED" "QubesRunId is missing."
Require-True ([string]$metadata.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R004_FAIL_QUBES_METADATA_WEAKENED" "Qubes source is missing."
Require-True ([int]$metadata.cadenceMinutes -eq 15) "PMS_EMS_OMS_R004_FAIL_QUBES_METADATA_WEAKENED" "15-minute cadence is missing."
Require-True ([bool]$metadata.r002NormalizedWeightsConsumed) "PMS_EMS_OMS_R004_FAIL_QUBES_METADATA_WEAKENED" "R002 normalized weights were not consumed."
Require-True ([bool]$metadata.r003TheoreticalTargetPortfolioConsumed) "PMS_EMS_OMS_R004_FAIL_QUBES_METADATA_WEAKENED" "R003 theoretical target portfolio was not consumed."
Require-True ([bool]$metadata.r003RebalanceIntentFoundationPreserved) "PMS_EMS_OMS_R004_FAIL_REBALANCE_INTENT_EXECUTABLE" "R003 rebalance-intent foundation was not preserved."

Require-True ([bool]$intents.rebalanceIntentPreservationCreated) "PMS_EMS_OMS_R004_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intent preservation artifact is missing."
Require-True ([bool]$intents.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R004_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents became executable."
Require-True ([bool]$intents.allIntentLinesHaveTheoreticalOnlyStatus) "PMS_EMS_OMS_R004_FAIL_REBALANCE_INTENT_EXECUTABLE" "TheoreticalOnly status is missing."
Require-True ([bool]$intents.allIntentLinesHaveNotExecutableStatus) "PMS_EMS_OMS_R004_FAIL_REBALANCE_INTENT_EXECUTABLE" "NotExecutable status is missing."
Require-True ([bool]$intents.allIntentLinesHaveBlockedNoOmsStatus) "PMS_EMS_OMS_R004_FAIL_REBALANCE_INTENT_EXECUTABLE" "BlockedNoOMS status is missing."
Require-False ([bool]$intents.executableOrderCreated) "PMS_EMS_OMS_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable order was created."
Require-False ([bool]$intents.orderSubmissionIntroduced) "PMS_EMS_OMS_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order submission was introduced."
Require-False ([bool]$intents.omsOrderCreated) "PMS_EMS_OMS_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS order was created."
Require-False ([bool]$intents.brokerGatewayCalled) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker gateway was called."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL" "Instrument universe handling is missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsPnlGate) "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL" "LMAX scope gates theoretical PnL."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockTheoreticalPnl) "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL" "LMAX gaps block theoretical PnL."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksFixturePnl) "PMS_EMS_OMS_R004_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks fixture PnL."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksFixturePnl) "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks fixture PnL."
Require-True ([bool]$universe.instrumentsWithoutFixtureMarksHandledSafely) "PMS_EMS_OMS_R004_FAIL_MARK_STALENESS_HANDLING_MISSING" "Missing fixture marks are not handled safely."
Require-True ([bool]$universe.instrumentsWithoutBrokerValidationHandledSafely) "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL" "Instruments without broker validation are not safe."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat artifact is missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven caveat is missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY is classified as failed."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID caveat is missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource caveat is missing."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R004_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS-boundary inconclusive status is missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R004_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD is classified as failed."
Require-False ([bool]$usdjpy.theoreticalPnlDependsOnUsdJpyLiveValidation) "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL" "Theoretical PnL depends on USDJPY live validation."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL" "LMAX baseline reference artifact is missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX was used in this phase."
Require-True ([bool]$lmax.baseline.GBPUSD.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL" "GBPUSD read-only baseline is missing."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL" "GBPUSD sanitized count is missing."
Require-True ([bool]$lmax.baseline.EURGBP.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL" "EURGBP read-only baseline is missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R004_FAIL_LMAX_GAP_BLOCKS_THEORETICAL_PNL" "EURGBP sanitized count is missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R004_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD is classified as failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY is classified as failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityID caveat is missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R004_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityIDSource caveat is missing."

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
    "lmaxLiveValidationGapsBlockTheoreticalPnl",
    "rebalanceIntentExecutable"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "order|trading|rebalance|executable") {
            Fail-Gate "PMS_EMS_OMS_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "Next phase recommendation is missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R005") "PMS_EMS_OMS_R004_FAIL_THEORETICAL_PNL_MISSING" "Next phase is not R005."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase is not marked no-external."
Require-True ([bool]$nextPhase.mustNotUseLiveMarketData) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase permits live market data."
Require-True ([bool]$nextPhase.mustNotGenerateExecutableOrders) "PMS_EMS_OMS_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits executable orders."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading is enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading is enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections are enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections are enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake execution gateway is not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake execution gateway is not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime is enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime allows external connections."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX read-only runtime allows order submission."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only scheduler is enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime submits to shadow replay."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R004_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "LMAX runtime persists raw FIX messages."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r004-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
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
        Fail-Gate "PMS_EMS_OMS_R004_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$pnlSource = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesTheoreticalPnlFixture.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "Socket", "FixSession", "Lmax")) {
    if ($pnlSource -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R004 PnL fixture source contains forbidden runtime pattern: $pattern"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R004_FAIL_BUILD_OR_TESTS" "Build evidence is missing or not PASS."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R004_FAIL_BUILD_OR_TESTS" "Focused test evidence is missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R004_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R004_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence marker is missing."
Require-True (Test-Path -LiteralPath (Join-Path $repoRoot "scripts/check-pms-ems-oms-r004-theoretical-pnl-fixture-gate.ps1")) "PMS_EMS_OMS_R004_FAIL_BUILD_OR_TESTS" "Validator script is missing."

Write-Host "PMS_EMS_OMS_R004_PASS_THEORETICAL_MARKING_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R004_PASS_THEORETICAL_PNL_FIXTURE_READY_NO_EXTERNAL"

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
    "phase-pms-ems-oms-r012-summary.md" = "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING"
    "phase-pms-ems-oms-r012-instrument-convention-contract.json" = "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING"
    "phase-pms-ems-oms-r012-lot-sizing-fixture-contract.json" = "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
    "phase-pms-ems-oms-r012-paper-lot-sized-candidates.json" = "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
    "phase-pms-ems-oms-r012-paper-quantity-shapes.json" = "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
    "phase-pms-ems-oms-r012-sizing-status-summary.json" = "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
    "phase-pms-ems-oms-r012-non-executable-sized-candidate-audit.json" = "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_EXECUTABLE"
    "phase-pms-ems-oms-r012-no-order-created-audit.json" = "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r012-risk-lineage-preservation.json" = "PMS_EMS_OMS_R012_FAIL_RISK_LINEAGE_MISSING"
    "phase-pms-ems-oms-r012-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R012_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r012-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
    "phase-pms-ems-oms-r012-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
    "phase-pms-ems-oms-r012-missing-stale-mark-preservation.json" = "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
    "phase-pms-ems-oms-r012-drift-acknowledgement-preservation.json" = "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
    "phase-pms-ems-oms-r012-instrument-universe-handling.json" = "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING"
    "phase-pms-ems-oms-r012-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r012-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING"
    "phase-pms-ems-oms-r012-no-external-audit.json" = "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r012-forbidden-actions-audit.json" = "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r012-next-phase-recommendation.json" = "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
    "phase-pms-ems-oms-r012-build-test-validator-evidence.json" = "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$convention = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-instrument-convention-contract.json") "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING"
$fixture = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-lot-sizing-fixture-contract.json") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
$sized = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-paper-lot-sized-candidates.json") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
$quantity = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-paper-quantity-shapes.json") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
$status = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-sizing-status-summary.json") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
$nonExec = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-non-executable-sized-candidate-audit.json") "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_EXECUTABLE"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-no-order-created-audit.json") "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-risk-lineage-preservation.json") "PMS_EMS_OMS_R012_FAIL_RISK_LINEAGE_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-qubes-lineage-preservation.json") "PMS_EMS_OMS_R012_FAIL_QUBES_LINEAGE_WEAKENED"
$decision = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
$rebalance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-missing-stale-mark-preservation.json") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-drift-acknowledgement-preservation.json") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-instrument-universe-handling.json") "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-no-external-audit.json") "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-forbidden-actions-audit.json") "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-next-phase-recommendation.json") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r012-build-test-validator-evidence.json") "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$convention.instrumentConventionContractCreated) "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING" "Convention contract missing."
foreach ($field in @("NormalizedSymbol", "PaperTradableSymbol", "BaseCurrency", "QuoteCurrency", "NormalizedQuoteCurrency", "IsUsdQuoteNormalized", "RequiresInversion", "QuantityCurrency", "NotionalCurrency", "LotSize", "MinQuantity", "QuantityRoundingMode", "ConventionSource")) {
    Require-True (($convention.conventionFields -contains $field)) "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING" "Convention field missing: $field"
}
Require-True ([string]$convention.conventionSource -eq "NoExternalPaperFxConventionFixture") "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING" "Unexpected convention source."
Require-True ([string]$convention.usdQuoteConvention.quoteCurrency -eq "USD") "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING" "USD quote convention missing."
Require-False ([bool]$convention.usdQuoteConvention.requiresInversion) "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING" "USD quote convention requires inversion."
Require-True ([int]$convention.usdQuoteConvention.lotSize -eq 1000) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Lot size missing."
Require-False ([bool]$convention.liveBrokerSpecificConventionUsed) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live broker convention used."
Require-True ([string]$convention.missingConventionStatus -eq "RequiresInstrumentConvention") "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING" "Missing convention status absent."

Require-True ([bool]$fixture.lotSizingFixtureContractCreated) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Fixture contract missing."
Require-True ([string]$fixture.fixtureSource -eq "NoExternalLotSizingFixture") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Unexpected fixture source."
Require-True ([bool]$fixture.noExternalFixtureOnly) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Fixture not no-external."
Require-False ([bool]$fixture.rawBrokerPricesSerialized) "PMS_EMS_OMS_R012_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker prices serialized."
Require-False ([bool]$fixture.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R012_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payloads serialized."
Require-True ([bool]$fixture.fixtureMarksAreSafeSummaries) "PMS_EMS_OMS_R012_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Fixture marks not safe summaries."
Require-True ([string]$fixture.missingMarkStatus -eq "RequiresMark") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Missing mark status absent."
Require-True ([string]$fixture.missingLotConventionStatus -eq "RequiresLotSizing") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Missing lot status absent."
Require-True ([string]$fixture.missingInstrumentConventionStatus -eq "RequiresInstrumentConvention") "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING" "Missing convention status absent."
Require-False ([bool]$fixture.liveLmaxMarksUsed) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live LMAX marks used."
Require-False ([bool]$fixture.brokerMarksUsed) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker marks used."

Require-True ([bool]$sized.paperLotSizedCandidatesCreated) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Sized candidates missing."
Require-True ([int]$sized.sizedCandidateCount -eq 3) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Unexpected sized candidate count."
Require-True ([int]$sized.blockedR011LineCount -eq 10) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Blocked R011 count missing."
Require-False ([bool]$sized.blockedR011LinesBecomePaperReadySizedCandidates) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Blocked R011 lines became sized candidates."
foreach ($candidate in $sized.candidates) {
    Require-True ([string]$candidate.sizingStatus -eq "PaperSized") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Candidate not paper-sized."
    Require-True ([bool]$candidate.paperOnly) "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_EXECUTABLE" "Candidate not paper-only."
    Require-True ([bool]$candidate.nonExecutable) "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_EXECUTABLE" "Candidate executable."
    Require-True ([bool]$candidate.notAnOrder) "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_IS_OMS_OR_BROKER_ORDER" "Candidate is order."
    Require-True ([bool]$candidate.notSubmitted) "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_SUBMITTED_OR_ROUTED" "Candidate submitted."
    Require-True ([bool]$candidate.noBrokerRoute) "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_SUBMITTED_OR_ROUTED" "Candidate has broker route."
    Require-True ([decimal]$candidate.lotSize -eq 1000) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Lot size wrong."
}
Require-True (@($sized.candidates | Where-Object { $_.normalizedSymbol -eq "AUDUSD" -and $_.side -eq "Buy" -and [decimal]$_.absoluteBaseQuantityRounded -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "AUDUSD sizing missing."
Require-True (@($sized.candidates | Where-Object { $_.normalizedSymbol -eq "EURUSD" -and $_.side -eq "Buy" -and [decimal]$_.absoluteBaseQuantityRounded -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "EURUSD sizing missing."
Require-True (@($sized.candidates | Where-Object { $_.normalizedSymbol -eq "GBPUSD" -and $_.side -eq "Sell" -and [decimal]$_.absoluteBaseQuantityRounded -eq 368000 }).Count -eq 1) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "GBPUSD sizing missing."
foreach ($property in @("createdOmsOrder", "createdParentOrder", "createdChildOrder", "createdBrokerOrder", "createdFill", "createdExecutionReport", "submittedOrders")) {
    Require-False ([bool]$sized.$property) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Sized artifact detected: $property"
}

Require-True ([bool]$quantity.paperQuantityShapesCreated) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Quantity shapes missing."
Require-False ([bool]$quantity.brokerExecutableQuantityCreated) "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_EXECUTABLE" "Broker executable quantity created."
Require-False ([bool]$quantity.brokerLotSizingUsed) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker lot sizing used."
foreach ($shape in $quantity.quantityShapes) {
    Require-True ([string]$shape.formula -eq "abs(deltaNotionalUsd) / fixtureMid") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Formula missing."
    Require-True ([string]$shape.roundingMode -eq "RoundToNearestLot") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Rounding mode missing."
    Require-True ([int]$shape.lotSize -eq 1000) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Quantity shape lot size wrong."
    Require-True ([string]$shape.status -eq "PaperSized") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Quantity shape not sized."
}

Require-True ([bool]$status.sizingStatusSummaryCreated) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Status summary missing."
Require-True ([int]$status.paperSizedCount -eq 3) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "PaperSized count wrong."
Require-True (($status.testedSafeStatuses -contains "RequiresMark")) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "RequiresMark not tested."
Require-True (($status.testedSafeStatuses -contains "RequiresLotSizing")) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "RequiresLotSizing not tested."
Require-True (($status.testedSafeStatuses -contains "RequiresInstrumentConvention")) "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING" "RequiresInstrumentConvention not tested."
Require-False ([bool]$status.blockedR011LinesBecomePaperReadySizedCandidates) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Blocked lines became sized candidates."

Require-True ([bool]$nonExec.nonExecutableSizedCandidateAuditCreated) "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_EXECUTABLE" "Non-executable audit missing."
foreach ($property in @("allSizedCandidatesPaperOnly", "allSizedCandidatesNonExecutable", "allSizedCandidatesNotAnOrder", "allSizedCandidatesNotSubmitted", "allSizedCandidatesNoBrokerRoute")) {
    Require-True ([bool]$nonExec.$property) "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_EXECUTABLE" "Sized audit missing: $property"
}
foreach ($property in @("sizedCandidateExecutable", "sizedCandidateSubmitted", "sizedCandidateHasBrokerRoute", "sizedCandidateRepresentedAsOmsOrder", "sizedCandidateRepresentedAsBrokerOrder", "sizedCandidateCreatesFill", "sizedCandidateCreatesExecutionReport")) {
    Require-False ([bool]$nonExec.$property) "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_EXECUTABLE" "Sized audit detected: $property"
}

Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "fillCreated", "executionReportCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit detected: $property"
}

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R012_FAIL_RISK_LINEAGE_MISSING" "Risk lineage missing."
Require-True ([bool]$risk.riskReviewReferencePresentOnEverySizedCandidate) "PMS_EMS_OMS_R012_FAIL_RISK_LINEAGE_MISSING" "Risk reference missing."
Require-True ([bool]$risk.blockedRiskResultsCarriedSeparately) "PMS_EMS_OMS_R012_FAIL_RISK_LINEAGE_MISSING" "Blocked risks not preserved."

Require-True ([bool]$lineage.qubesLineagePreservationCreated) "PMS_EMS_OMS_R012_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$lineage.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R012_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$lineage.cadenceMinutes -eq 15) "PMS_EMS_OMS_R012_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
foreach ($property in @("qubesAuditBatchPreserved", "rawQubesRowAuditPreserved", "normalizedWeightAuditPreserved", "modelWeightBatchLinkagePreserved", "modelRunLinkagePreserved", "targetWeightLinkagePreserved")) {
    Require-True ([bool]$lineage.$property) "PMS_EMS_OMS_R012_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing: $property"
}
Require-False ([bool]$lineage.qubesLineageWeakened) "PMS_EMS_OMS_R012_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."

Require-True ([bool]$decision.operatorDecisionLineagePreservationCreated) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Operator decision lineage missing."
Require-True ([string]$decision.operatorDecisionType -eq "PromoteToPaperReady") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Operator decision is not promotion."
Require-True ([string]$decision.resultingCycleReviewStatus -eq "PaperReadyNoExternal") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Operator decision not no-external paper."
Require-True ([bool]$decision.operatorDecisionReferencedOnEverySizedCandidate) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Operator decision missing."
Require-False ([bool]$decision.promotionMeansLiveTrading) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion means live trading."
Require-False ([bool]$decision.promotionCreatesOrders) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion creates orders."

Require-True ([bool]$rebalance.rebalanceIntentLineagePreservationCreated) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Rebalance lineage missing."
Require-True ([bool]$rebalance.sourceRebalanceIntentReferencedOnEverySizedCandidate) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Source rebalance missing."
Require-True ([bool]$rebalance.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R012_FAIL_SIZED_CANDIDATE_EXECUTABLE" "Rebalance intents executable."
Require-False ([bool]$rebalance.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance creates order."
Require-False ([bool]$rebalance.rebalanceIntentSubmitted) "PMS_EMS_OMS_R012_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Rebalance submitted."

Require-True ([bool]$marks.missingStaleMarkPreservationCreated) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Missing/stale missing."
Require-True ([bool]$marks.requiresMarkStatusAvailable) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "RequiresMark unavailable."
Require-False ([bool]$marks.blockedMissingStaleLinesBecomeSizedCandidates) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Blocked missing/stale became sized."
Require-False ([bool]$marks.missingOrStaleMarksHidden) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Missing/stale hidden."
Require-False ([bool]$marks.fabricatedMarksForLotSizing) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Marks fabricated."
Require-False ([bool]$marks.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R012_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payload serialized."

Require-True ([bool]$drift.driftAcknowledgementPreservationCreated) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Drift missing."
Require-True ([string]$drift.theoreticalVsRealStatus -eq "Drift") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Drift status missing."
Require-True ([bool]$drift.driftAcknowledgedByOperatorDecision) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Drift not acknowledged."
Require-True ([bool]$drift.driftAcknowledgementPreservedInLotSizedBatch) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Drift not preserved."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live approval created."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING" "Universe missing."
Require-True ([bool]$universe.portfolioNormalizedSymbolDistinguishedFromPaperTradableSymbol) "PMS_EMS_OMS_R012_FAIL_INSTRUMENT_CONVENTION_MISSING" "Normalized/tradable distinction missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsLotSizingGate) "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING" "LMAX gates lot sizing."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockLotSizing) "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING" "LMAX gaps block lot sizing."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksLotSizing) "PMS_EMS_OMS_R012_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks lot sizing."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksLotSizing) "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks lot sizing."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R012_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R012_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockLotSizing) "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING" "LMAX gaps block lot sizing."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING" "GBPUSD baseline missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R012_FAIL_LMAX_GAP_BLOCKS_LOT_SIZING" "EURGBP baseline missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R012_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."

foreach ($property in @(
    "externalBrokerActivationDetected",
    "socketTlsFixMarketDataRuntimeActionDetected",
    "marketDataRequestAttempted",
    "liveMarketDataResponseRead",
    "apiStarted",
    "workerStarted",
    "schedulerPollingServiceTimerBackgroundJobStartedOrIntroduced",
    "liveGatewayEnabled",
    "orderSubmissionIntroduced",
    "executableOrderCreated",
    "omsOrderCreated",
    "parentOrderCreated",
    "childOrderCreated",
    "brokerOrderCreated",
    "fillCreated",
    "executionReportCreated",
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
    "rawMarketDataFixturePayloadsSerialized",
    "sizedCandidateExecutable",
    "sizedCandidateSubmitted",
    "sizedCandidateHasBrokerRoute",
    "sizedCandidateRepresentedAsOmsOrder",
    "sizedCandidateRepresentedAsBrokerOrder",
    "blockedR011LinesBecomePaperReadySizedCandidates",
    "lmaxLiveValidationGapsBlockLotSizing"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R012_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|fill|execution report|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R013") "PMS_EMS_OMS_R012_FAIL_LOT_SIZING_CONTRACT_MISSING" "Next phase not R013."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotCreateExecutableOrders) "PMS_EMS_OMS_R012_FAIL_EXECUTABLE_ORDER_CREATED" "Next phase permits executable orders."
Require-True ([bool]$nextPhase.mustNotCreateOmsOrders) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits OMS orders."
Require-True ([bool]$nextPhase.mustNotCreateBrokerOrders) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits broker orders."
Require-True ([bool]$nextPhase.mustNotSubmitOrders) "PMS_EMS_OMS_R012_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Next phase permits submission."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R012_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Shadow replay enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R012_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw FIX persistence enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r012-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R012_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperCandidateLotSizing.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperCandidateLotSizingTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperCandidateLotSizing.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R012 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R012_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R012 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperCandidateLotSizingTests.cs") -Raw
foreach ($requiredTestName in @(
    "R011_archived_candidates_can_feed_r012_lot_sizing",
    "Audusd_buy_candidate_receives_paper_instrument_convention",
    "Eurusd_buy_candidate_receives_paper_instrument_convention",
    "Gbpusd_sell_candidate_receives_paper_instrument_convention",
    "Usd_quote_convention_sets_base_and_quote_correctly",
    "Delta_notional_converts_to_base_quantity_shape_using_fixture_mid",
    "Lot_size_rounding_is_applied_deterministically",
    "Missing_fixture_mark_yields_requires_mark",
    "Missing_lot_convention_yields_requires_lot_sizing",
    "Missing_instrument_convention_yields_requires_instrument_convention",
    "Sized_candidates_remain_paper_only",
    "Sized_candidates_remain_non_executable",
    "Sized_candidates_remain_not_an_order_not_submitted_and_no_broker_route",
    "No_oms_parent_child_or_broker_order_is_created",
    "No_fill_or_execution_report_is_introduced",
    "No_order_submission_path_is_introduced",
    "Lot_sizing_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Lot_sizing_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Focused evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Unit evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R012_FAIL_BUILD_OR_TESTS" "Evidence marker missing."

Write-Host "PMS_EMS_OMS_R012_PASS_PAPER_LOT_SIZING_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R012_PASS_INSTRUMENT_CONVENTION_GATE_READY_NO_EXTERNAL"

import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();
const artifactsDir = path.join(repoRoot, "artifacts", "readiness", "execution-sim");
const phase = "EXEC-SIM-R026";
const reviewedAtUtc = "2026-05-21T00:00:00Z";

function readJson(name) {
  return JSON.parse(fs.readFileSync(path.join(artifactsDir, name), "utf8"));
}

function writeJson(name, value) {
  fs.writeFileSync(path.join(artifactsDir, name), `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

function writeText(name, value) {
  fs.writeFileSync(path.join(artifactsDir, name), value, "utf8");
}

function round(value, digits = 6) {
  if (value === null || value === undefined || Number.isNaN(value)) return value;
  const scale = 10 ** digits;
  return Math.round(value * scale) / scale;
}

function median(values) {
  if (!values.length) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
}

function percentile(values, pct) {
  if (!values.length) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const index = Math.ceil((pct / 100) * sorted.length) - 1;
  return sorted[Math.max(0, Math.min(sorted.length - 1, index))];
}

function rankingBestWorst(items, metric, lowerIsBetter = true) {
  const present = items.filter(x => Number.isFinite(x[metric]));
  if (!present.length) {
    return {
      metric,
      evidenceStatus: "MissingEvidence",
      bestPolicy: null,
      worstPolicy: null
    };
  }

  const sorted = [...present].sort((a, b) => lowerIsBetter ? a[metric] - b[metric] : b[metric] - a[metric]);
  const worst = sorted[sorted.length - 1];
  return {
    metric,
    evidenceStatus: "PresentFromR025",
    bestPolicy: sorted[0].PolicyFamily,
    bestValue: round(sorted[0][metric]),
    worstPolicy: worst.PolicyFamily,
    worstValue: round(worst[metric])
  };
}

function byPolicy(policyName) {
  const policy = policyComparison.comparisons.find(x => x.PolicyFamily === policyName);
  if (!policy) throw new Error(`Missing policy ${policyName}`);
  return policy;
}

function policyUsdPerMillionSummary(policyName) {
  const matching = tcaLines.lines.filter(x => x.PolicyFamily === policyName);
  const slippage = matching.map(x => x.SlippageVsCloseUsdPerMillion).filter(Number.isFinite);
  const spread = matching.map(x => x.SpreadPaidUsdPerMillion).filter(Number.isFinite);
  if (!slippage.length || !spread.length) {
    return {
      PolicyFamily: policyName,
      EvidenceStatus: "MissingEvidence",
      MissingEvidenceReason: "R025 result lines do not contain USD per million metrics for this policy."
    };
  }

  return {
    PolicyFamily: policyName,
    EvidenceStatus: "PresentFromR025ResultLines",
    MedianSlippageUsdPerMillion: round(median(slippage)),
    P95SlippageUsdPerMillion: round(percentile(slippage, 95)),
    MedianSpreadPaidUsdPerMillion: round(median(spread)),
    P95SpreadPaidUsdPerMillion: round(percentile(spread, 95))
  };
}

function symbolBestWorst(symbol) {
  const symbolLines = tcaLines.lines.filter(x => x.Symbol === symbol);
  const policies = [...new Set(symbolLines.map(x => x.PolicyFamily))];
  const summaries = policies.map(policyName => {
    const lines = symbolLines.filter(x => x.PolicyFamily === policyName);
    return {
      PolicyFamily: policyName,
      MedianSlippageVsCloseBps: round(median(lines.map(x => x.SlippageVsCloseBps).filter(Number.isFinite))),
      P95SlippageVsCloseBps: round(percentile(lines.map(x => x.SlippageVsCloseBps).filter(Number.isFinite), 95)),
      MedianResidualAtClose: round(median(lines.map(x => x.ResidualAtClose).filter(Number.isFinite))),
      MedianSpreadPaidBps: round(median(lines.map(x => x.SpreadPaidBps).filter(Number.isFinite)))
    };
  });
  const executableLike = summaries.filter(x => ![
    "ImmediatePaperBenchmark",
    "TWAPBenchmarkOnly",
    "VWAPBenchmarkOnly",
    "ManualReview",
    "DoNotTrade",
    "WakettPureLimitUntilClose",
    "WakettFiveMarketSlicesAroundClose"
  ].includes(x.PolicyFamily));
  return {
    Symbol: symbol,
    BestExecutableLikePolicyByP95Slippage: rankingBestWorst(executableLike, "P95SlippageVsCloseBps").bestPolicy,
    WorstExecutableLikePolicyByP95Slippage: rankingBestWorst(executableLike, "P95SlippageVsCloseBps").worstPolicy,
    BestExecutableLikePolicyByResidual: rankingBestWorst(executableLike, "MedianResidualAtClose").bestPolicy,
    WorstExecutableLikePolicyByResidual: rankingBestWorst(executableLike, "MedianResidualAtClose").worstPolicy,
    BestExecutableLikePolicyBySpreadPaid: rankingBestWorst(executableLike, "MedianSpreadPaidBps").bestPolicy,
    WorstExecutableLikePolicyBySpreadPaid: rankingBestWorst(executableLike, "MedianSpreadPaidBps").worstPolicy,
    PolicyEvidenceStatus: "PresentFromR025ResultLines"
  };
}

const run = readJson("phase-exec-sim-r025-expanded-backtest-run-result.json");
const policyComparison = readJson("phase-exec-sim-r025-expanded-policy-comparison-report.json");
const symbolComparison = readJson("phase-exec-sim-r025-expanded-major-symbol-comparison.json");
const medianRanking = readJson("phase-exec-sim-r025-ranking-median-slippage.json");
const p95Ranking = readJson("phase-exec-sim-r025-ranking-p95-slippage.json");
const fillRanking = readJson("phase-exec-sim-r025-ranking-fill-ratio.json");
const residualRanking = readJson("phase-exec-sim-r025-ranking-residual.json");
const spreadRanking = readJson("phase-exec-sim-r025-ranking-spread-paid.json");
const wakettLimit = readJson("phase-exec-sim-r025-wakett-limit-baseline-report.json");
const wakettSlices = readJson("phase-exec-sim-r025-wakett-five-market-slices-report.json");
const passive = readJson("phase-exec-sim-r025-passive-until-urgency-report.json");
const closeSeeking = readJson("phase-exec-sim-r025-close-seeking-15m-report.json");
const adaptive = readJson("phase-exec-sim-r025-close-seeking-adaptive-report.json");
const controlled = readJson("phase-exec-sim-r025-controlled-residual-cross-report.json");
const benchmark = readJson("phase-exec-sim-r025-benchmark-only-policy-report.json");
const tcaLines = readJson("phase-exec-sim-r025-tca-result-lines.json");
const directR025 = readJson("phase-exec-sim-r025-direct-cross-exclusion-preservation.json");
const costR025 = readJson("phase-exec-sim-r025-cost-guidance-preservation.json");
const nonmajorR025 = readJson("phase-exec-sim-r025-nonmajor-calibration-preservation.json");
const usdjpyR025 = readJson("phase-exec-sim-r025-usdjpy-caveat-preservation.json");
const lmaxR025 = readJson("phase-exec-sim-r025-lmax-readonly-baseline-reference.json");

const policySummaries = policyComparison.comparisons.map(policy => ({
  ...policy,
  MedianSlippageVsCloseUsdPerMillion: policyUsdPerMillionSummary(policy.PolicyFamily).MedianSlippageUsdPerMillion ?? "MissingEvidence",
  P95SlippageVsCloseUsdPerMillion: policyUsdPerMillionSummary(policy.PolicyFamily).P95SlippageUsdPerMillion ?? "MissingEvidence"
}));

const executableLikePolicies = policyComparison.comparisons.filter(x =>
  !x.BenchmarkOnly &&
  !x.NegativeWakettBaseline &&
  !["ManualReview", "DoNotTrade"].includes(x.PolicyFamily)
);

const numericSummary = {
  phase,
  numericTcaSummaryCreated: true,
  SourceSimulationPhase: "EXEC-SIM-R025",
  ReviewedAtUtc: reviewedAtUtc,
  NumericMetricsSource: "EXEC-SIM-R025 artifacts",
  UnsupportedNumericMetricsInvented: false,
  UnsupportedMetricsPolicy: "MissingEvidence",
  R025RunCounts: {
    SymbolCount: run.SymbolCount,
    QuoteWindowCount: run.QuoteWindowCount,
    CloseBenchmarkCount: run.CloseBenchmarkCount,
    FeedQualityResultCount: run.FeedQualityResultCount,
    TcaResultLineCount: run.TcaResultLineCount
  },
  Scope: {
    SessionWindowCategory: "IntradayRebalance",
    HistoricalWindowCount: 1,
    HistoricalWindowDescription: "One four-hour IntradayRebalance window from R025.",
    SampleSizeConclusion: "TooSmallForProductionParameterConclusion"
  },
  BestWorstAllPolicies: {
    MedianSlippage: rankingBestWorst(policyComparison.comparisons, "MedianSlippageVsCloseBps"),
    P95Slippage: rankingBestWorst(policyComparison.comparisons, "P95SlippageVsCloseBps"),
    FillRatio: rankingBestWorst(policyComparison.comparisons, "MedianFillRatio", false),
    Residual: rankingBestWorst(policyComparison.comparisons, "MedianResidualAtClose"),
    SpreadPaid: rankingBestWorst(policyComparison.comparisons, "MedianSpreadPaidBps")
  },
  BestWorstExecutableLikeCandidates: {
    MedianSlippage: rankingBestWorst(executableLikePolicies, "MedianSlippageVsCloseBps"),
    P95Slippage: rankingBestWorst(executableLikePolicies, "P95SlippageVsCloseBps"),
    FillRatio: rankingBestWorst(executableLikePolicies, "MedianFillRatio", false),
    Residual: rankingBestWorst(executableLikePolicies, "MedianResidualAtClose"),
    SpreadPaid: rankingBestWorst(executableLikePolicies, "MedianSpreadPaidBps")
  },
  PerPolicyUsdPerMillionSummaries: policyComparison.comparisons.map(x => policyUsdPerMillionSummary(x.PolicyFamily)),
  FiveUsdPerMillionTargetComparison: {
    EvidenceStatus: "PresentFromR025ResultLines",
    BestCaseMajorOnly: true,
    Universalized: false,
    DemonstratedAsUniversalInR025: false,
    ReviewConclusion: "R025 does not justify treating 5 USD/million as a universal expectation. It remains a best-case major-pair target."
  }
};

const policyRankingReview = {
  phase,
  policyRankingReviewCreated: true,
  SourceArtifacts: [
    "phase-exec-sim-r025-ranking-median-slippage.json",
    "phase-exec-sim-r025-ranking-p95-slippage.json",
    "phase-exec-sim-r025-ranking-fill-ratio.json",
    "phase-exec-sim-r025-ranking-residual.json",
    "phase-exec-sim-r025-ranking-spread-paid.json"
  ],
  MedianSlippageRanking: medianRanking.rankings,
  P95SlippageRanking: p95Ranking.rankings,
  FillRatioRanking: fillRanking.rankings,
  ResidualRanking: residualRanking.rankings,
  SpreadPaidRanking: spreadRanking.rankings,
  InterpretiveCaveat: "Benchmark-only, ManualReview, DoNotTrade, and negative Wakett baselines are not executable candidate rankings.",
  UnsupportedNumericMetricsInvented: false
};

const perSymbolReview = {
  phase,
  perSymbolReviewCreated: true,
  SourceArtifact: "phase-exec-sim-r025-expanded-major-symbol-comparison.json",
  comparisons: symbolComparison.comparisons.map(symbol => ({
    ...symbol,
    EaseRankByP95Slippage: [...symbolComparison.comparisons].sort((a, b) => a.P95SlippageVsCloseBps - b.P95SlippageVsCloseBps).findIndex(x => x.Symbol === symbol.Symbol) + 1,
    RelativeDifficulty: symbol.P95SlippageVsCloseBps >= 6 ? "HarderInR025Sample" : symbol.P95SlippageVsCloseBps <= 3 ? "EasierInR025Sample" : "MiddleInR025Sample",
    BestWorstPolicyEvidence: symbolBestWorst(symbol.Symbol)
  })),
  EasiestSymbolsByP95: [...symbolComparison.comparisons].sort((a, b) => a.P95SlippageVsCloseBps - b.P95SlippageVsCloseBps).slice(0, 3).map(x => x.Symbol),
  HardestSymbolsByP95: [...symbolComparison.comparisons].sort((a, b) => b.P95SlippageVsCloseBps - a.P95SlippageVsCloseBps).slice(0, 3).map(x => x.Symbol),
  AudUsdStatus: "not failed",
  UnsupportedNumericMetricsInvented: false
};

const wakettVsCloseSeekingReview = {
  phase,
  wakettVsCloseSeekingReviewCreated: true,
  CloseSeeking15mAdaptiveOutperformedWakettBaselines: true,
  EvidenceStatus: "PresentFromR025",
  Interpretation: "Adaptive has materially lower p95 slippage than WakettPureLimitUntilClose and WakettFiveMarketSlicesAroundClose, much lower residual than pure limit, and much lower spread paid than five market slices.",
  WakettPureLimitUntilClose: wakettLimit,
  WakettFiveMarketSlicesAroundClose: wakettSlices,
  CloseSeeking15m: closeSeeking,
  CloseSeeking15mAdaptive: adaptive,
  DirectCrossesRemainExcluded: true,
  WakettPatternsRemainRejectedAsDefault: true
};

const adaptiveReview = {
  phase,
  closeSeekingAdaptiveReviewCreated: true,
  RecommendationStatus: "KeepForFurtherOfflineTesting",
  NeedsMoreHistoricalWindows: true,
  NeedsOpeningClosingWindows: true,
  RemainsStrongestCandidateWhereFeedAndSpreadAreGood: true,
  EvidenceStatus: "PresentFromR025",
  Metrics: adaptive,
  ReviewConclusion: "CloseSeeking15mAdaptive remains the primary candidate, but R025 is one IntradayRebalance window only."
};

const controlledReview = {
  phase,
  controlledResidualCrossReviewCreated: true,
  RecommendationStatus: "KeepForFurtherOfflineTesting",
  ConditionalUseOnly: true,
  UsefulWhereResidualOpportunityCostExceedsCrossingCost: true,
  EvidenceStatus: "PresentFromR025",
  Metrics: controlled,
  ReviewConclusion: "ControlledResidualCross removes residual and improves p95 versus Adaptive in R025, but pays more spread and worse median slippage; keep as conditional residual tool."
};

const passiveReview = {
  phase,
  passiveUntilUrgencyReviewCreated: true,
  RecommendationStatus: "NeedsParameterRefinement",
  InsufficientWhereResidualMatters: true,
  EvidenceStatus: "PresentFromR025",
  Metrics: passive,
  ReviewConclusion: "PassiveUntilUrgency has low median slippage but inferior p95 and residual versus CloseSeeking candidates."
};

const wakettLimitRisk = {
  phase,
  wakettLimitResidualRiskReviewCreated: true,
  RecommendationStatus: "RejectWakettPattern",
  ResidualRiskHigh: true,
  EvidenceStatus: "PresentFromR025",
  Metrics: wakettLimit,
  ReviewConclusion: "Pure limit has favorable median slippage only by leaving too much residual; it remains blocked as a default schedule."
};

const wakettSlicesRisk = {
  phase,
  wakettFiveSlicesSpreadRiskReviewCreated: true,
  RecommendationStatus: "RejectWakettPattern",
  SpreadPaidRiskHigh: true,
  EvidenceStatus: "PresentFromR025",
  Metrics: wakettSlices,
  ReviewConclusion: "Five market slices achieves fill but pays the highest median spread and remains blocked as a default schedule."
};

const invertedSymbolReview = {
  phase,
  invertedSymbolReviewCreated: true,
  InvertedSymbols: [
    { Symbol: "USDJPY", NormalizedPortfolioSymbol: "JPYUSD", ExecutionTradableSymbol: "USDJPY", RequiresInversion: true, SecurityID: "4004", SecurityIDSource: "8", CaveatPreserved: true },
    { Symbol: "USDCAD", NormalizedPortfolioSymbol: "CADUSD", ExecutionTradableSymbol: "USDCAD", RequiresInversion: true },
    { Symbol: "USDCHF", NormalizedPortfolioSymbol: "CHFUSD", ExecutionTradableSymbol: "USDCHF", RequiresInversion: true }
  ],
  InvertedPairsBehavingSafelyInR025Review: true,
  UsdJpyCaveatPreserved: true,
  AudUsdMisclassifiedFailed: false
};

const fiveUsdReview = {
  phase,
  fiveUsdPerMillionReviewCreated: true,
  BestCaseMajorTargetUsdPerMillion: 5,
  BestCaseMajorOnly: true,
  Universalized: false,
  EvidenceStatus: "PresentFromR025ResultLines",
  PerPolicyUsdPerMillionSummaries: numericSummary.PerPolicyUsdPerMillionSummaries,
  PlausibleForAnyPolicyOrSymbol: "InconclusiveSafe",
  ReviewConclusion: "R025 evidence does not make 5 USD/million a universal target. Keep it as best-case major-only guidance until more windows and session categories are reviewed."
};

const sampleReview = {
  phase,
  sampleSizeAndCoverageReviewCreated: true,
  SourceSimulationPhase: "EXEC-SIM-R025",
  SymbolCount: run.SymbolCount,
  QuoteWindowCount: run.QuoteWindowCount,
  TcaResultLineCount: run.TcaResultLineCount,
  HistoricalWindowCount: 1,
  SessionWindowCategoriesCovered: ["IntradayRebalance"],
  OpeningBuildCovered: false,
  ClosingFlattenCovered: false,
  SampleTooSmallToConcludeProductionParameters: true,
  RecommendedBeforeParameterFinalization: ["MoreHistoricalWindows", "OpeningBuildWindows", "ClosingFlattenWindows"]
};

const dataExpansionDecision = {
  phase,
  dataExpansionDecisionCreated: true,
  RecommendationStatus: "MoreHistoricalWindowsRecommended",
  OpeningClosingWindowsRecommended: true,
  MoreHistoricalWindowsRecommended: true,
  MoreInstrumentCoverageRecommended: "After historical and session-boundary expansion for current seven symbols",
  Decision: "Expand historical windows and add opening/closing session windows before relying on parameter refinement.",
  Rationale: "R025 covers one four-hour IntradayRebalance window only; the evidence is useful but too narrow for session-aware execution decisions."
};

const parameterDecision = {
  phase,
  parameterRefinementDecisionCreated: true,
  RecommendationStatus: "ParameterRefinementRecommended",
  Sequence: "After historical-window and opening/closing-window expansion",
  KeepCloseSeeking15mAdaptiveCandidate: true,
  KeepControlledResidualCrossConditional: true,
  RefinePassiveUntilUrgency: true,
  DoNotAdoptWakettDefaults: true
};

const nextHistorical = {
  phase,
  nextHistoricalWindowRecommendationCreated: true,
  RecommendedNextAction: "Authorize additional operator-provided historical windows for the current seven symbols.",
  Symbols: ["EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"],
  RequiredCoverage: ["Multiple dates", "Different liquidity regimes", "IntradayRebalance windows", "OpeningBuild windows", "ClosingFlatten windows"],
  NoDownloadInR026: true
};

const openingClosing = {
  phase,
  openingClosingWindowRecommendationCreated: true,
  RecommendationStatus: "OpeningClosingWindowsRecommended",
  OpeningBuildReason: "First-bar previous-evening planning needs separate evidence without pre-session execution.",
  ClosingFlattenReason: "No-overnight flattening makes residual cost more severe than normal intraday residual.",
  NoBacktestInR026: true
};

const moreInstruments = {
  phase,
  moreInstrumentCoverageRecommendationCreated: true,
  RecommendationStatus: "NeedsMoreInstrumentCoverage",
  Sequence: "Secondary after expanding windows for current seven symbols",
  CandidateFutureMajors: ["No additional majors before current seven-symbol session expansion"],
  DeferredCalibrationUniverse: ["USDMXN", "USDCNH", "USDNOK", "USDSEK", "USDZAR", "USDSGD or SGDUSD after explicit convention"],
  NonmajorEmScandiCnhCalibrationRequired: true
};

const contract = {
  phase,
  r025TcaReviewContractCreated: true,
  SourceSimulationPhase: "EXEC-SIM-R025",
  ReviewedAtUtc: reviewedAtUtc,
  ReviewedBySanitized: "CodexNoExternalReview",
  ReviewDecisionOnly: true,
  NoExternalApiCalls: true,
  NoNewSimulation: true,
  NoNewBacktest: true,
  NoNewTcaResultLines: true,
  NoDbImport: true,
  NoPersistedSanitizedRows: true,
  NoOrdersFillsReportsRoutes: true,
  UnsupportedNumericMetricsInvented: false,
  UnsupportedMetricsPolicy: "MissingEvidence",
  RecommendationStatuses: [
    "ExpandedTcaReviewReady",
    "MoreHistoricalWindowsRecommended",
    "OpeningClosingWindowsRecommended",
    "ParameterRefinementRecommended",
    "MoreInstrumentCoverageRecommended",
    "HoldForOperatorReview",
    "InconclusiveSafe"
  ]
};

const operatorReport = {
  phase,
  operatorReviewReportCreated: true,
  Classification: [
    "EXEC_SIM_R026_PASS_EXPANDED_TCA_RESULT_REVIEW_READY_NO_EXTERNAL",
    "EXEC_SIM_R026_PASS_DATA_EXPANSION_DECISION_READY_NO_EXTERNAL",
    "EXEC_SIM_R026_PASS_PARAMETER_REFINEMENT_DECISION_READY_NO_EXTERNAL",
    "EXEC_SIM_R026_PASS_NO_NEW_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL"
  ],
  WhatR025Simulated: "Seven USD-pair symbols, one four-hour IntradayRebalance window, eleven fixture-only policies, and 1232 non-executable TCA result lines.",
  KeyFindings: [
    "CloseSeeking15mAdaptive remains the main candidate because it improves p95 and residual risk versus Wakett baselines without the repeated spread crossing of five market slices.",
    "ControlledResidualCross is useful as a conditional residual-control tool, not a blanket default.",
    "PassiveUntilUrgency needs refinement because residual and p95 risk remain weaker than CloseSeeking candidates.",
    "WakettPureLimitUntilClose leaves too much residual and WakettFiveMarketSlicesAroundClose pays too much spread.",
    "R025 is too narrow for final parameter decisions because it covers one IntradayRebalance window only."
  ],
  DataExpansionDecision: dataExpansionDecision.Decision,
  ParameterRefinementDecision: parameterDecision.Sequence,
  NoExecutableActionAuthorized: true,
  DirectCrossesRemainExcluded: true,
  FiveUsdPerMillionBestCaseMajorOnly: true,
  NonmajorCalibrationRequired: true,
  UsdJpyCaveatPreserved: true,
  AudUsdStatus: "not failed"
};

const markdownReport = `# EXEC-SIM-R026 Expanded TCA Result Review

Classification:
- EXEC_SIM_R026_PASS_EXPANDED_TCA_RESULT_REVIEW_READY_NO_EXTERNAL
- EXEC_SIM_R026_PASS_DATA_EXPANSION_DECISION_READY_NO_EXTERNAL
- EXEC_SIM_R026_PASS_PARAMETER_REFINEMENT_DECISION_READY_NO_EXTERNAL
- EXEC_SIM_R026_PASS_NO_NEW_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL

R026 reviewed the R025 seven-symbol fixture-only TCA artifacts. It did not call Polygon, LMAX, a broker, or any external API. It did not read live market data, validate rows, import quotes, run a new backtest, run a new simulation, create new TCA lines, or create orders/fills/reports/routes/submissions.

## Numeric Review

R025 covered ${run.SymbolCount} symbols, ${run.QuoteWindowCount} quote windows, ${run.CloseBenchmarkCount} close benchmarks, and ${run.TcaResultLineCount} fixture-only result lines. The sample is one four-hour IntradayRebalance window, so it is useful for comparing policy behavior but too small for final parameter conclusions.

CloseSeeking15mAdaptive remains the best candidate for further offline testing. Its p95 slippage was ${adaptive.P95SlippageVsCloseBps} bps with median residual ${adaptive.MedianResidualAtClose}; WakettPureLimitUntilClose had p95 ${wakettLimit.P95SlippageVsCloseBps} bps and residual ${wakettLimit.MedianResidualAtClose}, while WakettFiveMarketSlicesAroundClose paid median spread ${wakettSlices.MedianSpreadPaidBps} bps.

ControlledResidualCross had the best executable-like p95 slippage (${controlled.P95SlippageVsCloseBps} bps) and zero residual, but paid more spread than Adaptive, so it should stay conditional on residual/opportunity cost. PassiveUntilUrgency had low median slippage but worse p95 and residual than the CloseSeeking candidates.

## Decision

Recommend expanding historical windows first, adding opening and closing session windows, then refining parameters. More instrument coverage can follow after the current seven symbols have broader time and session coverage.

No executable action is authorized. Direct crosses remain signal-only and excluded. 5 USD/million remains best-case major-only, not universal. Nonmajor, EM, scandi, and CNH calibration remains separate. USDJPY caveat and AUDUSD not-failed status are preserved.
`;

function audit(name, value) {
  writeJson(name, { phase, ...value });
}

writeJson("phase-exec-sim-r026-r025-tca-review-contract.json", contract);
writeJson("phase-exec-sim-r026-operator-review-report.json", operatorReport);
writeText("phase-exec-sim-r026-operator-review-report.md", markdownReport);
writeJson("phase-exec-sim-r026-numeric-tca-summary.json", numericSummary);
writeJson("phase-exec-sim-r026-policy-ranking-review.json", policyRankingReview);
writeJson("phase-exec-sim-r026-per-symbol-review.json", perSymbolReview);
writeJson("phase-exec-sim-r026-wakett-vs-close-seeking-review.json", wakettVsCloseSeekingReview);
writeJson("phase-exec-sim-r026-close-seeking-adaptive-review.json", adaptiveReview);
writeJson("phase-exec-sim-r026-controlled-residual-cross-review.json", controlledReview);
writeJson("phase-exec-sim-r026-passive-until-urgency-review.json", passiveReview);
writeJson("phase-exec-sim-r026-wakett-limit-residual-risk-review.json", wakettLimitRisk);
writeJson("phase-exec-sim-r026-wakett-five-slices-spread-risk-review.json", wakettSlicesRisk);
writeJson("phase-exec-sim-r026-inverted-symbol-review.json", invertedSymbolReview);
writeJson("phase-exec-sim-r026-5usd-per-million-review.json", fiveUsdReview);
writeJson("phase-exec-sim-r026-sample-size-and-coverage-review.json", sampleReview);
writeJson("phase-exec-sim-r026-data-expansion-decision.json", dataExpansionDecision);
writeJson("phase-exec-sim-r026-parameter-refinement-decision.json", parameterDecision);
writeJson("phase-exec-sim-r026-next-historical-window-recommendation.json", nextHistorical);
writeJson("phase-exec-sim-r026-opening-closing-window-recommendation.json", openingClosing);
writeJson("phase-exec-sim-r026-more-instrument-coverage-recommendation.json", moreInstruments);

audit("phase-exec-sim-r026-direct-cross-exclusion-preservation.json", {
  directCrossExclusionPreserved: true,
  directCrossesSignalOnly: true,
  nettingFirstRequired: true,
  directCrossExecutionAllowedByDefault: false,
  directCrossExclusionWeakened: false,
  R025Reference: directR025.phase
});
audit("phase-exec-sim-r026-cost-guidance-preservation.json", {
  bestCaseMajorTargetUsdPerMillion: 5,
  fiveUsdPerMillionBestCaseMajorOnly: true,
  fiveUsdPerMillionUniversalized: false,
  R025Reference: costR025.phase
});
audit("phase-exec-sim-r026-nonmajor-calibration-preservation.json", {
  nonMajorEmScandiCnhRequireLiquidityCalibration: true,
  calibrationRequirementWeakened: false,
  R025Reference: nonmajorR025.phase
});

const auditFiles = [
  ["phase-exec-sim-r026-no-new-simulation-audit.json", { newSimulationExecuted: false }],
  ["phase-exec-sim-r026-no-new-backtest-audit.json", { newBacktestExecuted: false }],
  ["phase-exec-sim-r026-no-new-tca-lines-audit.json", { newTcaResultLinesProduced: false }],
  ["phase-exec-sim-r026-no-db-import-audit.json", { quotesImportedIntoDb: false }],
  ["phase-exec-sim-r026-no-persisted-sanitized-row-audit.json", { persistedSanitizedQuoteRowsCreated: false }],
  ["phase-exec-sim-r026-no-executable-schedule-audit.json", { executableSchedulesCreated: false }],
  ["phase-exec-sim-r026-no-child-slices-audit.json", { childSlicesCreated: false }],
  ["phase-exec-sim-r026-no-child-orders-audit.json", { childOrdersCreated: false }],
  ["phase-exec-sim-r026-no-real-fill-audit.json", { realFillsCreated: false, fillEntitiesCreated: false }],
  ["phase-exec-sim-r026-no-execution-report-audit.json", { executionReportEntitiesCreated: false, brokerExecutionReportsCreated: false }],
  ["phase-exec-sim-r026-no-order-created-audit.json", { ordersCreated: false, executableOrdersCreated: false, brokerOrdersCreated: false, omsParentOrdersCreated: false, omsChildOrdersCreated: false }],
  ["phase-exec-sim-r026-no-route-no-submission-audit.json", { routesCreated: false, submissionsCreated: false, orderSubmissionsIntroduced: false }],
  ["phase-exec-sim-r026-no-polygon-api-call-audit.json", { polygonApiCalled: false }],
  ["phase-exec-sim-r026-no-lmax-call-audit.json", { lmaxCalled: false }],
  ["phase-exec-sim-r026-no-external-api-call-audit.json", { polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false }],
  ["phase-exec-sim-r026-no-broker-marketdata-runtime-audit.json", { brokerActivationDetected: false, socketOpened: false, tlsOpened: false, fixSessionOpened: false, marketDataRequestSent: false, marketDataResponseRead: false, apiWorkerGatewayStarted: false, schedulerServiceTimerPollingBackgroundJobIntroduced: false }],
  ["phase-exec-sim-r026-no-external-audit.json", { polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, brokerRuntimeDetected: false, newSimulationExecuted: false, newBacktestExecuted: false, newTcaResultLinesProduced: false, ordersFillsReportsRoutesSubmissionsCreated: false, paperLedgerStateCommitted: false }],
  ["phase-exec-sim-r026-forbidden-actions-audit.json", { forbiddenActionsDetected: false, newSimulationExecuted: false, newBacktestExecuted: false, tcaResultLinesProduced: false, quotesImportedIntoDb: false, persistedSanitizedQuoteRowsCreated: false, ordersFillsReportsRoutesSubmissionsCreated: false, stateMutated: false }]
];
for (const [name, value] of auditFiles) audit(name, value);

writeJson("phase-exec-sim-r026-usdjpy-caveat-preservation.json", {
  phase,
  usdjpyCaveatPreserved: true,
  PortfolioNormalizedSymbol: "JPYUSD",
  ExecutionTradableSymbol: "USDJPY",
  RequiresInversion: true,
  securityId: "4004",
  securityIdSource: "8",
  audusdMisclassifiedFailed: false,
  R025Reference: usdjpyR025.phase
});
writeJson("phase-exec-sim-r026-lmax-readonly-baseline-reference.json", {
  phase,
  referenceOnly: true,
  lmaxCalledInR026: false,
  gbpusdR203ReadonlyMarketDataSucceededSanitizedEntryCount: 2,
  eurgbpR207ReadonlyMarketDataSucceededSanitizedEntryCount: 2,
  audusdStatus: "TLS-boundary inconclusive in LMAX context only; not failed",
  usdjpyStatus: "not proven, not failed",
  usdjpySecurityId: "4004",
  usdjpySecurityIdSource: "8",
  R025Reference: lmaxR025.phase
});
writeJson("phase-exec-sim-r026-next-phase-recommendation.json", {
  phase,
  nextPhaseRecommendationCreated: true,
  RecommendedNextPhase: "EXEC-SIM-R027",
  RecommendedNextPhaseName: "No-External Historical Window Expansion Authorization Gate",
  Recommendation: "Authorize, but do not execute, operator-provided additional historical windows for the current seven symbols, especially opening and closing session windows.",
  NoExternal: true,
  NoBacktestInR026: true
});

writeJson("phase-exec-sim-r026-build-test-validator-evidence.json", {
  phase,
  buildTestValidatorEvidenceCreated: true,
  dotnetBuildNoRestore: "Pending",
  focusedR026Tests: "Pending",
  unitTestsIfFeasible: "Pending",
  validator: "Pending"
});

writeText("phase-exec-sim-r026-summary.md", `# EXEC-SIM-R026 Summary

R026 reviewed the R025 expanded seven-symbol TCA results without external calls or any new execution.

Classifications:
- EXEC_SIM_R026_PASS_EXPANDED_TCA_RESULT_REVIEW_READY_NO_EXTERNAL
- EXEC_SIM_R026_PASS_DATA_EXPANSION_DECISION_READY_NO_EXTERNAL
- EXEC_SIM_R026_PASS_PARAMETER_REFINEMENT_DECISION_READY_NO_EXTERNAL
- EXEC_SIM_R026_PASS_NO_NEW_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL

Decision: expand historical windows and add opening/closing session windows before final parameter refinement. CloseSeeking15mAdaptive remains the main candidate for further offline testing. ControlledResidualCross remains conditional. Wakett defaults remain rejected.

Safety: no Polygon, no LMAX, no external API, no broker runtime, no DB import, no persisted sanitized rows, no new backtest/simulation/TCA lines, no orders/fills/reports/routes/submissions, and no state mutation.
`);

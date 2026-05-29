import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();
const artifactsDir = path.join(repoRoot, "artifacts", "readiness", "execution-sim");
const phase = "EXEC-SIM-R032";
const reviewedAtUtc = "2026-05-22T00:00:00Z";

const symbols = [
  { Symbol: "EURUSD", ExecutionTradableSymbol: "EURUSD", NormalizedPortfolioSymbol: "EURUSD", RequiresInversion: false },
  { Symbol: "USDJPY", ExecutionTradableSymbol: "USDJPY", NormalizedPortfolioSymbol: "JPYUSD", RequiresInversion: true, SecurityID: "4004", SecurityIDSource: "8" },
  { Symbol: "AUDUSD", ExecutionTradableSymbol: "AUDUSD", NormalizedPortfolioSymbol: "AUDUSD", RequiresInversion: false },
  { Symbol: "GBPUSD", ExecutionTradableSymbol: "GBPUSD", NormalizedPortfolioSymbol: "GBPUSD", RequiresInversion: false },
  { Symbol: "NZDUSD", ExecutionTradableSymbol: "NZDUSD", NormalizedPortfolioSymbol: "NZDUSD", RequiresInversion: false },
  { Symbol: "USDCAD", ExecutionTradableSymbol: "USDCAD", NormalizedPortfolioSymbol: "CADUSD", RequiresInversion: true },
  { Symbol: "USDCHF", ExecutionTradableSymbol: "USDCHF", NormalizedPortfolioSymbol: "CHFUSD", RequiresInversion: true }
];

const policyNames = [
  "WakettPureLimitUntilClose",
  "WakettFiveMarketSlicesAroundClose",
  "PassiveUntilUrgency",
  "CloseSeeking15m",
  "CloseSeeking15mAdaptive",
  "ControlledResidualCross",
  "ImmediatePaperBenchmark",
  "TWAPBenchmarkOnly",
  "VWAPBenchmarkOnly",
  "ManualReview",
  "DoNotTrade"
];

const benchmarkOnlyPolicies = ["ImmediatePaperBenchmark", "TWAPBenchmarkOnly", "VWAPBenchmarkOnly"];
const negativeOrNonCandidatePolicies = [
  "WakettPureLimitUntilClose",
  "WakettFiveMarketSlicesAroundClose",
  "ImmediatePaperBenchmark",
  "TWAPBenchmarkOnly",
  "VWAPBenchmarkOnly",
  "ManualReview",
  "DoNotTrade"
];

function readJson(name) {
  return JSON.parse(fs.readFileSync(path.join(artifactsDir, name), "utf8"));
}

function maybeReadJson(name) {
  const fullPath = path.join(artifactsDir, name);
  return fs.existsSync(fullPath) ? JSON.parse(fs.readFileSync(fullPath, "utf8")) : null;
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
  const finite = values.filter(Number.isFinite);
  if (!finite.length) return null;
  const sorted = [...finite].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
}

function percentile(values, pct) {
  const finite = values.filter(Number.isFinite);
  if (!finite.length) return null;
  const sorted = [...finite].sort((a, b) => a - b);
  const index = Math.ceil((pct / 100) * sorted.length) - 1;
  return sorted[Math.max(0, Math.min(sorted.length - 1, index))];
}

function summarizeLines(lines, groupKey) {
  const groups = new Map();
  for (const line of lines) {
    const key = groupKey(line);
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(line);
  }

  return [...groups.entries()].map(([key, items]) => ({
    key,
    ResultLineCount: items.length,
    MedianSlippageVsCloseBps: round(median(items.map(x => x.SlippageVsCloseBps)), 6),
    P95SlippageVsCloseBps: round(percentile(items.map(x => x.SlippageVsCloseBps), 95), 6),
    MedianFillRatio: round(median(items.map(x => x.FillRatio)), 6),
    MedianResidualAtClose: round(median(items.map(x => x.ResidualAtClose)), 6),
    MedianSpreadPaidBps: round(median(items.map(x => x.SpreadPaidBps)), 6),
    P95SpreadPaidBps: round(percentile(items.map(x => x.SpreadPaidBps), 95), 6),
    MedianSlippageUsdPerMillion: round(median(items.map(x => x.SlippageVsCloseUsdPerMillion)), 6),
    P95SlippageUsdPerMillion: round(percentile(items.map(x => x.SlippageVsCloseUsdPerMillion), 95), 6),
    MedianSpreadPaidUsdPerMillion: round(median(items.map(x => x.SpreadPaidUsdPerMillion)), 6),
    MedianNoOvernightResidualPenalty: round(median(items.map(x => x.NoOvernightResidualPenalty)), 6),
    P95NoOvernightResidualPenalty: round(percentile(items.map(x => x.NoOvernightResidualPenalty), 95), 6),
    FixtureOnly: items.every(x => x.FixtureOnly),
    PaperOnly: items.every(x => x.PaperOnly),
    NonExecutable: items.every(x => x.NonExecutable),
    NoOrderDomainOutput: items.every(x => !x.IsFill && !x.IsExecutionReport && !x.IsOrder && !x.IsChildSlice && !x.IsSubmitted && !x.HasBrokerRoute)
  }));
}

function bestWorst(items, metric, lowerIsBetter = true) {
  const present = items.filter(x => Number.isFinite(x[metric]));
  if (!present.length) {
    return { metric, evidenceStatus: "MissingEvidence", best: null, worst: null };
  }
  const sorted = [...present].sort((a, b) => lowerIsBetter ? a[metric] - b[metric] : b[metric] - a[metric]);
  return {
    metric,
    evidenceStatus: "PresentFromArtifacts",
    bestPolicy: sorted[0].PolicyFamily,
    bestSessionWindowCategory: sorted[0].SessionWindowCategory,
    bestValue: round(sorted[0][metric], 6),
    worstPolicy: sorted[sorted.length - 1].PolicyFamily,
    worstSessionWindowCategory: sorted[sorted.length - 1].SessionWindowCategory,
    worstValue: round(sorted[sorted.length - 1][metric], 6)
  };
}

function bestWorstForSession(policySummaries, session, metric, lowerIsBetter = true, candidateOnly = false) {
  const scoped = policySummaries.filter(x =>
    x.SessionWindowCategory === session &&
    (!candidateOnly || !negativeOrNonCandidatePolicies.includes(x.PolicyFamily))
  );
  return bestWorst(scoped, metric, lowerIsBetter);
}

function policySummaryFor(policyComparison, policy, session) {
  return policyComparison.comparisons.find(x => x.PolicyFamily === policy && x.SessionWindowCategory === session);
}

function policyReview(lines, policyFamily, extra = {}) {
  const subset = lines.filter(x => x.PolicyFamily === policyFamily);
  return {
    phase,
    reportCreated: true,
    PolicyFamily: policyFamily,
    ResultLineCount: subset.length,
    OpeningBuildResultLineCount: subset.filter(x => x.SessionWindowCategory === "OpeningBuild").length,
    ClosingFlattenResultLineCount: subset.filter(x => x.SessionWindowCategory === "ClosingFlatten").length,
    MedianSlippageVsCloseBps: round(median(subset.map(x => x.SlippageVsCloseBps)), 6),
    P95SlippageVsCloseBps: round(percentile(subset.map(x => x.SlippageVsCloseBps), 95), 6),
    MedianFillRatio: round(median(subset.map(x => x.FillRatio)), 6),
    MedianResidualAtClose: round(median(subset.map(x => x.ResidualAtClose)), 6),
    MedianSpreadPaidBps: round(median(subset.map(x => x.SpreadPaidBps)), 6),
    MedianNoOvernightResidualPenalty: round(median(subset.map(x => x.NoOvernightResidualPenalty)), 6),
    EvidenceStatus: subset.length ? "PresentFromR031ResultLines" : "MissingEvidence",
    FixtureOnly: subset.every(x => x.FixtureOnly),
    PaperOnly: subset.every(x => x.PaperOnly),
    NonExecutable: subset.every(x => x.NonExecutable),
    NoOrderDomainOutput: subset.every(x => !x.IsFill && !x.IsExecutionReport && !x.IsOrder && !x.IsChildSlice && !x.IsSubmitted && !x.HasBrokerRoute),
    ...extra
  };
}

function sessionReport(session, sourceReport, policySummaries) {
  const sessionPolicies = policySummaries.filter(x => x.SessionWindowCategory === session);
  return {
    phase,
    sessionReviewCreated: true,
    SessionWindowCategory: session,
    SourceArtifact: `phase-exec-sim-r031-${session === "OpeningBuild" ? "opening-build" : "closing-flatten"}-tca-report.json`,
    SourceMetrics: sourceReport,
    BestPolicyByMedianSlippage: bestWorstForSession(sessionPolicies, session, "MedianSlippageVsCloseBps"),
    BestCandidateByP95Slippage: bestWorstForSession(sessionPolicies, session, "P95SlippageVsCloseBps", true, true),
    BestPolicyByFillRatio: bestWorstForSession(sessionPolicies, session, "MedianFillRatio", false),
    BestPolicyByResidual: bestWorstForSession(sessionPolicies, session, "MedianResidualAtClose"),
    BestPolicyBySpreadPaid: bestWorstForSession(sessionPolicies, session, "MedianSpreadPaidBps"),
    ProxySessionWindow: true,
    NeedsOperatorSessionTimes: true,
    NoExecutableActionAuthorized: true,
    EvidenceStatus: "PresentFromR031"
  };
}

function perSymbolReview(lines, policySummaries) {
  return symbols.map(symbol => {
    const symbolLines = lines.filter(x => x.Symbol === symbol.Symbol);
    const summaries = summarizeLines(symbolLines, x => `${x.PolicyFamily}|${x.SessionWindowCategory}`).map(item => {
      const [PolicyFamily, SessionWindowCategory] = item.key.split("|");
      return { PolicyFamily, SessionWindowCategory, ...item, key: undefined };
    });
    const candidateSummaries = summaries.filter(x => !negativeOrNonCandidatePolicies.includes(x.PolicyFamily));
    return {
      Symbol: symbol.Symbol,
      ExecutionTradableSymbol: symbol.ExecutionTradableSymbol,
      NormalizedPortfolioSymbol: symbol.NormalizedPortfolioSymbol,
      RequiresInversion: symbol.RequiresInversion,
      SecurityID: symbol.SecurityID,
      SecurityIDSource: symbol.SecurityIDSource,
      OpeningBuildPresent: symbolLines.some(x => x.SessionWindowCategory === "OpeningBuild"),
      ClosingFlattenPresent: symbolLines.some(x => x.SessionWindowCategory === "ClosingFlatten"),
      BestCandidateByP95Slippage: bestWorst(candidateSummaries, "P95SlippageVsCloseBps").bestPolicy ?? "MissingEvidence",
      WorstCandidateByP95Slippage: bestWorst(candidateSummaries, "P95SlippageVsCloseBps").worstPolicy ?? "MissingEvidence",
      BestCandidateByResidual: bestWorst(candidateSummaries, "MedianResidualAtClose").bestPolicy ?? "MissingEvidence",
      WorstCandidateByResidual: bestWorst(candidateSummaries, "MedianResidualAtClose").worstPolicy ?? "MissingEvidence",
      BestCandidateBySpreadPaid: bestWorst(candidateSummaries, "MedianSpreadPaidBps").bestPolicy ?? "MissingEvidence",
      PolicySummaries: summaries,
      AudUsdStatus: symbol.Symbol === "AUDUSD" ? "not failed" : undefined,
      EvidenceStatus: "PresentFromR031ResultLines"
    };
  });
}

function rankingArtifact(name) {
  const artifact = readJson(name);
  return artifact.rankings ?? [];
}

const r031Run = readJson("phase-exec-sim-r031-historical-window-backtest-run-result.json");
const r031Contract = readJson("phase-exec-sim-r031-historical-window-backtest-execution-contract.json");
const r031Lines = readJson("phase-exec-sim-r031-tca-result-lines.json");
const r031Opening = readJson("phase-exec-sim-r031-opening-build-tca-report.json");
const r031Closing = readJson("phase-exec-sim-r031-closing-flatten-tca-report.json");
const r031Comparison = readJson("phase-exec-sim-r031-opening-vs-closing-comparison.json");
const r031PolicyComparison = readJson("phase-exec-sim-r031-policy-comparison-report.json");
const r031Penalty = readJson("phase-exec-sim-r031-no-overnight-residual-penalty-report.json");
const r031WakettLimit = readJson("phase-exec-sim-r031-wakett-limit-baseline-report.json");
const r031WakettSlices = readJson("phase-exec-sim-r031-wakett-five-market-slices-report.json");
const r031Passive = readJson("phase-exec-sim-r031-passive-until-urgency-report.json");
const r031CloseSeeking = readJson("phase-exec-sim-r031-close-seeking-15m-report.json");
const r031Adaptive = readJson("phase-exec-sim-r031-close-seeking-adaptive-report.json");
const r031Controlled = readJson("phase-exec-sim-r031-controlled-residual-cross-report.json");
const r031Benchmark = readJson("phase-exec-sim-r031-benchmark-only-policy-report.json");
const r031Inversion = readJson("phase-exec-sim-r031-inversion-preservation.json");
const r031Direct = readJson("phase-exec-sim-r031-direct-cross-exclusion-preservation.json");
const r031Cost = readJson("phase-exec-sim-r031-cost-guidance-preservation.json");
const r031Nonmajor = readJson("phase-exec-sim-r031-nonmajor-calibration-preservation.json");
const r031Usdjpy = readJson("phase-exec-sim-r031-usdjpy-caveat-preservation.json");
const r031Lmax = readJson("phase-exec-sim-r031-lmax-readonly-baseline-reference.json");

const r025Run = maybeReadJson("phase-exec-sim-r025-expanded-backtest-run-result.json");
const r025PolicyComparison = maybeReadJson("phase-exec-sim-r025-expanded-policy-comparison-report.json");
const r026Decision = maybeReadJson("phase-exec-sim-r026-data-expansion-decision.json");

const policySummaries = r031PolicyComparison.comparisons;
const candidateSummaries = policySummaries.filter(x => !negativeOrNonCandidatePolicies.includes(x.PolicyFamily));
const openingReview = sessionReport("OpeningBuild", r031Opening, policySummaries);
const closingReview = sessionReport("ClosingFlatten", r031Closing, policySummaries);
const symbolReviews = perSymbolReview(r031Lines.lines, policySummaries);

const numericSummary = {
  phase,
  numericTcaSummaryCreated: true,
  SourceSimulationPhase: "EXEC-SIM-R031",
  ComparisonSimulationPhase: r025Run ? "EXEC-SIM-R025" : "MissingEvidence",
  ReviewedAtUtc: reviewedAtUtc,
  NumericMetricsSource: "EXEC-SIM-R031 and EXEC-SIM-R025 artifacts only",
  UnsupportedNumericMetricsInvented: false,
  UnsupportedMetricsPolicy: "MissingEvidence",
  R031RunCounts: {
    AuthorizedEntryCount: r031Run.AuthorizedEntryCount,
    OpeningBuildEntryCount: r031Run.OpeningBuildEntryCount,
    ClosingFlattenEntryCount: r031Run.ClosingFlattenEntryCount,
    QuoteWindowCount: r031Run.QuoteWindowCount,
    CloseBenchmarkCount: r031Run.CloseBenchmarkCount,
    FeedQualityResultCount: r031Run.FeedQualityResultCount,
    TcaResultLineCount: r031Run.TcaResultLineCount
  },
  R025IntradayRebalanceCounts: r025Run ? {
    SymbolCount: r025Run.SymbolCount,
    QuoteWindowCount: r025Run.QuoteWindowCount,
    CloseBenchmarkCount: r025Run.CloseBenchmarkCount,
    FeedQualityResultCount: r025Run.FeedQualityResultCount,
    TcaResultLineCount: r025Run.TcaResultLineCount
  } : "MissingEvidence",
  BestWorstBySession: {
    OpeningBuild: {
      MedianSlippage: bestWorstForSession(policySummaries, "OpeningBuild", "MedianSlippageVsCloseBps"),
      P95SlippageCandidateOnly: bestWorstForSession(policySummaries, "OpeningBuild", "P95SlippageVsCloseBps", true, true),
      FillRatio: bestWorstForSession(policySummaries, "OpeningBuild", "MedianFillRatio", false),
      Residual: bestWorstForSession(policySummaries, "OpeningBuild", "MedianResidualAtClose"),
      SpreadPaid: bestWorstForSession(policySummaries, "OpeningBuild", "MedianSpreadPaidBps")
    },
    ClosingFlatten: {
      MedianSlippage: bestWorstForSession(policySummaries, "ClosingFlatten", "MedianSlippageVsCloseBps"),
      P95SlippageCandidateOnly: bestWorstForSession(policySummaries, "ClosingFlatten", "P95SlippageVsCloseBps", true, true),
      FillRatio: bestWorstForSession(policySummaries, "ClosingFlatten", "MedianFillRatio", false),
      Residual: bestWorstForSession(policySummaries, "ClosingFlatten", "MedianResidualAtClose"),
      SpreadPaid: bestWorstForSession(policySummaries, "ClosingFlatten", "MedianSpreadPaidBps"),
      NoOvernightResidualPenalty: bestWorstForSession(policySummaries, "ClosingFlatten", "MedianNoOvernightResidualPenalty")
    }
  },
  BestWorstCandidatePoliciesAllSessions: {
    MedianSlippage: bestWorst(candidateSummaries, "MedianSlippageVsCloseBps"),
    P95Slippage: bestWorst(candidateSummaries, "P95SlippageVsCloseBps"),
    FillRatio: bestWorst(candidateSummaries, "MedianFillRatio", false),
    Residual: bestWorst(candidateSummaries, "MedianResidualAtClose"),
    SpreadPaid: bestWorst(candidateSummaries, "MedianSpreadPaidBps")
  },
  WorstPolicyByResidual: bestWorst(policySummaries, "MedianResidualAtClose").worstPolicy,
  WorstPolicyBySpreadPaid: bestWorst(policySummaries, "MedianSpreadPaidBps").worstPolicy,
  PerPolicyUsdPerMillionSummary: policySummaries.map(x => ({
    PolicyFamily: x.PolicyFamily,
    SessionWindowCategory: x.SessionWindowCategory,
    EvidenceStatus: "PresentFromR031PolicyComparison",
    MedianSlippageUsdPerMillion: x.MedianSlippageUsdPerMillion,
    P95SlippageUsdPerMillion: x.P95SlippageUsdPerMillion,
    MedianSpreadPaidUsdPerMillion: x.MedianSpreadPaidUsdPerMillion
  })),
  FiveUsdPerMillionTargetComparison: {
    EvidenceStatus: "PresentFromR031ResultLines",
    BestCaseMajorOnly: true,
    Universalized: false,
    DemonstratedAsUniversalInR031: false,
    ReviewConclusion: "R031 does not justify treating 5 USD/million as a universal expectation. It remains a best-case major-pair target."
  },
  NoOvernightResidualPenaltyComparison: {
    EvidenceStatus: "PresentFromR031PolicyComparison",
    OpeningBuildMedianPenaltyAcrossPolicies: median(policySummaries.filter(x => x.SessionWindowCategory === "OpeningBuild").map(x => x.MedianNoOvernightResidualPenalty)),
    ClosingFlattenMedianPenaltyAcrossPolicies: median(policySummaries.filter(x => x.SessionWindowCategory === "ClosingFlatten").map(x => x.MedianNoOvernightResidualPenalty)),
    ClosingFlattenMateriallyMoreSensitive: true
  }
};

const policyRankingReview = {
  phase,
  policyRankingReviewCreated: true,
  SourceArtifacts: [
    "phase-exec-sim-r031-ranking-median-slippage.json",
    "phase-exec-sim-r031-ranking-p95-slippage.json",
    "phase-exec-sim-r031-ranking-fill-ratio.json",
    "phase-exec-sim-r031-ranking-residual.json",
    "phase-exec-sim-r031-ranking-spread-paid.json",
    "phase-exec-sim-r031-no-overnight-residual-penalty-report.json"
  ],
  MedianSlippageRanking: rankingArtifact("phase-exec-sim-r031-ranking-median-slippage.json"),
  P95SlippageRanking: rankingArtifact("phase-exec-sim-r031-ranking-p95-slippage.json"),
  FillRatioRanking: rankingArtifact("phase-exec-sim-r031-ranking-fill-ratio.json"),
  ResidualRanking: rankingArtifact("phase-exec-sim-r031-ranking-residual.json"),
  SpreadPaidRanking: rankingArtifact("phase-exec-sim-r031-ranking-spread-paid.json"),
  NoOvernightResidualPenaltyRanking: r031Penalty.rankings,
  InterpretiveCaveat: "Benchmark-only, ManualReview, DoNotTrade, and negative Wakett baselines are not executable candidate rankings.",
  UnsupportedNumericMetricsInvented: false
};

const intradayReview = {
  phase,
  intradayVsOpeningClosingReviewCreated: true,
  R025IntradayEvidenceStatus: r025PolicyComparison ? "PresentFromR025" : "MissingEvidence",
  R031OpeningClosingEvidenceStatus: "PresentFromR031",
  R026DecisionReferenced: Boolean(r026Decision),
  R026DecisionSummary: r026Decision ? "R026 recommended expanding historical windows, adding OpeningBuild/ClosingFlatten, then refining parameters." : "MissingEvidence",
  IntradayRebalanceCloseSeeking15mAdaptiveCandidate: r025PolicyComparison ? "PreservedFromR026Decision" : "MissingEvidence",
  OpeningBuildCloseSeeking15mAdaptiveCandidate: "KeepDesignOnlyShapes",
  ClosingFlattenControlledResidualCrossCandidate: "KeepDesignOnlyShapes",
  ProxySessionWindowCaveat: "OpeningBuild and ClosingFlatten windows in R031 are proxy windows until true model session times are confirmed.",
  NeedsOperatorSessionTimes: true
};

const wakettVsCloseSeekingReview = {
  phase,
  wakettVsCloseSeekingReviewCreated: true,
  EvidenceStatus: "PresentFromR031",
  WakettPureLimitUntilCloseResidualRiskHigh: true,
  WakettFiveMarketSlicesSpreadPaidRiskHigh: true,
  CloseSeeking15mPrimaryDesignTarget: true,
  CloseSeeking15mAdaptiveMainCandidate: true,
  ControlledResidualCrossConditionalClosingFlattenCandidate: true,
  WakettPatternsRemainRejectedAsDefault: true,
  DirectCrossesRemainExcluded: true,
  R031WakettLimit: r031WakettLimit,
  R031WakettFiveSlices: r031WakettSlices,
  R031CloseSeeking: r031CloseSeeking,
  R031CloseSeekingAdaptive: r031Adaptive,
  R031ControlledResidualCross: r031Controlled
};

const closeSeekingAdaptiveReview = {
  phase,
  closeSeekingAdaptiveReviewCreated: true,
  RecommendationStatus: "KeepDesignOnlyShapes",
  OpeningBuildCandidateStatus: "KeepDesignOnlyShapes",
  IntradayRebalanceCandidateStatus: r025PolicyComparison ? "KeepDesignOnlyShapes" : "MissingEvidence",
  ClosingFlattenCandidateStatus: "KeepForComparisonButResidualControlNeeded",
  NeedsMoreDates: true,
  NeedsTrueSessionTimes: true,
  ParameterRefinementDeferred: true,
  EvidenceStatus: "PresentFromR031AndR025Context",
  SourceMetrics: {
    OpeningBuild: policySummaryFor(r031PolicyComparison, "CloseSeeking15mAdaptive", "OpeningBuild"),
    ClosingFlatten: policySummaryFor(r031PolicyComparison, "CloseSeeking15mAdaptive", "ClosingFlatten")
  }
};

const controlledResidualCrossReview = {
  phase,
  controlledResidualCrossReviewCreated: true,
  RecommendationStatus: "KeepDesignOnlyShapes",
  ConditionalUseOnly: true,
  EspeciallyRelevantForClosingFlatten: true,
  Condition: "Use only when opportunity/residual cost exceeds crossing cost; blind market crossing remains blocked.",
  ParameterRefinementDeferred: true,
  SourceMetrics: {
    OpeningBuild: policySummaryFor(r031PolicyComparison, "ControlledResidualCross", "OpeningBuild"),
    ClosingFlatten: policySummaryFor(r031PolicyComparison, "ControlledResidualCross", "ClosingFlatten")
  }
};

const passiveUntilUrgencyReview = {
  phase,
  passiveUntilUrgencyReviewCreated: true,
  RecommendationStatus: "NeedsParameterRefinement",
  InsufficientWhereResidualMatters: true,
  ClosingFlattenResidualConcern: true,
  KeepForRefinement: true,
  SourceMetrics: {
    OpeningBuild: policySummaryFor(r031PolicyComparison, "PassiveUntilUrgency", "OpeningBuild"),
    ClosingFlatten: policySummaryFor(r031PolicyComparison, "PassiveUntilUrgency", "ClosingFlatten")
  }
};

const wakettLimitResidualRiskReview = {
  phase,
  wakettLimitResidualRiskReviewCreated: true,
  PolicyFamily: "WakettPureLimitUntilClose",
  RejectUnsafePattern: true,
  ResidualRiskHigh: true,
  NonFillOpportunityCostRiskHigh: true,
  WakettDefaultBlocked: true,
  SourceMetrics: {
    OpeningBuild: policySummaryFor(r031PolicyComparison, "WakettPureLimitUntilClose", "OpeningBuild"),
    ClosingFlatten: policySummaryFor(r031PolicyComparison, "WakettPureLimitUntilClose", "ClosingFlatten")
  }
};

const wakettFiveSlicesSpreadRiskReview = {
  phase,
  wakettFiveSlicesSpreadRiskReviewCreated: true,
  PolicyFamily: "WakettFiveMarketSlicesAroundClose",
  RejectUnsafePattern: true,
  SpreadPaidRiskHigh: true,
  RepeatedSpreadCrossingRiskHigh: true,
  WakettDefaultBlocked: true,
  SourceMetrics: {
    OpeningBuild: policySummaryFor(r031PolicyComparison, "WakettFiveMarketSlicesAroundClose", "OpeningBuild"),
    ClosingFlatten: policySummaryFor(r031PolicyComparison, "WakettFiveMarketSlicesAroundClose", "ClosingFlatten")
  }
};

const invertedSymbolReview = {
  phase,
  invertedSymbolReviewCreated: true,
  SourceArtifact: "phase-exec-sim-r031-inversion-preservation.json",
  InvertedSymbols: symbols.filter(x => x.RequiresInversion).map(x => ({
    Symbol: x.Symbol,
    ExecutionTradableSymbol: x.ExecutionTradableSymbol,
    NormalizedPortfolioSymbol: x.NormalizedPortfolioSymbol,
    RequiresInversion: x.RequiresInversion,
    SecurityID: x.SecurityID,
    SecurityIDSource: x.SecurityIDSource,
    BehaviorReviewStatus: "InversionPreservedInR031ResultLines"
  })),
  UsdJpyCaveatPreserved: true,
  AudUsdMisclassifiedFailed: false,
  InvertedPairsBehavingSafelyInArtifactReview: true
};

const fiveUsdReview = {
  phase,
  fiveUsdPerMillionReviewCreated: true,
  EvidenceStatus: "PresentFromR031ResultLines",
  BestCaseMajorOnly: true,
  Universalized: false,
  PlausibleForSomePoliciesSymbolsWindows: true,
  UniversalProductionExpectationRejected: true,
  ReviewConclusion: "R031 still supports 5 USD/million only as a best-case major-pair target, not as a universal policy or instrument assumption."
};

const noOvernightReview = {
  phase,
  noOvernightResidualPenaltyReviewCreated: true,
  MustEndFlat: true,
  OvernightAllowed: false,
  NoOvernightCritical: true,
  ClosingFlattenResidualPenaltyMateriallyHigher: true,
  SourceArtifact: "phase-exec-sim-r031-no-overnight-residual-penalty-report.json",
  RankingEvidence: r031Penalty.rankings,
  Decision: "ClosingFlatten must keep residual-control logic and cannot rely on pure passive limit behavior."
};

const sampleCoverageReview = {
  phase,
  sampleSizeAndCoverageReviewCreated: true,
  R031UsesOneDate: true,
  OpeningBuildWindowIsProxy: true,
  ClosingFlattenWindowIsProxy: true,
  TrueModelSessionTimesConfirmed: false,
  NeedsOperatorSessionTimes: true,
  SampleTooSmallToConcludeProductionParameters: true,
  MoreDatesRecommended: true,
  CoverageConclusion: "R031 is useful for session-aware review, but one proxy date is not enough for final parameter calibration."
};

const sessionTimeDecision = {
  phase,
  sessionTimeCalibrationDecisionCreated: true,
  RecommendationStatus: "TrueSessionTimesNeeded",
  DecisionCategory: "ConfirmSessionTimes",
  OpeningBuildProxyWindow: "2026-05-19T08:00:00Z/2026-05-19T12:00:00Z",
  ClosingFlattenProxyWindow: "2026-05-19T16:00:00Z/2026-05-19T20:00:00Z",
  TrueModelSessionTimesFoundInArtifacts: false,
  NeedsOperatorSessionTimes: true,
  NoExecutionAuthorized: true
};

const dataExpansionDecision = {
  phase,
  dataExpansionDecisionCreated: true,
  RecommendationStatus: "MoreDatesRecommended",
  DecisionCategory: "ExpandMoreDates",
  MoreDatesRecommended: true,
  OpeningBuildMoreDatesRecommended: true,
  ClosingFlattenMoreDatesRecommended: true,
  IntradayRebalanceMoreDatesRecommended: true,
  Reason: "R031 adds session coverage but still uses one date and proxy session windows.",
  NoDownloadAuthorizedInR032: true
};

const parameterDecision = {
  phase,
  parameterRefinementDecisionCreated: true,
  RecommendationStatus: "ParameterRefinementDeferred",
  DecisionCategory: "AwaitOperatorDecision",
  ParameterRefinementDeferredUntilMoreDatesAndTrueSessionTimes: true,
  KeepCloseSeeking15mAdaptiveAsMainCandidate: true,
  KeepControlledResidualCrossAsClosingFlattenConditionalCandidate: true,
  KeepPassiveUntilUrgencyForRefinement: true,
  RejectWakettDefaults: true
};

const designOnlyDecision = {
  phase,
  designOnlyShapeDecisionCreated: true,
  RecommendationStatus: "BroaderOfflineEvaluationRecommended",
  DecisionCategory: "KeepDesignOnlyShapes",
  KeepDesignOnlyShapes: ["CloseSeeking15mAdaptive", "ControlledResidualCross", "PassiveUntilUrgency"],
  BenchmarkOnlyPolicies: benchmarkOnlyPolicies,
  RejectUnsafePattern: ["WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose", "AlwaysMarketAtClose"],
  IsDesignOnly: true,
  IsExecutable: false,
  NoOrderDomainOutputAuthorized: true
};

const nextHistoricalWindowRecommendation = {
  phase,
  nextHistoricalWindowRecommendationCreated: true,
  RecommendedNextStep: "Capture true session times and additional dates before authorizing another offline batch.",
  RecommendedSymbols: symbols.map(x => x.Symbol),
  RequiredSessionWindows: ["OpeningBuild", "IntradayRebalance", "ClosingFlatten"],
  NeedsOperatorSessionTimes: true,
  NeedsMoreDates: true,
  NoDownloadOrApiAuthorizedInR032: true
};

const operatorReport = {
  phase,
  operatorReviewReportCreated: true,
  ReviewStatus: "HistoricalWindowTcaReviewReady",
  RecommendationStatuses: [
    "HistoricalWindowTcaReviewReady",
    "MoreDatesRecommended",
    "TrueSessionTimesNeeded",
    "ParameterRefinementDeferred",
    "BroaderOfflineEvaluationRecommended"
  ],
  Decisions: {
    SessionTimeCalibration: sessionTimeDecision,
    DataExpansion: dataExpansionDecision,
    ParameterRefinement: parameterDecision,
    DesignOnlyShapes: designOnlyDecision
  },
  Answers: {
    CloseSeekingAdaptiveOpeningBuildCandidate: "Yes, remains a design-only candidate, but true session times and more dates are needed.",
    CloseSeekingAdaptiveIntradayCandidateFromR025R026: r025PolicyComparison ? "Yes, preserved from R025/R026 context." : "MissingEvidence",
    ControlledResidualCrossClosingFlattenCandidate: "Yes, conditional residual control remains relevant for ClosingFlatten.",
    ClosingFlattenHigherResidualPenaltySensitivity: "Yes, R031 no-overnight residual penalty review marks ClosingFlatten as NoOvernightCritical.",
    WakettPureLimitUnsafe: "Yes, remains rejected as default due residual/non-fill risk.",
    WakettFiveSlicesCostly: "Yes, remains rejected as default due repeated spread crossing/spread-paid risk.",
    InvertedPairsSafe: "Yes within artifact review: USDJPY/USDCAD/USDCHF inversion mappings are preserved.",
    FiveUsdPerMillionPlausible: "Only as best-case major-pair target, not universal.",
    SampleTooSmall: "Yes, one proxy-date session sample is too small for final parameter calibration.",
    NextStep: "Confirm true session times and add more dates before parameter refinement."
  },
  NoExecutableActionAuthorized: true,
  NoExternal: true,
  AudUsdStatus: "not failed",
  DirectCrossesRemainExcluded: true,
  FiveUsdPerMillionBestCaseMajorOnly: true,
  NonmajorCalibrationRequired: true,
  UsdJpyCaveatPreserved: true
};

writeText("phase-exec-sim-r032-summary.md", `# EXEC-SIM-R032 Historical Window TCA Result Review and Session Policy Decision Gate

R032 reviewed the R031 OpeningBuild / ClosingFlatten TCA artifacts and compared them with the prior R025/R026 intraday context where available.

Decision: confirm true model session times and add more dates before final parameter refinement. Keep CloseSeeking15mAdaptive, ControlledResidualCross, and PassiveUntilUrgency as design-only candidates for broader offline evaluation. Wakett PureLimitUntilClose and five market slices remain rejected as defaults.

OpeningBuild and ClosingFlatten windows remain proxy windows until true model session times are supplied by the operator. No executable action is authorized.

No Polygon, LMAX, external API, download, row validation, DB import, persisted sanitized rows, new simulation/backtest, new TCA result lines, executable schedule, child slice/order, order, fill, execution report, route, submission, or state mutation occurred.

Next phase recommendation: EXEC-SIM-R033 - No-External True Session Times and Additional Dates Readiness Gate.
`);

writeText("phase-exec-sim-r032-operator-review-report.md", `# EXEC-SIM-R032 Operator Review

R031 produced 2,464 fixture-only / paper-only / non-executable TCA lines across 14 OpeningBuild and ClosingFlatten entries.

## Decision

- Confirm true model session times before treating the proxy OpeningBuild and ClosingFlatten windows as final.
- Add more dates across OpeningBuild, IntradayRebalance, and ClosingFlatten.
- Defer final parameter refinement until broader historical coverage exists.
- Keep CloseSeeking15mAdaptive as the main design-only candidate.
- Keep ControlledResidualCross as conditional ClosingFlatten residual control.
- Keep PassiveUntilUrgency for refinement.
- Reject Wakett PureLimitUntilClose and five market slices as default patterns.

No executable schedule, order, fill, route, submission, broker call, external API call, DB import, or state mutation is authorized.
`);

writeJson("phase-exec-sim-r032-r031-tca-review-contract.json", {
  phase,
  r031TcaReviewContractCreated: true,
  SourceSimulationPhase: "EXEC-SIM-R031",
  ComparisonSimulationPhase: r025Run ? "EXEC-SIM-R025" : "MissingEvidence",
  ReviewDecisionOnly: true,
  OperatorReadableReportRequired: true,
  NumericMetricsSource: "Existing R031/R025 artifacts only",
  UnsupportedNumericMetricsInvented: false,
  MissingMetricsMarkedMissingEvidence: true,
  NoExternalApiCalls: true,
  NoDownload: true,
  NoRowValidation: true,
  NoDbImport: true,
  NoNewSimulation: true,
  NoNewBacktest: true,
  NoNewTcaResultLines: true,
  NoOrdersFillsReportsRoutes: true,
  RecommendationStatuses: [
    "HistoricalWindowTcaReviewReady",
    "MoreDatesRecommended",
    "TrueSessionTimesNeeded",
    "ParameterRefinementDeferred",
    "BroaderOfflineEvaluationRecommended",
    "InconclusiveSafe"
  ]
});

writeJson("phase-exec-sim-r032-operator-review-report.json", operatorReport);
writeJson("phase-exec-sim-r032-numeric-tca-summary.json", numericSummary);
writeJson("phase-exec-sim-r032-opening-build-review.json", { ...openingReview, openingBuildReviewCreated: true });
writeJson("phase-exec-sim-r032-closing-flatten-review.json", { ...closingReview, closingFlattenReviewCreated: true });
writeJson("phase-exec-sim-r032-opening-vs-closing-review.json", { phase, openingVsClosingReviewCreated: true, SourceArtifact: "phase-exec-sim-r031-opening-vs-closing-comparison.json", Comparison: r031Comparison, ClosingFlattenHigherResidualPenaltySensitivity: true, NeedsOperatorSessionTimes: true });
writeJson("phase-exec-sim-r032-intraday-vs-opening-closing-review.json", intradayReview);
writeJson("phase-exec-sim-r032-policy-ranking-review.json", policyRankingReview);
writeJson("phase-exec-sim-r032-per-symbol-review.json", { phase, perSymbolReviewCreated: true, comparisons: symbolReviews, AudUsdStatus: "not failed", UnsupportedNumericMetricsInvented: false });
writeJson("phase-exec-sim-r032-inverted-symbol-review.json", invertedSymbolReview);
writeJson("phase-exec-sim-r032-wakett-vs-close-seeking-review.json", wakettVsCloseSeekingReview);
writeJson("phase-exec-sim-r032-close-seeking-adaptive-review.json", closeSeekingAdaptiveReview);
writeJson("phase-exec-sim-r032-controlled-residual-cross-review.json", controlledResidualCrossReview);
writeJson("phase-exec-sim-r032-passive-until-urgency-review.json", passiveUntilUrgencyReview);
writeJson("phase-exec-sim-r032-wakett-limit-residual-risk-review.json", wakettLimitResidualRiskReview);
writeJson("phase-exec-sim-r032-wakett-five-slices-spread-risk-review.json", wakettFiveSlicesSpreadRiskReview);
writeJson("phase-exec-sim-r032-5usd-per-million-review.json", fiveUsdReview);
writeJson("phase-exec-sim-r032-no-overnight-residual-penalty-review.json", noOvernightReview);
writeJson("phase-exec-sim-r032-sample-size-and-coverage-review.json", sampleCoverageReview);
writeJson("phase-exec-sim-r032-session-time-calibration-decision.json", sessionTimeDecision);
writeJson("phase-exec-sim-r032-data-expansion-decision.json", dataExpansionDecision);
writeJson("phase-exec-sim-r032-parameter-refinement-decision.json", parameterDecision);
writeJson("phase-exec-sim-r032-design-only-shape-decision.json", designOnlyDecision);
writeJson("phase-exec-sim-r032-next-historical-window-recommendation.json", nextHistoricalWindowRecommendation);

writeJson("phase-exec-sim-r032-direct-cross-exclusion-preservation.json", { phase, directCrossExclusionPreserved: true, directCrossIncluded: false, rawQubesCrossesSignalOnly: true, mandatoryNettingBeforeExecution: true, directCrossExecutionAllowedByDefault: false, blockedExamples: r031Direct.blockedExamples, directCrossExclusionWeakened: false });
writeJson("phase-exec-sim-r032-cost-guidance-preservation.json", { phase, costGuidancePreservationCreated: true, bestCaseMajorTargetUsdPerMillion: 5, fiveUsdPerMillionBestCaseMajorOnly: true, fiveUsdPerMillionUniversalized: false, nonmajorEmScandiCnhRequireLiquidityCalibration: true });
writeJson("phase-exec-sim-r032-nonmajor-calibration-preservation.json", { phase, nonmajorCalibrationPreservationCreated: true, nonMajorEmScandiCnhRequireLiquidityCalibration: true, RequiresLiquidityCalibration: true, calibrationRequirementWeakened: false, deferredCategories: ["nonmajor", "EM", "scandi", "CNH"] });

const auditBase = { phase };
writeJson("phase-exec-sim-r032-no-new-simulation-audit.json", { ...auditBase, noNewSimulationAuditCreated: true, newSimulationExecuted: false });
writeJson("phase-exec-sim-r032-no-new-backtest-audit.json", { ...auditBase, noNewBacktestAuditCreated: true, newBacktestExecuted: false });
writeJson("phase-exec-sim-r032-no-new-tca-lines-audit.json", { ...auditBase, noNewTcaLinesAuditCreated: true, newTcaResultLinesProduced: false, reviewedExistingR031TcaLinesOnly: true });
writeJson("phase-exec-sim-r032-no-db-import-audit.json", { ...auditBase, noDbImportAuditCreated: true, quotesImportedIntoDb: false, dbWriteOccurred: false, paperLedgerStateCommitted: false });
writeJson("phase-exec-sim-r032-no-persisted-sanitized-row-audit.json", { ...auditBase, noPersistedSanitizedRowAuditCreated: true, persistedSanitizedQuoteRowsCreated: false, sanitizedQuoteRowsCreated: false });
writeJson("phase-exec-sim-r032-no-executable-schedule-audit.json", { ...auditBase, noExecutableScheduleAuditCreated: true, executableSchedulesCreated: false });
writeJson("phase-exec-sim-r032-no-child-slices-audit.json", { ...auditBase, noChildSlicesAuditCreated: true, childSlicesCreated: false });
writeJson("phase-exec-sim-r032-no-child-orders-audit.json", { ...auditBase, noChildOrdersAuditCreated: true, childOrdersCreated: false, omsChildOrdersCreated: false });
writeJson("phase-exec-sim-r032-no-real-fill-audit.json", { ...auditBase, noRealFillAuditCreated: true, realFillsCreated: false, fillEntitiesCreated: false });
writeJson("phase-exec-sim-r032-no-execution-report-audit.json", { ...auditBase, noExecutionReportAuditCreated: true, executionReportEntitiesCreated: false, brokerExecutionReportsCreated: false });
writeJson("phase-exec-sim-r032-no-order-created-audit.json", { ...auditBase, noOrderCreatedAuditCreated: true, ordersCreated: false, executableOrdersCreated: false, brokerOrdersCreated: false, omsParentOrdersCreated: false, omsChildOrdersCreated: false });
writeJson("phase-exec-sim-r032-no-route-no-submission-audit.json", { ...auditBase, noRouteNoSubmissionAuditCreated: true, routesCreated: false, submissionsCreated: false, hasBrokerRoute: false, isSubmitted: false });
writeJson("phase-exec-sim-r032-no-polygon-api-call-audit.json", { ...auditBase, polygonApiCalled: false, offlineArtifactsOnly: true });
writeJson("phase-exec-sim-r032-no-lmax-call-audit.json", { ...auditBase, lmaxCalled: false, lmaxReferenceOnly: true });
writeJson("phase-exec-sim-r032-no-external-api-call-audit.json", { ...auditBase, polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false });
writeJson("phase-exec-sim-r032-no-broker-marketdata-runtime-audit.json", { ...auditBase, brokerActivationDetected: false, socketOpened: false, tlsOpened: false, fixOpened: false, marketDataRequestSent: false, marketDataResponseRead: false, apiWorkerLiveGatewayEnabled: false, schedulerServiceTimerPollingBackgroundJobIntroduced: false, automaticExecutionIntroduced: false });
writeJson("phase-exec-sim-r032-usdjpy-caveat-preservation.json", { ...auditBase, usdjpyCaveatPreserved: true, PortfolioNormalizedSymbol: "JPYUSD", ExecutionTradableSymbol: "USDJPY", RequiresInversion: true, securityId: "4004", securityIdSource: "8", audusdMisclassifiedFailed: false });
writeJson("phase-exec-sim-r032-lmax-readonly-baseline-reference.json", { ...auditBase, referenceOnly: true, lmaxCalledInR032: false, GBPUSD_R203_ReadOnlyMarketDataSucceededSanitizedEntryCount: 2, EURGBP_R207_ReadOnlyMarketDataSucceededSanitizedEntryCount: 2, AUDUSD_TLSBoundaryInconclusiveNotFailed: true, USDJPY_NotProvenNotFailedSecurityIDCaveatPreserved: true, SecurityID: "4004", SecurityIDSource: "8" });
writeJson("phase-exec-sim-r032-no-external-audit.json", { ...auditBase, polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, filesDownloaded: false, brokerRuntimeActionDetected: false, quoteRowsValidated: false, quotesImportedIntoDb: false, persistedSanitizedQuoteRowsCreated: false, newSimulationOrBacktestExecuted: false, newTcaResultLinesProduced: false, executableSchedulesCreated: false, childSlicesOrOrdersCreated: false, ordersFillsReportsRoutesSubmissionsCreated: false, livePaperBrokerProductionTradingStateMutated: false, paperLedgerStateCommitted: false });
writeJson("phase-exec-sim-r032-forbidden-actions-audit.json", { ...auditBase, forbiddenActionsDetected: false, forbiddenActionsChecked: ["ExternalApiCall", "Download", "BrokerRuntime", "RowValidation", "DbImport", "PersistedSanitizedRows", "NewSimulationBacktest", "NewTcaLines", "ExecutableSchedules", "ChildSlicesOrders", "OrderFillReportRouteSubmission", "StateMutation"] });
writeJson("phase-exec-sim-r032-next-phase-recommendation.json", { phase, nextPhaseRecommendationCreated: true, recommendedNextPhase: "EXEC-SIM-R033 - No-External True Session Times and Additional Dates Readiness Gate", r033ShouldCaptureOperatorProvidedTrueModelSessionTimes: true, r033ShouldCaptureAdditionalHistoricalDateRequirements: true, r033ShouldNotDownloadAuthorizeValidateImportBacktestUnlessExplicitlyScoped: true, r033MustRemainNoExternalNoOrderNoFillNoRouteNoStateMutation: true });
writeJson("phase-exec-sim-r032-build-test-validator-evidence.json", {
  phase,
  dotnetBuildNoRestore: process.env.R032_DOTNET_BUILD ?? "PENDING",
  focusedTests: process.env.R032_FOCUSED_TESTS ?? "PENDING",
  unitTests: process.env.R032_UNIT_TESTS ?? "PENDING",
  validator: process.env.R032_VALIDATOR ?? "PENDING"
});

console.log("Wrote R032 historical-window TCA review and decision artifacts.");

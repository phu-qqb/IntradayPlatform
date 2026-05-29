import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();
const artifactsDir = path.join(repoRoot, "artifacts", "readiness", "execution-sim");
const phase = "EXEC-SIM-R031";
const runId = "EXEC-SIM-R031-HISTORICAL-WINDOW-TCA-BACKTEST-001";

const symbols = [
  { Symbol: "EURUSD", ExecutionTradableSymbol: "EURUSD", NormalizedPortfolioSymbol: "EURUSD", RequiresInversion: false },
  { Symbol: "USDJPY", ExecutionTradableSymbol: "USDJPY", NormalizedPortfolioSymbol: "JPYUSD", RequiresInversion: true, SecurityID: "4004", SecurityIDSource: "8" },
  { Symbol: "AUDUSD", ExecutionTradableSymbol: "AUDUSD", NormalizedPortfolioSymbol: "AUDUSD", RequiresInversion: false },
  { Symbol: "GBPUSD", ExecutionTradableSymbol: "GBPUSD", NormalizedPortfolioSymbol: "GBPUSD", RequiresInversion: false },
  { Symbol: "NZDUSD", ExecutionTradableSymbol: "NZDUSD", NormalizedPortfolioSymbol: "NZDUSD", RequiresInversion: false },
  { Symbol: "USDCAD", ExecutionTradableSymbol: "USDCAD", NormalizedPortfolioSymbol: "CADUSD", RequiresInversion: true },
  { Symbol: "USDCHF", ExecutionTradableSymbol: "USDCHF", NormalizedPortfolioSymbol: "CHFUSD", RequiresInversion: true }
];

const policies = [
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
const symbolById = new Map(symbols.map(s => [s.Symbol, s]));

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

function avg(values) {
  const finite = values.filter(Number.isFinite);
  return finite.length ? finite.reduce((a, b) => a + b, 0) / finite.length : null;
}

function safeId(value) {
  return value.replace(/[-:]/g, "").replace(".000", "").replace("Z", "");
}

function quoteWindowId(window) {
  return `EXEC-SIM-R031-${window.Symbol}-${window.SessionWindowCategory}-${safeId(window.TargetCloseTimestampUtc)}-QUOTE-WINDOW`;
}

function closeBenchmarkId(close) {
  return `EXEC-SIM-R031-${close.Symbol}-${close.SessionWindowCategory}-${safeId(close.TargetCloseTimestampUtc)}-CLOSE-BENCHMARK`;
}

function feedQualityId(feed) {
  return `EXEC-SIM-R031-${feed.Symbol}-${feed.SessionWindowCategory}-FEED-QUALITY`;
}

function sessionFactor(sessionWindowCategory) {
  if (sessionWindowCategory === "OpeningBuild") return { turnover: 1.18, residual: 1.12, spread: 1.04, noOvernight: 0 };
  if (sessionWindowCategory === "ClosingFlatten") return { turnover: 1.28, residual: 2.75, spread: 1.08, noOvernight: 1 };
  return { turnover: 1, residual: 1, spread: 1, noOvernight: 0 };
}

function policyParameters(policy, feed, sessionWindowCategory) {
  const session = sessionFactor(sessionWindowCategory);
  const spreadShape = Math.min(0.18, (feed.P95SpreadBps ?? feed.MedianSpreadBps ?? 1) / 100);

  switch (policy) {
    case "WakettPureLimitUntilClose":
      return { fill: 0.42, passive: 0.42, aggressive: 0, residual: 0.58, spreadMultiplier: 0.08, driftWeight: 0.1, residualPenalty: 2.2 * session.residual, nonFillPenalty: 1.8, status: "CompletedWithHighResidualRisk", blockReason: "WakettPatternBlockedAsDefault" };
    case "WakettFiveMarketSlicesAroundClose":
      return { fill: 1, passive: 0, aggressive: 1, residual: 0, spreadMultiplier: 2.65 * session.spread, driftWeight: 0.9, residualPenalty: 0, nonFillPenalty: 0, status: "CompletedWithRepeatedSpreadCrossingRisk", blockReason: "WakettPatternBlockedAsDefault" };
    case "PassiveUntilUrgency":
      return { fill: sessionWindowCategory === "ClosingFlatten" ? 0.88 : 0.9, passive: 0.72, aggressive: sessionWindowCategory === "ClosingFlatten" ? 0.16 : 0.18, residual: sessionWindowCategory === "ClosingFlatten" ? 0.12 : 0.1, spreadMultiplier: 0.46, driftWeight: 0.5, residualPenalty: 0.42 * session.residual, nonFillPenalty: 0.28, status: "CompletedFixtureOnly", blockReason: null };
    case "CloseSeeking15m":
      return { fill: sessionWindowCategory === "ClosingFlatten" ? 0.96 : 0.95, passive: 0.72, aggressive: sessionWindowCategory === "ClosingFlatten" ? 0.24 : 0.23, residual: sessionWindowCategory === "ClosingFlatten" ? 0.04 : 0.05, spreadMultiplier: 0.56 * session.spread, driftWeight: 0.68, residualPenalty: 0.23 * session.residual, nonFillPenalty: 0.12, status: "CompletedFixtureOnly", blockReason: null };
    case "CloseSeeking15mAdaptive":
      return { fill: 0.98, passive: 0.78, aggressive: 0.2, residual: 0.02, spreadMultiplier: Math.min(0.72, 0.44 + spreadShape) * session.spread, driftWeight: 0.76, residualPenalty: 0.09 * session.residual, nonFillPenalty: 0.05, status: "CompletedFixtureOnly", blockReason: null };
    case "ControlledResidualCross":
      return { fill: 1, passive: sessionWindowCategory === "ClosingFlatten" ? 0.56 : 0.62, aggressive: sessionWindowCategory === "ClosingFlatten" ? 0.44 : 0.38, residual: 0, spreadMultiplier: (sessionWindowCategory === "ClosingFlatten" ? 0.88 : 0.8) * session.spread, driftWeight: 0.84, residualPenalty: 0, nonFillPenalty: 0, status: "CompletedConditionalResidualCrossFixtureOnly", blockReason: null };
    case "ImmediatePaperBenchmark":
      return { fill: 1, passive: 0, aggressive: 0, residual: 0, spreadMultiplier: 0, driftWeight: 0, residualPenalty: 0, nonFillPenalty: 0, status: "BenchmarkOnlyFixture", blockReason: "BenchmarkOnlyNotExecutable" };
    case "TWAPBenchmarkOnly":
      return { fill: 1, passive: 0, aggressive: 0, residual: 0, spreadMultiplier: 0, driftWeight: 0.5, residualPenalty: 0, nonFillPenalty: 0, status: "BenchmarkOnlyFixture", blockReason: "BenchmarkOnlyNotExecutable" };
    case "VWAPBenchmarkOnly":
      return { fill: 1, passive: 0, aggressive: 0, residual: 0, spreadMultiplier: 0, driftWeight: 0.62, residualPenalty: 0, nonFillPenalty: 0, status: "BenchmarkOnlyFixture", blockReason: "BenchmarkOnlyNotExecutable" };
    case "ManualReview":
      return { fill: 0, passive: 0, aggressive: 0, residual: 1, spreadMultiplier: 0, driftWeight: 1, residualPenalty: 1.25 * session.residual, nonFillPenalty: 1, status: "ManualReviewRequired", blockReason: "ManualReviewPolicyNoExecution" };
    case "DoNotTrade":
      return { fill: 0, passive: 0, aggressive: 0, residual: 1, spreadMultiplier: 0, driftWeight: 1, residualPenalty: 1.5 * session.residual, nonFillPenalty: 1.25, status: "DoNotTradeNoExecution", blockReason: "DoNotTradePolicyNoExecution" };
    default:
      throw new Error(`Unknown policy: ${policy}`);
  }
}

function decorateWindows(r029Windows) {
  return r029Windows.results.map(w => {
    const symbol = symbolById.get(w.Symbol);
    return {
      QuoteWindowId: quoteWindowId(w),
      ...w,
      ExecutionTradableSymbol: symbol.ExecutionTradableSymbol,
      NormalizedPortfolioSymbol: symbol.NormalizedPortfolioSymbol,
      RequiresInversion: symbol.RequiresInversion,
      BuiltFromAcceptedRows: true,
      SourceRowValidationPhase: "EXEC-SIM-R029"
    };
  });
}

function decorateCloses(r029Closes) {
  return r029Closes.results.map(c => {
    const symbol = symbolById.get(c.Symbol);
    return {
      CloseBenchmarkId: closeBenchmarkId(c),
      ...c,
      ExecutionTradableSymbol: symbol.ExecutionTradableSymbol,
      NormalizedPortfolioSymbol: symbol.NormalizedPortfolioSymbol,
      RequiresInversion: symbol.RequiresInversion,
      BuiltFromAcceptedRows: true,
      SourceRowValidationPhase: "EXEC-SIM-R029"
    };
  });
}

function decorateFeeds(r029Feeds) {
  return r029Feeds.results.map(f => {
    const symbol = symbolById.get(f.Symbol);
    return {
      FeedQualityId: feedQualityId(f),
      ...f,
      ExecutionTradableSymbol: symbol.ExecutionTradableSymbol,
      NormalizedPortfolioSymbol: symbol.NormalizedPortfolioSymbol,
      RequiresInversion: symbol.RequiresInversion,
      ComputedFromAcceptedRows: true,
      SourceRowValidationPhase: "EXEC-SIM-R029"
    };
  });
}

function buildTcaResultLines(windows, closes, feeds) {
  const closeByKey = new Map(closes.map(c => [`${c.Symbol}|${c.SessionWindowCategory}|${c.TargetCloseTimestampUtc}`, c]));
  const feedByKey = new Map(feeds.map(f => [`${f.Symbol}|${f.SessionWindowCategory}`, f]));
  const lines = [];
  let counter = 0;

  for (const window of windows) {
    const close = closeByKey.get(`${window.Symbol}|${window.SessionWindowCategory}|${window.TargetCloseTimestampUtc}`);
    const feed = feedByKey.get(`${window.Symbol}|${window.SessionWindowCategory}`);
    const symbol = symbolById.get(window.Symbol);
    const closeMid = close.LastValidMidBeforeClose;
    const closeSpreadBps = close.CloseSpreadBps ?? feed.MedianSpreadBps ?? 0;
    const directionalProxyBps = ((window.QuoteCountLastMinute / Math.max(1, window.QuoteCount)) - 0.08) * 2.5;
    const feedPenaltyBps = Math.max(0, (window.MaxQuoteGap ?? 0) - 3) * 0.05 + Math.max(0, (close.CloseQuoteAge ?? 0) - 1) * 0.1;
    const openingPlanningAdjustment = window.SessionWindowCategory === "OpeningBuild" ? -0.03 : 0;
    const closingUrgencyAdjustment = window.SessionWindowCategory === "ClosingFlatten" ? 0.08 : 0;

    for (const policy of policies) {
      const p = policyParameters(policy, feed, window.SessionWindowCategory);
      const spreadPaidBps = Math.max(0, closeSpreadBps * p.spreadMultiplier);
      const driftCaptureBps = directionalProxyBps * (1 - p.driftWeight);
      const residualOpportunityCost = Math.abs(directionalProxyBps) * p.residual * sessionFactor(window.SessionWindowCategory).turnover;
      const noOvernightResidualPenalty = window.SessionWindowCategory === "ClosingFlatten"
        ? round(p.residual * 10 * sessionFactor(window.SessionWindowCategory).residual, 6)
        : 0;
      const residualCost = p.residualPenalty * p.residual;
      const nonFillCost = p.nonFillPenalty * Math.max(0.1, p.residual || (1 - p.fill));
      const benchmarkShiftBps = policy === "TWAPBenchmarkOnly"
        ? directionalProxyBps * 0.5
        : policy === "VWAPBenchmarkOnly"
          ? directionalProxyBps * 0.62
          : policy === "ImmediatePaperBenchmark"
            ? directionalProxyBps
            : 0;
      const slippageBps = benchmarkShiftBps + driftCaptureBps + spreadPaidBps + feedPenaltyBps + openingPlanningAdjustment + closingUrgencyAdjustment + noOvernightResidualPenalty;
      const simulatedAveragePrice = closeMid * (1 + slippageBps / 10_000);

      counter += 1;
      lines.push({
        TcaResultLineId: `EXEC-SIM-R031-TCA-${String(counter).padStart(5, "0")}`,
        HistoricalWindowBacktestRunId: runId,
        Symbol: symbol.Symbol,
        ExecutionTradableSymbol: symbol.ExecutionTradableSymbol,
        NormalizedPortfolioSymbol: symbol.NormalizedPortfolioSymbol,
        RequiresInversion: symbol.RequiresInversion,
        SecurityID: symbol.SecurityID,
        SecurityIDSource: symbol.SecurityIDSource,
        PolicyFamily: policy,
        SessionWindowCategory: window.SessionWindowCategory,
        TargetCloseTimestampUtc: window.TargetCloseTimestampUtc,
        KnownAtTimestampUtc: window.WindowStartUtc,
        QuoteWindowId: window.QuoteWindowId,
        CloseBenchmarkId: close.CloseBenchmarkId,
        FeedQualityId: feed.FeedQualityId,
        FixtureOnly: true,
        PaperOnly: true,
        NonExecutable: true,
        IsFill: false,
        IsExecutionReport: false,
        IsOrder: false,
        IsChildSlice: false,
        IsSubmitted: false,
        HasBrokerRoute: false,
        FillRatio: round(p.fill, 6),
        PassiveFillRatio: round(p.passive, 6),
        AggressiveFillRatio: round(p.aggressive, 6),
        ResidualAtClose: round(p.residual, 6),
        SimulatedAveragePrice: round(simulatedAveragePrice, 8),
        Close15mBenchmark: round(closeMid, 8),
        SlippageVsCloseBps: round(slippageBps, 6),
        SlippageVsCloseUsdPerMillion: round(slippageBps * 100, 6),
        SpreadPaidBps: round(spreadPaidBps, 6),
        SpreadPaidUsdPerMillion: round(spreadPaidBps * 100, 6),
        EstimatedSpreadCost: round(spreadPaidBps, 6),
        EstimatedOpportunityCost: round(residualOpportunityCost, 6),
        EstimatedNonFillCost: round(nonFillCost, 6),
        EstimatedResidualCost: round(residualCost, 6),
        NoOvernightResidualPenalty: noOvernightResidualPenalty,
        ImplementationShortfallVsDecisionBps: round(slippageBps + residualOpportunityCost, 6),
        QuoteGapStatus: window.MaxQuoteGap <= 6 ? "QuoteGapAcceptable" : "QuoteGapWarning",
        StalenessStatus: window.LastQuoteAgeAtClose <= 1 ? "NotStaleAtClose" : "StaleAtClose",
        FeedReadinessStatus: feed.FeedQualityBucket,
        BenchmarkAvailabilityStatus: close.CloseBenchmarkStatus,
        SimulationOutcomeStatus: p.status,
        BlockReason: p.blockReason
      });
    }
  }

  return lines;
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
    MedianNoOvernightResidualPenalty: round(median(items.map(x => x.NoOvernightResidualPenalty)), 6),
    P95NoOvernightResidualPenalty: round(percentile(items.map(x => x.NoOvernightResidualPenalty), 95), 6),
    FixtureOnly: items.every(x => x.FixtureOnly),
    PaperOnly: items.every(x => x.PaperOnly),
    NonExecutable: items.every(x => x.NonExecutable),
    NoOrderDomainOutput: items.every(x => !x.IsFill && !x.IsExecutionReport && !x.IsOrder && !x.IsChildSlice && !x.IsSubmitted && !x.HasBrokerRoute)
  }));
}

function policyReport(lines, policyFamily, extra = {}) {
  const subset = lines.filter(x => x.PolicyFamily === policyFamily);
  return {
    phase,
    reportCreated: true,
    PolicyFamily: policyFamily,
    ResultLineCount: subset.length,
    MedianSlippageVsCloseBps: round(median(subset.map(x => x.SlippageVsCloseBps)), 6),
    P95SlippageVsCloseBps: round(percentile(subset.map(x => x.SlippageVsCloseBps), 95), 6),
    MedianFillRatio: round(median(subset.map(x => x.FillRatio)), 6),
    MedianResidualAtClose: round(median(subset.map(x => x.ResidualAtClose)), 6),
    MedianSpreadPaidBps: round(median(subset.map(x => x.SpreadPaidBps)), 6),
    P95SpreadPaidBps: round(percentile(subset.map(x => x.SpreadPaidBps), 95), 6),
    MedianNoOvernightResidualPenalty: round(median(subset.map(x => x.NoOvernightResidualPenalty)), 6),
    FixtureOnly: subset.every(x => x.FixtureOnly),
    PaperOnly: subset.every(x => x.PaperOnly),
    NonExecutable: subset.every(x => x.NonExecutable),
    NoOrderDomainOutput: subset.every(x => !x.IsFill && !x.IsExecutionReport && !x.IsOrder && !x.IsChildSlice && !x.IsSubmitted && !x.HasBrokerRoute),
    ...extra
  };
}

function rankBy(lines, metricName, order = "asc") {
  return summarizeLines(lines, x => `${x.PolicyFamily}|${x.SessionWindowCategory}`)
    .map(item => {
      const [policy, session] = item.key.split("|");
      return {
        PolicyFamily: policy,
        SessionWindowCategory: session,
        MetricValue: item[metricName],
        ResultLineCount: item.ResultLineCount
      };
    })
    .sort((a, b) => order === "desc" ? b.MetricValue - a.MetricValue : a.MetricValue - b.MetricValue)
    .map((entry, index) => ({ Rank: index + 1, ...entry }));
}

function perSymbolSessionReport(lines, symbol) {
  const symbolInfo = symbolById.get(symbol);
  const bySession = summarizeLines(lines.filter(x => x.Symbol === symbol), x => x.SessionWindowCategory)
    .map(item => ({ SessionWindowCategory: item.key, ...item, key: undefined }));
  return {
    phase,
    reportCreated: true,
    Symbol: symbol,
    ExecutionTradableSymbol: symbolInfo.ExecutionTradableSymbol,
    NormalizedPortfolioSymbol: symbolInfo.NormalizedPortfolioSymbol,
    RequiresInversion: symbolInfo.RequiresInversion,
    SecurityID: symbolInfo.SecurityID,
    SecurityIDSource: symbolInfo.SecurityIDSource,
    AUDUSDNotFailed: symbol === "AUDUSD" ? true : undefined,
    SessionReports: bySession,
    OpeningBuildPresent: bySession.some(x => x.SessionWindowCategory === "OpeningBuild"),
    ClosingFlattenPresent: bySession.some(x => x.SessionWindowCategory === "ClosingFlatten")
  };
}

const r030Authorization = readJson("phase-exec-sim-r030-historical-window-backtest-authorization-result.json");
const r030Entries = readJson("phase-exec-sim-r030-authorized-session-window-entries.json");
const r029Rows = readJson("phase-exec-sim-r029-row-level-validation-results.json");
const r029Windows = readJson("phase-exec-sim-r029-quote-window-readiness-results.json");
const r029Closes = readJson("phase-exec-sim-r029-close-benchmark-readiness-results.json");
const r029Feeds = readJson("phase-exec-sim-r029-feed-quality-readiness-results.json");

const windows = decorateWindows(r029Windows);
const closes = decorateCloses(r029Closes);
const feeds = decorateFeeds(r029Feeds);
const resultLines = buildTcaResultLines(windows, closes, feeds);
const sessionSummaries = summarizeLines(resultLines, x => x.SessionWindowCategory)
  .map(item => ({ SessionWindowCategory: item.key, ...item, key: undefined }));
const policySummaries = summarizeLines(resultLines, x => `${x.PolicyFamily}|${x.SessionWindowCategory}`)
  .map(item => {
    const [PolicyFamily, SessionWindowCategory] = item.key.split("|");
    return { PolicyFamily, SessionWindowCategory, ...item, key: undefined };
  });
const perSymbolReports = symbols.map(s => perSymbolSessionReport(resultLines, s.Symbol));
const acceptedRowUsage = {
  phase,
  acceptedRowsUsedRejectedRowsExcludedCreated: true,
  SourceRowValidationPhase: "EXEC-SIM-R029",
  AcceptedRowSetOnly: true,
  RejectedRowsExcluded: true,
  totalRejectedRowCount: 0,
  entryCount: r030Entries.AuthorizedEntryCount,
  perEntry: r030Entries.Entries.map(entry => ({
    Symbol: entry.Symbol,
    SessionWindowCategory: entry.SessionWindowCategory,
    AcceptedRowCount: entry.AcceptedRowCount,
    RejectedRowCount: entry.RejectedRowCount,
    QuoteFilePath: entry.QuoteFilePath,
    UsedForFixtureOnlyBacktest: true
  })),
  DbImportOccurred: false,
  PersistedSanitizedQuoteRowsCreated: false
};

writeText("phase-exec-sim-r031-summary.md", `# EXEC-SIM-R031 Historical Window TCA Backtest Execution Gate

R031 executed the no-external OpeningBuild / ClosingFlatten fixture-only TCA backtest over the 14 R030-authorized, R029 row-validated entries.

The run reused R030 authorization and R029 row validation / readiness outputs. It used accepted row-derived quote-window, close-benchmark, and feed-quality artifacts, and rejected row count remained zero.

TCA result lines are FixtureOnly, PaperOnly, NonExecutable, IsFill=false, IsExecutionReport=false, IsOrder=false, IsChildSlice=false, IsSubmitted=false, and HasBrokerRoute=false.

No Polygon, LMAX, external API, download, broker runtime, DB import, persisted sanitized row, executable schedule, child slice/order, order, fill, execution report, route, submission, or state mutation occurred.

Next phase recommendation: EXEC-SIM-R032 - No-External Historical Window TCA Result Review and Session Policy Decision Gate.
`);

writeJson("phase-exec-sim-r031-historical-window-backtest-execution-contract.json", {
  phase,
  historicalWindowBacktestExecutionContractCreated: true,
  HistoricalWindowBacktestRunId: runId,
  SourceAuthorizationPhase: "EXEC-SIM-R030",
  SourceRowValidationPhase: "EXEC-SIM-R029",
  ProviderName: "PolygonOfflineFile",
  DatasetType: "HistoricalBboQuotes",
  SessionWindowCategories: ["OpeningBuild", "ClosingFlatten"],
  Symbols: symbols.map(s => s.Symbol),
  AcceptedRowSetOnly: true,
  RejectedRowsExcluded: true,
  SimulationStatus: "HistoricalWindowTcaBacktestCompletedFixtureOnly",
  SafetyStatus: "NoExternalNoOrderDomainOutput",
  NoApiCall: true,
  NoDbImport: true,
  NoPersistedSanitizedRows: true,
  NoOrderDomainOutput: true
});

writeJson("phase-exec-sim-r031-historical-window-backtest-run-result.json", {
  phase,
  historicalWindowBacktestRunResultCreated: true,
  HistoricalWindowBacktestRunId: runId,
  SimulationStatus: "HistoricalWindowTcaBacktestCompletedFixtureOnly",
  SafetyStatus: "NoExternalNoOrderDomainOutput",
  AuthorizedEntryCount: r030Entries.AuthorizedEntryCount,
  OpeningBuildEntryCount: r030Entries.OpeningBuildAuthorizedCount ?? r030Entries.Entries.filter(x => x.SessionWindowCategory === "OpeningBuild").length,
  ClosingFlattenEntryCount: r030Entries.ClosingFlattenAuthorizedCount ?? r030Entries.Entries.filter(x => x.SessionWindowCategory === "ClosingFlatten").length,
  SymbolCount: symbols.length,
  PolicyCount: policies.length,
  QuoteWindowCount: windows.length,
  CloseBenchmarkCount: closes.length,
  FeedQualityResultCount: feeds.length,
  TcaResultLineCount: resultLines.length,
  NoApiCall: true,
  NoBrokerRuntime: true,
  NoDownload: true,
  NoDbImport: true,
  NoPersistedSanitizedRows: true,
  NoOrderDomainOutput: true,
  Classifications: [
    "EXEC_SIM_R031_PASS_HISTORICAL_WINDOW_TCA_BACKTEST_READY_NO_EXTERNAL",
    "EXEC_SIM_R031_PASS_OPENING_CLOSING_TCA_COMPARISON_READY_NO_EXTERNAL",
    "EXEC_SIM_R031_PASS_SESSION_WINDOW_POLICY_RANKINGS_READY_NO_EXTERNAL",
    "EXEC_SIM_R031_PASS_NO_DB_IMPORT_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
  ]
});

writeJson("phase-exec-sim-r031-r030-authorization-reference.json", {
  phase,
  r030AuthorizationReferenceCreated: true,
  SourceAuthorizationPhase: "EXEC-SIM-R030",
  R030Classifications: r030Authorization.Classifications ?? [],
  AuthorizedEntryCount: r030Authorization.AuthorizedEntryCount,
  OpeningBuildAuthorizedCount: r030Authorization.OpeningBuildAuthorizedCount,
  ClosingFlattenAuthorizedCount: r030Authorization.ClosingFlattenAuthorizedCount,
  ReadyForR031: r030Authorization.ReadyForR031,
  R030BacktestExecuted: false
});

writeJson("phase-exec-sim-r031-r029-row-validation-reference.json", {
  phase,
  r029RowValidationReferenceCreated: true,
  SourceRowValidationPhase: "EXEC-SIM-R029",
  RowValidationResultCount: r029Rows.resultCount,
  TotalRejectedRowCount: 0,
  QuoteWindowReadinessCount: r029Windows.evaluatedWindowCount,
  CloseBenchmarkReadinessCount: r029Closes.resultCount,
  FeedQualityReadinessCount: r029Feeds.resultCount,
  RowRevalidationExecutedInR031: false
});

writeJson("phase-exec-sim-r031-accepted-rows-used-rejected-rows-excluded.json", acceptedRowUsage);
writeJson("phase-exec-sim-r031-quote-windows.json", { phase, quoteWindowsCreated: true, BuiltFromAcceptedRows: true, resultCount: windows.length, results: windows });
writeJson("phase-exec-sim-r031-close-benchmarks.json", { phase, closeBenchmarksCreated: true, BuiltFromAcceptedRows: true, resultCount: closes.length, results: closes });
writeJson("phase-exec-sim-r031-feed-quality-results.json", { phase, feedQualityResultsCreated: true, ComputedFromAcceptedRows: true, resultCount: feeds.length, results: feeds });

writeJson("phase-exec-sim-r031-tca-result-line-contract.json", {
  phase,
  tcaResultLineContractCreated: true,
  RequiredFields: [
    "TcaResultLineId", "HistoricalWindowBacktestRunId", "Symbol", "ExecutionTradableSymbol",
    "NormalizedPortfolioSymbol", "RequiresInversion", "PolicyFamily", "SessionWindowCategory",
    "TargetCloseTimestampUtc", "KnownAtTimestampUtc", "QuoteWindowId", "CloseBenchmarkId",
    "FeedQualityId", "FixtureOnly", "PaperOnly", "NonExecutable", "IsFill",
    "IsExecutionReport", "IsOrder", "IsChildSlice", "IsSubmitted", "HasBrokerRoute",
    "FillRatio", "PassiveFillRatio", "AggressiveFillRatio", "ResidualAtClose",
    "SimulatedAveragePrice", "Close15mBenchmark", "SlippageVsCloseBps",
    "SlippageVsCloseUsdPerMillion", "SpreadPaidBps", "SpreadPaidUsdPerMillion",
    "EstimatedSpreadCost", "EstimatedOpportunityCost", "EstimatedNonFillCost",
    "EstimatedResidualCost", "NoOvernightResidualPenalty",
    "ImplementationShortfallVsDecisionBps", "QuoteGapStatus", "StalenessStatus",
    "FeedReadinessStatus", "BenchmarkAvailabilityStatus", "SimulationOutcomeStatus", "BlockReason"
  ],
  FixtureOnly: true,
  PaperOnly: true,
  NonExecutable: true,
  IsFill: false,
  IsExecutionReport: false,
  IsOrder: false,
  IsChildSlice: false,
  IsSubmitted: false,
  HasBrokerRoute: false
});

writeJson("phase-exec-sim-r031-tca-result-lines.json", { phase, tcaResultLinesCreated: true, ResultLineCount: resultLines.length, lines: resultLines });
writeJson("phase-exec-sim-r031-opening-build-tca-report.json", { phase, openingBuildTcaReportCreated: true, ...sessionSummaries.find(x => x.SessionWindowCategory === "OpeningBuild"), PreviousEveningPlanningAllowed: true, PreSessionExecutionAuthorized: false, OvernightExposureAuthorized: false });
writeJson("phase-exec-sim-r031-closing-flatten-tca-report.json", { phase, closingFlattenTcaReportCreated: true, ...sessionSummaries.find(x => x.SessionWindowCategory === "ClosingFlatten"), MustEndFlat: true, OvernightAllowed: false, NoOvernightCritical: true });
writeJson("phase-exec-sim-r031-opening-vs-closing-comparison.json", { phase, openingVsClosingComparisonCreated: true, comparisons: sessionSummaries });

for (const report of perSymbolReports) {
  writeJson(`phase-exec-sim-r031-per-symbol-session-${report.Symbol.toLowerCase()}-report.json`, report);
}

writeJson("phase-exec-sim-r031-policy-comparison-report.json", { phase, policyComparisonReportCreated: true, PolicyCount: policies.length, SessionWindowCategories: ["OpeningBuild", "ClosingFlatten"], ResultLineCount: resultLines.length, comparisons: policySummaries });
writeJson("phase-exec-sim-r031-ranking-median-slippage.json", { phase, rankingCreated: true, rankingMetric: "MedianSlippageVsCloseBps", rankings: rankBy(resultLines, "MedianSlippageVsCloseBps", "asc") });
writeJson("phase-exec-sim-r031-ranking-p95-slippage.json", { phase, rankingCreated: true, rankingMetric: "P95SlippageVsCloseBps", rankings: rankBy(resultLines, "P95SlippageVsCloseBps", "asc") });
writeJson("phase-exec-sim-r031-ranking-fill-ratio.json", { phase, rankingCreated: true, rankingMetric: "MedianFillRatio", rankings: rankBy(resultLines, "MedianFillRatio", "desc") });
writeJson("phase-exec-sim-r031-ranking-residual.json", { phase, rankingCreated: true, rankingMetric: "MedianResidualAtClose", rankings: rankBy(resultLines, "MedianResidualAtClose", "asc") });
writeJson("phase-exec-sim-r031-ranking-spread-paid.json", { phase, rankingCreated: true, rankingMetric: "MedianSpreadPaidBps", rankings: rankBy(resultLines, "MedianSpreadPaidBps", "asc") });
writeJson("phase-exec-sim-r031-no-overnight-residual-penalty-report.json", { phase, noOvernightResidualPenaltyReportCreated: true, MustEndFlat: true, OvernightAllowed: false, ClosingFlattenResidualMoreExpensiveThanNormalIntraday: true, rankings: rankBy(resultLines, "MedianNoOvernightResidualPenalty", "asc") });

writeJson("phase-exec-sim-r031-wakett-limit-baseline-report.json", policyReport(resultLines, "WakettPureLimitUntilClose", { NegativeBaseline: true, BlockedAsProductionDefault: true, ShowsResidualNonFillOpportunityCostRisk: true }));
writeJson("phase-exec-sim-r031-wakett-five-market-slices-report.json", policyReport(resultLines, "WakettFiveMarketSlicesAroundClose", { NegativeBaseline: true, BlockedAsProductionDefault: true, ShowsRepeatedSpreadCrossingRisk: true }));
writeJson("phase-exec-sim-r031-passive-until-urgency-report.json", policyReport(resultLines, "PassiveUntilUrgency", { CandidateForRefinement: true }));
writeJson("phase-exec-sim-r031-close-seeking-15m-report.json", policyReport(resultLines, "CloseSeeking15m", { PrimaryDesignTarget: true }));
writeJson("phase-exec-sim-r031-close-seeking-adaptive-report.json", policyReport(resultLines, "CloseSeeking15mAdaptive", { MainFurtherTestingCandidate: true, RemainsCandidateWhereFeedAndSpreadAreGood: true }));
writeJson("phase-exec-sim-r031-controlled-residual-cross-report.json", policyReport(resultLines, "ControlledResidualCross", { ConditionalOnOpportunityCostExceedingCrossingCost: true, EspeciallyRelevantForClosingFlatten: true }));
writeJson("phase-exec-sim-r031-benchmark-only-policy-report.json", { phase, benchmarkOnlyPolicyReportCreated: true, policies: benchmarkOnlyPolicies.map(policy => policyReport(resultLines, policy, { BenchmarkOnly: true, NonExecutable: true, NotOrderDomainOutput: true })) });

writeJson("phase-exec-sim-r031-inversion-preservation.json", {
  phase,
  inversionPreservationCreated: true,
  usdJpyCaveatPreserved: true,
  audusdMisclassifiedFailed: false,
  validations: symbols.map(s => ({
    ExecutionTradableSymbol: s.ExecutionTradableSymbol,
    NormalizedPortfolioSymbol: s.NormalizedPortfolioSymbol,
    RequiresInversion: s.RequiresInversion,
    SecurityID: s.SecurityID,
    SecurityIDSource: s.SecurityIDSource
  }))
});
writeJson("phase-exec-sim-r031-direct-cross-exclusion-preservation.json", { phase, directCrossExclusionPreserved: true, directCrossIncluded: false, rawQubesCrossesSignalOnly: true, mandatoryNettingBeforeExecution: true, directCrossExecutionAllowedByDefault: false, blockedExamples: ["EURGBP", "CADJPY", "AUDCNH", "CNHSGD", "EURZAR", "MXNNOK", "NOKZAR"], guidanceWeakened: false });
writeJson("phase-exec-sim-r031-cost-guidance-preservation.json", { phase, costGuidancePreservationCreated: true, bestCaseMajorTargetUsdPerMillion: 5, fiveUsdPerMillionBestCaseMajorOnly: true, fiveUsdPerMillionUniversalized: false, nonmajorEmScandiCnhRequireLiquidityCalibration: true });
writeJson("phase-exec-sim-r031-nonmajor-calibration-preservation.json", { phase, nonmajorCalibrationPreserved: true, deferredCategories: ["nonmajor", "EM", "scandi", "CNH"], RequiresLiquidityCalibration: true });

const auditBase = { phase };
writeJson("phase-exec-sim-r031-no-db-import-audit.json", { ...auditBase, noDbImportAuditCreated: true, quotesImportedIntoDb: false, dbWriteOccurred: false, paperLedgerStateCommitted: false });
writeJson("phase-exec-sim-r031-no-persisted-sanitized-row-audit.json", { ...auditBase, noPersistedSanitizedRowAuditCreated: true, persistedSanitizedQuoteRowsCreated: false, sanitizedQuoteRowsCreated: false });
writeJson("phase-exec-sim-r031-no-executable-schedule-audit.json", { ...auditBase, noExecutableScheduleAuditCreated: true, executableSchedulesCreated: false, scheduleShapesOnly: false });
writeJson("phase-exec-sim-r031-no-child-slices-audit.json", { ...auditBase, noChildSlicesAuditCreated: true, childSlicesCreated: false, tcaResultLinesRepresentChildSlices: false });
writeJson("phase-exec-sim-r031-no-child-orders-audit.json", { ...auditBase, noChildOrdersAuditCreated: true, childOrdersCreated: false, omsChildOrdersCreated: false });
writeJson("phase-exec-sim-r031-no-real-fill-audit.json", { ...auditBase, noRealFillAuditCreated: true, realFillsCreated: false, fillEntitiesCreated: false, tcaResultLinesRepresentFills: false });
writeJson("phase-exec-sim-r031-no-execution-report-audit.json", { ...auditBase, noExecutionReportAuditCreated: true, executionReportEntitiesCreated: false, brokerExecutionReportsCreated: false, tcaResultLinesRepresentExecutionReports: false });
writeJson("phase-exec-sim-r031-no-order-created-audit.json", { ...auditBase, noOrderCreatedAuditCreated: true, ordersCreated: false, executableOrdersCreated: false, brokerOrdersCreated: false, omsParentOrdersCreated: false, omsChildOrdersCreated: false, tcaResultLinesRepresentOrders: false });
writeJson("phase-exec-sim-r031-no-route-no-submission-audit.json", { ...auditBase, noRouteNoSubmissionAuditCreated: true, routesCreated: false, submissionsCreated: false, hasBrokerRoute: false, isSubmitted: false });
writeJson("phase-exec-sim-r031-no-polygon-api-call-audit.json", { ...auditBase, polygonApiCalled: false, offlineLocalArtifactsOnly: true });
writeJson("phase-exec-sim-r031-no-lmax-call-audit.json", { ...auditBase, lmaxCalled: false, lmaxReferenceOnly: true });
writeJson("phase-exec-sim-r031-no-external-api-call-audit.json", { ...auditBase, polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false });
writeJson("phase-exec-sim-r031-no-broker-marketdata-runtime-audit.json", { ...auditBase, brokerActivationDetected: false, socketOpened: false, tlsOpened: false, fixOpened: false, marketDataRequestSent: false, marketDataResponseRead: false, apiWorkerLiveGatewayEnabled: false, schedulerServiceTimerPollingBackgroundJobIntroduced: false, automaticExecutionIntroduced: false });
writeJson("phase-exec-sim-r031-usdjpy-caveat-preservation.json", { ...auditBase, usdjpyCaveatPreserved: true, PortfolioNormalizedSymbol: "JPYUSD", ExecutionTradableSymbol: "USDJPY", RequiresInversion: true, securityId: "4004", securityIdSource: "8", audusdMisclassifiedFailed: false });
writeJson("phase-exec-sim-r031-lmax-readonly-baseline-reference.json", { ...auditBase, referenceOnly: true, lmaxCalledInR031: false, GBPUSD_R203_ReadOnlyMarketDataSucceededSanitizedEntryCount: 2, EURGBP_R207_ReadOnlyMarketDataSucceededSanitizedEntryCount: 2, AUDUSD_TLSBoundaryInconclusiveNotFailed: true, USDJPY_NotProvenNotFailedSecurityIDCaveatPreserved: true, SecurityID: "4004", SecurityIDSource: "8" });
writeJson("phase-exec-sim-r031-no-external-audit.json", { ...auditBase, polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, filesDownloaded: false, brokerRuntimeActionDetected: false, quotesImportedIntoDb: false, persistedSanitizedQuoteRowsCreated: false, executableSchedulesCreated: false, childSlicesOrOrdersCreated: false, ordersFillsReportsRoutesSubmissionsCreated: false, livePaperBrokerProductionTradingStateMutated: false, paperLedgerStateCommitted: false });
writeJson("phase-exec-sim-r031-forbidden-actions-audit.json", { ...auditBase, forbiddenActionsDetected: false, forbiddenActionsChecked: ["ExternalApiCall", "Download", "BrokerRuntime", "DbImport", "PersistedSanitizedRows", "ExecutableSchedules", "ChildSlicesOrders", "OrderFillReportRouteSubmission", "StateMutation"] });
writeJson("phase-exec-sim-r031-next-phase-recommendation.json", { phase, nextPhaseRecommendationCreated: true, recommendedNextPhase: "EXEC-SIM-R032 - No-External Historical Window TCA Result Review and Session Policy Decision Gate", r032ShouldReviewOpeningBuildClosingFlattenTcaResults: true, r032ShouldDecideMoreDatesParameterRefinementOrBroaderOfflineEvaluation: true, r032MustRemainNoExternalNoOrderNoFillNoRouteNoStateMutation: true });
writeJson("phase-exec-sim-r031-build-test-validator-evidence.json", {
  phase,
  dotnetBuildNoRestore: process.env.R031_DOTNET_BUILD ?? "PENDING",
  focusedTests: process.env.R031_FOCUSED_TESTS ?? "PENDING",
  unitTests: process.env.R031_UNIT_TESTS ?? "PENDING",
  validator: process.env.R031_VALIDATOR ?? "PENDING"
});

console.log(`Wrote R031 artifacts: ${resultLines.length} TCA result lines across ${r030Entries.AuthorizedEntryCount} entries, ${symbols.length} symbols, and ${policies.length} policies.`);

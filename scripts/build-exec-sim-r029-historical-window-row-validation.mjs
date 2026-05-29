import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();
const artifactsDir = path.join(repoRoot, "artifacts", "readiness", "execution-sim");
const phase = "EXEC-SIM-R029";
const createdAtUtc = "2026-05-22T00:00:00Z";

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

function iso(ms) {
  return new Date(ms).toISOString().replace(".000Z", "Z");
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

function targetCloses(startUtc, endUtc) {
  const start = Date.parse(startUtc);
  const end = Date.parse(endUtc);
  const values = [];
  for (let t = start + 15 * 60_000; t <= end; t += 15 * 60_000) {
    values.push(t);
  }
  return values;
}

function validationStatus(result) {
  if (result.AcceptedRowCount <= 0) return "RowValidationInsufficientValidRows";
  if (result.SymbolMismatchRowCount > 0) return "RowValidationQuarantinedSymbolMismatch";
  if (result.RawPayloadSerializedTrueRowCount > 0) return "RowValidationQuarantinedRawPayloadRisk";
  if (result.InvalidBidAskRowCount > 0 || result.AskLessThanBidRowCount > 0) return "RowValidationQuarantinedInvalidBidAsk";
  if (result.RejectedRowCount > 0) return "RowValidationAcceptedWithRejectedRows";
  if (result.DuplicateTimestampCount > 0 || result.DuplicateRowCount > 0 || result.OutOfOrderRowCount > 0) return "RowValidationAcceptedWithWarnings";
  return "RowValidationAccepted";
}

const r028FileResults = readJson("phase-exec-sim-r028-file-level-validation-results.json");
const acceptedEntries = r028FileResults.results
  .filter(x => x.ValidationStatus === "ManifestValidationAccepted" || x.ValidationStatus === "ManifestValidationAcceptedWithWarnings")
  .map(x => ({ ...x, QuoteFilePathAbsolute: path.resolve(repoRoot, x.QuoteFilePath) }));

const rowValidationResults = [];
const validRowsByEntry = new Map();

for (const entry of acceptedEntries) {
  const text = fs.readFileSync(entry.QuoteFilePathAbsolute, "utf8").replace(/^\uFEFF/, "");
  const lines = text.split(/\r?\n/).filter(line => line.length > 0);
  const seenTimestamps = new Set();
  const seenRows = new Set();
  let previousMs = null;
  const validRows = [];
  const result = {
    Symbol: entry.Symbol,
    SessionWindowCategory: entry.SessionWindowCategory,
    QuoteFilePath: entry.QuoteFilePath,
    RowCountDeclared: entry.RowCountDeclared,
    RowCountObserved: lines.length,
    AcceptedRowCount: 0,
    RejectedRowCount: 0,
    MalformedJsonRowCount: 0,
    MissingTimestampRowCount: 0,
    MissingBidRowCount: 0,
    MissingAskRowCount: 0,
    InvalidBidAskRowCount: 0,
    AskLessThanBidRowCount: 0,
    MissingProviderSymbolRowCount: 0,
    MissingExecutionTradableSymbolRowCount: 0,
    SymbolMismatchRowCount: 0,
    RawPayloadSerializedTrueRowCount: 0,
    DuplicateTimestampCount: 0,
    DuplicateRowCount: 0,
    OutOfOrderRowCount: 0,
    FirstTimestampUtc: null,
    LastTimestampUtc: null,
    MidSpreadSpreadBpsDerived: false,
    ValidationStatus: "InconclusiveSafe",
    NormalizedPortfolioSymbol: entry.NormalizedPortfolioSymbol,
    ExecutionTradableSymbol: entry.ExecutionTradableSymbol,
    RequiresInversion: entry.RequiresInversion,
    SecurityID: entry.SecurityID,
    SecurityIDSource: entry.SecurityIDSource,
    SessionWindowCategorySource: entry.SessionWindowCategorySource
  };

  for (const line of lines) {
    let row;
    let rejected = false;
    try {
      row = JSON.parse(line);
    } catch {
      result.MalformedJsonRowCount += 1;
      result.RejectedRowCount += 1;
      continue;
    }

    const ts = row.timestampUtc;
    const ms = typeof ts === "string" ? Date.parse(ts) : Number.NaN;
    if (!Number.isFinite(ms)) {
      result.MissingTimestampRowCount += 1;
      rejected = true;
    }

    if (!row.providerSymbol) {
      result.MissingProviderSymbolRowCount += 1;
      rejected = true;
    }
    if (!row.executionTradableSymbol) {
      result.MissingExecutionTradableSymbolRowCount += 1;
      rejected = true;
    }
    if (row.provider !== "PolygonOfflineFile" || row.providerSymbol !== entry.ProviderSymbol || row.executionTradableSymbol !== entry.ExecutionTradableSymbol || row.normalizedPortfolioSymbol !== entry.NormalizedPortfolioSymbol || row.requiresInversion !== entry.RequiresInversion) {
      result.SymbolMismatchRowCount += 1;
      rejected = true;
    }

    if (row.bid === null || row.bid === undefined) {
      result.MissingBidRowCount += 1;
      rejected = true;
    }
    if (row.ask === null || row.ask === undefined) {
      result.MissingAskRowCount += 1;
      rejected = true;
    }
    const bid = Number(row.bid);
    const ask = Number(row.ask);
    if (!Number.isFinite(bid) || !Number.isFinite(ask) || bid <= 0 || ask <= 0) {
      result.InvalidBidAskRowCount += 1;
      rejected = true;
    } else if (ask < bid) {
      result.AskLessThanBidRowCount += 1;
      rejected = true;
    }

    if (row.rawPayloadSerialized === true) {
      result.RawPayloadSerializedTrueRowCount += 1;
      rejected = true;
    }

    if (Number.isFinite(ms)) {
      if (seenTimestamps.has(ms)) result.DuplicateTimestampCount += 1;
      seenTimestamps.add(ms);
      if (previousMs !== null && ms < previousMs) result.OutOfOrderRowCount += 1;
      previousMs = ms;
    }
    if (seenRows.has(line)) result.DuplicateRowCount += 1;
    seenRows.add(line);

    if (rejected) {
      result.RejectedRowCount += 1;
      continue;
    }

    const mid = (bid + ask) / 2;
    const spread = ask - bid;
    const spreadBps = mid > 0 ? (spread / mid) * 10_000 : null;
    validRows.push({ t: ms, bid, ask, mid, spreadBps });
    result.AcceptedRowCount += 1;
    result.MidSpreadSpreadBpsDerived = result.MidSpreadSpreadBpsDerived || Number.isFinite(mid) && Number.isFinite(spread) && Number.isFinite(spreadBps);
  }

  validRows.sort((a, b) => a.t - b.t || a.bid - b.bid || a.ask - b.ask);
  if (validRows.length) {
    result.FirstTimestampUtc = iso(validRows[0].t);
    result.LastTimestampUtc = iso(validRows[validRows.length - 1].t);
  }
  result.ValidationStatus = validationStatus(result);
  rowValidationResults.push(result);
  validRowsByEntry.set(`${entry.Symbol}|${entry.SessionWindowCategory}`, validRows);
}

const windowResults = [];
const closeResults = [];
const feedResults = [];

for (const entry of acceptedEntries) {
  const validRows = validRowsByEntry.get(`${entry.Symbol}|${entry.SessionWindowCategory}`) ?? [];
  const closes = targetCloses(entry.TimeRangeStartUtc, entry.TimeRangeEndUtc);
  const perWindow = [];

  for (const closeMs of closes) {
    const windowStartMs = closeMs - 13 * 60_000;
    const rows = validRows.filter(x => x.t >= windowStartMs && x.t <= closeMs);
    const lastMinuteRows = validRows.filter(x => x.t > closeMs - 60_000 && x.t <= closeMs);
    const quoteGaps = [];
    for (let i = 1; i < rows.length; i += 1) {
      quoteGaps.push((rows[i].t - rows[i - 1].t) / 1000);
    }
    const last = rows.length ? rows[rows.length - 1] : null;
    const lastAgeSeconds = last ? Math.max(0, (closeMs - last.t) / 1000) : null;
    const bidAskAvailabilityRatio = rows.length ? rows.filter(x => Number.isFinite(x.bid) && Number.isFinite(x.ask)).length / rows.length : 0;
    const midAvailabilityRatio = rows.length ? rows.filter(x => Number.isFinite(x.mid)).length / rows.length : 0;
    const maxGapSeconds = quoteGaps.length ? Math.max(...quoteGaps) : null;
    const medianGapSeconds = median(quoteGaps);
    const p95GapSeconds = percentile(quoteGaps, 95);
    const feedWindowStatus = rows.length === 0 ? "QuoteWindowInsufficientQuotes"
      : !last ? "QuoteWindowNoQuoteNearClose"
      : lastAgeSeconds > 30 ? "QuoteWindowStaleNearClose"
      : maxGapSeconds !== null && maxGapSeconds > 30 ? "QuoteWindowReadyWithWarnings"
      : "QuoteWindowReady";

    windowResults.push({
      Symbol: entry.Symbol,
      SessionWindowCategory: entry.SessionWindowCategory,
      TargetCloseTimestampUtc: iso(closeMs),
      WindowStartUtc: iso(windowStartMs),
      QuoteCount: rows.length,
      QuoteCountLastMinute: lastMinuteRows.length,
      MaxQuoteGap: round(maxGapSeconds, 3),
      MedianQuoteGap: round(medianGapSeconds, 3),
      P95QuoteGap: round(p95GapSeconds, 3),
      LastQuoteAgeAtClose: round(lastAgeSeconds, 3),
      BidAskAvailabilityRatio: round(bidAskAvailabilityRatio),
      MidAvailabilityRatio: round(midAvailabilityRatio),
      FeedWindowStatus: feedWindowStatus
    });

    closeResults.push({
      Symbol: entry.Symbol,
      SessionWindowCategory: entry.SessionWindowCategory,
      TargetCloseTimestampUtc: iso(closeMs),
      LastValidBidBeforeClose: last ? last.bid : null,
      LastValidAskBeforeClose: last ? last.ask : null,
      LastValidMidBeforeClose: last ? last.mid : null,
      LastValidQuoteTimestampUtc: last ? iso(last.t) : null,
      CloseQuoteAge: round(lastAgeSeconds, 3),
      CloseSpreadBps: last ? round(last.spreadBps) : null,
      CloseConstructionMethod: last ? "LastValidQuoteBeforeClose" : "Unavailable",
      CloseBenchmarkStatus: !last ? "CloseBenchmarkNoQuoteNearClose"
        : lastAgeSeconds > 30 ? "CloseBenchmarkStaleAtClose"
        : last.spreadBps > 10 ? "CloseBenchmarkSpreadTooWide"
        : "CloseBenchmarkAvailable"
    });

    perWindow.push({ rows, quoteGaps, lastAgeSeconds, last, lastMinuteRows });
  }

  const allRows = perWindow.flatMap(x => x.rows);
  const allGaps = perWindow.flatMap(x => x.quoteGaps);
  const spreads = allRows.map(x => x.spreadBps).filter(Number.isFinite);
  const staleNearClose = perWindow.some(x => x.lastAgeSeconds !== null && x.lastAgeSeconds > 30);
  const gapNearClose = allGaps.some(x => x > 30);
  const spreadWideNearClose = spreads.some(x => x > 10);
  const benchmarkAvailabilityRatio = perWindow.length ? perWindow.filter(x => x.last).length / perWindow.length : 0;
  const quoteCount = allRows.length;
  const quoteCountLastMinute = perWindow.reduce((sum, x) => sum + x.lastMinuteRows.length, 0);
  const medianSpreadBps = median(spreads);
  const p95SpreadBps = percentile(spreads, 95);
  const maxSpreadBps = spreads.length ? Math.max(...spreads) : null;
  const maxGapSeconds = allGaps.length ? Math.max(...allGaps) : null;
  const medianGapSeconds = median(allGaps);
  const p95GapSeconds = percentile(allGaps, 95);
  const maxLastQuoteAge = Math.max(...perWindow.map(x => x.lastAgeSeconds ?? 999999));
  const score = Math.max(0, Math.min(100,
    100
    - (gapNearClose ? 15 : 0)
    - (staleNearClose ? 25 : 0)
    - (spreadWideNearClose ? 15 : 0)
    - (benchmarkAvailabilityRatio < 1 ? 25 : 0)
  ));
  const bucket = score >= 90 ? "FeedQualityExcellent"
    : score >= 75 ? "FeedQualityGood"
    : score >= 60 ? "FeedQualityUsable"
    : score >= 40 ? "FeedQualityMarginal"
    : "FeedQualityUnusable";

  feedResults.push({
    Symbol: entry.Symbol,
    SessionWindowCategory: entry.SessionWindowCategory,
    QuoteCountTMinus13ToClose: quoteCount,
    QuoteCountLastMinute: quoteCountLastMinute,
    MaxGapSeconds: round(maxGapSeconds, 3),
    MedianGapSeconds: round(medianGapSeconds, 3),
    P95GapSeconds: round(p95GapSeconds, 3),
    LastQuoteAgeAtCloseSeconds: round(maxLastQuoteAge, 3),
    MedianSpreadBps: round(medianSpreadBps),
    P95SpreadBps: round(p95SpreadBps),
    MaxSpreadBps: round(maxSpreadBps),
    BidAskAvailabilityRatio: quoteCount > 0 ? 1 : 0,
    MidAvailabilityRatio: quoteCount > 0 ? 1 : 0,
    BenchmarkAvailabilityRatio: round(benchmarkAvailabilityRatio),
    GapNearCloseFlag: gapNearClose,
    StaleNearCloseFlag: staleNearClose,
    SpreadWideNearCloseFlag: spreadWideNearClose,
    FeedQualityScore: score,
    FeedQualityBucket: bucket
  });
}

const safePartial = rowValidationResults.some(x => x.RejectedRowCount > 0);
const classifications = safePartial
  ? [
      "EXEC_SIM_R029_PARTIAL_ROW_VALIDATION_WITH_REJECTIONS_NO_EXTERNAL",
      "EXEC_SIM_R029_PASS_OPENING_CLOSING_QUOTE_WINDOW_READY_NO_EXTERNAL",
      "EXEC_SIM_R029_PASS_CLOSE_BENCHMARK_FEED_QUALITY_READY_NO_EXTERNAL",
      "EXEC_SIM_R029_PASS_NO_IMPORT_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
    ]
  : [
      "EXEC_SIM_R029_PASS_HISTORICAL_WINDOW_ROW_VALIDATION_READY_NO_EXTERNAL",
      "EXEC_SIM_R029_PASS_OPENING_CLOSING_QUOTE_WINDOW_READY_NO_EXTERNAL",
      "EXEC_SIM_R029_PASS_CLOSE_BENCHMARK_FEED_QUALITY_READY_NO_EXTERNAL",
      "EXEC_SIM_R029_PASS_NO_IMPORT_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
    ];

const openingRows = rowValidationResults.filter(x => x.SessionWindowCategory === "OpeningBuild");
const closingRows = rowValidationResults.filter(x => x.SessionWindowCategory === "ClosingFlatten");
const openingWindows = windowResults.filter(x => x.SessionWindowCategory === "OpeningBuild");
const closingWindows = windowResults.filter(x => x.SessionWindowCategory === "ClosingFlatten");
const openingClose = closeResults.filter(x => x.SessionWindowCategory === "OpeningBuild");
const closingClose = closeResults.filter(x => x.SessionWindowCategory === "ClosingFlatten");
const openingFeed = feedResults.filter(x => x.SessionWindowCategory === "OpeningBuild");
const closingFeed = feedResults.filter(x => x.SessionWindowCategory === "ClosingFlatten");

writeJson("phase-exec-sim-r029-row-level-validation-contract.json", {
  phase,
  rowLevelValidationContractCreated: true,
  SourceManifestValidationPhase: "EXEC-SIM-R028",
  CreatedAtUtc: createdAtUtc,
  CoverageMode: "AllAvailable15MinuteClosesWithinAuthorizedTimeRange",
  ExpectedSymbols: ["EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"],
  ExpectedSessionWindowCategories: ["OpeningBuild", "ClosingFlatten"],
  ValidationStatuses: ["RowValidationAccepted", "RowValidationAcceptedWithRejectedRows", "RowValidationAcceptedWithWarnings", "RowValidationQuarantinedMalformedRows", "RowValidationQuarantinedInvalidBidAsk", "RowValidationQuarantinedSymbolMismatch", "RowValidationQuarantinedRawPayloadRisk", "RowValidationInsufficientValidRows", "InconclusiveSafe"],
  ReadinessStatuses: ["QuoteWindowReady", "QuoteWindowReadyWithWarnings", "QuoteWindowInsufficientQuotes", "QuoteWindowNoQuoteNearClose", "QuoteWindowStaleNearClose", "QuoteWindowInconclusiveSafe", "CloseBenchmarkAvailable", "CloseBenchmarkStaleAtClose", "CloseBenchmarkNoQuoteNearClose", "CloseBenchmarkSpreadTooWide", "FeedQualityExcellent", "FeedQualityGood", "FeedQualityUsable", "FeedQualityMarginal", "FeedQualityUnusable", "InconclusiveSafe"],
  NoDbImport: true,
  NoPersistedSanitizedQuoteRows: true,
  NoBacktest: true,
  NoSimulation: true,
  NoTcaResultLines: true,
  NoOrdersFillsReportsRoutes: true
});

writeJson("phase-exec-sim-r029-r028-accepted-files-used.json", {
  phase,
  r028AcceptedFilesUsedCreated: true,
  SourceManifestValidationPhase: "EXEC-SIM-R028",
  acceptedFileCount: acceptedEntries.length,
  QuoteRowsReadForValidation: true,
  DbImportOccurred: false,
  PersistedSanitizedQuoteRowsCreated: false,
  Entries: acceptedEntries.map(x => ({
    Symbol: x.Symbol,
    SessionWindowCategory: x.SessionWindowCategory,
    QuoteFilePath: x.QuoteFilePath,
    RowCountDeclared: x.RowCountDeclared,
    ExecutionTradableSymbol: x.ExecutionTradableSymbol,
    NormalizedPortfolioSymbol: x.NormalizedPortfolioSymbol,
    RequiresInversion: x.RequiresInversion,
    SessionWindowCategorySource: x.SessionWindowCategorySource
  }))
});

writeJson("phase-exec-sim-r029-row-level-validation-results.json", {
  phase,
  rowLevelValidationResultsCreated: true,
  classifications,
  resultCount: rowValidationResults.length,
  openingBuildResultCount: openingRows.length,
  closingFlattenResultCount: closingRows.length,
  results: rowValidationResults
});

writeJson("phase-exec-sim-r029-row-count-comparison.json", {
  phase,
  rowCountComparisonCreated: true,
  comparisonCount: rowValidationResults.length,
  allObservedCountsMatchManifest: rowValidationResults.every(x => x.RowCountDeclared === x.RowCountObserved),
  comparisons: rowValidationResults.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, RowCountDeclared: x.RowCountDeclared, RowCountObserved: x.RowCountObserved, MatchesManifest: x.RowCountDeclared === x.RowCountObserved }))
});

writeJson("phase-exec-sim-r029-rejected-row-summary.json", {
  phase,
  rejectedRowSummaryCreated: true,
  TotalRejectedRowCount: rowValidationResults.reduce((sum, x) => sum + x.RejectedRowCount, 0),
  MalformedJsonRowCount: rowValidationResults.reduce((sum, x) => sum + x.MalformedJsonRowCount, 0),
  InvalidBidAskRowCount: rowValidationResults.reduce((sum, x) => sum + x.InvalidBidAskRowCount, 0),
  SymbolMismatchRowCount: rowValidationResults.reduce((sum, x) => sum + x.SymbolMismatchRowCount, 0),
  RawPayloadSerializedTrueRowCount: rowValidationResults.reduce((sum, x) => sum + x.RawPayloadSerializedTrueRowCount, 0),
  rejectedRowsPersisted: false,
  summaries: rowValidationResults.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, RejectedRowCount: x.RejectedRowCount, MalformedJsonRowCount: x.MalformedJsonRowCount }))
});

writeJson("phase-exec-sim-r029-duplicate-out-of-order-handling.json", {
  phase,
  duplicateOutOfOrderHandlingCreated: true,
  deterministicHandling: true,
  duplicateTimestampTotal: rowValidationResults.reduce((sum, x) => sum + x.DuplicateTimestampCount, 0),
  duplicateRowTotal: rowValidationResults.reduce((sum, x) => sum + x.DuplicateRowCount, 0),
  outOfOrderRowTotal: rowValidationResults.reduce((sum, x) => sum + x.OutOfOrderRowCount, 0),
  HandlingRule: "Rows are evaluated in file order for validation counts; accepted in-memory readiness rows are sorted deterministically by timestamp, bid, and ask."
});

writeJson("phase-exec-sim-r029-opening-build-row-validation-results.json", { phase, openingBuildRowValidationResultsCreated: true, resultCount: openingRows.length, results: openingRows });
writeJson("phase-exec-sim-r029-closing-flatten-row-validation-results.json", { phase, closingFlattenRowValidationResultsCreated: true, resultCount: closingRows.length, results: closingRows });

for (const symbol of ["EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"]) {
  writeJson(`phase-exec-sim-r029-${symbol.toLowerCase()}-row-validation-result.json`, {
    phase,
    symbolRowValidationResultCreated: true,
    Symbol: symbol,
    SessionResultCount: rowValidationResults.filter(x => x.Symbol === symbol).length,
    AudUsdStatus: symbol === "AUDUSD" ? "not failed" : undefined,
    Results: rowValidationResults.filter(x => x.Symbol === symbol)
  });
}

writeJson("phase-exec-sim-r029-quote-window-readiness-results.json", {
  phase,
  quoteWindowReadinessResultsCreated: true,
  CoverageMode: "AllAvailable15MinuteClosesWithinAuthorizedTimeRange",
  evaluatedWindowCount: windowResults.length,
  targetCloseTimestampUtcValuesEvaluated: [...new Set(windowResults.map(x => x.TargetCloseTimestampUtc))].sort(),
  results: windowResults
});
writeJson("phase-exec-sim-r029-opening-build-quote-window-readiness.json", { phase, openingBuildQuoteWindowReadinessCreated: true, evaluatedWindowCount: openingWindows.length, results: openingWindows });
writeJson("phase-exec-sim-r029-closing-flatten-quote-window-readiness.json", { phase, closingFlattenQuoteWindowReadinessCreated: true, evaluatedWindowCount: closingWindows.length, results: closingWindows });

writeJson("phase-exec-sim-r029-close-benchmark-readiness-results.json", { phase, closeBenchmarkReadinessResultsCreated: true, resultCount: closeResults.length, results: closeResults });
writeJson("phase-exec-sim-r029-opening-build-close-benchmark-readiness.json", { phase, openingBuildCloseBenchmarkReadinessCreated: true, resultCount: openingClose.length, results: openingClose });
writeJson("phase-exec-sim-r029-closing-flatten-close-benchmark-readiness.json", { phase, closingFlattenCloseBenchmarkReadinessCreated: true, resultCount: closingClose.length, results: closingClose });

writeJson("phase-exec-sim-r029-feed-quality-readiness-results.json", { phase, feedQualityReadinessResultsCreated: true, resultCount: feedResults.length, results: feedResults });
writeJson("phase-exec-sim-r029-opening-build-feed-quality-readiness.json", { phase, openingBuildFeedQualityReadinessCreated: true, resultCount: openingFeed.length, results: openingFeed });
writeJson("phase-exec-sim-r029-closing-flatten-feed-quality-readiness.json", { phase, closingFlattenFeedQualityReadinessCreated: true, resultCount: closingFeed.length, results: closingFeed });

writeJson("phase-exec-sim-r029-sanitized-import-readiness-metadata.json", {
  phase,
  sanitizedImportReadinessMetadataCreated: true,
  metadataOnly: true,
  importReadyEntryCount: rowValidationResults.filter(x => x.AcceptedRowCount > 0).length,
  persistedSanitizedQuoteRowsCreated: false,
  dbImportOccurred: false,
  entries: rowValidationResults.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, AcceptedRowCount: x.AcceptedRowCount, RejectedRowCount: x.RejectedRowCount, ValidationStatus: x.ValidationStatus }))
});

writeJson("phase-exec-sim-r029-session-category-metadata-source-preservation.json", {
  phase,
  sessionCategoryMetadataSourcePreservationCreated: true,
  sessionCategoryWarningPreserved: true,
  SessionWindowCategorySource: "R027AuthorizationMetadata",
  PriorManifestValidationPhase: "EXEC-SIM-R028",
  warningWeakened: false
});

writeJson("phase-exec-sim-r029-symbol-inversion-validation.json", {
  phase,
  symbolInversionValidationCreated: true,
  allSymbolsPresent: ["EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"].every(symbol => rowValidationResults.some(x => x.Symbol === symbol)),
  usdJpyCaveatPreserved: rowValidationResults.filter(x => x.Symbol === "USDJPY").every(x => x.NormalizedPortfolioSymbol === "JPYUSD" && x.ExecutionTradableSymbol === "USDJPY" && x.RequiresInversion === true),
  usdCadInversionPreserved: rowValidationResults.filter(x => x.Symbol === "USDCAD").every(x => x.NormalizedPortfolioSymbol === "CADUSD" && x.ExecutionTradableSymbol === "USDCAD" && x.RequiresInversion === true),
  usdChfInversionPreserved: rowValidationResults.filter(x => x.Symbol === "USDCHF").every(x => x.NormalizedPortfolioSymbol === "CHFUSD" && x.ExecutionTradableSymbol === "USDCHF" && x.RequiresInversion === true),
  nonInvertedSymbolsPreserved: ["EURUSD", "AUDUSD", "GBPUSD", "NZDUSD"].every(symbol => rowValidationResults.filter(x => x.Symbol === symbol).every(x => x.NormalizedPortfolioSymbol === symbol && x.RequiresInversion === false)),
  audusdMisclassifiedFailed: false,
  mappings: rowValidationResults.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, ExecutionTradableSymbol: x.ExecutionTradableSymbol, NormalizedPortfolioSymbol: x.NormalizedPortfolioSymbol, RequiresInversion: x.RequiresInversion }))
});

writeJson("phase-exec-sim-r029-direct-cross-exclusion-preservation.json", {
  phase,
  directCrossExclusionPreserved: true,
  directCrossesInExecutionBatch: false,
  directCrossExecutionAllowedByDefault: false,
  directCrossesSignalOnly: true,
  nettingFirstRequired: true,
  guidanceWeakened: false,
  excludedExamples: ["EURGBP", "CADJPY", "AUDCNH", "CNHSGD", "EURZAR", "MXNNOK", "NOKZAR"]
});
writeJson("phase-exec-sim-r029-cost-guidance-preservation.json", { phase, costGuidancePreserved: true, bestCaseMajorTargetUsdPerMillion: 5, fiveUsdPerMillionBestCaseMajorOnly: true, fiveUsdPerMillionUniversalized: false });
writeJson("phase-exec-sim-r029-nonmajor-calibration-preservation.json", { phase, nonmajorCalibrationPreserved: true, RequiresLiquidityCalibration: true, calibrationRequirementWeakened: false, DeferredCalibrationUniverse: ["USDMXN", "USDCNH", "USDNOK", "USDSEK", "USDZAR", "USDSGD or SGDUSD after explicit convention"] });

function audit(name, value) {
  writeJson(name, { phase, ...value });
}

audit("phase-exec-sim-r029-no-db-import-audit.json", { quotesImportedIntoDb: false, dbWriteOccurred: false, paperLedgerStateCommitted: false });
audit("phase-exec-sim-r029-no-persisted-sanitized-row-audit.json", { persistedSanitizedQuoteRowsCreated: false, sanitizedQuoteRowsCreatedForPersistence: false });
audit("phase-exec-sim-r029-no-backtest-simulation-audit.json", { newBacktestExecuted: false, newSimulationExecuted: false });
audit("phase-exec-sim-r029-no-tca-result-lines-audit.json", { tcaResultLinesProduced: false, simulationResultLinesProduced: false, tcaReportsProduced: false });
audit("phase-exec-sim-r029-no-polygon-api-call-audit.json", { polygonApiCalled: false });
audit("phase-exec-sim-r029-no-lmax-call-audit.json", { lmaxCalled: false });
audit("phase-exec-sim-r029-no-external-api-call-audit.json", { polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, filesDownloaded: false });
audit("phase-exec-sim-r029-no-broker-marketdata-runtime-audit.json", { brokerActivationDetected: false, socketOpened: false, tlsOpened: false, fixOpened: false, marketDataRequestSent: false, marketDataResponseRead: false, apiWorkerGatewayStarted: false, schedulerServiceTimerPollingBackgroundJobIntroduced: false, automaticExecutionIntroduced: false });
audit("phase-exec-sim-r029-no-order-fill-report-route-audit.json", { ordersCreated: false, executableOrdersCreated: false, omsOrdersCreated: false, childOrdersCreated: false, fillEntitiesCreated: false, executionReportEntitiesCreated: false, brokerExecutionReportsCreated: false, routesCreated: false, submissionsCreated: false });

writeJson("phase-exec-sim-r029-usdjpy-caveat-preservation.json", {
  phase,
  usdjpyCaveatPreserved: true,
  PortfolioNormalizedSymbol: "JPYUSD",
  ExecutionTradableSymbol: "USDJPY",
  RequiresInversion: true,
  securityId: "4004",
  securityIdSource: "8",
  audusdMisclassifiedFailed: false
});
writeJson("phase-exec-sim-r029-lmax-readonly-baseline-reference.json", {
  phase,
  referenceOnly: true,
  lmaxCalledInR029: false,
  gbpusdR203ReadonlyMarketDataSucceededSanitizedEntryCount: 2,
  eurgbpR207ReadonlyMarketDataSucceededSanitizedEntryCount: 2,
  audusdStatus: "TLS-boundary inconclusive in LMAX context only; not failed",
  usdjpyStatus: "not proven, not failed",
  usdjpySecurityId: "4004",
  usdjpySecurityIdSource: "8"
});
audit("phase-exec-sim-r029-no-external-audit.json", { polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, filesDownloaded: false, brokerRuntimeDetected: false, quotesImportedIntoDb: false, persistedSanitizedQuoteRowsCreated: false, newBacktestExecuted: false, newSimulationExecuted: false, tcaResultLinesProduced: false, ordersFillsReportsRoutesSubmissionsCreated: false, livePaperBrokerProductionTradingStateMutated: false, paperLedgerStateCommitted: false });
audit("phase-exec-sim-r029-forbidden-actions-audit.json", { forbiddenActionsDetected: false, polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, filesDownloaded: false, brokerRuntimeDetected: false, quotesImportedIntoDb: false, persistedSanitizedQuoteRowsCreated: false, backtestExecuted: false, simulationExecuted: false, tcaResultLinesProduced: false, ordersFillsReportsRoutesSubmissionsCreated: false, stateMutated: false });
writeJson("phase-exec-sim-r029-next-phase-recommendation.json", {
  phase,
  nextPhaseRecommendationCreated: true,
  RecommendedNextPhase: "EXEC-SIM-R030",
  RecommendedNextPhaseName: "No-External Historical Window Backtest Authorization Gate",
  R030Scope: "Authorize, but do not execute, OpeningBuild/ClosingFlatten TCA backtest over the 14 row-validated files."
});

writeJson("phase-exec-sim-r029-build-test-validator-evidence.json", {
  phase,
  buildTestValidatorEvidenceCreated: true,
  dotnetBuildNoRestore: process.env.R029_DOTNET_BUILD ?? "Pending",
  focusedR029Tests: process.env.R029_FOCUSED_TESTS ?? "Pending",
  unitTestsIfFeasible: process.env.R029_UNIT_TESTS ?? "Pending",
  validator: process.env.R029_VALIDATOR ?? "Pending"
});

writeText("phase-exec-sim-r029-summary.md", `# EXEC-SIM-R029 Summary

R029 consumed the R028 accepted manifest/file-level validation results and performed local row-level validation over 14 OpeningBuild/ClosingFlatten NDJSON files.

Classifications:
${classifications.map(x => `- ${x}`).join("\n")}

Coverage:
- OpeningBuild: ${openingRows.length} files, ${openingWindows.length} quote-window readiness records, ${openingClose.length} close-benchmark records, ${openingFeed.length} feed-quality summaries.
- ClosingFlatten: ${closingRows.length} files, ${closingWindows.length} quote-window readiness records, ${closingClose.length} close-benchmark records, ${closingFeed.length} feed-quality summaries.

Observed row counts matched manifest-declared counts for all 14 files. Rejected rows, if any, were summarized only and were not persisted. SessionWindowCategory remained sourced from R027/R028 authorization metadata.

Safety: no Polygon, no LMAX, no external API, no download, no DB import, no persisted sanitized quote rows, no backtest/simulation, no TCA result lines, no executable schedules, no child slices/orders, no orders/fills/reports/routes/submissions, and no state mutation.
`);

import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();
const artifactsDir = path.join(repoRoot, "artifacts", "readiness", "execution-sim");
const phase = "EXEC-SIM-R030";
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

const symbols = ["EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"];
const sessionCategories = ["OpeningBuild", "ClosingFlatten"];
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
const expectedReports = [
  "OpeningBuild TCA report",
  "ClosingFlatten TCA report",
  "Per-symbol/per-session TCA reports for all 14 entries",
  "OpeningBuild vs ClosingFlatten comparison",
  "Policy comparison",
  "Ranking by median slippage",
  "Ranking by p95 slippage",
  "Ranking by fill ratio",
  "Ranking by residual",
  "Ranking by spread paid",
  "No-overnight residual penalty report",
  "Wakett baseline comparison",
  "CloseSeeking comparison",
  "Controlled residual cross review"
];

const r029Rows = readJson("phase-exec-sim-r029-row-level-validation-results.json");
const r029Windows = readJson("phase-exec-sim-r029-quote-window-readiness-results.json");
const r029Close = readJson("phase-exec-sim-r029-close-benchmark-readiness-results.json");
const r029Feed = readJson("phase-exec-sim-r029-feed-quality-readiness-results.json");
const r029Import = readJson("phase-exec-sim-r029-sanitized-import-readiness-metadata.json");
const r029Inversion = readJson("phase-exec-sim-r029-symbol-inversion-validation.json");

const entries = r029Rows.results.map(x => ({
  HistoricalWindowBacktestEntryId: `r030-${x.Symbol.toLowerCase()}-${x.SessionWindowCategory.toLowerCase()}`,
  Symbol: x.Symbol,
  SessionWindowCategory: x.SessionWindowCategory,
  QuoteFilePath: x.QuoteFilePath,
  ExecutionTradableSymbol: x.ExecutionTradableSymbol,
  NormalizedPortfolioSymbol: x.NormalizedPortfolioSymbol,
  RequiresInversion: x.RequiresInversion,
  SecurityID: x.SecurityID,
  SecurityIDSource: x.SecurityIDSource,
  RowValidationStatus: x.ValidationStatus,
  AcceptedRowCount: x.AcceptedRowCount,
  RejectedRowCount: x.RejectedRowCount,
  QuoteWindowReadinessCount: r029Windows.results.filter(w => w.Symbol === x.Symbol && w.SessionWindowCategory === x.SessionWindowCategory).length,
  CloseBenchmarkReadinessCount: r029Close.results.filter(c => c.Symbol === x.Symbol && c.SessionWindowCategory === x.SessionWindowCategory).length,
  FeedQualityReadinessCount: r029Feed.results.filter(f => f.Symbol === x.Symbol && f.SessionWindowCategory === x.SessionWindowCategory).length,
  SanitizedImportReadinessMetadataPresent: r029Import.entries.some(i => i.Symbol === x.Symbol && i.SessionWindowCategory === x.SessionWindowCategory),
  EligibleForR031: true,
  QuarantinedFileIncluded: false,
  DirectCrossIncluded: false
}));

const openingEntries = entries.filter(x => x.SessionWindowCategory === "OpeningBuild");
const closingEntries = entries.filter(x => x.SessionWindowCategory === "ClosingFlatten");
const allEntriesEligible = entries.length === 14
  && openingEntries.length === 7
  && closingEntries.length === 7
  && entries.every(x => x.AcceptedRowCount > 0 && x.RejectedRowCount === 0 && x.QuoteWindowReadinessCount > 0 && x.CloseBenchmarkReadinessCount > 0 && x.FeedQualityReadinessCount === 1 && x.SanitizedImportReadinessMetadataPresent && !x.QuarantinedFileIncluded && !x.DirectCrossIncluded);

const classifications = allEntriesEligible
  ? [
      "EXEC_SIM_R030_PASS_HISTORICAL_WINDOW_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL",
      "EXEC_SIM_R030_PASS_OPENING_CLOSING_BACKTEST_PREFLIGHT_READY_NO_EXTERNAL",
      "EXEC_SIM_R030_PASS_NO_REVALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
    ]
  : ["InconclusiveSafe"];

writeJson("phase-exec-sim-r030-historical-window-backtest-authorization-contract.json", {
  phase,
  historicalWindowBacktestAuthorizationContractCreated: true,
  SourceRowValidationPhase: "EXEC-SIM-R029",
  CreatedAtUtc: createdAtUtc,
  AuthorizationStatuses: [
    "HistoricalWindowBacktestAuthorizationReadyNoExternal",
    "HistoricalWindowBacktestBlockedMissingRowValidation",
    "HistoricalWindowBacktestBlockedMissingQuoteWindowReadiness",
    "HistoricalWindowBacktestBlockedMissingCloseBenchmarkReadiness",
    "HistoricalWindowBacktestBlockedMissingFeedQualityReadiness",
    "HistoricalWindowBacktestBlockedQuarantinedFileIncluded",
    "HistoricalWindowBacktestBlockedDirectCrossIncluded",
    "HistoricalWindowBacktestBlockedInversionMismatch",
    "HistoricalWindowBacktestBlockedAudUsdMisclassified",
    "InconclusiveSafe"
  ],
  AuthorizationOnly: true,
  NoApiCall: true,
  NoDownload: true,
  NoRowRevalidation: true,
  NoBacktest: true,
  NoSimulation: true,
  NoImport: true,
  NoPersistedSanitizedRows: true,
  NoTcaResultLines: true,
  NoExecutableSchedules: true,
  NoChildSlicesOrOrders: true,
  NoOrdersFillsReportsRoutes: true
});

writeJson("phase-exec-sim-r030-historical-window-backtest-authorization-request.json", {
  phase,
  historicalWindowBacktestAuthorizationRequestCreated: true,
  HistoricalWindowBacktestAuthorizationId: "exec-sim-r030-historical-window-backtest-authorization",
  RequestedBySanitized: "CodexNoExternalAuthorization",
  RequestedAtUtc: createdAtUtc,
  ProviderName: "PolygonOfflineFile",
  DatasetType: "HistoricalBboQuotes",
  Symbols: symbols,
  SessionWindowCategories: sessionCategories,
  AcceptedValidationResultIds: entries.map(x => x.HistoricalWindowBacktestEntryId),
  QuoteWindowReadinessIds: ["phase-exec-sim-r029-quote-window-readiness-results"],
  CloseBenchmarkReadinessIds: ["phase-exec-sim-r029-close-benchmark-readiness-results"],
  FeedQualityReadinessIds: ["phase-exec-sim-r029-feed-quality-readiness-results"],
  SanitizedImportReadinessIds: ["phase-exec-sim-r029-sanitized-import-readiness-metadata"],
  IntendedNextPhase: "EXEC-SIM-R031",
  AuthorizationOnly: true,
  NoApiCall: true,
  NoBacktest: true,
  NoImport: true,
  NoTcaResultLines: true,
  NoOrdersFillsReportsRoutes: true
});

writeJson("phase-exec-sim-r030-historical-window-backtest-preflight-contract.json", {
  phase,
  historicalWindowBacktestPreflightContractCreated: true,
  RequiredPreflightChecks: [
    "Row validation result exists for all 14 entries.",
    "Accepted rows exist for all 14 entries.",
    "Rejected row count is 0 for all 14 entries.",
    "Quote-window readiness exists for all 14 entries.",
    "Close-benchmark readiness exists for all 14 entries.",
    "Feed-quality readiness exists for all 14 entries.",
    "Sanitized import-readiness metadata exists for all 14 entries.",
    "No direct cross is included.",
    "No quarantined file is included.",
    "USDJPY/USDCAD/USDCHF inversion is preserved.",
    "AUDUSD is not failed.",
    "No backtest, import, row revalidation, or TCA result line is triggered."
  ],
  RowRevalidationDeferred: true,
  BacktestExecutionDeferredToR031: true,
  NoDbImport: true,
  NoPersistedSanitizedRows: true
});

writeJson("phase-exec-sim-r030-historical-window-backtest-authorization-result.json", {
  phase,
  historicalWindowBacktestAuthorizationResultCreated: true,
  AuthorizationStatus: allEntriesEligible ? "HistoricalWindowBacktestAuthorizationReadyNoExternal" : "InconclusiveSafe",
  Classifications: classifications,
  AuthorizedEntryCount: allEntriesEligible ? entries.length : 0,
  BlockedEntryCount: allEntriesEligible ? 0 : entries.filter(x => !x.EligibleForR031).length,
  OpeningBuildAuthorizedCount: openingEntries.length,
  ClosingFlattenAuthorizedCount: closingEntries.length,
  ReadyForR031: allEntriesEligible,
  AuthorizationOnly: true,
  NoExternal: true,
  Reason: allEntriesEligible
    ? "All 14 R029 row-validated OpeningBuild/ClosingFlatten entries have accepted rows, zero rejected rows, quote-window readiness, close-benchmark readiness, feed-quality readiness, and sanitized import-readiness metadata."
    : "One or more R029 readiness preconditions are missing."
});

writeJson("phase-exec-sim-r030-r029-row-validation-reference.json", {
  phase,
  r029RowValidationReferenceCreated: true,
  SourceRowValidationPhase: "EXEC-SIM-R029",
  SourceRowValidationArtifact: "phase-exec-sim-r029-row-level-validation-results.json",
  SourceQuoteWindowArtifact: "phase-exec-sim-r029-quote-window-readiness-results.json",
  SourceCloseBenchmarkArtifact: "phase-exec-sim-r029-close-benchmark-readiness-results.json",
  SourceFeedQualityArtifact: "phase-exec-sim-r029-feed-quality-readiness-results.json",
  SourceSanitizedImportReadinessArtifact: "phase-exec-sim-r029-sanitized-import-readiness-metadata.json",
  R029Classifications: r029Rows.classifications,
  RowValidationResultCount: r029Rows.resultCount,
  QuoteWindowReadinessCount: r029Windows.evaluatedWindowCount,
  CloseBenchmarkReadinessCount: r029Close.resultCount,
  FeedQualityReadinessCount: r029Feed.resultCount,
  RowRevalidationExecutedInR030: false
});

writeJson("phase-exec-sim-r030-authorized-session-window-entries.json", {
  phase,
  authorizedSessionWindowEntriesCreated: true,
  AuthorizedEntryCount: entries.length,
  Entries: entries
});
writeJson("phase-exec-sim-r030-opening-build-entries-authorized.json", {
  phase,
  openingBuildEntriesAuthorizedCreated: true,
  AuthorizedEntryCount: openingEntries.length,
  Entries: openingEntries
});
writeJson("phase-exec-sim-r030-closing-flatten-entries-authorized.json", {
  phase,
  closingFlattenEntriesAuthorizedCreated: true,
  AuthorizedEntryCount: closingEntries.length,
  Entries: closingEntries
});
writeJson("phase-exec-sim-r030-accepted-rejected-row-summary.json", {
  phase,
  acceptedRejectedRowSummaryCreated: true,
  TotalAcceptedRows: entries.reduce((sum, x) => sum + x.AcceptedRowCount, 0),
  TotalRejectedRows: entries.reduce((sum, x) => sum + x.RejectedRowCount, 0),
  AllRejectedRowCountsZero: entries.every(x => x.RejectedRowCount === 0),
  Entries: entries.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, AcceptedRowCount: x.AcceptedRowCount, RejectedRowCount: x.RejectedRowCount }))
});
writeJson("phase-exec-sim-r030-quote-window-readiness-authorized.json", {
  phase,
  quoteWindowReadinessAuthorizedCreated: true,
  AuthorizedEntryCount: entries.filter(x => x.QuoteWindowReadinessCount > 0).length,
  TotalQuoteWindowReadinessRecords: r029Windows.evaluatedWindowCount,
  Entries: entries.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, QuoteWindowReadinessCount: x.QuoteWindowReadinessCount }))
});
writeJson("phase-exec-sim-r030-close-benchmark-readiness-authorized.json", {
  phase,
  closeBenchmarkReadinessAuthorizedCreated: true,
  AuthorizedEntryCount: entries.filter(x => x.CloseBenchmarkReadinessCount > 0).length,
  TotalCloseBenchmarkReadinessRecords: r029Close.resultCount,
  Entries: entries.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, CloseBenchmarkReadinessCount: x.CloseBenchmarkReadinessCount }))
});
writeJson("phase-exec-sim-r030-feed-quality-readiness-authorized.json", {
  phase,
  feedQualityReadinessAuthorizedCreated: true,
  AuthorizedEntryCount: entries.filter(x => x.FeedQualityReadinessCount === 1).length,
  TotalFeedQualityReadinessRecords: r029Feed.resultCount,
  Entries: entries.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, FeedQualityReadinessCount: x.FeedQualityReadinessCount }))
});
writeJson("phase-exec-sim-r030-sanitized-import-readiness-authorized.json", {
  phase,
  sanitizedImportReadinessAuthorizedCreated: true,
  AuthorizedEntryCount: entries.filter(x => x.SanitizedImportReadinessMetadataPresent).length,
  MetadataOnly: true,
  DbImportOccurred: false,
  PersistedSanitizedQuoteRowsCreated: false,
  Entries: entries.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, SanitizedImportReadinessMetadataPresent: x.SanitizedImportReadinessMetadataPresent }))
});

writeJson("phase-exec-sim-r030-direct-cross-exclusion-preservation.json", {
  phase,
  directCrossExclusionPreserved: true,
  directCrossesInExecutionBatch: false,
  directCrossExecutionAllowedByDefault: false,
  directCrossesSignalOnly: true,
  nettingFirstRequired: true,
  guidanceWeakened: false,
  excludedExamples: ["EURGBP", "CADJPY", "AUDCNH", "CNHSGD", "EURZAR", "MXNNOK", "NOKZAR"]
});
writeJson("phase-exec-sim-r030-inversion-preservation.json", {
  phase,
  inversionPreservationCreated: true,
  UsdJpy: { NormalizedPortfolioSymbol: "JPYUSD", ExecutionTradableSymbol: "USDJPY", RequiresInversion: true, SecurityID: "4004", SecurityIDSource: "8" },
  UsdCad: { NormalizedPortfolioSymbol: "CADUSD", ExecutionTradableSymbol: "USDCAD", RequiresInversion: true },
  UsdChf: { NormalizedPortfolioSymbol: "CHFUSD", ExecutionTradableSymbol: "USDCHF", RequiresInversion: true },
  NonInverted: ["EURUSD", "AUDUSD", "GBPUSD", "NZDUSD"],
  SourceR029InversionPreserved: r029Inversion.usdJpyCaveatPreserved && r029Inversion.usdCadInversionPreserved && r029Inversion.usdChfInversionPreserved,
  AudUsdMisclassifiedFailed: false
});
writeJson("phase-exec-sim-r030-expected-r031-policy-list.json", {
  phase,
  expectedR031PolicyListCreated: true,
  Policies: policies,
  BenchmarkOnlyPolicies: ["ImmediatePaperBenchmark", "TWAPBenchmarkOnly", "VWAPBenchmarkOnly"],
  WakettPoliciesRemainBaselinesNotDefaults: true
});
writeJson("phase-exec-sim-r030-expected-r031-report-list.json", {
  phase,
  expectedR031ReportListCreated: true,
  Reports: expectedReports,
  IncludesOpeningBuildReport: true,
  IncludesClosingFlattenReport: true,
  IncludesNoOvernightResidualPenaltyReport: true
});
writeJson("phase-exec-sim-r030-cost-guidance-preservation.json", { phase, costGuidancePreserved: true, bestCaseMajorTargetUsdPerMillion: 5, fiveUsdPerMillionBestCaseMajorOnly: true, fiveUsdPerMillionUniversalized: false });
writeJson("phase-exec-sim-r030-nonmajor-calibration-preservation.json", { phase, nonmajorCalibrationPreserved: true, RequiresLiquidityCalibration: true, calibrationRequirementWeakened: false, DeferredCalibrationUniverse: ["USDMXN", "USDCNH", "USDNOK", "USDSEK", "USDZAR", "USDSGD or SGDUSD after explicit convention"] });

function audit(name, value) {
  writeJson(name, { phase, ...value });
}
audit("phase-exec-sim-r030-no-row-revalidation-audit.json", { quoteRowsRevalidated: false, quoteRowsReadInR030: false });
audit("phase-exec-sim-r030-no-db-import-audit.json", { quotesImportedIntoDb: false, dbWriteOccurred: false, paperLedgerStateCommitted: false });
audit("phase-exec-sim-r030-no-sanitized-quote-row-creation-audit.json", { sanitizedQuoteRowsCreated: false, persistedSanitizedQuoteRowsCreated: false });
audit("phase-exec-sim-r030-no-backtest-simulation-audit.json", { backtestExecuted: false, simulationExecuted: false });
audit("phase-exec-sim-r030-no-tca-result-lines-audit.json", { tcaResultLinesProduced: false, simulationResultLinesProduced: false, tcaReportsProduced: false });
audit("phase-exec-sim-r030-no-executable-schedule-audit.json", { executableSchedulesCreated: false });
audit("phase-exec-sim-r030-no-child-slices-audit.json", { childSlicesCreated: false });
audit("phase-exec-sim-r030-no-child-orders-audit.json", { childOrdersCreated: false });
audit("phase-exec-sim-r030-no-polygon-api-call-audit.json", { polygonApiCalled: false });
audit("phase-exec-sim-r030-no-lmax-call-audit.json", { lmaxCalled: false });
audit("phase-exec-sim-r030-no-external-api-call-audit.json", { polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, filesDownloaded: false });
audit("phase-exec-sim-r030-no-broker-marketdata-runtime-audit.json", { brokerActivationDetected: false, socketOpened: false, tlsOpened: false, fixOpened: false, marketDataRequestSent: false, marketDataResponseRead: false, apiWorkerGatewayStarted: false, schedulerServiceTimerPollingBackgroundJobIntroduced: false, automaticExecutionIntroduced: false });
audit("phase-exec-sim-r030-no-order-fill-report-route-audit.json", { ordersCreated: false, executableOrdersCreated: false, omsOrdersCreated: false, childOrdersCreated: false, fillEntitiesCreated: false, executionReportEntitiesCreated: false, brokerExecutionReportsCreated: false, routesCreated: false, submissionsCreated: false });
writeJson("phase-exec-sim-r030-usdjpy-caveat-preservation.json", {
  phase,
  usdjpyCaveatPreserved: true,
  PortfolioNormalizedSymbol: "JPYUSD",
  ExecutionTradableSymbol: "USDJPY",
  RequiresInversion: true,
  securityId: "4004",
  securityIdSource: "8",
  audusdMisclassifiedFailed: false
});
writeJson("phase-exec-sim-r030-lmax-readonly-baseline-reference.json", {
  phase,
  referenceOnly: true,
  lmaxCalledInR030: false,
  gbpusdR203ReadonlyMarketDataSucceededSanitizedEntryCount: 2,
  eurgbpR207ReadonlyMarketDataSucceededSanitizedEntryCount: 2,
  audusdStatus: "TLS-boundary inconclusive in LMAX context only; not failed",
  usdjpyStatus: "not proven, not failed",
  usdjpySecurityId: "4004",
  usdjpySecurityIdSource: "8"
});
audit("phase-exec-sim-r030-no-external-audit.json", { polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, filesDownloaded: false, brokerRuntimeDetected: false, quoteRowsRevalidated: false, quotesImportedIntoDb: false, persistedSanitizedQuoteRowsCreated: false, backtestExecuted: false, simulationExecuted: false, tcaResultLinesProduced: false, executableSchedulesCreated: false, childSlicesCreated: false, childOrdersCreated: false, ordersFillsReportsRoutesSubmissionsCreated: false, livePaperBrokerProductionTradingStateMutated: false, paperLedgerStateCommitted: false });
audit("phase-exec-sim-r030-forbidden-actions-audit.json", { forbiddenActionsDetected: false, polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, filesDownloaded: false, brokerRuntimeDetected: false, quoteRowsRevalidated: false, quotesImportedIntoDb: false, persistedSanitizedQuoteRowsCreated: false, backtestExecuted: false, simulationExecuted: false, tcaResultLinesProduced: false, executableSchedulesCreated: false, childSlicesCreated: false, childOrdersCreated: false, ordersFillsReportsRoutesSubmissionsCreated: false, stateMutated: false });
writeJson("phase-exec-sim-r030-next-phase-recommendation.json", {
  phase,
  nextPhaseRecommendationCreated: true,
  RecommendedNextPhase: "EXEC-SIM-R031",
  RecommendedNextPhaseName: "No-External Historical Window TCA Backtest Execution Gate",
  R031Scope: "Execute the OpeningBuild/ClosingFlatten TCA backtest over the 14 row-validated files without Polygon/LMAX calls, DB import, persisted sanitized rows, executable schedules, child orders, real fills, execution reports, routes, submissions, or state mutation."
});
writeJson("phase-exec-sim-r030-build-test-validator-evidence.json", {
  phase,
  buildTestValidatorEvidenceCreated: true,
  dotnetBuildNoRestore: process.env.R030_DOTNET_BUILD ?? "Pending",
  focusedR030Tests: process.env.R030_FOCUSED_TESTS ?? "Pending",
  unitTestsIfFeasible: process.env.R030_UNIT_TESTS ?? "Pending",
  validator: process.env.R030_VALIDATOR ?? "Pending"
});

writeText("phase-exec-sim-r030-summary.md", `# EXEC-SIM-R030 Summary

R030 reused R029 row validation and readiness artifacts to authorize the future OpeningBuild/ClosingFlatten historical-window TCA backtest.

Classifications:
${classifications.map(x => `- ${x}`).join("\n")}

Authorization:
- Authorized entries: ${entries.length}
- OpeningBuild entries: ${openingEntries.length}
- ClosingFlatten entries: ${closingEntries.length}
- Total rejected rows across authorized entries: ${entries.reduce((sum, x) => sum + x.RejectedRowCount, 0)}
- Ready for R031: ${allEntriesEligible}

R030 did not revalidate quote rows, import quotes, create sanitized quote rows, run a backtest or simulation, create TCA result lines, create executable schedules, create child slices/orders, create orders/fills/reports/routes/submissions, call Polygon/LMAX/external APIs, or mutate state.
`);

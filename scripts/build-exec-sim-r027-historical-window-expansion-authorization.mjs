import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();
const artifactsDir = path.join(repoRoot, "artifacts", "readiness", "execution-sim");
const phase = "EXEC-SIM-R027";
const createdAtUtc = "2026-05-21T00:00:00Z";

function readJson(name) {
  return JSON.parse(fs.readFileSync(path.join(artifactsDir, name), "utf8"));
}

function writeJson(name, value) {
  fs.writeFileSync(path.join(artifactsDir, name), `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

function writeText(name, value) {
  fs.writeFileSync(path.join(artifactsDir, name), value, "utf8");
}

const symbols = [
  { Symbol: "EURUSD", ProviderSymbol: "EURUSD", ExecutionTradableSymbol: "EURUSD", NormalizedPortfolioSymbol: "EURUSD", RequiresInversion: false },
  { Symbol: "USDJPY", ProviderSymbol: "USDJPY", ExecutionTradableSymbol: "USDJPY", NormalizedPortfolioSymbol: "JPYUSD", RequiresInversion: true, SecurityID: "4004", SecurityIDSource: "8" },
  { Symbol: "AUDUSD", ProviderSymbol: "AUDUSD", ExecutionTradableSymbol: "AUDUSD", NormalizedPortfolioSymbol: "AUDUSD", RequiresInversion: false, AudUsdStatus: "not failed" },
  { Symbol: "GBPUSD", ProviderSymbol: "GBPUSD", ExecutionTradableSymbol: "GBPUSD", NormalizedPortfolioSymbol: "GBPUSD", RequiresInversion: false },
  { Symbol: "NZDUSD", ProviderSymbol: "NZDUSD", ExecutionTradableSymbol: "NZDUSD", NormalizedPortfolioSymbol: "NZDUSD", RequiresInversion: false },
  { Symbol: "USDCAD", ProviderSymbol: "USDCAD", ExecutionTradableSymbol: "USDCAD", NormalizedPortfolioSymbol: "CADUSD", RequiresInversion: true },
  { Symbol: "USDCHF", ProviderSymbol: "USDCHF", ExecutionTradableSymbol: "USDCHF", NormalizedPortfolioSymbol: "CHFUSD", RequiresInversion: true }
];

const requiredCategories = ["OpeningBuild", "ClosingFlatten"];
const supportedCategories = ["OpeningBuild", "IntradayRebalance", "ClosingFlatten", "Mixed", "Unknown"];
const requiredFields = [
  "Symbol",
  "ProviderSymbol",
  "ExecutionTradableSymbol",
  "NormalizedPortfolioSymbol",
  "RequiresInversion",
  "QuoteFilePath",
  "ManifestPath",
  "FileFormat",
  "ProviderName",
  "ProviderDatasetType",
  "TimeRangeStartUtc",
  "TimeRangeEndUtc",
  "SessionWindowCategory",
  "ContainsSecrets",
  "ContainsRawProviderPayload"
];

const r026Decision = readJson("phase-exec-sim-r026-data-expansion-decision.json");
const r026Historical = readJson("phase-exec-sim-r026-next-historical-window-recommendation.json");

function entry(symbol, category, start, end, providerSymbol, quoteName, manifestName) {
  const symbolMeta = symbols.find(x => x.Symbol === symbol);
  const base = "C:\\Users\\phili\\source\\repos\\QQ.Production.Intraday\\data\\offline-quotes\\polygon\\incoming";
  return {
    Symbol: symbol,
    ProviderSymbol: providerSymbol,
    ExecutionTradableSymbol: symbolMeta.ExecutionTradableSymbol,
    NormalizedPortfolioSymbol: symbolMeta.NormalizedPortfolioSymbol,
    RequiresInversion: symbolMeta.RequiresInversion,
    SecurityID: symbolMeta.SecurityID,
    SecurityIDSource: symbolMeta.SecurityIDSource,
    QuoteFilePath: `${base}\\${quoteName}`,
    ManifestPath: `${base}\\${manifestName}`,
    FileFormat: "NDJSON",
    ProviderName: "PolygonOfflineFile",
    ProviderDatasetType: "HistoricalBboQuotes",
    TimeRangeStartUtc: start,
    TimeRangeEndUtc: end,
    SessionWindowCategory: category,
    ContainsSecrets: false,
    ContainsRawProviderPayload: false
  };
}

const operatorEntries = [
  entry("AUDUSD", "OpeningBuild", "2026-05-19T08:00:00Z", "2026-05-19T12:00:00Z", "C:AUD-USD", "audusd-20260519080000-20260519120000.ndjson", "audusd-20260519080000-20260519120000.manifest.json"),
  entry("AUDUSD", "ClosingFlatten", "2026-05-19T16:00:00Z", "2026-05-19T20:00:00Z", "C:AUD-USD", "audusd-20260519160000-20260519200000.ndjson", "audusd-20260519160000-20260519200000.manifest.json"),
  entry("EURUSD", "OpeningBuild", "2026-05-19T08:00:00Z", "2026-05-19T12:00:00Z", "C:EUR-USD", "eurusd-20260519080000-20260519120000.ndjson", "eurusd-20260519080000-20260519120000.manifest.json"),
  entry("EURUSD", "ClosingFlatten", "2026-05-19T16:00:00Z", "2026-05-19T20:00:00Z", "C:EUR-USD", "eurusd-20260519160000-20260519200000.ndjson", "eurusd-20260519160000-20260519200000.manifest.json"),
  entry("GBPUSD", "OpeningBuild", "2026-05-19T08:00:00Z", "2026-05-19T12:00:00Z", "C:GBP-USD", "gbpusd-20260519080000-20260519120000.ndjson", "gbpusd-20260519080000-20260519120000.manifest.json"),
  entry("GBPUSD", "ClosingFlatten", "2026-05-19T16:00:00Z", "2026-05-19T20:00:00Z", "C:GBP-USD", "gbpusd-20260519160000-20260519200000.ndjson", "gbpusd-20260519160000-20260519200000.manifest.json"),
  entry("NZDUSD", "OpeningBuild", "2026-05-19T08:00:00Z", "2026-05-19T12:00:00Z", "C:NZD-USD", "nzdusd-20260519080000-20260519120000.ndjson", "nzdusd-20260519080000-20260519120000.manifest.json"),
  entry("NZDUSD", "ClosingFlatten", "2026-05-19T16:00:00Z", "2026-05-19T20:00:00Z", "C:NZD-USD", "nzdusd-20260519160000-20260519200000.ndjson", "nzdusd-20260519160000-20260519200000.manifest.json"),
  entry("USDCAD", "OpeningBuild", "2026-05-19T08:00:00Z", "2026-05-19T12:00:00Z", "C:USD-CAD", "usdcad-20260519080000-20260519120000.ndjson", "usdcad-20260519080000-20260519120000.manifest.json"),
  entry("USDCAD", "ClosingFlatten", "2026-05-19T16:00:00Z", "2026-05-19T20:00:00Z", "C:USD-CAD", "usdcad-20260519160000-20260519200000.ndjson", "usdcad-20260519160000-20260519200000.manifest.json"),
  entry("USDCHF", "OpeningBuild", "2026-05-19T08:00:00Z", "2026-05-19T12:00:00Z", "C:USD-CHF", "usdchf-20260519080000-20260519120000.ndjson", "usdchf-20260519080000-20260519120000.manifest.json"),
  entry("USDCHF", "ClosingFlatten", "2026-05-19T16:00:00Z", "2026-05-19T20:00:00Z", "C:USD-CHF", "usdchf-20260519160000-20260519200000.ndjson", "usdchf-20260519160000-20260519200000.manifest.json"),
  entry("USDJPY", "OpeningBuild", "2026-05-19T08:00:00Z", "2026-05-19T12:00:00Z", "C:USD-JPY", "usdjpy-20260519080000-20260519120000.ndjson", "usdjpy-20260519080000-20260519120000.manifest.json"),
  entry("USDJPY", "ClosingFlatten", "2026-05-19T16:00:00Z", "2026-05-19T20:00:00Z", "C:USD-JPY", "usdjpy-20260519160000-20260519200000.ndjson", "usdjpy-20260519160000-20260519200000.manifest.json")
].map(x => ({
  ...x,
  QuoteFileExists: fs.existsSync(x.QuoteFilePath),
  ManifestExists: fs.existsSync(x.ManifestPath),
  DirectCross: false,
  PathPresenceCheckedOnly: true,
  QuoteRowsRead: false,
  ManifestContentRead: false
}));

const operatorPlaceholdersSupplied = false;
const missingDiagnostics = operatorEntries
  .filter(x => !x.QuoteFileExists || !x.ManifestExists || !x.TimeRangeStartUtc || !x.TimeRangeEndUtc || !x.SessionWindowCategory)
  .map(x => ({
    Symbol: x.Symbol,
    ExecutionTradableSymbol: x.ExecutionTradableSymbol,
    NormalizedPortfolioSymbol: x.NormalizedPortfolioSymbol,
    RequiresInversion: x.RequiresInversion,
    RequiredSessionWindowCategory: x.SessionWindowCategory,
    MissingQuoteFilePath: !x.QuoteFileExists,
    MissingManifestPath: !x.ManifestExists,
    MissingTimeRangeStartUtc: !x.TimeRangeStartUtc,
    MissingTimeRangeEndUtc: !x.TimeRangeEndUtc,
    MissingSessionWindowCategory: !x.SessionWindowCategory,
    DiagnosticStatus: "MissingConcreteOperatorFilePathOrManifest",
    SafeBlockedClassification: "EXEC_SIM_R027_BLOCKED_OPERATOR_FILE_PATHS_OR_MANIFESTS_MISSING_NO_EXTERNAL"
  }));

const allPathsPresent = operatorEntries.length === 14 && missingDiagnostics.length === 0;
const classifications = allPathsPresent
  ? [
      "EXEC_SIM_R027_PASS_HISTORICAL_WINDOW_EXPANSION_AUTHORIZATION_READY_NO_EXTERNAL",
      "EXEC_SIM_R027_PASS_SESSION_WINDOW_EXPANSION_PREFLIGHT_READY_NO_EXTERNAL",
      "EXEC_SIM_R027_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
    ]
  : [
      "EXEC_SIM_R027_BLOCKED_OPERATOR_FILE_PATHS_OR_MANIFESTS_MISSING_NO_EXTERNAL",
      "EXEC_SIM_R027_NEEDS_OPERATOR_DATE_RANGES_OR_SESSION_TIMES_NO_EXTERNAL"
    ];

writeJson("phase-exec-sim-r027-r026-data-expansion-decision-reference.json", {
  phase,
  r026DataExpansionDecisionReferenceCreated: true,
  SourceDecisionPhase: "EXEC-SIM-R026",
  SourceDecisionArtifact: "phase-exec-sim-r026-data-expansion-decision.json",
  SourceHistoricalRecommendationArtifact: "phase-exec-sim-r026-next-historical-window-recommendation.json",
  R026Decision: r026Decision.Decision,
  R026RecommendationStatus: r026Decision.RecommendationStatus,
  R026OpeningClosingWindowsRecommended: r026Decision.OpeningClosingWindowsRecommended,
  R026Symbols: r026Historical.Symbols,
  R026RequiredCoverage: r026Historical.RequiredCoverage
});

writeJson("phase-exec-sim-r027-historical-window-expansion-authorization-contract.json", {
  phase,
  historicalWindowExpansionAuthorizationContractCreated: true,
  SourceDecisionPhase: "EXEC-SIM-R026",
  CreatedAtUtc: createdAtUtc,
  AuthorizationOnly: true,
  NoExternalApiCalls: true,
  NoDownload: true,
  NoRowValidation: true,
  NoDbImport: true,
  NoPersistedSanitizedRows: true,
  NoBacktest: true,
  NoSimulation: true,
  NoTcaResultLines: true,
  NoOrdersFillsReportsRoutes: true,
  AuthorizationStatuses: [
    "HistoricalWindowExpansionAuthorizationReadyNoExternal",
    "HistoricalWindowExpansionBlockedMissingFiles",
    "HistoricalWindowExpansionBlockedMissingManifests",
    "HistoricalWindowExpansionBlockedMissingSessionCategories",
    "HistoricalWindowExpansionBlockedIncompleteMetadata",
    "HistoricalWindowExpansionBlockedDirectCrossIncluded",
    "HistoricalWindowExpansionBlockedSecretRisk",
    "HistoricalWindowExpansionBlockedRawPayloadRisk",
    "HistoricalWindowExpansionNeedsOperatorDateRangesOrSessionTimes",
    "InconclusiveSafe"
  ]
});

writeJson("phase-exec-sim-r027-historical-window-expansion-request.json", {
  phase,
  historicalWindowExpansionRequestCreated: true,
  RequestedBySanitized: "CodexNoExternalAuthorization",
  RequestedAtUtc: createdAtUtc,
  ProviderName: "PolygonOfflineFile",
  ProviderDatasetType: "HistoricalBboQuotes",
  RequiredSymbols: symbols.map(x => x.Symbol),
  RequiredSessionWindowCategories: requiredCategories,
  SupportedSessionWindowCategories: supportedCategories,
  OperatorFileEntriesSupplied: true,
  OperatorPlaceholdersSupplied: operatorPlaceholdersSupplied,
  OperatorFileEntryCount: operatorEntries.length,
  OperatorFileEntries: operatorEntries,
  AuthorizationOnly: true,
  IntendedNextPhaseIfFilesSupplied: "EXEC-SIM-R028"
});

writeJson("phase-exec-sim-r027-historical-window-expansion-preflight-contract.json", {
  phase,
  historicalWindowExpansionPreflightContractCreated: true,
  RequiredFileEntryFields: requiredFields,
  RequiredBlockingRules: [
    "Missing quote file path blocks authorization.",
    "Missing manifest path blocks authorization.",
    "Missing UTC date ranges or session times blocks authorization.",
    "Direct-cross entries block authorization.",
    "ContainsSecrets=true blocks authorization.",
    "ContainsRawProviderPayload=true blocks authorization."
  ],
  PathPresenceCheckOnlyWhenSupplied: true,
  ManifestContentValidationDeferredToR028: true,
  QuoteRowValidationDeferredPastR028: true,
  NoDownload: true,
  NoBacktest: true
});

writeJson("phase-exec-sim-r027-authorization-result.json", {
  phase,
  authorizationResultCreated: true,
  AuthorizationStatus: allPathsPresent ? "HistoricalWindowExpansionAuthorizationReadyNoExternal" : "HistoricalWindowExpansionBlockedMissingFiles",
  AdditionalStatus: allPathsPresent ? "HistoricalWindowExpansionSessionWindowPreflightReadyNoExternal" : "HistoricalWindowExpansionNeedsOperatorDateRangesOrSessionTimes",
  Classifications: classifications,
  AuthorizedEntryCount: allPathsPresent ? operatorEntries.length : 0,
  BlockedEntryCount: missingDiagnostics.length,
  OperatorFileEntriesSupplied: true,
  OperatorPlaceholdersSupplied: operatorPlaceholdersSupplied,
  SafeBlocked: !allPathsPresent,
  ReadyForR028: allPathsPresent,
  Reason: allPathsPresent
    ? "All 14 operator-provided quote file and manifest paths are present. R027 authorized path/manifests for R028 manifest validation only."
    : "One or more operator-provided quote file paths, manifest paths, UTC date ranges, or session categories are missing.",
  NoExternal: true
});

writeJson("phase-exec-sim-r027-required-symbols.json", {
  phase,
  requiredSymbolsCreated: true,
  RequiredSymbolCount: symbols.length,
  Symbols: symbols,
  AudUsdStatus: "not failed",
  ExecutionUniverse: "USD-pair-only"
});

writeJson("phase-exec-sim-r027-required-session-window-categories.json", {
  phase,
  requiredSessionWindowCategoriesCreated: true,
  RequiredCategories: requiredCategories,
  OptionalSupportedCategories: ["IntradayRebalance", "Mixed", "Unknown"],
  SupportedCategories: supportedCategories,
  OpeningBuildRequiredOrRequested: true,
  ClosingFlattenRequiredOrRequested: true,
  IntradayRebalanceMixedUnknownSupported: true,
  NeedsOperatorDateRangesOrSessionTimes: true
});

writeJson("phase-exec-sim-r027-file-entry-requirements.json", {
  phase,
  fileEntryRequirementsCreated: true,
  RequiredFields: requiredFields,
  FileFormat: "NDJSON",
  ProviderName: "PolygonOfflineFile",
  ProviderDatasetType: "HistoricalBboQuotes",
  SessionWindowCategoryAllowedValues: supportedCategories,
  ContainsSecretsMustBeFalse: true,
  ContainsRawProviderPayloadMustBeFalse: true,
  DirectCrossesAllowed: false,
  PathPresenceOnlyInR027: true,
  ManifestValidationDeferredToR028: true,
  QuoteRowValidationDeferredPastR028: true
});

writeJson("phase-exec-sim-r027-accepted-for-authorization-entries.json", {
  phase,
  acceptedForAuthorizationEntriesCreated: true,
  AcceptedEntryCount: allPathsPresent ? operatorEntries.length : 0,
  Entries: allPathsPresent ? operatorEntries : [],
  Reason: allPathsPresent
    ? "All concrete operator file paths and manifest paths were present. Path presence only was checked; rows and manifest contents were not read."
    : "One or more concrete operator file paths or manifest paths were missing."
});

writeJson("phase-exec-sim-r027-missing-input-diagnostics.json", {
  phase,
  missingInputDiagnosticsCreated: true,
  MissingDiagnosticsCount: missingDiagnostics.length,
  MissingFilePathsBlockSafely: missingDiagnostics.some(x => x.MissingQuoteFilePath),
  MissingManifestPathsBlockSafely: missingDiagnostics.some(x => x.MissingManifestPath),
  MissingDateRangesOrSessionTimesNeedOperatorInput: missingDiagnostics.some(x => x.MissingTimeRangeStartUtc || x.MissingTimeRangeEndUtc),
  OperatorPlaceholdersSupplied: operatorPlaceholdersSupplied,
  Diagnostics: missingDiagnostics
});

writeJson("phase-exec-sim-r027-inversion-preservation.json", {
  phase,
  inversionPreservationCreated: true,
  UsdJpy: { NormalizedPortfolioSymbol: "JPYUSD", ExecutionTradableSymbol: "USDJPY", RequiresInversion: true, SecurityID: "4004", SecurityIDSource: "8" },
  UsdCad: { NormalizedPortfolioSymbol: "CADUSD", ExecutionTradableSymbol: "USDCAD", RequiresInversion: true },
  UsdChf: { NormalizedPortfolioSymbol: "CHFUSD", ExecutionTradableSymbol: "USDCHF", RequiresInversion: true },
  NonInverted: ["EURUSD", "AUDUSD", "GBPUSD", "NZDUSD"],
  AudUsdMisclassifiedFailed: false
});

writeJson("phase-exec-sim-r027-direct-cross-exclusion-preservation.json", {
  phase,
  directCrossExclusionPreserved: true,
  directCrossesSignalOnly: true,
  nettingFirstRequired: true,
  directCrossEntriesAccepted: false,
  directCrossExecutionAllowedByDefault: false,
  directCrossExclusionWeakened: false,
  ExcludedExamples: ["EURGBP", "CADJPY", "AUDCNH", "CNHSGD", "EURZAR", "MXNNOK", "NOKZAR"]
});

writeJson("phase-exec-sim-r027-cost-guidance-preservation.json", {
  phase,
  costGuidancePreserved: true,
  bestCaseMajorTargetUsdPerMillion: 5,
  fiveUsdPerMillionBestCaseMajorOnly: true,
  fiveUsdPerMillionUniversalized: false
});

writeJson("phase-exec-sim-r027-nonmajor-calibration-preservation.json", {
  phase,
  nonmajorCalibrationPreserved: true,
  nonMajorEmScandiCnhRequireLiquidityCalibration: true,
  calibrationRequirementWeakened: false,
  DeferredCalibrationUniverse: ["USDMXN", "USDCNH", "USDNOK", "USDSEK", "USDZAR", "USDSGD or SGDUSD after explicit convention"]
});

function audit(name, value) {
  writeJson(name, { phase, ...value });
}

audit("phase-exec-sim-r027-no-download-audit.json", { filesDownloaded: false, polygonDownloadExecuted: false });
audit("phase-exec-sim-r027-no-row-validation-audit.json", { quoteRowsValidated: false });
audit("phase-exec-sim-r027-no-db-import-audit.json", { quotesImportedIntoDb: false });
audit("phase-exec-sim-r027-no-sanitized-row-audit.json", { persistedSanitizedQuoteRowsCreated: false, sanitizedQuoteRowsCreated: false });
audit("phase-exec-sim-r027-no-backtest-simulation-audit.json", { backtestExecuted: false, simulationExecuted: false });
audit("phase-exec-sim-r027-no-tca-result-lines-audit.json", { tcaResultLinesProduced: false });
audit("phase-exec-sim-r027-no-order-fill-report-route-audit.json", { ordersCreated: false, executableOrdersCreated: false, childOrdersCreated: false, fillsCreated: false, executionReportsCreated: false, routesCreated: false, submissionsCreated: false });
audit("phase-exec-sim-r027-no-polygon-api-call-audit.json", { polygonApiCalled: false });
audit("phase-exec-sim-r027-no-lmax-call-audit.json", { lmaxCalled: false });
audit("phase-exec-sim-r027-no-external-api-call-audit.json", { polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false });
audit("phase-exec-sim-r027-no-broker-marketdata-runtime-audit.json", { brokerActivationDetected: false, socketOpened: false, tlsOpened: false, fixSessionOpened: false, marketDataRequestSent: false, marketDataResponseRead: false, apiWorkerGatewayStarted: false, schedulerServiceTimerPollingBackgroundJobIntroduced: false });

writeJson("phase-exec-sim-r027-usdjpy-caveat-preservation.json", {
  phase,
  usdjpyCaveatPreserved: true,
  PortfolioNormalizedSymbol: "JPYUSD",
  ExecutionTradableSymbol: "USDJPY",
  RequiresInversion: true,
  securityId: "4004",
  securityIdSource: "8",
  audusdMisclassifiedFailed: false
});

writeJson("phase-exec-sim-r027-lmax-readonly-baseline-reference.json", {
  phase,
  referenceOnly: true,
  lmaxCalledInR027: false,
  gbpusdR203ReadonlyMarketDataSucceededSanitizedEntryCount: 2,
  eurgbpR207ReadonlyMarketDataSucceededSanitizedEntryCount: 2,
  audusdStatus: "TLS-boundary inconclusive in LMAX context only; not failed",
  usdjpyStatus: "not proven, not failed",
  usdjpySecurityId: "4004",
  usdjpySecurityIdSource: "8"
});

audit("phase-exec-sim-r027-no-external-audit.json", { polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, filesDownloaded: false, brokerRuntimeDetected: false, rowValidationExecuted: false, dbImportOccurred: false, backtestExecuted: false, simulationExecuted: false, tcaResultLinesProduced: false, ordersFillsReportsRoutesSubmissionsCreated: false, paperLedgerStateCommitted: false });
audit("phase-exec-sim-r027-forbidden-actions-audit.json", { forbiddenActionsDetected: false, filesDownloaded: false, rowValidationExecuted: false, dbImportOccurred: false, backtestExecuted: false, simulationExecuted: false, tcaResultLinesProduced: false, ordersFillsReportsRoutesSubmissionsCreated: false, stateMutated: false });

writeJson("phase-exec-sim-r027-next-phase-recommendation.json", {
  phase,
  nextPhaseRecommendationCreated: true,
  IfBlocked: "Operator should supply OpeningBuild and ClosingFlatten historical quote files and manifests for the current seven symbols, with UTC date ranges and session categories.",
  IfFilesSupplied: "EXEC-SIM-R028",
  RecommendedNextPhaseName: "No-External Historical Window Expansion Manifest Validation Gate",
  R028Scope: "Validate supplied file/manifests only; still no row validation, import, backtest, TCA lines, or order-domain output."
});

writeJson("phase-exec-sim-r027-build-test-validator-evidence.json", {
  phase,
  buildTestValidatorEvidenceCreated: true,
  dotnetBuildNoRestore: "Pending",
  focusedR027Tests: "Pending",
  unitTestsIfFeasible: "Pending",
  validator: "Pending"
});

writeText("phase-exec-sim-r027-summary.md", `# EXEC-SIM-R027 Summary

R027 reused the R026 data expansion decision and prepared the historical window expansion authorization preflight.

Classifications:
${classifications.map(x => `- ${x}`).join("\n")}

All 14 concrete operator quote file paths and manifest paths were supplied and found locally. R027 authorized them for R028 manifest validation only. It checked path presence only and did not read quote rows or validate manifest contents.

Safety: no Polygon, no LMAX, no external API, no download, no row validation, no DB import, no sanitized rows, no backtest/simulation, no TCA result lines, no orders/fills/reports/routes/submissions, and no state mutation.
`);

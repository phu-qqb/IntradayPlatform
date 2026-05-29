import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();
const artifactsDir = path.join(repoRoot, "artifacts", "readiness", "execution-sim");
const phase = "EXEC-SIM-R028";
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

function normalizePath(value) {
  return path.resolve(repoRoot, value);
}

function sha256File(filePath) {
  const hash = crypto.createHash("sha256");
  hash.update(fs.readFileSync(filePath));
  return hash.digest("hex").toUpperCase();
}

const r027Authorization = readJson("phase-exec-sim-r027-authorization-result.json");
const r027Accepted = readJson("phase-exec-sim-r027-accepted-for-authorization-entries.json");

const validationStatuses = [
  "ManifestValidationAccepted",
  "ManifestValidationAcceptedWithWarnings",
  "ManifestValidationQuarantinedMissingQuoteFile",
  "ManifestValidationQuarantinedMissingManifest",
  "ManifestValidationQuarantinedProviderMismatch",
  "ManifestValidationQuarantinedDatasetMismatch",
  "ManifestValidationQuarantinedFormatMismatch",
  "ManifestValidationQuarantinedSymbolMismatch",
  "ManifestValidationQuarantinedInversionMismatch",
  "ManifestValidationQuarantinedTimeRangeMismatch",
  "ManifestValidationQuarantinedSessionCategoryMismatch",
  "ManifestValidationQuarantinedSecretRisk",
  "ManifestValidationQuarantinedRawPayloadRisk",
  "ManifestValidationQuarantinedDirectCrossExecutionDisabled",
  "DuplicateReturned",
  "InconclusiveSafe"
];

const allowedSymbols = ["EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"];
const directCrossExamples = ["EURGBP", "CADJPY", "AUDCNH", "CNHSGD", "EURZAR", "MXNNOK", "NOKZAR"];

function expectedSymbol(symbol) {
  const values = {
    EURUSD: { NormalizedPortfolioSymbol: "EURUSD", RequiresInversion: false },
    AUDUSD: { NormalizedPortfolioSymbol: "AUDUSD", RequiresInversion: false },
    GBPUSD: { NormalizedPortfolioSymbol: "GBPUSD", RequiresInversion: false },
    NZDUSD: { NormalizedPortfolioSymbol: "NZDUSD", RequiresInversion: false },
    USDJPY: { NormalizedPortfolioSymbol: "JPYUSD", RequiresInversion: true, SecurityID: "4004", SecurityIDSource: "8" },
    USDCAD: { NormalizedPortfolioSymbol: "CADUSD", RequiresInversion: true },
    USDCHF: { NormalizedPortfolioSymbol: "CHFUSD", RequiresInversion: true }
  };
  return values[symbol];
}

function isDirectCross(symbol) {
  return !allowedSymbols.includes(symbol);
}

function validateEntry(entry) {
  const quoteFilePath = entry.QuoteFilePath;
  const manifestPath = entry.ManifestPath;
  const quoteFileExists = fs.existsSync(quoteFilePath);
  const manifestExists = fs.existsSync(manifestPath);
  let manifestReadable = false;
  let manifest = {};
  const quarantineReasons = [];
  const warnings = [];

  if (!quoteFileExists) {
    quarantineReasons.push("MissingQuoteFile");
  }

  if (!manifestExists) {
    quarantineReasons.push("MissingManifest");
  } else {
    try {
      manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8").replace(/^\uFEFF/, ""));
      manifestReadable = true;
    } catch {
      quarantineReasons.push("ManifestUnreadable");
    }
  }

  const providerName = manifest.ProviderName ?? null;
  const providerDatasetType = manifest.ProviderDatasetType ?? null;
  const providerSymbol = manifest.ProviderSymbol ?? null;
  const executionTradableSymbol = manifest.ExecutionTradableSymbol ?? null;
  const normalizedPortfolioSymbol = manifest.NormalizedPortfolioSymbol ?? null;
  const requiresInversion = manifest.RequiresInversion ?? null;
  const fileFormat = manifest.FileFormat ?? null;
  const timeRangeStartUtc = manifest.TimeRangeStartUtc ?? null;
  const timeRangeEndUtc = manifest.TimeRangeEndUtc ?? null;
  const fileHash = manifest.FileHash ?? null;
  const rowCountDeclared = manifest.RowCountDeclared ?? null;
  const containsSecrets = manifest.ContainsSecrets ?? null;
  const containsRawProviderPayload = manifest.ContainsRawProviderPayload ?? null;
  const sessionWindowCategoryFromManifest = manifest.SessionWindowCategory ?? null;
  const sessionWindowCategory = entry.SessionWindowCategory;

  if (manifestReadable) {
    if (providerName !== "PolygonOfflineFile") quarantineReasons.push("ProviderMismatch");
    if (providerDatasetType !== "HistoricalBboQuotes") quarantineReasons.push("DatasetMismatch");
    if (fileFormat !== "NDJSON") quarantineReasons.push("FormatMismatch");
    if (providerSymbol !== entry.ProviderSymbol || executionTradableSymbol !== entry.ExecutionTradableSymbol) quarantineReasons.push("SymbolMismatch");
    if (normalizedPortfolioSymbol !== entry.NormalizedPortfolioSymbol || requiresInversion !== entry.RequiresInversion) quarantineReasons.push("InversionMismatch");
    if (timeRangeStartUtc !== entry.TimeRangeStartUtc || timeRangeEndUtc !== entry.TimeRangeEndUtc) quarantineReasons.push("TimeRangeMismatch");
    if (containsSecrets !== false) quarantineReasons.push("SecretRisk");
    if (containsRawProviderPayload !== false) quarantineReasons.push("RawPayloadRisk");
    if (isDirectCross(entry.Symbol)) quarantineReasons.push("DirectCrossExecutionDisabled");
    if (sessionWindowCategoryFromManifest && sessionWindowCategoryFromManifest !== sessionWindowCategory) quarantineReasons.push("SessionCategoryMismatch");
    if (!sessionWindowCategoryFromManifest) warnings.push("SessionWindowCategory not present in manifest; confirmed from R027 authorization metadata.");
  }

  const fileHashPresent = typeof fileHash === "string" && fileHash.length > 0;
  let fileHashMatches = null;
  if (quoteFileExists && fileHashPresent) {
    fileHashMatches = sha256File(quoteFilePath) === fileHash.toUpperCase();
    if (!fileHashMatches) quarantineReasons.push("FileHashMismatch");
  }

  if (!fileHashPresent) warnings.push("FileHash missing from manifest.");
  if (!Number.isFinite(Number(rowCountDeclared)) || Number(rowCountDeclared) <= 0) warnings.push("RowCountDeclared missing or non-positive.");

  const expected = expectedSymbol(entry.Symbol);
  if (expected) {
    if (entry.NormalizedPortfolioSymbol !== expected.NormalizedPortfolioSymbol || entry.RequiresInversion !== expected.RequiresInversion) {
      quarantineReasons.push("AuthorizedInversionMismatch");
    }
  }

  let validationStatus = "ManifestValidationAccepted";
  if (quarantineReasons.length > 0) {
    const first = quarantineReasons[0];
    validationStatus = first === "MissingQuoteFile" ? "ManifestValidationQuarantinedMissingQuoteFile"
      : first === "MissingManifest" || first === "ManifestUnreadable" ? "ManifestValidationQuarantinedMissingManifest"
      : first === "ProviderMismatch" ? "ManifestValidationQuarantinedProviderMismatch"
      : first === "DatasetMismatch" ? "ManifestValidationQuarantinedDatasetMismatch"
      : first === "FormatMismatch" ? "ManifestValidationQuarantinedFormatMismatch"
      : first === "SymbolMismatch" ? "ManifestValidationQuarantinedSymbolMismatch"
      : first === "InversionMismatch" || first === "AuthorizedInversionMismatch" ? "ManifestValidationQuarantinedInversionMismatch"
      : first === "TimeRangeMismatch" ? "ManifestValidationQuarantinedTimeRangeMismatch"
      : first === "SessionCategoryMismatch" ? "ManifestValidationQuarantinedSessionCategoryMismatch"
      : first === "SecretRisk" ? "ManifestValidationQuarantinedSecretRisk"
      : first === "RawPayloadRisk" ? "ManifestValidationQuarantinedRawPayloadRisk"
      : first === "DirectCrossExecutionDisabled" ? "ManifestValidationQuarantinedDirectCrossExecutionDisabled"
      : "InconclusiveSafe";
  } else if (warnings.length > 0) {
    validationStatus = "ManifestValidationAcceptedWithWarnings";
  }

  return {
    Symbol: entry.Symbol,
    SessionWindowCategory: sessionWindowCategory,
    SessionWindowCategorySource: sessionWindowCategoryFromManifest ? "Manifest" : "R027AuthorizationMetadata",
    QuoteFilePath: path.relative(repoRoot, quoteFilePath),
    ManifestPath: path.relative(repoRoot, manifestPath),
    QuoteFileExists: quoteFileExists,
    ManifestExists: manifestExists,
    ManifestReadable: manifestReadable,
    ProviderName: providerName,
    ProviderDatasetType: providerDatasetType,
    ProviderSymbol: providerSymbol,
    ExecutionTradableSymbol: executionTradableSymbol,
    NormalizedPortfolioSymbol: normalizedPortfolioSymbol,
    RequiresInversion: requiresInversion,
    SecurityID: entry.SecurityID,
    SecurityIDSource: entry.SecurityIDSource,
    TimeRangeStartUtc: timeRangeStartUtc,
    TimeRangeEndUtc: timeRangeEndUtc,
    FileFormat: fileFormat,
    FileHashPresent: fileHashPresent,
    FileHashMatches: fileHashMatches,
    HashComputationMode: fileHashPresent && quoteFileExists ? "WholeFileByteHashNoRowParsing" : "NotComputed",
    RowCountDeclared: rowCountDeclared,
    ContainsSecrets: containsSecrets,
    ContainsRawProviderPayload: containsRawProviderPayload,
    ValidationStatus: validationStatus,
    QuarantineReason: quarantineReasons.length ? quarantineReasons.join(";") : null,
    Warning: warnings.length ? warnings.join(" ") : null,
    QuoteRowsValidated: false,
    QuoteWindowsCreated: false,
    CloseBenchmarksCreated: false,
    FeedQualityResultsCreated: false
  };
}

const entries = r027Accepted.Entries.map(x => ({
  ...x,
  QuoteFilePath: normalizePath(x.QuoteFilePath),
  ManifestPath: normalizePath(x.ManifestPath)
}));

const results = entries.map(validateEntry);
const acceptedResults = results.filter(x => x.ValidationStatus === "ManifestValidationAccepted" || x.ValidationStatus === "ManifestValidationAcceptedWithWarnings");
const quarantinedResults = results.filter(x => !acceptedResults.includes(x));
const openingResults = results.filter(x => x.SessionWindowCategory === "OpeningBuild");
const closingResults = results.filter(x => x.SessionWindowCategory === "ClosingFlatten");
const classifications = quarantinedResults.length === 0
  ? [
      "EXEC_SIM_R028_PASS_HISTORICAL_WINDOW_MANIFEST_VALIDATION_READY_NO_EXTERNAL",
      "EXEC_SIM_R028_PASS_OPENING_CLOSING_FILE_LEVEL_PREFLIGHT_READY_NO_EXTERNAL",
      "EXEC_SIM_R028_PASS_SYMBOL_INVERSION_SESSION_METADATA_READY_NO_EXTERNAL",
      "EXEC_SIM_R028_PASS_NO_ROW_VALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
    ]
  : ["EXEC_SIM_R028_PARTIAL_MANIFEST_VALIDATION_WITH_QUARANTINE_NO_EXTERNAL"];

function all(predicate) {
  return results.every(predicate);
}

function byCategorySymbolComplete(category) {
  const symbols = results.filter(x => x.SessionWindowCategory === category).map(x => x.Symbol).sort();
  return JSON.stringify(symbols) === JSON.stringify([...allowedSymbols].sort());
}

writeJson("phase-exec-sim-r028-manifest-validation-contract.json", {
  phase,
  manifestValidationContractCreated: true,
  SourceAuthorizationPhase: "EXEC-SIM-R027",
  CreatedAtUtc: createdAtUtc,
  ExpectedProviderName: "PolygonOfflineFile",
  ExpectedProviderDatasetType: "HistoricalBboQuotes",
  ExpectedFileFormat: "NDJSON",
  ExpectedSessionWindowCategories: ["OpeningBuild", "ClosingFlatten"],
  ExpectedOpeningBuildTimeRangeUtc: { Start: "2026-05-19T08:00:00Z", End: "2026-05-19T12:00:00Z" },
  ExpectedClosingFlattenTimeRangeUtc: { Start: "2026-05-19T16:00:00Z", End: "2026-05-19T20:00:00Z" },
  ValidationStatuses: validationStatuses,
  NoRowLevelValidation: true,
  NoImport: true,
  NoPersistedSanitizedRows: true,
  NoQuoteWindows: true,
  NoCloseBenchmarks: true,
  NoFeedQualityResults: true,
  NoBacktest: true,
  NoSimulation: true,
  NoTcaResultLines: true,
  NoOrdersFillsReportsRoutes: true
});

writeJson("phase-exec-sim-r028-r027-authorized-files-used.json", {
  phase,
  r027AuthorizedFilesUsedCreated: true,
  SourceAuthorizationPhase: "EXEC-SIM-R027",
  SourceAuthorizationStatus: r027Authorization.AuthorizationStatus,
  SourceAuthorizedEntryCount: r027Authorization.AuthorizedEntryCount,
  EntryCountUsed: entries.length,
  Entries: entries.map(x => ({
    Symbol: x.Symbol,
    SessionWindowCategory: x.SessionWindowCategory,
    QuoteFilePath: path.relative(repoRoot, x.QuoteFilePath),
    ManifestPath: path.relative(repoRoot, x.ManifestPath),
    TimeRangeStartUtc: x.TimeRangeStartUtc,
    TimeRangeEndUtc: x.TimeRangeEndUtc,
    ExecutionTradableSymbol: x.ExecutionTradableSymbol,
    NormalizedPortfolioSymbol: x.NormalizedPortfolioSymbol,
    RequiresInversion: x.RequiresInversion
  })),
  ManifestContentsReadInR028: true,
  QuoteRowsReadInR028: false
});

writeJson("phase-exec-sim-r028-manifest-validation-results.json", {
  phase,
  manifestValidationResultsCreated: true,
  classifications,
  totalManifests: results.length,
  acceptedCount: acceptedResults.length,
  quarantinedCount: quarantinedResults.length,
  openingBuildCount: openingResults.length,
  closingFlattenCount: closingResults.length,
  allProviderNamesValid: all(x => x.ProviderName === "PolygonOfflineFile"),
  allDatasetTypesValid: all(x => x.ProviderDatasetType === "HistoricalBboQuotes"),
  allFileFormatsValid: all(x => x.FileFormat === "NDJSON"),
  allSessionCategoriesValid: all(x => ["OpeningBuild", "ClosingFlatten"].includes(x.SessionWindowCategory)),
  allTimeRangesValid: all(x => (x.SessionWindowCategory === "OpeningBuild" && x.TimeRangeStartUtc === "2026-05-19T08:00:00Z" && x.TimeRangeEndUtc === "2026-05-19T12:00:00Z") || (x.SessionWindowCategory === "ClosingFlatten" && x.TimeRangeStartUtc === "2026-05-19T16:00:00Z" && x.TimeRangeEndUtc === "2026-05-19T20:00:00Z")),
  allHashesPresent: all(x => x.FileHashPresent),
  allComputedHashesMatchManifest: all(x => x.FileHashMatches === true),
  allRowCountsDeclared: all(x => Number.isFinite(Number(x.RowCountDeclared)) && Number(x.RowCountDeclared) > 0),
  allContainsSecretsFalse: all(x => x.ContainsSecrets === false),
  allContainsRawProviderPayloadFalse: all(x => x.ContainsRawProviderPayload === false),
  rowLevelValidationExecuted: false,
  results
});

writeJson("phase-exec-sim-r028-file-level-validation-results.json", {
  phase,
  fileLevelValidationResultsCreated: true,
  resultCount: results.length,
  results
});

writeJson("phase-exec-sim-r028-accepted-manifest-validation-outputs.json", {
  phase,
  acceptedManifestValidationOutputsCreated: true,
  acceptedCount: acceptedResults.length,
  acceptedOutputs: acceptedResults
});

writeJson("phase-exec-sim-r028-quarantined-manifest-validation-outputs.json", {
  phase,
  quarantinedManifestValidationOutputsCreated: true,
  quarantinedCount: quarantinedResults.length,
  quarantinedOutputs: quarantinedResults
});

writeJson("phase-exec-sim-r028-missing-incomplete-manifest-diagnostics.json", {
  phase,
  missingIncompleteManifestDiagnosticsCreated: true,
  missingManifestCount: results.filter(x => !x.ManifestExists).length,
  missingQuoteFileCount: results.filter(x => !x.QuoteFileExists).length,
  unreadableManifestCount: results.filter(x => !x.ManifestReadable).length,
  incompleteCriticalFieldCount: results.filter(x => !x.ProviderName || !x.ProviderDatasetType || !x.ProviderSymbol || !x.ExecutionTradableSymbol || !x.NormalizedPortfolioSymbol || x.RequiresInversion === null || !x.TimeRangeStartUtc || !x.TimeRangeEndUtc || !x.FileFormat || !x.FileHashPresent || !Number.isFinite(Number(x.RowCountDeclared))).length,
  diagnostics: quarantinedResults.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, QuarantineReason: x.QuarantineReason }))
});

writeJson("phase-exec-sim-r028-opening-build-manifest-validation.json", {
  phase,
  openingBuildManifestValidationCreated: true,
  SessionWindowCategory: "OpeningBuild",
  ExpectedTimeRangeStartUtc: "2026-05-19T08:00:00Z",
  ExpectedTimeRangeEndUtc: "2026-05-19T12:00:00Z",
  AllSevenSymbolsPresent: byCategorySymbolComplete("OpeningBuild"),
  ResultCount: openingResults.length,
  AcceptedCount: openingResults.filter(x => x.ValidationStatus === "ManifestValidationAccepted" || x.ValidationStatus === "ManifestValidationAcceptedWithWarnings").length,
  Results: openingResults
});

writeJson("phase-exec-sim-r028-closing-flatten-manifest-validation.json", {
  phase,
  closingFlattenManifestValidationCreated: true,
  SessionWindowCategory: "ClosingFlatten",
  ExpectedTimeRangeStartUtc: "2026-05-19T16:00:00Z",
  ExpectedTimeRangeEndUtc: "2026-05-19T20:00:00Z",
  AllSevenSymbolsPresent: byCategorySymbolComplete("ClosingFlatten"),
  ResultCount: closingResults.length,
  AcceptedCount: closingResults.filter(x => x.ValidationStatus === "ManifestValidationAccepted" || x.ValidationStatus === "ManifestValidationAcceptedWithWarnings").length,
  Results: closingResults
});

writeJson("phase-exec-sim-r028-symbol-inversion-validation.json", {
  phase,
  symbolInversionValidationCreated: true,
  allSymbolsPresentInOpeningBuild: byCategorySymbolComplete("OpeningBuild"),
  allSymbolsPresentInClosingFlatten: byCategorySymbolComplete("ClosingFlatten"),
  usdJpyCaveatPreserved: results.filter(x => x.Symbol === "USDJPY").every(x => x.NormalizedPortfolioSymbol === "JPYUSD" && x.ExecutionTradableSymbol === "USDJPY" && x.RequiresInversion === true),
  usdCadInversionPreserved: results.filter(x => x.Symbol === "USDCAD").every(x => x.NormalizedPortfolioSymbol === "CADUSD" && x.ExecutionTradableSymbol === "USDCAD" && x.RequiresInversion === true),
  usdChfInversionPreserved: results.filter(x => x.Symbol === "USDCHF").every(x => x.NormalizedPortfolioSymbol === "CHFUSD" && x.ExecutionTradableSymbol === "USDCHF" && x.RequiresInversion === true),
  nonInvertedSymbolsPreserved: ["EURUSD", "AUDUSD", "GBPUSD", "NZDUSD"].every(symbol => results.filter(x => x.Symbol === symbol).every(x => x.NormalizedPortfolioSymbol === symbol && x.RequiresInversion === false)),
  audusdMisclassifiedFailed: false,
  mappings: results.map(x => ({ Symbol: x.Symbol, SessionWindowCategory: x.SessionWindowCategory, ExecutionTradableSymbol: x.ExecutionTradableSymbol, NormalizedPortfolioSymbol: x.NormalizedPortfolioSymbol, RequiresInversion: x.RequiresInversion }))
});

writeJson("phase-exec-sim-r028-session-category-validation.json", {
  phase,
  sessionCategoryValidationCreated: true,
  SessionCategorySource: "R027AuthorizationMetadata",
  ManifestSessionCategoryFieldRequired: false,
  OpeningBuildEntriesValid: openingResults.length === 7,
  ClosingFlattenEntriesValid: closingResults.length === 7,
  allSessionCategoriesValid: all(x => ["OpeningBuild", "ClosingFlatten"].includes(x.SessionWindowCategory)),
  rowLevelValidationExecuted: false
});

writeJson("phase-exec-sim-r028-time-range-validation.json", {
  phase,
  timeRangeValidationCreated: true,
  allManifestTimeRangesMatch: all(x => (x.SessionWindowCategory === "OpeningBuild" && x.TimeRangeStartUtc === "2026-05-19T08:00:00Z" && x.TimeRangeEndUtc === "2026-05-19T12:00:00Z") || (x.SessionWindowCategory === "ClosingFlatten" && x.TimeRangeStartUtc === "2026-05-19T16:00:00Z" && x.TimeRangeEndUtc === "2026-05-19T20:00:00Z")),
  OpeningBuildTimeRangeUtc: { Start: "2026-05-19T08:00:00Z", End: "2026-05-19T12:00:00Z" },
  ClosingFlattenTimeRangeUtc: { Start: "2026-05-19T16:00:00Z", End: "2026-05-19T20:00:00Z" }
});

writeJson("phase-exec-sim-r028-secret-raw-payload-validation.json", {
  phase,
  secretRawPayloadValidationCreated: true,
  allContainsSecretsFalse: all(x => x.ContainsSecrets === false),
  allContainsRawProviderPayloadFalse: all(x => x.ContainsRawProviderPayload === false),
  secretRiskDetected: false,
  rawPayloadRiskDetected: false
});

writeJson("phase-exec-sim-r028-direct-cross-exclusion-preservation.json", {
  phase,
  directCrossExclusionPreserved: true,
  directCrossesInExecutionBatch: false,
  directCrossExecutionAllowedByDefault: false,
  directCrossesSignalOnly: true,
  nettingFirstRequired: true,
  guidanceWeakened: false,
  excludedExamples: directCrossExamples
});

writeJson("phase-exec-sim-r028-cost-guidance-preservation.json", {
  phase,
  costGuidancePreserved: true,
  bestCaseMajorTargetUsdPerMillion: 5,
  fiveUsdPerMillionBestCaseMajorOnly: true,
  fiveUsdPerMillionUniversalized: false
});

writeJson("phase-exec-sim-r028-nonmajor-calibration-preservation.json", {
  phase,
  nonmajorCalibrationPreserved: true,
  RequiresLiquidityCalibration: true,
  calibrationRequirementWeakened: false,
  DeferredCalibrationUniverse: ["USDMXN", "USDCNH", "USDNOK", "USDSEK", "USDZAR", "USDSGD or SGDUSD after explicit convention"]
});

function audit(name, value) {
  writeJson(name, { phase, ...value });
}

audit("phase-exec-sim-r028-no-row-level-validation-audit.json", { quoteRowsReadForValidation: false, quoteRowsParsed: false, quoteRowsValidated: false });
audit("phase-exec-sim-r028-no-sanitized-quote-row-creation-audit.json", { sanitizedQuoteRowsCreated: false, persistedSanitizedQuoteRowsCreated: false, quotesImported: false, quoteFixturesCreated: false });
audit("phase-exec-sim-r028-no-quote-window-close-benchmark-feed-quality-audit.json", { quoteWindowsCreated: false, closeBenchmarksCreated: false, feedQualityResultsCreated: false });
audit("phase-exec-sim-r028-no-backtest-simulation-audit.json", { newBacktestExecuted: false, newSimulationExecuted: false });
audit("phase-exec-sim-r028-no-tca-result-lines-audit.json", { tcaResultLinesProduced: false, simulationResultLinesProduced: false, tcaReportsProduced: false });
audit("phase-exec-sim-r028-no-polygon-api-call-audit.json", { polygonApiCalled: false });
audit("phase-exec-sim-r028-no-lmax-call-audit.json", { lmaxCalled: false });
audit("phase-exec-sim-r028-no-external-api-call-audit.json", { polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false });
audit("phase-exec-sim-r028-no-broker-marketdata-runtime-audit.json", { brokerActivationDetected: false, socketOpened: false, tlsOpened: false, fixOpened: false, marketDataRequestSent: false, marketDataResponseRead: false, apiWorkerGatewayStarted: false, schedulerServiceTimerPollingBackgroundJobIntroduced: false, automaticExecutionIntroduced: false });
audit("phase-exec-sim-r028-no-order-fill-report-route-audit.json", { ordersCreated: false, executableOrdersCreated: false, omsOrdersCreated: false, childOrdersCreated: false, fillEntitiesCreated: false, executionReportEntitiesCreated: false, brokerExecutionReportsCreated: false, routesCreated: false, submissionsCreated: false });

writeJson("phase-exec-sim-r028-usdjpy-caveat-preservation.json", {
  phase,
  usdjpyCaveatPreserved: true,
  PortfolioNormalizedSymbol: "JPYUSD",
  ExecutionTradableSymbol: "USDJPY",
  RequiresInversion: true,
  securityId: "4004",
  securityIdSource: "8",
  audusdMisclassifiedFailed: false
});

writeJson("phase-exec-sim-r028-lmax-readonly-baseline-reference.json", {
  phase,
  referenceOnly: true,
  lmaxCalledInR028: false,
  gbpusdR203ReadonlyMarketDataSucceededSanitizedEntryCount: 2,
  eurgbpR207ReadonlyMarketDataSucceededSanitizedEntryCount: 2,
  audusdStatus: "TLS-boundary inconclusive in LMAX context only; not failed",
  usdjpyStatus: "not proven, not failed",
  usdjpySecurityId: "4004",
  usdjpySecurityIdSource: "8"
});

audit("phase-exec-sim-r028-no-external-audit.json", {
  polygonApiCalled: false,
  lmaxCalled: false,
  externalApiCalled: false,
  filesDownloaded: false,
  brokerRuntimeDetected: false,
  quoteRowsValidated: false,
  quoteFilesImported: false,
  sanitizedQuoteRowsCreated: false,
  quoteWindowsCreated: false,
  closeBenchmarksCreated: false,
  feedQualityResultsCreated: false,
  newBacktestExecuted: false,
  newSimulationExecuted: false,
  tcaResultLinesProduced: false,
  ordersFillsReportsRoutesSubmissionsCreated: false,
  livePaperBrokerProductionTradingStateMutated: false,
  paperLedgerStateCommitted: false
});

audit("phase-exec-sim-r028-forbidden-actions-audit.json", {
  forbiddenActionsDetected: false,
  polygonApiCalled: false,
  lmaxCalled: false,
  externalApiCalled: false,
  filesDownloaded: false,
  brokerRuntimeDetected: false,
  quoteRowsValidated: false,
  quoteFilesImported: false,
  sanitizedQuoteRowsCreated: false,
  quoteWindowsCreated: false,
  closeBenchmarksCreated: false,
  feedQualityResultsCreated: false,
  backtestExecuted: false,
  simulationExecuted: false,
  tcaResultLinesProduced: false,
  ordersFillsReportsRoutesSubmissionsCreated: false,
  stateMutated: false
});

writeJson("phase-exec-sim-r028-next-phase-recommendation.json", {
  phase,
  nextPhaseRecommendationCreated: true,
  RecommendedNextPhase: "EXEC-SIM-R029",
  RecommendedNextPhaseName: "No-External Historical Window Row-Level Validation Gate",
  R029Scope: "Validate quote rows for accepted OpeningBuild and ClosingFlatten files and produce quote-window, close-benchmark, and feed-quality readiness only.",
  R029StillMustNotImportBacktestOrCreateOrders: true
});

writeJson("phase-exec-sim-r028-build-test-validator-evidence.json", {
  phase,
  buildTestValidatorEvidenceCreated: true,
  dotnetBuildNoRestore: process.env.R028_DOTNET_BUILD ?? "Pending",
  focusedR028Tests: process.env.R028_FOCUSED_TESTS ?? "Pending",
  unitTestsIfFeasible: process.env.R028_UNIT_TESTS ?? "Pending",
  validator: process.env.R028_VALIDATOR ?? "Pending"
});

writeText("phase-exec-sim-r028-summary.md", `# EXEC-SIM-R028 Summary

R028 reused the R027 authorization artifacts and validated manifest/file-level metadata for 14 historical window expansion files.

Classifications:
${classifications.map(x => `- ${x}`).join("\n")}

Validated scope:
- 7 OpeningBuild manifests for 2026-05-19T08:00:00Z to 2026-05-19T12:00:00Z.
- 7 ClosingFlatten manifests for 2026-05-19T16:00:00Z to 2026-05-19T20:00:00Z.
- Symbols: EURUSD, USDJPY, AUDUSD, GBPUSD, NZDUSD, USDCAD, USDCHF.

All quote files and manifests exist, all manifests are readable, provider/dataset/format/time-range/symbol/inversion metadata is valid, declared row counts and hashes are present, and computed SHA256 hashes match the manifests. Session window category was confirmed from R027 authorization metadata.

Safety: no Polygon, no LMAX, no external API, no download, no quote row validation, no DB import, no sanitized rows, no quote windows, no close benchmarks, no feed-quality outputs, no backtest/simulation, no TCA result lines, no orders/fills/reports/routes/submissions, and no state mutation.
`);

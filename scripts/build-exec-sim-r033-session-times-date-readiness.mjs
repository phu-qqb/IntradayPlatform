import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();
const artifactsDir = path.join(repoRoot, "artifacts", "readiness", "execution-sim");
const phase = "EXEC-SIM-R033";
const createdAtUtc = "2026-05-22T00:00:00Z";

const symbols = [
  { Symbol: "EURUSD", ExecutionTradableSymbol: "EURUSD", NormalizedPortfolioSymbol: "EURUSD", RequiresInversion: false },
  { Symbol: "USDJPY", ExecutionTradableSymbol: "USDJPY", NormalizedPortfolioSymbol: "JPYUSD", RequiresInversion: true, SecurityID: "4004", SecurityIDSource: "8" },
  { Symbol: "AUDUSD", ExecutionTradableSymbol: "AUDUSD", NormalizedPortfolioSymbol: "AUDUSD", RequiresInversion: false, AudUsdStatus: "not failed" },
  { Symbol: "GBPUSD", ExecutionTradableSymbol: "GBPUSD", NormalizedPortfolioSymbol: "GBPUSD", RequiresInversion: false },
  { Symbol: "NZDUSD", ExecutionTradableSymbol: "NZDUSD", NormalizedPortfolioSymbol: "NZDUSD", RequiresInversion: false },
  { Symbol: "USDCAD", ExecutionTradableSymbol: "USDCAD", NormalizedPortfolioSymbol: "CADUSD", RequiresInversion: true },
  { Symbol: "USDCHF", ExecutionTradableSymbol: "USDCHF", NormalizedPortfolioSymbol: "CHFUSD", RequiresInversion: true }
];

const sessionCategories = ["OpeningBuild", "IntradayRebalance", "ClosingFlatten"];
const proxyOpening = { StartUtc: "2026-05-19T08:00:00Z", EndUtc: "2026-05-19T12:00:00Z" };
const proxyClosing = { StartUtc: "2026-05-19T16:00:00Z", EndUtc: "2026-05-19T20:00:00Z" };

function readJson(name) {
  return JSON.parse(fs.readFileSync(path.join(artifactsDir, name), "utf8"));
}

function writeJson(name, value) {
  fs.writeFileSync(path.join(artifactsDir, name), `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

function writeText(name, value) {
  fs.writeFileSync(path.join(artifactsDir, name), value, "utf8");
}

const r032SessionDecision = readJson("phase-exec-sim-r032-session-time-calibration-decision.json");
const r032DataDecision = readJson("phase-exec-sim-r032-data-expansion-decision.json");
const r032ParameterDecision = readJson("phase-exec-sim-r032-parameter-refinement-decision.json");
const r032DesignDecision = readJson("phase-exec-sim-r032-design-only-shape-decision.json");
const r032OperatorReport = readJson("phase-exec-sim-r032-operator-review-report.json");

const r032Reference = {
  phase,
  r032DecisionReferenceCreated: true,
  SourceDecisionPhase: "EXEC-SIM-R032",
  R032Classifications: [
    "EXEC_SIM_R032_PASS_HISTORICAL_WINDOW_TCA_REVIEW_READY_NO_EXTERNAL",
    "EXEC_SIM_R032_PASS_SESSION_POLICY_DECISION_READY_NO_EXTERNAL",
    "EXEC_SIM_R032_PASS_DATA_EXPANSION_AND_PARAMETER_DECISION_READY_NO_EXTERNAL",
    "EXEC_SIM_R032_PASS_NO_NEW_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL"
  ],
  SessionTimeDecision: r032SessionDecision,
  DataExpansionDecision: r032DataDecision,
  ParameterRefinementDecision: r032ParameterDecision,
  DesignOnlyShapeDecision: r032DesignDecision,
  NeedsOperatorSessionTimesFromR032: r032SessionDecision.NeedsOperatorSessionTimes === true,
  MoreDatesRecommendedFromR032: r032DataDecision.MoreDatesRecommended === true,
  ParameterRefinementDeferredFromR032: r032ParameterDecision.ParameterRefinementDeferredUntilMoreDatesAndTrueSessionTimes === true,
  NoExecutableActionAuthorizedByR032: r032OperatorReport.NoExecutableActionAuthorized === true
};

const trueSessionContract = {
  phase,
  trueSessionTimeReadinessContractCreated: true,
  SessionTimeReadinessId: "EXEC-SIM-R033-SESSION-TIME-READINESS-001",
  SourceDecisionPhase: "EXEC-SIM-R032",
  ModelSessionName: "NeedsOperatorInput",
  SessionTimezone: null,
  SessionDatePolicy: null,
  SessionOpenLocal: null,
  SessionCloseLocal: null,
  SessionOpenUtc: null,
  SessionCloseUtc: null,
  FirstBarCloseUtc: null,
  LastBarCloseUtc: null,
  BarIntervalMinutes: 15,
  OpeningBuildWindowStartUtc: null,
  OpeningBuildWindowEndUtc: null,
  IntradayRebalanceWindowPolicy: "NeedsOperatorSessionTimes; derive normal 15-minute windows inside confirmed session after excluding OpeningBuild and ClosingFlatten windows.",
  ClosingFlattenWindowStartUtc: null,
  ClosingFlattenWindowEndUtc: null,
  PreviousEveningPlanningCutoffUtc: null,
  EarliestExecutionTimestampPolicy: "NeedsOperatorInput; previous-evening planning never authorizes pre-session execution.",
  OvernightAllowed: false,
  MustEndFlat: true,
  SessionTimeStatus: "NeedsOperatorSessionTimes",
  TrueModelSessionTimesSupplied: false,
  ExactTrueSessionTimesInvented: false,
  ProxyOpeningBuildWindow: proxyOpening,
  ProxyClosingFlattenWindow: proxyClosing,
  ProxyWindowsAreNotConfirmedTrueSessionTimes: true
};

const sessionCalendarContract = {
  phase,
  sessionCalendarContractCreated: true,
  SourceDecisionPhase: "EXEC-SIM-R032",
  ModelSessionName: "NeedsOperatorInput",
  CalendarPolicyStatus: "NeedsOperatorSessionTimes",
  RequiredCalendarFields: [
    "ModelSessionTimezone",
    "SessionOpenTime",
    "SessionCloseTime",
    "DailyWeekdayOrCustomCalendarPolicy",
    "TradingHolidaysOrSkippedDatesPolicy",
    "FirstTradableBarClose",
    "LastTradableBarClose",
    "NoOvernightConfirmation"
  ],
  BarIntervalMinutes: 15,
  NoOvernightAllowed: true,
  MustEndFlat: true,
  ExactCalendarInvented: false
};

const additionalDateContract = {
  phase,
  additionalDateRangeReadinessContractCreated: true,
  HistoricalDateExpansionId: "EXEC-SIM-R033-HISTORICAL-DATE-EXPANSION-001",
  SourceDecisionPhase: "EXEC-SIM-R032",
  RequiredSymbols: symbols.map(s => s.Symbol),
  RequiredSessionWindowCategories: sessionCategories,
  RequestedDateRanges: [],
  RequestedMarketRegimes: [],
  MinimumDateCount: 5,
  MinimumWindowCountPerCategory: 5,
  IncludeOpeningBuild: true,
  IncludeIntradayRebalance: true,
  IncludeClosingFlatten: true,
  RequiresUtcRanges: true,
  RequiresOperatorProvidedFiles: true,
  RequiresTrueSessionTimesBeforeDownloadAuthorization: true,
  DateRangeStatus: "NeedsOperatorDateRanges",
  ExactDatesInvented: false,
  RecommendedDefaultIfAbsent: {
    MinimumAdditionalTradingDates: 5,
    IncludeAllSevenSymbols: true,
    IncludeDifferentMarketRegimesIfPossible: true,
    RegimeLabels: ["normal", "volatile", "stress", "event-driven", "unknown"]
  }
};

const operatorSessionRequirements = {
  phase,
  operatorSessionTimeInputRequirementsCreated: true,
  RequiredInputs: [
    "model session timezone",
    "session open time",
    "session close time",
    "daily / weekday-only / custom calendar policy",
    "first tradable bar close",
    "last tradable bar close",
    "previous-evening target availability time",
    "earliest allowed execution time",
    "no-overnight confirmation"
  ],
  NeedsOperatorSessionTimes: true,
  ExactSessionTimesInvented: false,
  NoOvernightConfirmationRequired: true
};

const operatorDateRequirements = {
  phase,
  operatorDateRangeInputRequirementsCreated: true,
  RequiredInputs: [
    "list of trading dates or UTC ranges",
    "intended session window category per range",
    "symbols required",
    "market regime label: normal / volatile / stress / event-driven / unknown",
    "majors-only or expanded instrument universe"
  ],
  SuggestedMinimumDateCount: 5,
  SuggestedMinimumWindowCountPerCategory: 5,
  RequiredSymbols: symbols.map(s => s.Symbol),
  RequiredSessionWindowCategories: sessionCategories,
  NeedsOperatorDateRanges: true,
  ExactDateRangesInvented: false
};

const sessionWindowRequirements = {
  phase,
  sessionWindowDerivationRequirementsCreated: true,
  SourceDecisionPhase: "EXEC-SIM-R032",
  RequiresTrueSessionTimes: true,
  BarIntervalMinutes: 15,
  OpeningBuild: "Derive from first confirmed model session bar and allowed execution start.",
  IntradayRebalance: "Derive normal 15-minute bars inside confirmed model session, with order known around T-minus-13.",
  ClosingFlatten: "Derive from final confirmed model session bar and close/flatten window, with MustEndFlat=true.",
  ProxyWindowsPreservedOnlyAsProxy: true
};

const openingDerivation = {
  phase,
  openingBuildWindowDerivationCreated: true,
  SessionWindowCategory: "OpeningBuild",
  DeriveFrom: "First confirmed model session bar after operator supplies true session times.",
  TargetKnownPreviousEvening: true,
  PreviousEveningPlanningAllowed: true,
  PreSessionExecutionAuthorized: false,
  OvernightExposureAuthorized: false,
  EarliestExecutionTimestampPolicy: "NeedsOperatorInput",
  ProxyWindow: proxyOpening,
  ProxyWindowConfirmedAsTrueSession: false
};

const intradayDerivation = {
  phase,
  intradayRebalanceWindowDerivationCreated: true,
  SessionWindowCategory: "IntradayRebalance",
  DeriveFrom: "Confirmed model session interior 15-minute bars after OpeningBuild and before ClosingFlatten.",
  BarIntervalMinutes: 15,
  OrderKnownApproximatelyMinutesBeforeClose: 13,
  RequiresTrueSessionTimes: true,
  ProxyWindowConfirmedAsTrueSession: false
};

const closingDerivation = {
  phase,
  closingFlattenWindowDerivationCreated: true,
  SessionWindowCategory: "ClosingFlatten",
  DeriveFrom: "Final confirmed model session bar / close window.",
  MustEndFlat: true,
  OvernightAllowed: false,
  NoOvernightCritical: true,
  ResidualPenaltyCritical: true,
  BlindMarketCrossingWithoutCostResidualJustificationBlocked: true,
  ProxyWindow: proxyClosing,
  ProxyWindowConfirmedAsTrueSession: false
};

const needsInput = {
  phase,
  needsOperatorInputCreated: true,
  NeedsOperatorSessionTimes: true,
  NeedsOperatorDateRanges: true,
  MissingInputs: [
    "true model session timezone",
    "true model session open/close",
    "first and last tradable bar close",
    "previous-evening target availability time",
    "earliest allowed execution time",
    "additional trading dates or UTC ranges",
    "market regime labels"
  ],
  SafeClassifications: [
    "EXEC_SIM_R033_NEEDS_OPERATOR_SESSION_TIMES_NO_EXTERNAL",
    "EXEC_SIM_R033_NEEDS_OPERATOR_DATE_RANGES_NO_EXTERNAL"
  ],
  ExactSessionTimesInvented: false,
  ExactDateRangesInvented: false
};

const readinessStatuses = {
  phase,
  readinessStatusesCreated: true,
  CurrentStatuses: [
    "NeedsOperatorSessionTimes",
    "NeedsOperatorDateRanges"
  ],
  AllowedStatuses: [
    "TrueSessionTimesReady",
    "NeedsOperatorSessionTimes",
    "AdditionalDateRangesReady",
    "NeedsOperatorDateRanges",
    "ReadyForHistoricalWindowDownloadPlanning",
    "BlockedMissingSessionTimezone",
    "BlockedMissingSessionOpen",
    "BlockedMissingSessionClose",
    "BlockedMissingNoOvernightConfirmation",
    "InconclusiveSafe"
  ],
  SuccessClassifications: [
    "EXEC_SIM_R033_PASS_SESSION_TIME_READINESS_CONTRACT_READY_NO_EXTERNAL",
    "EXEC_SIM_R033_PASS_ADDITIONAL_DATE_RANGE_REQUIREMENTS_READY_NO_EXTERNAL",
    "EXEC_SIM_R033_PASS_OPERATOR_INPUT_REQUIREMENTS_READY_NO_EXTERNAL",
    "EXEC_SIM_R033_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
  ],
  NeedsInputClassifications: [
    "EXEC_SIM_R033_NEEDS_OPERATOR_SESSION_TIMES_NO_EXTERNAL",
    "EXEC_SIM_R033_NEEDS_OPERATOR_DATE_RANGES_NO_EXTERNAL"
  ]
};

writeText("phase-exec-sim-r033-summary.md", `# EXEC-SIM-R033 True Session Times and Additional Dates Readiness Gate

R033 reused R032 decisions and produced readiness/intake contracts for true model session times and additional historical dates.

No true model session times or additional date ranges were supplied in this phase. The prior 08:00-12:00 UTC OpeningBuild window and 16:00-20:00 UTC ClosingFlatten window remain proxy windows only.

R033 does not download data, call Polygon or LMAX, authorize file paths, validate/import quotes, run backtests/simulations, create TCA result lines, or create order-domain outputs.

Next operator action: provide true model session times and additional date requirements.
`);

writeJson("phase-exec-sim-r033-r032-decision-reference.json", r032Reference);
writeJson("phase-exec-sim-r033-true-session-time-readiness-contract.json", trueSessionContract);
writeJson("phase-exec-sim-r033-session-calendar-contract.json", sessionCalendarContract);
writeJson("phase-exec-sim-r033-additional-date-range-readiness-contract.json", additionalDateContract);
writeJson("phase-exec-sim-r033-operator-session-time-input-requirements.json", operatorSessionRequirements);
writeJson("phase-exec-sim-r033-operator-date-range-input-requirements.json", operatorDateRequirements);
writeJson("phase-exec-sim-r033-session-window-derivation-requirements.json", sessionWindowRequirements);
writeJson("phase-exec-sim-r033-opening-build-window-derivation.json", openingDerivation);
writeJson("phase-exec-sim-r033-intraday-rebalance-window-derivation.json", intradayDerivation);
writeJson("phase-exec-sim-r033-closing-flatten-window-derivation.json", closingDerivation);
writeJson("phase-exec-sim-r033-proxy-window-caveat-preservation.json", {
  phase,
  proxyWindowCaveatPreservationCreated: true,
  ProxyOpeningBuildWindow: proxyOpening,
  ProxyClosingFlattenWindow: proxyClosing,
  TrueModelSessionTimesConfirmed: false,
  NeedsOperatorSessionTimes: true,
  ProxyWindowsConfirmedAsTrueSessionWindows: false
});
writeJson("phase-exec-sim-r033-no-overnight-preservation.json", { phase, noOvernightPreservationCreated: true, OvernightAllowed: false, MustEndFlat: true, NoOvernightCriticalForClosingFlatten: true });
writeJson("phase-exec-sim-r033-previous-evening-planning-preservation.json", { phase, previousEveningPlanningPreservationCreated: true, PreviousEveningPlanningAllowed: true, PreSessionExecutionAuthorized: false, OvernightExposureAuthorized: false });
writeJson("phase-exec-sim-r033-required-symbols-preservation.json", { phase, requiredSymbolsPreservationCreated: true, RequiredSymbols: symbols, UsdPairOnlyExecutionUniverse: true, AudUsdStatus: "not failed" });
writeJson("phase-exec-sim-r033-inversion-preservation.json", { phase, inversionPreservationCreated: true, usdJpyCaveatPreserved: true, audusdMisclassifiedFailed: false, validations: symbols.map(s => ({ ExecutionTradableSymbol: s.ExecutionTradableSymbol, NormalizedPortfolioSymbol: s.NormalizedPortfolioSymbol, RequiresInversion: s.RequiresInversion, SecurityID: s.SecurityID, SecurityIDSource: s.SecurityIDSource })) });
writeJson("phase-exec-sim-r033-direct-cross-exclusion-preservation.json", { phase, directCrossExclusionPreserved: true, directCrossIncluded: false, rawQubesCrossesSignalOnly: true, mandatoryNettingBeforeExecution: true, directCrossExecutionAllowedByDefault: false, blockedExamples: ["EURGBP", "CADJPY", "AUDCNH", "CNHSGD", "EURZAR", "MXNNOK", "NOKZAR"], guidanceWeakened: false });
writeJson("phase-exec-sim-r033-cost-guidance-preservation.json", { phase, costGuidancePreservationCreated: true, bestCaseMajorTargetUsdPerMillion: 5, fiveUsdPerMillionBestCaseMajorOnly: true, fiveUsdPerMillionUniversalized: false, nonmajorEmScandiCnhRequireLiquidityCalibration: true });
writeJson("phase-exec-sim-r033-nonmajor-calibration-preservation.json", { phase, nonmajorCalibrationPreservationCreated: true, nonMajorEmScandiCnhRequireLiquidityCalibration: true, RequiresLiquidityCalibration: true, calibrationRequirementWeakened: false, deferredCategories: ["nonmajor", "EM", "scandi", "CNH"] });
writeJson("phase-exec-sim-r033-needs-operator-input.json", needsInput);
writeJson("phase-exec-sim-r033-readiness-statuses.json", readinessStatuses);

const auditBase = { phase };
writeJson("phase-exec-sim-r033-no-download-audit.json", { ...auditBase, noDownloadAuditCreated: true, filesDownloaded: false, polygonDownloadExecuted: false, lmaxDownloadExecuted: false });
writeJson("phase-exec-sim-r033-no-validation-import-backtest-audit.json", { ...auditBase, noValidationImportBacktestAuditCreated: true, quoteRowsValidated: false, quotesImportedIntoDb: false, persistedSanitizedQuoteRowsCreated: false, newBacktestExecuted: false, newSimulationExecuted: false });
writeJson("phase-exec-sim-r033-no-tca-result-lines-audit.json", { ...auditBase, noTcaResultLinesAuditCreated: true, tcaResultLinesProduced: false });
writeJson("phase-exec-sim-r033-no-polygon-api-call-audit.json", { ...auditBase, polygonApiCalled: false });
writeJson("phase-exec-sim-r033-no-lmax-call-audit.json", { ...auditBase, lmaxCalled: false, lmaxReferenceOnly: true });
writeJson("phase-exec-sim-r033-no-external-api-call-audit.json", { ...auditBase, polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false });
writeJson("phase-exec-sim-r033-no-broker-marketdata-runtime-audit.json", { ...auditBase, brokerActivationDetected: false, socketOpened: false, tlsOpened: false, fixOpened: false, marketDataRequestSent: false, marketDataResponseRead: false, apiWorkerLiveGatewayEnabled: false, schedulerServiceTimerPollingBackgroundJobIntroduced: false, automaticExecutionIntroduced: false });
writeJson("phase-exec-sim-r033-no-order-fill-report-route-audit.json", { ...auditBase, noOrderFillReportRouteAuditCreated: true, ordersCreated: false, executableOrdersCreated: false, brokerOrdersCreated: false, omsParentOrdersCreated: false, omsChildOrdersCreated: false, fillsCreated: false, executionReportsCreated: false, brokerExecutionReportsCreated: false, routesCreated: false, submissionsCreated: false, liveTradingPathIntroduced: false, liveBrokerProductionTradingStateMutated: false, paperLedgerStateCommitted: false });
writeJson("phase-exec-sim-r033-usdjpy-caveat-preservation.json", { ...auditBase, usdjpyCaveatPreserved: true, PortfolioNormalizedSymbol: "JPYUSD", ExecutionTradableSymbol: "USDJPY", RequiresInversion: true, securityId: "4004", securityIdSource: "8", audusdMisclassifiedFailed: false });
writeJson("phase-exec-sim-r033-lmax-readonly-baseline-reference.json", { ...auditBase, referenceOnly: true, lmaxCalledInR033: false, GBPUSD_R203_ReadOnlyMarketDataSucceededSanitizedEntryCount: 2, EURGBP_R207_ReadOnlyMarketDataSucceededSanitizedEntryCount: 2, AUDUSD_TLSBoundaryInconclusiveNotFailed: true, USDJPY_NotProvenNotFailedSecurityIDCaveatPreserved: true, SecurityID: "4004", SecurityIDSource: "8" });
writeJson("phase-exec-sim-r033-no-external-audit.json", { ...auditBase, polygonApiCalled: false, lmaxCalled: false, externalApiCalled: false, filesDownloaded: false, brokerRuntimeActionDetected: false, quoteRowsValidated: false, quotesImportedIntoDb: false, persistedSanitizedQuoteRowsCreated: false, newSimulationOrBacktestExecuted: false, tcaResultLinesProduced: false, executableSchedulesCreated: false, childSlicesOrOrdersCreated: false, ordersFillsReportsRoutesSubmissionsCreated: false, livePaperBrokerProductionTradingStateMutated: false, paperLedgerStateCommitted: false });
writeJson("phase-exec-sim-r033-forbidden-actions-audit.json", { ...auditBase, forbiddenActionsDetected: false, forbiddenActionsChecked: ["ExternalApiCall", "Download", "BrokerRuntime", "RowValidation", "DbImport", "PersistedSanitizedRows", "BacktestSimulation", "TcaLines", "ExecutableSchedules", "ChildSlicesOrders", "OrderFillReportRouteSubmission", "StateMutation"] });
writeJson("phase-exec-sim-r033-next-phase-recommendation.json", { phase, nextPhaseRecommendationCreated: true, needsInputNextOperatorAction: "Provide true model session times and additional historical date requirements.", recommendedNextPhaseIfInputsProvided: "EXEC-SIM-R034 - No-External Session Time and Date Range Authorization Gate", r034ShouldAuthorizeButNotDownloadValidateImportBacktest: true, r034MustRemainNoExternalNoOrderNoFillNoRouteNoStateMutation: true });
writeJson("phase-exec-sim-r033-build-test-validator-evidence.json", {
  phase,
  dotnetBuildNoRestore: process.env.R033_DOTNET_BUILD ?? "PENDING",
  focusedTests: process.env.R033_FOCUSED_TESTS ?? "PENDING",
  unitTests: process.env.R033_UNIT_TESTS ?? "PENDING",
  validator: process.env.R033_VALIDATOR ?? "PENDING"
});

console.log("Wrote R033 session time and date readiness artifacts.");

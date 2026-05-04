export type HealthDto = {
  application: string;
  environment: string;
  persistenceProvider: string;
  databaseReachable: boolean;
  pendingMigrationsCount: number;
  databaseTarget: string;
  executionGateway: string;
  marketDataMode: string;
  liveTradingEnabled: boolean;
  externalConnectionsEnabled: boolean;
  utcServerTime: string;
};

export type ReferenceDataIntegrityIssueDto = {
  id: string;
  type: string;
  severity: string;
  status: string;
  key: string;
  description: string;
  createdAtUtc: string;
};

export type ReferenceDataIntegrityDto = {
  checkedAtUtc: string;
  blockingIssueCount: number;
  warningIssueCount: number;
  issues: ReferenceDataIntegrityIssueDto[];
};

export type ModelRunDto = {
  id: string;
  fundId: string;
  modelName: string;
  asOfUtc: string;
  receivedAtUtc: string;
  effectiveAtUtc: string;
  frequencyMinutes: number;
  navUsd: number;
  status: string;
  inputHash: string;
  sourceFileName: string;
  isProcessed: boolean;
  targetQuantityMode: string;
};

export type CreateModelRunRequest = {
  modelName: string;
  asOfUtc: string;
  effectiveAtUtc: string;
  navUsd: number;
  frequencyMinutes: number;
  targetQuantityMode: string;
  weights: Array<{ symbol: string; weight: number; rawSecurityId?: string }>;
};

export type ProcessModelRunResult = {
  modelRunId: string;
  processed: boolean;
  status: string;
  blockedReason?: string | null;
  message?: string | null;
  tradeIntentCount: number;
  riskDecisionCount: number;
  orderCount: number;
  executionReportCount: number;
  fillCount: number;
  reconciliationBreakCount: number;
  isAlreadyProcessed: boolean;
  completedAtUtc: string;
};

export type ModelWeightBatchDto = {
  id: string;
  externalBatchId: string;
  sourceSystem: string;
  fundCode: string;
  fundId?: string | null;
  modelName: string;
  asOfUtc: string;
  effectiveAtUtc: string;
  frequencyMinutes: number;
  navUsd: number;
  targetQuantityMode: string;
  status: string;
  expectedRowCount?: number | null;
  contentHash?: string | null;
  createdAtUtc: string;
  readyAtUtc?: string | null;
  acceptedAtUtc?: string | null;
  promotedAtUtc?: string | null;
  rejectedAtUtc?: string | null;
  promotedModelRunId?: string | null;
  message?: string | null;
};

export type ModelWeightRowDto = {
  id: string;
  batchId: string;
  rawSecurityId: string;
  symbol: string;
  instrumentId?: string | null;
  weight: number;
  createdAtUtc: string;
};

export type ModelWeightValidationIssueDto = {
  id: string;
  batchId: string;
  issueType: string;
  severity: string;
  message: string;
  rowId?: string | null;
  rowNumber?: number | null;
  createdAtUtc: string;
};

export type ModelWeightPromotionResultDto = {
  batchId?: string | null;
  status?: string | null;
  promotedModelRunId?: string | null;
  modelRunId?: string | null;
  validationIssueCount: number;
  issues: ModelWeightValidationIssueDto[];
  message: string;
  succeeded: boolean;
  alreadyPromoted: boolean;
};

export type LmaxReportImportRunDto = {
  id: string;
  reportType: string;
  reportDate: string;
  venueId: string;
  brokerAccountId: string;
  status: string;
  fileName?: string | null;
  rowCount?: number | null;
  createdAtUtc: string;
  completedAtUtc?: string | null;
  message?: string | null;
};

export type LmaxReportValidationIssueDto = {
  id: string;
  importRunId: string;
  issueType: string;
  severity: string;
  message: string;
  rowNumber?: number | null;
  rawLine?: string | null;
  createdAtUtc: string;
};

export type LmaxIndividualTradeDto = {
  id: string;
  executionId: string;
  timestampUtc: string;
  tradeQuantity: number;
  tradePrice: number;
  tradeDate: string;
  lmaxSymbol: string;
  instrumentId?: string | null;
  instructionId?: string | null;
  orderId?: string | null;
  totalCommission: number;
  accountId: string;
  unitsBoughtSold: number;
  notionalValue: number;
  tradeUti: string;
};

export type LmaxTradeSummaryDto = {
  id: string;
  dateTimeUtc: string;
  instrument: string;
  type: string;
  currency: string;
  contracts: number;
  averagePrice: number;
  commissionRounded: number;
  notionalValue: number;
  lmaxSymbol: string;
  commissionFullPrecision: number;
  accountId: string;
};

export type LmaxCurrencyWalletDto = {
  id: string;
  reportDate: string;
  currency: string;
  walletBalance: number;
  rateToBaseCcy: number;
  walletBalanceBaseUsd: number;
  profitLoss: number;
  profitLossBaseUsd: number;
  commission: number;
  commissionBaseUsd: number;
  dividends: number;
  dividendsBaseUsd: number;
  financing: number;
  financingBaseUsd: number;
  accountId: string;
};

export type GenerateFakeLmaxEodRequest = {
  reportDate: string;
  venueName?: string;
  brokerAccountCode?: string;
  mutationMode?: string;
};

export type FakeLmaxEodReportGenerationDto = {
  reportDate: string;
  individualTradesPath: string;
  tradesSummaryPath: string;
  currencyWalletsPath: string;
  individualTradeCount: number;
  tradeSummaryCount: number;
  currencyWalletCount: number;
  mutationMode: string;
};

export type LmaxReportImportResultDto = {
  importRunId: string;
  status: string;
  rowCount: number;
  blockingIssueCount: number;
  issues: LmaxReportValidationIssueDto[];
  message: string;
};

export type EodReconciliationRunDto = {
  id: string;
  reportDate: string;
  venueId: string;
  brokerAccountId: string;
  createdAtUtc: string;
  hasBlockingBreaks: boolean;
};

export type EodReconciliationBreakDto = {
  id: string;
  runId: string;
  type: string;
  severity: string;
  status: string;
  instrumentId?: string | null;
  description: string;
  brokerExecutionId?: string | null;
  internalFillId?: string | null;
  createdAtUtc: string;
};

export type EodPnlCurrencyRowDto = {
  currency: string;
  walletBalance: number;
  rateToBaseCcy: number;
  walletBalanceBaseUsd: number;
  profitLoss: number;
  profitLossBaseUsd: number;
  commission: number;
  commissionBaseUsd: number;
  dividends: number;
  dividendsBaseUsd: number;
  financing: number;
  financingBaseUsd: number;
};

export type EodPnlSummaryDto = {
  reportDate: string;
  venueName: string;
  brokerAccountCode: string;
  totalWalletBalanceUsd: number;
  totalProfitLossUsd: number;
  totalCommissionUsd: number;
  totalDividendsUsd: number;
  totalFinancingUsd: number;
  totalNetPnlUsd: number;
  currencyRows: EodPnlCurrencyRowDto[];
};

export type CreateFakeModelWeightBatchRequest = {
  externalBatchId?: string;
  sourceSystem: string;
  fundCode: string;
  modelName: string;
  asOfUtc: string;
  effectiveAtUtc: string;
  frequencyMinutes: number;
  navUsd: number;
  targetQuantityMode: string;
  status: string;
  weights: Array<{ rawSecurityId: string; symbol: string; weight: number }>;
};

export type TargetPositionDto = {
  modelRunId: string;
  instrumentId: string;
  symbol?: string | null;
  targetNotionalUsd: number;
  targetBaseQuantity: number;
  targetVenueQuantity: number;
  targetQuantityMode: string;
};

export type DriftSnapshotDto = {
  modelRunId: string;
  instrumentId: string;
  symbol?: string | null;
  targetBaseQuantity: number;
  currentBaseQuantity: number;
  driftBaseQuantity: number;
  targetVenueQuantity: number;
  currentVenueQuantity: number;
  driftVenueQuantity: number;
};

export type PositionDto = {
  instrumentId: string;
  symbol?: string | null;
  baseQuantity: number;
  asOfUtc?: string | null;
};

export type ReconciliationBreakDto = {
  id: string;
  reconciliationRunId: string;
  modelRunId?: string | null;
  phase?: string | null;
  type: string;
  severity: string;
  status: string;
  instrumentId?: string | null;
  symbol?: string | null;
  description: string;
  createdAtUtc?: string | null;
};

export type TradeIntentDto = {
  id: string;
  modelRunId: string;
  fundId: string;
  instrumentId: string;
  symbol?: string | null;
  side: string;
  requestedBaseQuantity: number;
  requestedVenueQuantity: number;
  reason: string;
  status: string;
  createdAtUtc: string;
};

export type RiskDecisionDto = {
  id: string;
  tradeIntentId: string;
  riskLimitSetId?: string | null;
  riskLimitSetName?: string | null;
  riskLimitSetVersion?: number | null;
  modelRunId?: string | null;
  instrumentId?: string | null;
  venueId?: string | null;
  venueName?: string | null;
  symbol?: string | null;
  status: string;
  rejectReason?: string | null;
  message: string;
  createdAtUtc: string;
  summaryObservedValue?: number | null;
  summaryLimitValue?: number | null;
  summaryUnit?: string | null;
  summaryCheckName?: string | null;
  details?: RiskDecisionDetailDto[];
};

export type RiskDecisionDetailDto = {
  id: string;
  riskDecisionId: string;
  checkName: string;
  status: string;
  rejectReason?: string | null;
  observedValue?: number | null;
  limitValue?: number | null;
  unit?: string | null;
  message: string;
  createdAtUtc: string;
};

export type RiskLimitSetDto = {
  id: string;
  fundId: string;
  modelName?: string | null;
  name: string;
  version: number;
  status: string;
  isActive: boolean;
  effectiveFromUtc?: string | null;
  effectiveToUtc?: string | null;
  createdAtUtc?: string | null;
  createdBy?: string | null;
  activatedAtUtc?: string | null;
  activatedBy?: string | null;
  retiredAtUtc?: string | null;
  retiredBy?: string | null;
  description?: string | null;
  globalTradingEnabled: boolean;
  maxGrossExposureUsd: number;
  maxModelRunAgeSeconds: number;
  maxMarketDataAgeSeconds: number;
  positionToleranceBaseQuantity: number;
  minDriftVenueQuantity: number;
};

export type RiskLimitDto = {
  id: string;
  riskLimitSetId: string;
  name: string;
  value: number;
  unit: string;
  scope: string;
  isEnabled: boolean;
};

export type InstrumentRiskLimitDto = {
  id: string;
  riskLimitSetId: string;
  instrumentId: string;
  symbol?: string | null;
  maxTradeNotionalUsd: number;
  maxPositionUsd: number;
  minTradeQuantity: number;
  maxOrdersPerDay: number;
  isTradingEnabled: boolean;
};

export type VenueRiskLimitDto = {
  id: string;
  riskLimitSetId: string;
  venueId: string;
  venueName?: string | null;
  maxTradeNotionalUsd: number;
  maxDailyTurnoverUsd: number;
  maxOrdersPerMinute: number;
  isVenueEnabled: boolean;
};

export type TradingWindowDto = {
  id: string;
  fundId: string;
  modelName: string;
  dayOfWeek: string;
  timeZoneId: string;
  tradingEnabled: boolean;
  openTime: string;
  closeTime: string;
  noNewOrdersAfter: string;
  flattenTime?: string | null;
  isActive: boolean;
  scheduleName: string;
  version: number;
  createdAtUtc?: string | null;
  updatedAtUtc?: string | null;
};

export type OrdersDto = {
  parentOrders: ParentOrderDto[];
  childOrders: ChildOrderDto[];
};

export type ParentOrderDto = {
  id: string;
  tradeIntentId: string;
  instrumentId?: string | null;
  clientOrderId: string;
  side: string;
  baseQuantity: number;
  algo: string;
  status: string;
  createdAtUtc: string;
};

export type ChildOrderDto = {
  id: string;
  parentOrderId: string;
  venueId: string;
  instrumentId?: string | null;
  clientOrderId: string;
  brokerOrderId?: string | null;
  side: string;
  orderType: string;
  timeInForce: string;
  baseQuantity: number;
  venueQuantity: number;
  status: string;
  createdAtUtc: string;
};

export type FillDto = {
  id: string;
  brokerExecutionId: string;
  childOrderId: string;
  instrumentId: string;
  symbol?: string | null;
  venueId: string;
  venueName?: string | null;
  side: string;
  baseQuantity: number;
  venueQuantity: number;
  price: number;
  tradeDateUtc: string;
  receivedAtUtc: string;
};

export type MarketDataSnapshotDto = {
  id: string;
  instrumentId: string;
  symbol?: string | null;
  venueId: string;
  venueName?: string | null;
  bid: number;
  ask: number;
  mid: number;
  spread: number;
  source: string;
  sourceTimestampUtc: string;
  receivedAtUtc: string;
  sequenceNumber?: number | null;
  isSynthetic: boolean;
  createdAtUtc: string;
};

export type MarketDataBarDto = {
  id: string;
  instrumentId: string;
  symbol?: string | null;
  venueId: string;
  venueName?: string | null;
  timeframe: string;
  barStartUtc: string;
  barEndUtc: string;
  source: string;
  bidOpen: number;
  bidHigh: number;
  bidLow: number;
  bidClose: number;
  askOpen: number;
  askHigh: number;
  askLow: number;
  askClose: number;
  midOpen: number;
  midHigh: number;
  midLow: number;
  midClose: number;
  spreadOpen: number;
  spreadHigh: number;
  spreadLow: number;
  spreadClose: number;
  spreadAverage: number;
  observationCount: number;
  firstSnapshotUtc?: string | null;
  lastSnapshotUtc?: string | null;
  isComplete: boolean;
  qualityStatus: string;
  buildRunId?: string | null;
  builderVersion: string;
  createdAtUtc: string;
};

export type KillSwitchDto = {
  id: string;
  isActive: boolean;
  reason?: string | null;
  updatedAtUtc: string;
};

export type InstrumentDto = {
  id: string;
  symbol: string;
  assetClass: string;
  baseCurrency: string;
  quoteCurrency: string;
  pricePrecision: number;
  quantityPrecision: number;
  isEnabled: boolean;
  isTradingEnabled: boolean;
  isReportImportEnabled: boolean;
  isMarketDataEnabled: boolean;
};

export type VenueDto = {
  id: string;
  name: string;
  venueType: string;
  isEnabled: boolean;
  isTradingEnabled: boolean;
  isReportImportEnabled: boolean;
  isMarketDataEnabled: boolean;
};

export type InstrumentAliasDto = {
  id: string;
  source: string;
  externalSymbol: string;
  externalInstrumentId?: string | null;
  isEnabled: boolean;
};

export type VenueInstrumentMappingDto = {
  id: string;
  venueId: string;
  venueName?: string | null;
  venueSymbol: string;
  venueInstrumentId?: string | null;
  isEnabled: boolean;
};

export type RiskInstrumentDto = {
  instrument: InstrumentDto;
  aliases: InstrumentAliasDto[];
  venueMappings: VenueInstrumentMappingDto[];
};

export type RiskVenueDto = {
  venue: VenueDto;
};

export type FakeSnapshotsRequest = {
  instrumentSymbol?: string;
  venueName?: string;
  startUtc: string;
  intervalSeconds: number;
  count: number;
  bid: number;
  ask: number;
  bidStep?: number;
  askStep?: number;
};

export type BuildBarsRequest = {
  venueName?: string;
  timeframe: string;
  startUtc: string;
  endUtc: string;
};

export type OperatorAuditEventDto = {
  id: string;
  occurredAtUtc: string;
  actorType: string;
  actorId: string;
  actorDisplayName: string;
  eventType: string;
  severity: string;
  result: string;
  entityType?: string | null;
  entityId?: string | null;
  correlationId?: string | null;
  causationId?: string | null;
  requestId?: string | null;
  source: string;
  description: string;
  reason?: string | null;
  beforeJson?: string | null;
  afterJson?: string | null;
  metadataJson?: string | null;
};

export type OperatorUserDto = {
  id: string;
  operatorId: string;
  displayName: string;
  email?: string | null;
  isEnabled: boolean;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  roles: string[];
  permissions: string[];
};

export type ApprovalRequestDto = {
  id: string;
  type: string;
  status: string;
  requestedByOperatorId: string;
  requestedByDisplayName: string;
  requestedAtUtc: string;
  requiredApproverRole: string;
  entityType: string;
  entityId: string;
  reason: string;
  payloadJson: string;
  beforeJson?: string | null;
  afterJson?: string | null;
  correlationId?: string | null;
  expiresAtUtc?: string | null;
  approvedAtUtc?: string | null;
  approvedByOperatorId?: string | null;
  rejectedAtUtc?: string | null;
  rejectedByOperatorId?: string | null;
  executedAtUtc?: string | null;
  executedByOperatorId?: string | null;
  resultMessage?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
};

export type ApprovalDecisionDto = {
  id: string;
  approvalRequestId: string;
  decision: string;
  decidedByOperatorId: string;
  decidedByDisplayName: string;
  reason: string;
  decidedAtUtc: string;
  correlationId?: string | null;
};

export type GovernedActionResultDto = {
  executed: boolean;
  approvalRequired: boolean;
  approvalRequestId?: string | null;
  status: string;
  message: string;
  entityId: string;
  resultEntityId?: string | null;
  correlationId?: string | null;
};

export type ExceptionCaseDto = {
  id: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  status: string;
  severity: string;
  type: string;
  source: string;
  title: string;
  description: string;
  entityType?: string | null;
  entityId?: string | null;
  instrumentId?: string | null;
  symbol?: string | null;
  correlationId?: string | null;
  assignedTo?: string | null;
  acknowledgedAtUtc?: string | null;
  acknowledgedBy?: string | null;
  resolvedAtUtc?: string | null;
  resolvedBy?: string | null;
  resolutionReason?: string | null;
  waiverReason?: string | null;
  metadataJson?: string | null;
};

export type ExceptionCaseActionDto = {
  id: string;
  caseId: string;
  actionType: string;
  actorId: string;
  actorDisplayName: string;
  occurredAtUtc: string;
  fromStatus?: string | null;
  toStatus?: string | null;
  reason?: string | null;
  note?: string | null;
  metadataJson?: string | null;
  correlationId?: string | null;
};

export type ExceptionCaseNoteDto = {
  id: string;
  caseId: string;
  createdAtUtc: string;
  createdBy: string;
  note: string;
  correlationId?: string | null;
};

export type CreateExceptionCaseRequest = {
  severity: string;
  type: string;
  source: string;
  title: string;
  description: string;
  entityType?: string;
  entityId?: string;
  instrumentId?: string;
  symbol?: string;
  assignedTo?: string;
  metadata?: unknown;
};

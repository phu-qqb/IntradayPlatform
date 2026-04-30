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
  modelRunId?: string | null;
  instrumentId?: string | null;
  symbol?: string | null;
  status: string;
  rejectReason: string;
  explanation: string;
  createdAtUtc: string;
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
};

export type VenueDto = {
  id: string;
  name: string;
  venueType: string;
  isEnabled: boolean;
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

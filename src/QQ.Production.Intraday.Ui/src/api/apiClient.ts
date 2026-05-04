import type {
  BuildBarsRequest,
  CreateExceptionCaseRequest,
  CreateFakeModelWeightBatchRequest,
  CreateModelRunRequest,
  DriftSnapshotDto,
  EodPnlSummaryDto,
  EodReconciliationBreakDto,
  EodReconciliationRunDto,
  ExceptionCaseActionDto,
  ExceptionCaseDto,
  ExceptionCaseNoteDto,
  FakeSnapshotsRequest,
  FakeLmaxEodReportGenerationDto,
  FillDto,
  GenerateFakeLmaxEodRequest,
  HealthDto,
  InstrumentDto,
  KillSwitchDto,
  LmaxCurrencyWalletDto,
  LmaxIndividualTradeDto,
  LmaxReportImportResultDto,
  LmaxReportImportRunDto,
  LmaxReportValidationIssueDto,
  LmaxTradeSummaryDto,
  MarketDataBarDto,
  MarketDataSnapshotDto,
  ModelRunDto,
  ModelWeightBatchDto,
  ModelWeightPromotionResultDto,
  ModelWeightRowDto,
  ModelWeightValidationIssueDto,
  OrdersDto,
  OperatorAuditEventDto,
  OperatorUserDto,
  ApprovalRequestDto,
  ApprovalDecisionDto,
  GovernedActionResultDto,
  PositionDto,
  ProcessModelRunResult,
  ReconciliationBreakDto,
  ReferenceDataIntegrityDto,
  RiskDecisionDto,
  RiskInstrumentDto,
  RiskLimitDto,
  RiskLimitSetDto,
  RiskVenueDto,
  InstrumentRiskLimitDto,
  TradingWindowDto,
  TargetPositionDto,
  TradeIntentDto,
  VenueRiskLimitDto,
  VenueDto
} from './types';

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5050';
export const API_BASE_URL = String(configuredBaseUrl).replace(/\/$/, '');
const OPERATOR_STORAGE_KEY = 'qq.operatorId';

export function getSelectedOperatorId(): string {
  return window.localStorage.getItem(OPERATOR_STORAGE_KEY) || 'local-admin';
}

export function setSelectedOperatorId(operatorId: string): void {
  window.localStorage.setItem(OPERATOR_STORAGE_KEY, operatorId);
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly statusText: string,
    public readonly body: string,
    public readonly correlationId?: string | null
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

function assertLocalUrl(url: string): void {
  const parsed = new URL(url);
  if (!['localhost', '127.0.0.1'].includes(parsed.hostname)) {
    throw new Error('The operator cockpit only permits local API URLs.');
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  assertLocalUrl(API_BASE_URL);
  const operatorId = getSelectedOperatorId();
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      'X-Operator-Id': operatorId,
      ...(init?.headers ?? {})
    },
    ...init
  });

  if (!response.ok) {
    let detail = response.statusText;
    try {
      detail = await response.text();
    } catch {
      detail = response.statusText;
    }
    const correlationId = response.headers.get('x-correlation-id') ?? response.headers.get('x-request-id');
    throw new ApiError(`${response.status} ${response.statusText}: ${detail}${correlationId ? ` (correlation ${correlationId})` : ''}`, response.status, response.statusText, detail, correlationId);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

const query = (params: Record<string, string | number | boolean | undefined | null>) => {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      search.set(key, String(value));
    }
  }
  const text = search.toString();
  return text ? `?${text}` : '';
};

export const apiClient = {
  getHealth: () => request<HealthDto>('/health'),
  getReferenceDataIntegrity: () => request<ReferenceDataIntegrityDto>('/admin/reference-data/integrity'),
  getModelWeightBatches: () => request<ModelWeightBatchDto[]>('/model-weight-batches?limit=100'),
  getModelWeightRows: (batchId: string) => request<ModelWeightRowDto[]>(`/model-weight-batches/${batchId}/rows`),
  getModelWeightValidationIssues: (batchId: string) => request<ModelWeightValidationIssueDto[]>(`/model-weight-batches/${batchId}/validation-issues`),
  createFakeModelWeightBatch: (body: CreateFakeModelWeightBatchRequest) => request<ModelWeightBatchDto>('/model-weight-batches/fake', { method: 'POST', body: JSON.stringify(body) }),
  validateModelWeightBatch: (batchId: string) => request<ModelWeightPromotionResultDto>(`/model-weight-batches/${batchId}/validate`, { method: 'POST' }),
  promoteModelWeightBatch: (batchId: string) => request<ModelWeightPromotionResultDto>(`/model-weight-batches/${batchId}/promote`, { method: 'POST' }),
  promoteReadyModelWeightBatches: (limit = 10) => request<ModelWeightPromotionResultDto[]>('/model-weight-batches/promote-ready', { method: 'POST', body: JSON.stringify({ limit }) }),
  getModelRuns: () => request<ModelRunDto[]>('/model-runs?limit=100'),
  createModelRun: (body: CreateModelRunRequest) => request<ModelRunDto>('/model-runs', { method: 'POST', body: JSON.stringify(body) }),
  processModelRun: (id: string) => request<ProcessModelRunResult>(`/model-runs/${id}/process`, { method: 'POST' }),
  getTargetPositions: () => request<TargetPositionDto[]>('/target-positions?limit=100'),
  getDriftSnapshots: () => request<DriftSnapshotDto[]>('/drift-snapshots?limit=100'),
  getInternalPositions: () => request<PositionDto[]>('/positions/internal'),
  getBrokerPositions: () => request<PositionDto[]>('/positions/broker'),
  getReconciliationBreaks: () => request<ReconciliationBreakDto[]>('/reconciliation/breaks?limit=100'),
  getTradeIntents: () => request<TradeIntentDto[]>('/trade-intents?limit=100'),
  getRiskDecisions: () => request<RiskDecisionDto[]>('/risk/decisions?limit=100'),
  getRiskLimitSets: () => request<RiskLimitSetDto[]>('/risk/limit-sets?limit=100'),
  getActiveRiskLimitSet: (fundCode = 'QQ_MASTER', modelName = 'IntradayFxModel') =>
    request<RiskLimitSetDto>(`/risk/limit-sets/active${query({ fundCode, modelName })}`),
  createRiskLimitSet: (body: { fundCode?: string; modelName?: string; name: string; description?: string; reason: string }) =>
    request<RiskLimitSetDto>('/risk/limit-sets', { method: 'POST', body: JSON.stringify(body) }),
  cloneRiskLimitSet: (id: string, reason: string) =>
    request<RiskLimitSetDto>(`/risk/limit-sets/${id}/clone`, { method: 'POST', body: JSON.stringify({ reason }) }),
  activateRiskLimitSet: (id: string, reason: string) =>
    request<RiskLimitSetDto | GovernedActionResultDto>(`/risk/limit-sets/${id}/activate`, { method: 'POST', body: JSON.stringify({ reason }) }),
  retireRiskLimitSet: (id: string, reason: string) =>
    request<RiskLimitSetDto | GovernedActionResultDto>(`/risk/limit-sets/${id}/retire`, { method: 'POST', body: JSON.stringify({ reason }) }),
  getRiskLimits: (riskLimitSetId: string) => request<RiskLimitDto[]>(`/risk/limits${query({ riskLimitSetId })}`),
  updateRiskLimit: (id: string, body: { value: number; unit?: string; isEnabled?: boolean; reason: string }) =>
    request<RiskLimitDto>(`/risk/limits/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  getInstrumentRiskLimits: (riskLimitSetId: string) => request<InstrumentRiskLimitDto[]>(`/risk/instrument-limits${query({ riskLimitSetId })}`),
  updateInstrumentRiskLimit: (id: string, body: { maxTradeNotionalUsd?: number; maxPositionUsd?: number; minTradeQuantity?: number; maxOrdersPerDay?: number; isTradingEnabled?: boolean; reason: string }) =>
    request<InstrumentRiskLimitDto>(`/risk/instrument-limits/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  getVenueRiskLimits: (riskLimitSetId: string) => request<VenueRiskLimitDto[]>(`/risk/venue-limits${query({ riskLimitSetId })}`),
  updateVenueRiskLimit: (id: string, body: { maxTradeNotionalUsd?: number; maxDailyTurnoverUsd?: number; maxOrdersPerMinute?: number; isVenueEnabled?: boolean; reason: string }) =>
    request<VenueRiskLimitDto>(`/risk/venue-limits/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  getTradingWindows: () => request<TradingWindowDto[]>('/risk/trading-windows'),
  updateTradingWindow: (id: string, body: Partial<TradingWindowDto> & { reason: string }) =>
    request<TradingWindowDto>(`/risk/trading-windows/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  getRiskInstruments: () => request<RiskInstrumentDto[]>('/risk/instruments'),
  updateRiskInstrumentControls: (id: string, body: { isEnabled?: boolean; isTradingEnabled?: boolean; isReportImportEnabled?: boolean; isMarketDataEnabled?: boolean; reason: string }) =>
    request<RiskInstrumentDto>(`/risk/instruments/${id}/controls`, { method: 'PUT', body: JSON.stringify(body) }),
  getRiskVenues: () => request<RiskVenueDto[]>('/risk/venues'),
  updateRiskVenueControls: (id: string, body: { isEnabled?: boolean; isTradingEnabled?: boolean; isReportImportEnabled?: boolean; isMarketDataEnabled?: boolean; reason: string }) =>
    request<RiskVenueDto>(`/risk/venues/${id}/controls`, { method: 'PUT', body: JSON.stringify(body) }),
  getOrders: () => request<OrdersDto>('/orders'),
  getFills: () => request<FillDto[]>('/fills?limit=100'),
  getMarketDataSnapshots: (params: { instrument?: string; venue?: string; fromUtc?: string; toUtc?: string; limit?: number } = {}) =>
    request<MarketDataSnapshotDto[]>(`/market-data/snapshots${query({ limit: 100, ...params })}`),
  createFakeSnapshots: (body: FakeSnapshotsRequest) => request<{ created: number }>('/market-data/fake-snapshots', { method: 'POST', body: JSON.stringify(body) }),
  getMarketDataBars: (params: { instrument?: string; venue?: string; timeframe?: string; fromUtc?: string; toUtc?: string; limit?: number } = {}) =>
    request<MarketDataBarDto[]>(`/market-data/bars${query({ limit: 100, timeframe: 'FifteenMinutes', ...params })}`),
  buildBars: (body: BuildBarsRequest) => request<{ runId: string; barsCreated: number; barsUpdated: number; status: string; errorMessage?: string | null }>('/market-data/build-bars', { method: 'POST', body: JSON.stringify(body) }),
  getKillSwitch: () => request<KillSwitchDto>('/admin/kill-switch'),
  activateKillSwitch: (reason: string) => request<{ active: boolean; reason?: string | null }>('/admin/kill-switch', { method: 'POST', body: JSON.stringify({ reason }) }),
  clearKillSwitch: (reason = 'Clear local kill switch') => request<{ active: boolean } | GovernedActionResultDto>('/admin/kill-switch/clear', { method: 'POST', body: JSON.stringify({ reason }) }),
  getInstruments: () => request<InstrumentDto[]>('/instruments'),
  getVenues: () => request<VenueDto[]>('/venues')
  ,
  getAuditEvents: (params: { limit?: number; severity?: string; eventType?: string; entityType?: string; entityId?: string; correlationId?: string; fromUtc?: string; toUtc?: string } = {}) =>
    request<OperatorAuditEventDto[]>(`/audit/events${query({ limit: 100, ...params })}`),
  getAuditEventsByEntity: (entityType: string, entityId: string, limit = 100) =>
    request<OperatorAuditEventDto[]>(`/audit/events/by-entity${query({ entityType, entityId, limit })}`),
  getAuditEventsByCorrelation: (correlationId: string, limit = 100) =>
    request<OperatorAuditEventDto[]>(`/audit/events/by-correlation/${encodeURIComponent(correlationId)}${query({ limit })}`),
  getExceptionCases: (params: { limit?: number; status?: string; severity?: string; type?: string; source?: string; assignedTo?: string; instrument?: string; fromUtc?: string; toUtc?: string } = {}) =>
    request<ExceptionCaseDto[]>(`/exceptions${query({ limit: 100, ...params })}`),
  getExceptionCaseActions: (id: string) => request<ExceptionCaseActionDto[]>(`/exceptions/${id}/actions`),
  getExceptionCaseNotes: (id: string) => request<ExceptionCaseNoteDto[]>(`/exceptions/${id}/notes`),
  createExceptionCase: (body: CreateExceptionCaseRequest) => request<ExceptionCaseDto>('/exceptions', { method: 'POST', body: JSON.stringify(body) }),
  acknowledgeExceptionCase: (id: string, reason?: string) => request<ExceptionCaseDto>(`/exceptions/${id}/acknowledge`, { method: 'POST', body: JSON.stringify({ reason }) }),
  assignExceptionCase: (id: string, assignedTo: string) => request<ExceptionCaseDto>(`/exceptions/${id}/assign`, { method: 'POST', body: JSON.stringify({ assignedTo }) }),
  investigateExceptionCase: (id: string, reason?: string) => request<ExceptionCaseDto>(`/exceptions/${id}/investigate`, { method: 'POST', body: JSON.stringify({ reason }) }),
  resolveExceptionCase: (id: string, reason: string) => request<ExceptionCaseDto | GovernedActionResultDto>(`/exceptions/${id}/resolve`, { method: 'POST', body: JSON.stringify({ reason }) }),
  falsePositiveExceptionCase: (id: string, reason: string) => request<ExceptionCaseDto | GovernedActionResultDto>(`/exceptions/${id}/false-positive`, { method: 'POST', body: JSON.stringify({ reason }) }),
  waiveExceptionCase: (id: string, reason: string) => request<ExceptionCaseDto | GovernedActionResultDto>(`/exceptions/${id}/waive`, { method: 'POST', body: JSON.stringify({ reason }) }),
  reopenExceptionCase: (id: string, reason?: string) => request<ExceptionCaseDto>(`/exceptions/${id}/reopen`, { method: 'POST', body: JSON.stringify({ reason }) }),
  addExceptionCaseNote: (id: string, note: string) => request<ExceptionCaseNoteDto>(`/exceptions/${id}/notes`, { method: 'POST', body: JSON.stringify({ note }) }),
  getLmaxEodImportRuns: () => request<LmaxReportImportRunDto[]>('/lmax-eod/import-runs?limit=100'),
  getLmaxEodValidationIssues: () => request<LmaxReportValidationIssueDto[]>('/lmax-eod/validation-issues?limit=100'),
  getLmaxIndividualTrades: (reportDate?: string) => request<LmaxIndividualTradeDto[]>(`/lmax-eod/individual-trades${query({ limit: 100, reportDate })}`),
  getLmaxTradeSummaries: (reportDate?: string) => request<LmaxTradeSummaryDto[]>(`/lmax-eod/trade-summaries${query({ limit: 100, reportDate })}`),
  getLmaxCurrencyWallets: (reportDate?: string) => request<LmaxCurrencyWalletDto[]>(`/lmax-eod/currency-wallets${query({ limit: 100, reportDate })}`),
  generateFakeLmaxEod: (body: GenerateFakeLmaxEodRequest) => request<FakeLmaxEodReportGenerationDto>('/lmax-eod/generate-fake', { method: 'POST', body: JSON.stringify(body) }),
  importGeneratedLmaxEod: (body: { reportDate: string; venueName?: string; brokerAccountCode?: string }) => request<LmaxReportImportResultDto>('/lmax-eod/import-generated', { method: 'POST', body: JSON.stringify(body) }),
  runEodReconciliation: (body: { reportDate: string; venueName?: string; brokerAccountCode?: string }) => request<{ runId: string; breakCount: number; blockingBreakCount: number; breaks: EodReconciliationBreakDto[] }>('/eod-reconciliation/run', { method: 'POST', body: JSON.stringify(body) }),
  getEodReconciliationRuns: (reportDate?: string) => request<EodReconciliationRunDto[]>(`/eod-reconciliation/runs${query({ limit: 100, reportDate })}`),
  getEodReconciliationBreaks: (reportDate?: string) => request<EodReconciliationBreakDto[]>(`/eod-reconciliation/breaks${query({ limit: 100, reportDate })}`),
  getEodPnlSummary: (reportDate: string, venueName = 'LMAX', brokerAccountCode = 'LMAX_DEMO_LOCAL') => request<EodPnlSummaryDto>(`/eod-pnl/summary${query({ reportDate, venueName, brokerAccountCode })}`),
  getCurrentOperator: () => request<OperatorUserDto>('/operators/current'),
  getOperators: () => request<OperatorUserDto[]>('/operators'),
  getOperatorPermissions: (operatorId: string) => request<string[]>(`/operators/${encodeURIComponent(operatorId)}/permissions`),
  getApprovals: (params: { limit?: number; status?: string; type?: string; requestedBy?: string; entityType?: string; entityId?: string } = {}) =>
    request<ApprovalRequestDto[]>(`/approvals${query({ limit: 100, ...params })}`),
  getApproval: (id: string) => request<ApprovalRequestDto>(`/approvals/${id}`),
  getApprovalDecisions: (id: string) => request<ApprovalDecisionDto[]>(`/approvals/${id}/decisions`),
  approveApproval: (id: string, reason: string) => request<ApprovalRequestDto>(`/approvals/${id}/approve`, { method: 'POST', body: JSON.stringify({ reason }) }),
  rejectApproval: (id: string, reason: string) => request<ApprovalRequestDto>(`/approvals/${id}/reject`, { method: 'POST', body: JSON.stringify({ reason }) }),
  cancelApproval: (id: string, reason: string) => request<ApprovalRequestDto>(`/approvals/${id}/cancel`, { method: 'POST', body: JSON.stringify({ reason }) }),
  executeApproval: (id: string) => request<GovernedActionResultDto>(`/approvals/${id}/execute`, { method: 'POST' })
};

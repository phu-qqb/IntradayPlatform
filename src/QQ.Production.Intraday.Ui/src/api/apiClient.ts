import type {
  BuildBarsRequest,
  CreateFakeModelWeightBatchRequest,
  CreateModelRunRequest,
  DriftSnapshotDto,
  EodPnlSummaryDto,
  EodReconciliationBreakDto,
  EodReconciliationRunDto,
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
  PositionDto,
  ProcessModelRunResult,
  ReconciliationBreakDto,
  ReferenceDataIntegrityDto,
  RiskDecisionDto,
  TargetPositionDto,
  TradeIntentDto,
  VenueDto
} from './types';

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5050';
export const API_BASE_URL = String(configuredBaseUrl).replace(/\/$/, '');

function assertLocalUrl(url: string): void {
  const parsed = new URL(url);
  if (!['localhost', '127.0.0.1'].includes(parsed.hostname)) {
    throw new Error('The operator cockpit only permits local API URLs.');
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  assertLocalUrl(API_BASE_URL);
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: {
      'Content-Type': 'application/json',
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
    throw new Error(`${response.status} ${response.statusText}: ${detail}`);
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
  getRiskDecisions: () => request<RiskDecisionDto[]>('/risk-decisions?limit=100'),
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
  clearKillSwitch: () => request<{ active: boolean }>('/admin/kill-switch/clear', { method: 'POST' }),
  getInstruments: () => request<InstrumentDto[]>('/instruments'),
  getVenues: () => request<VenueDto[]>('/venues')
  ,
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
  getEodPnlSummary: (reportDate: string, venueName = 'LMAX', brokerAccountCode = 'LMAX_DEMO_LOCAL') => request<EodPnlSummaryDto>(`/eod-pnl/summary${query({ reportDate, venueName, brokerAccountCode })}`)
};

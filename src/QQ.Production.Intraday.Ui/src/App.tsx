import { useCallback, useEffect, useMemo, useState } from 'react';
import { Activity, Archive, BarChart3, ClipboardList, FileSearch, Gauge, GitBranch, Landmark, RadioTower, ShieldAlert, WalletCards } from 'lucide-react';
import { apiClient } from './api/apiClient';
import type {
  DriftSnapshotDto,
  EodPnlSummaryDto,
  EodReconciliationBreakDto,
  EodReconciliationRunDto,
  ExceptionCaseActionDto,
  ExceptionCaseDto,
  ExceptionCaseNoteDto,
  FillDto,
  HealthDto,
  InstrumentDto,
  KillSwitchDto,
  MarketDataBarDto,
  MarketDataSnapshotDto,
  ModelRunDto,
  ModelWeightBatchDto,
  ModelWeightRowDto,
  ModelWeightValidationIssueDto,
  LmaxCurrencyWalletDto,
  LmaxIndividualTradeDto,
  LmaxReportImportRunDto,
  LmaxReportValidationIssueDto,
  LmaxTradeSummaryDto,
  OrdersDto,
  OperatorAuditEventDto,
  PositionDto,
  ReconciliationBreakDto,
  ReferenceDataIntegrityDto,
  RiskDecisionDto,
  TargetPositionDto,
  TradeIntentDto,
  VenueDto
} from './api/types';
import { AdminPanel } from './components/AdminPanel';
import { DataTable } from './components/DataTable';
import { DriftPanel } from './components/DriftPanel';
import { ErrorState } from './components/ErrorState';
import { FillsPanel } from './components/FillsPanel';
import { HealthPanel } from './components/HealthPanel';
import { LmaxEodReportsPanel } from './components/LmaxEodReportsPanel';
import { LoadingState } from './components/LoadingState';
import { MarketDataPanel } from './components/MarketDataPanel';
import { ModelRunsPanel } from './components/ModelRunsPanel';
import { ModelWeightsPanel } from './components/ModelWeightsPanel';
import { OrdersPanel } from './components/OrdersPanel';
import { PositionsPanel } from './components/PositionsPanel';
import { ReconciliationPanel } from './components/ReconciliationPanel';
import { ReferenceDataPanel } from './components/ReferenceDataPanel';
import { RiskPanel } from './components/RiskPanel';
import { SafetyPanel } from './components/SafetyPanel';
import { TopStatusBar } from './components/TopStatusBar';
import { CommandButton, DetailDrawer, MetricCard, SectionHeader, SeverityBadge, StatusChip, Timeline, toneForStatus } from './components/primitives';
import type { Tone } from './components/primitives';
import { formatDate, formatIdShort, formatPrice, formatQuantity, formatStatus, formatUsd, formatUtc } from './utils/format';

type DashboardState = {
  modelRuns: ModelRunDto[];
  modelWeightBatches: ModelWeightBatchDto[];
  modelWeightRows: ModelWeightRowDto[];
  modelWeightValidationIssues: ModelWeightValidationIssueDto[];
  targets: TargetPositionDto[];
  drifts: DriftSnapshotDto[];
  internalPositions: PositionDto[];
  brokerPositions: PositionDto[];
  reconciliationBreaks: ReconciliationBreakDto[];
  tradeIntents: TradeIntentDto[];
  riskDecisions: RiskDecisionDto[];
  orders?: OrdersDto;
  fills: FillDto[];
  snapshots: MarketDataSnapshotDto[];
  bars: MarketDataBarDto[];
  killSwitch?: KillSwitchDto;
  instruments: InstrumentDto[];
  venues: VenueDto[];
  lmaxImportRuns: LmaxReportImportRunDto[];
  lmaxValidationIssues: LmaxReportValidationIssueDto[];
  lmaxIndividualTrades: LmaxIndividualTradeDto[];
  lmaxTradeSummaries: LmaxTradeSummaryDto[];
  lmaxCurrencyWallets: LmaxCurrencyWalletDto[];
  eodReconciliationRuns: EodReconciliationRunDto[];
  eodReconciliationBreaks: EodReconciliationBreakDto[];
  eodPnlSummary?: EodPnlSummaryDto;
  exceptionCases: ExceptionCaseDto[];
  auditEvents: OperatorAuditEventDto[];
};

type PageId = 'command' | 'pms' | 'weights' | 'oms' | 'ems' | 'market' | 'exceptions' | 'recon' | 'lmax-eod' | 'risk-admin' | 'audit' | 'connectivity';

const emptyDashboard: DashboardState = {
  modelRuns: [],
  modelWeightBatches: [],
  modelWeightRows: [],
  modelWeightValidationIssues: [],
  targets: [],
  drifts: [],
  internalPositions: [],
  brokerPositions: [],
  reconciliationBreaks: [],
  tradeIntents: [],
  riskDecisions: [],
  fills: [],
  snapshots: [],
  bars: [],
  instruments: [],
  venues: [],
  lmaxImportRuns: [],
  lmaxValidationIssues: [],
  lmaxIndividualTrades: [],
  lmaxTradeSummaries: [],
  lmaxCurrencyWallets: [],
  eodReconciliationRuns: [],
  eodReconciliationBreaks: [],
  exceptionCases: [],
  auditEvents: []
};

const navSections: Array<{ label: string; items: Array<{ id: PageId; label: string; icon: typeof Activity }> }> = [
  {
    label: 'Operations',
    items: [
      { id: 'command', label: 'Command Center', icon: Gauge },
      { id: 'exceptions', label: 'Exceptions', icon: ShieldAlert },
      { id: 'recon', label: 'Reconciliation', icon: FileSearch }
    ]
  },
  {
    label: 'Trading',
    items: [
      { id: 'pms', label: 'PMS', icon: Landmark },
      { id: 'weights', label: 'Model Weights', icon: GitBranch },
      { id: 'oms', label: 'OMS', icon: Archive },
      { id: 'ems', label: 'EMS', icon: Activity }
    ]
  },
  {
    label: 'Data',
    items: [
      { id: 'market', label: 'Market Data', icon: BarChart3 },
      { id: 'lmax-eod', label: 'LMAX EOD', icon: WalletCards }
    ]
  },
  {
    label: 'Control',
    items: [
      { id: 'risk-admin', label: 'Risk & Admin', icon: ShieldAlert },
      { id: 'audit', label: 'Audit Journal', icon: ClipboardList },
      { id: 'connectivity', label: 'Connectivity Lab', icon: RadioTower }
    ]
  }
];

export default function App() {
  const [activePage, setActivePage] = useState<PageId>('command');
  const [selected, setSelected] = useState<unknown>();
  const [health, setHealth] = useState<HealthDto>();
  const [integrity, setIntegrity] = useState<ReferenceDataIntegrityDto>();
  const [dashboard, setDashboard] = useState<DashboardState>(emptyDashboard);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  const loadHealth = useCallback(async () => setHealth(await apiClient.getHealth()), []);
  const loadIntegrity = useCallback(async () => setIntegrity(await apiClient.getReferenceDataIntegrity()), []);

  const loadDashboard = useCallback(async () => {
    const [modelRuns, modelWeightBatches, targets, drifts, internalPositions, brokerPositions, reconciliationBreaks, tradeIntents, riskDecisions, orders, fills, snapshots, bars, killSwitch, instruments, venues, lmaxImportRuns, lmaxValidationIssues, lmaxIndividualTrades, lmaxTradeSummaries, lmaxCurrencyWallets, eodReconciliationRuns, eodReconciliationBreaks, exceptionCases, auditEvents] = await Promise.all([
      apiClient.getModelRuns(),
      apiClient.getModelWeightBatches(),
      apiClient.getTargetPositions(),
      apiClient.getDriftSnapshots(),
      apiClient.getInternalPositions(),
      apiClient.getBrokerPositions(),
      apiClient.getReconciliationBreaks(),
      apiClient.getTradeIntents(),
      apiClient.getRiskDecisions(),
      apiClient.getOrders(),
      apiClient.getFills(),
      apiClient.getMarketDataSnapshots({ instrument: 'EURUSD', venue: 'LMAX' }),
      apiClient.getMarketDataBars({ instrument: 'EURUSD', venue: 'LMAX' }),
      apiClient.getKillSwitch(),
      apiClient.getInstruments(),
      apiClient.getVenues(),
      apiClient.getLmaxEodImportRuns(),
      apiClient.getLmaxEodValidationIssues(),
      apiClient.getLmaxIndividualTrades(),
      apiClient.getLmaxTradeSummaries(),
      apiClient.getLmaxCurrencyWallets(),
      apiClient.getEodReconciliationRuns(),
      apiClient.getEodReconciliationBreaks(),
      apiClient.getExceptionCases(),
      apiClient.getAuditEvents({ limit: 100 })
    ]);

    setDashboard((current) => ({ ...current, modelRuns, modelWeightBatches, targets, drifts, internalPositions, brokerPositions, reconciliationBreaks, tradeIntents, riskDecisions, orders, fills, snapshots, bars, killSwitch, instruments, venues, lmaxImportRuns, lmaxValidationIssues, lmaxIndividualTrades, lmaxTradeSummaries, lmaxCurrencyWallets, eodReconciliationRuns, eodReconciliationBreaks, exceptionCases, auditEvents }));
  }, []);

  const refreshAll = useCallback(async () => {
    setError(undefined);
    try {
      await Promise.all([loadHealth(), loadIntegrity(), loadDashboard()]);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, [loadDashboard, loadHealth, loadIntegrity]);

  useEffect(() => {
    void refreshAll();
    const healthTimer = window.setInterval(() => void loadHealth().catch((err) => setError(String(err))), 5000);
    const integrityTimer = window.setInterval(() => void loadIntegrity().catch((err) => setError(String(err))), 10000);
    const dashboardTimer = window.setInterval(() => void loadDashboard().catch((err) => setError(String(err))), 10000);
    return () => {
      window.clearInterval(healthTimer);
      window.clearInterval(integrityTimer);
      window.clearInterval(dashboardTimer);
    };
  }, [loadDashboard, loadHealth, loadIntegrity, refreshAll]);

  const actions = useMemo(() => ({
    refreshAll,
    setSelected,
    onCreateModelRun: async (request: Parameters<typeof apiClient.createModelRun>[0]) => {
      await apiClient.createModelRun(request);
      await refreshAll();
    },
    onProcessModelRun: async (id: string) => {
      const result = await apiClient.processModelRun(id);
      setSelected(result);
      await refreshAll();
      return result;
    }
  }), [refreshAll]);

  const page = renderPage(activePage, dashboard, health, integrity, actions);

  return (
    <div className="operator-shell">
      <TopStatusBar health={health} integrity={integrity} onRefresh={refreshAll} />
      <div className="operator-body">
        <LeftNavigation activePage={activePage} onSelect={setActivePage} />
        <main className="main-workspace">
          {loading && <LoadingState label="Loading cockpit data" />}
          {error && <ErrorState message={error} />}
          {page}
        </main>
        <DetailDrawer item={selected} onClose={() => setSelected(undefined)} />
      </div>
      <AuditStrip health={health} dashboard={dashboard} />
    </div>
  );
}

function LeftNavigation({ activePage, onSelect }: { activePage: PageId; onSelect: (page: PageId) => void }) {
  return (
    <nav className="left-navigation" aria-label="Cockpit sections">
      {navSections.map((section) => (
        <div className="nav-section" key={section.label}>
          <span className="nav-section-label">{section.label}</span>
          {section.items.map((item) => {
            const Icon = item.icon;
            return (
              <button key={item.id} className={activePage === item.id ? 'active' : undefined} onClick={() => onSelect(item.id)}>
                <Icon size={16} /> <span className="nav-label">{item.label}</span>
              </button>
            );
          })}
        </div>
      ))}
    </nav>
  );
}

function renderPage(page: PageId, dashboard: DashboardState, health: HealthDto | undefined, integrity: ReferenceDataIntegrityDto | undefined, actions: { refreshAll: () => Promise<void>; setSelected: (item: unknown) => void; onCreateModelRun: (request: Parameters<typeof apiClient.createModelRun>[0]) => Promise<void>; onProcessModelRun: (id: string) => Promise<Awaited<ReturnType<typeof apiClient.processModelRun>>> }) {
  switch (page) {
    case 'pms':
      return <PmsPage dashboard={dashboard} />;
    case 'weights':
      return <WeightsPage dashboard={dashboard} actions={actions} />;
    case 'oms':
      return <OmsPage dashboard={dashboard} actions={actions} />;
    case 'ems':
      return <EmsPage dashboard={dashboard} health={health} />;
    case 'market':
      return <MarketPage dashboard={dashboard} actions={actions} />;
    case 'exceptions':
      return <ExceptionsPage dashboard={dashboard} actions={actions} />;
    case 'recon':
      return <ReconPage dashboard={dashboard} />;
    case 'lmax-eod':
      return <LmaxEodPage dashboard={dashboard} actions={actions} />;
    case 'risk-admin':
      return <RiskAdminPage dashboard={dashboard} health={health} integrity={integrity} actions={actions} />;
    case 'audit':
      return <AuditJournalPage events={dashboard.auditEvents} onSelect={actions.setSelected} />;
    case 'connectivity':
      return <ConnectivityLabPage />;
    case 'command':
    default:
      return <CommandCenter dashboard={dashboard} health={health} integrity={integrity} actions={actions} />;
  }
}

function obviousPositionMismatchCount(internalPositions: PositionDto[], brokerPositions: PositionDto[]) {
  const brokerByInstrument = new Map(brokerPositions.map((position) => [position.instrumentId ?? position.symbol ?? '-', position]));
  const internalByInstrument = new Map(internalPositions.map((position) => [position.instrumentId ?? position.symbol ?? '-', position]));
  const instruments = new Set([...brokerByInstrument.keys(), ...internalByInstrument.keys()]);
  return [...instruments].filter((instrument) => {
    const internal = internalByInstrument.get(instrument)?.baseQuantity;
    const broker = brokerByInstrument.get(instrument)?.baseQuantity;
    if (internal === undefined || broker === undefined) return true;
    return Math.abs(internal - broker) > 0.0001;
  }).length;
}

function CommandCenter({ dashboard, health, integrity, actions }: { dashboard: DashboardState; health?: HealthDto; integrity?: ReferenceDataIntegrityDto; actions: { setSelected: (item: unknown) => void } }) {
  const openOrders = (dashboard.orders?.childOrders ?? []).filter((order) => !['Filled', 'Cancelled', 'Rejected', 'Expired'].includes(order.status)).length;
  const blockingBreaks = dashboard.eodReconciliationBreaks.filter((item) => item.severity === 'Blocking').length;
  const openExceptions = dashboard.exceptionCases.filter((item) => !['Resolved', 'FalsePositive', 'Waived', 'Closed'].includes(item.status));
  const blockingExceptions = openExceptions.filter((item) => ['Blocking', 'Critical'].includes(item.severity)).length;
  const mismatchCount = obviousPositionMismatchCount(dashboard.internalPositions, dashboard.brokerPositions);
  const latestEod = dashboard.eodReconciliationRuns[0];
  return (
    <section className="workspace-page">
      <SectionHeader title="Command Center" eyebrow="Operational overview" actions={<CommandButton tone="info" onClick={() => actions.setSelected(dashboard.auditEvents[0])}>Latest Event</CommandButton>} />
      <div className="metric-grid">
        <MetricCard label="Runtime Safety" value={health?.executionGateway === 'FakeLmaxGateway' && !health.liveTradingEnabled && !health.externalConnectionsEnabled ? 'Safe Local' : 'Attention'} sublabel="FakeLmax-only runtime boundary" tone={health?.executionGateway === 'FakeLmaxGateway' && !health.liveTradingEnabled && !health.externalConnectionsEnabled ? 'ok' : 'danger'} />
        <MetricCard label="Latest Weight Batch" value={dashboard.modelWeightBatches[0]?.status ?? '-'} sublabel={formatIdShort(dashboard.modelWeightBatches[0]?.id)} tone={toneForStatus(dashboard.modelWeightBatches[0]?.status)} />
        <MetricCard label="Latest Model Run" value={dashboard.modelRuns[0]?.status ?? '-'} sublabel={formatIdShort(dashboard.modelRuns[0]?.id)} tone={toneForStatus(dashboard.modelRuns[0]?.status)} />
        <MetricCard label="Open Exceptions" value={openExceptions.length} sublabel={`${blockingExceptions} blocking/critical`} tone={blockingExceptions ? 'danger' : openExceptions.length ? 'warning' : 'ok'} />
        <MetricCard label="Position Match" value={mismatchCount === 0 ? 'Matched' : `${mismatchCount} hint${mismatchCount === 1 ? '' : 's'}`} sublabel="Visual hint only, backend recon is authoritative" tone={mismatchCount === 0 ? 'ok' : 'warning'} />
        <MetricCard label="Open Orders" value={openOrders} sublabel={`${dashboard.fills.length} fills loaded`} tone={openOrders === 0 ? 'neutral' : 'warning'} />
        <MetricCard label="Latest EOD Recon" value={latestEod ? (latestEod.hasBlockingBreaks ? 'Breaks' : 'Clean') : '-'} sublabel={latestEod ? formatDate(latestEod.reportDate) : 'No run loaded'} tone={latestEod?.hasBlockingBreaks ? 'danger' : latestEod ? 'ok' : 'neutral'} />
        <MetricCard label="EOD Blocking Breaks" value={blockingBreaks} sublabel="From imported LMAX reports" tone={blockingBreaks === 0 ? 'ok' : 'danger'} />
        <MetricCard label="PnL USD Summary" value={formatUsd(dashboard.eodPnlSummary?.totalNetPnlUsd)} sublabel="Wallet/cash/PnL only" tone="neutral" />
        <MetricCard label="LMAX Lab" value="Read-only" sublabel="Isolated; not connected to runtime" tone="info" />
      </div>
      <div className="page-grid two overview-grid">
        <HealthPanel health={health} />
        <ReferenceDataPanel integrity={integrity} />
      </div>
    </section>
  );
}

function PmsPage({ dashboard }: { dashboard: DashboardState }) {
  const mismatchCount = obviousPositionMismatchCount(dashboard.internalPositions, dashboard.brokerPositions);
  return (
    <section className="workspace-page">
      <SectionHeader title="PMS" eyebrow="Portfolio, positions, targets, drift, wallets, PnL" />
      <div className="metric-grid">
        <MetricCard label="Internal Positions" value={dashboard.internalPositions.length} tone="neutral" />
        <MetricCard label="Broker Positions" value={dashboard.brokerPositions.length} tone="neutral" />
        <MetricCard label="Position Hints" value={mismatchCount} sublabel="Obvious internal/broker visual mismatches" tone={mismatchCount === 0 ? 'ok' : 'warning'} />
        <MetricCard label="Wallet Balance USD" value={formatUsd(dashboard.eodPnlSummary?.totalWalletBalanceUsd)} tone="neutral" />
        <MetricCard label="Net PnL USD" value={formatUsd(dashboard.eodPnlSummary?.totalNetPnlUsd)} tone="neutral" />
      </div>
      <PositionsPanel internalPositions={dashboard.internalPositions} brokerPositions={dashboard.brokerPositions} />
      <DriftPanel targets={dashboard.targets} drifts={dashboard.drifts} />
      <WalletSummary dashboard={dashboard} />
    </section>
  );
}

function WeightsPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void> } }) {
  return (
    <section className="workspace-page">
      <SectionHeader title="Model Weights" eyebrow="DB-staged weight batches before canonical model runs" />
      <ModelWeightsPanel
        batches={dashboard.modelWeightBatches}
        rows={dashboard.modelWeightRows}
        issues={dashboard.modelWeightValidationIssues}
        onSelectBatch={async (id) => {
          const [rows, issues] = await Promise.all([apiClient.getModelWeightRows(id), apiClient.getModelWeightValidationIssues(id)]);
          void rows; void issues;
          await actions.refreshAll();
        }}
        onCreateFake={async (request) => {
          await apiClient.createFakeModelWeightBatch(request);
          await actions.refreshAll();
        }}
        onValidate={async (id) => {
          const result = await apiClient.validateModelWeightBatch(id);
          await actions.refreshAll();
          return result;
        }}
        onPromote={async (id) => {
          const result = await apiClient.promoteModelWeightBatch(id);
          await actions.refreshAll();
          return result;
        }}
        onPromoteReady={async () => {
          const result = await apiClient.promoteReadyModelWeightBatches();
          await actions.refreshAll();
          return result;
        }}
      />
    </section>
  );
}

function OmsPage({ dashboard, actions }: { dashboard: DashboardState; actions: { onCreateModelRun: (request: Parameters<typeof apiClient.createModelRun>[0]) => Promise<void>; onProcessModelRun: (id: string) => Promise<Awaited<ReturnType<typeof apiClient.processModelRun>>> } }) {
  const childOrders = dashboard.orders?.childOrders ?? [];
  const parentOrders = dashboard.orders?.parentOrders ?? [];
  const activeChildOrders = childOrders.filter((order) => !['Filled', 'Cancelled', 'Rejected', 'Expired'].includes(order.status));
  return (
    <section className="workspace-page">
      <SectionHeader title="OMS" eyebrow="Model runs, intents, risk decisions, orders, fills" />
      <div className="metric-grid">
        <MetricCard label="Model Runs" value={dashboard.modelRuns.length} tone="neutral" />
        <MetricCard label="Trade Intents" value={dashboard.tradeIntents.length} tone="info" />
        <MetricCard label="Parent Orders" value={parentOrders.length} tone="neutral" />
        <MetricCard label="Active Child Orders" value={activeChildOrders.length} tone={activeChildOrders.length ? 'warning' : 'ok'} />
        <MetricCard label="Fills" value={dashboard.fills.length} tone="info" />
        <MetricCard label="Risk Blocks" value={dashboard.riskDecisions.filter((item) => ['Rejected', 'Blocked'].includes(item.status)).length} tone={dashboard.riskDecisions.some((item) => ['Rejected', 'Blocked'].includes(item.status)) ? 'warning' : 'ok'} />
      </div>
      <ModelRunsPanel modelRuns={dashboard.modelRuns} onCreate={actions.onCreateModelRun} onProcess={actions.onProcessModelRun} />
      <RiskPanel tradeIntents={dashboard.tradeIntents} riskDecisions={dashboard.riskDecisions} />
      <OrdersPanel orders={dashboard.orders} />
      <FillsPanel fills={dashboard.fills} />
    </section>
  );
}

function EmsPage({ dashboard, health }: { dashboard: DashboardState; health?: HealthDto }) {
  return (
    <section className="workspace-page">
      <SectionHeader title="EMS" eyebrow="Execution, venues, fills, market data, algos" />
      <div className="metric-grid">
        <MetricCard label="Execution Gateway" value={health?.executionGateway ?? '-'} tone={health?.executionGateway === 'FakeLmaxGateway' ? 'ok' : 'danger'} />
        <MetricCard label="Execution Algo" value="MarketImmediate" tone="info" />
        <MetricCard label="Child Orders" value={dashboard.orders?.childOrders.length ?? 0} tone="neutral" />
        <MetricCard label="Fills" value={dashboard.fills.length} tone="info" />
      </div>
      <div className="page-grid two">
        <MarketDataPanel snapshots={dashboard.snapshots} bars={dashboard.bars} onCreateFakeSnapshots={async (request) => `Created ${(await apiClient.createFakeSnapshots(request)).created} local fake snapshots.`} onBuildBars={async (request) => {
          const result = await apiClient.buildBars(request);
          return `Bar build ${result.status}: created ${result.barsCreated}, updated ${result.barsUpdated}.`;
        }} />
        <FillsPanel fills={dashboard.fills} />
      </div>
      <div className="info-box">Execution quality, slippage, venue quality, and latency views are placeholders for future local-only analysis. No real LMAX execution controls are present.</div>
    </section>
  );
}

function MarketPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void> } }) {
  return (
    <section className="workspace-page">
      <SectionHeader title="Market Data" eyebrow="Fake/local snapshots and derived 15-minute bars" />
      <MarketDataPanel
        snapshots={dashboard.snapshots}
        bars={dashboard.bars}
        onCreateFakeSnapshots={async (request) => {
          const result = await apiClient.createFakeSnapshots(request);
          await actions.refreshAll();
          return `Created ${result.created} local fake snapshots.`;
        }}
        onBuildBars={async (request) => {
          const result = await apiClient.buildBars(request);
          await actions.refreshAll();
          return `Bar build ${result.status}: created ${result.barsCreated}, updated ${result.barsUpdated}.`;
        }}
      />
    </section>
  );
}

function ReconPage({ dashboard }: { dashboard: DashboardState }) {
  return (
    <section className="workspace-page">
      <SectionHeader title="Reconciliation" eyebrow="Intraday and EOD breaks" />
      <ReconciliationPanel breaks={dashboard.reconciliationBreaks} />
      <EodBreakTable rows={dashboard.eodReconciliationBreaks} />
    </section>
  );
}

function ExceptionsPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void>; setSelected: (item: unknown) => void } }) {
  const [selected, setSelected] = useState<ExceptionCaseDto | undefined>(dashboard.exceptionCases[0]);
  const [caseActions, setCaseActions] = useState<ExceptionCaseActionDto[]>([]);
  const [notes, setNotes] = useState<ExceptionCaseNoteDto[]>([]);
  const [status, setStatus] = useState('');
  const [severity, setSeverity] = useState('');
  const openCases = dashboard.exceptionCases.filter((item) => !['Resolved', 'FalsePositive', 'Waived', 'Closed'].includes(item.status));
  const resolvedToday = dashboard.exceptionCases.filter((item) => item.resolvedAtUtc?.slice(0, 10) === new Date().toISOString().slice(0, 10)).length;
  const assignedLocal = openCases.filter((item) => (item.assignedTo ?? '').toLowerCase().includes('local')).length;
  const filtered = dashboard.exceptionCases.filter((item) => (!status || item.status === status) && (!severity || item.severity === severity));

  const loadDetail = async (row: ExceptionCaseDto) => {
    setSelected(row);
    actions.setSelected(row);
    const [loadedActions, loadedNotes] = await Promise.all([apiClient.getExceptionCaseActions(row.id), apiClient.getExceptionCaseNotes(row.id)]);
    setCaseActions(loadedActions);
    setNotes(loadedNotes);
  };

  const runAction = async (label: string, callback: (reason?: string) => Promise<unknown>, requireReason = false) => {
    if (!selected) return;
    const reason = requireReason ? window.prompt(`${label} reason`) : window.prompt(`${label} reason (optional)`);
    if (requireReason && !reason?.trim()) return;
    if (label === 'Waive' && ['Blocking', 'Critical'].includes(selected.severity) && !window.confirm('Waive this blocking or critical exception case?')) return;
    await callback(reason ?? undefined);
    await actions.refreshAll();
    const refreshed = await apiClient.getExceptionCases({ limit: 100 });
    const updated = refreshed.find((item) => item.id === selected.id);
    if (updated) await loadDetail(updated);
  };

  return (
    <section className="workspace-page">
      <SectionHeader title="Exceptions" eyebrow="Operational exception and break management" />
      <div className="metric-grid">
        <MetricCard label="Open" value={openCases.length} tone={openCases.length ? 'warning' : 'ok'} />
        <MetricCard label="Blocking" value={openCases.filter((item) => item.severity === 'Blocking').length} tone={openCases.some((item) => item.severity === 'Blocking') ? 'danger' : 'ok'} />
        <MetricCard label="Critical" value={openCases.filter((item) => item.severity === 'Critical').length} tone={openCases.some((item) => item.severity === 'Critical') ? 'danger' : 'ok'} />
        <MetricCard label="Assigned Local" value={assignedLocal} tone={assignedLocal ? 'info' : 'neutral'} />
        <MetricCard label="Resolved Today" value={resolvedToday} tone="ok" />
      </div>
      <div className="panel wide">
        <SectionHeader title="Case Register" actions={(
          <div className="inline-controls">
            <select value={status} onChange={(event) => setStatus(event.target.value)} aria-label="Exception status filter">
              <option value="">All statuses</option>
              {['Open', 'Acknowledged', 'Investigating', 'Resolved', 'FalsePositive', 'Waived', 'Closed'].map((value) => <option key={value}>{value}</option>)}
            </select>
            <select value={severity} onChange={(event) => setSeverity(event.target.value)} aria-label="Exception severity filter">
              <option value="">All severities</option>
              {['Info', 'Warning', 'Blocking', 'Critical'].map((value) => <option key={value}>{value}</option>)}
            </select>
          </div>
        )} />
        <DataTable
          rows={filtered}
          getRowKey={(row) => row.id}
          onRowClick={(row) => void loadDetail(row)}
          emptyLabel="No exception cases"
          columns={[
            { key: 'created', header: 'Created', render: (row) => formatUtc(row.createdAtUtc), sortValue: (row) => row.createdAtUtc },
            { key: 'severity', header: 'Severity', render: (row) => <SeverityBadge value={row.severity} />, sortValue: (row) => row.severity },
            { key: 'status', header: 'Status', render: (row) => <StatusChip label={formatStatus(row.status)} tone={toneForStatus(row.status)} />, sortValue: (row) => row.status },
            { key: 'type', header: 'Type', render: (row) => formatStatus(row.type), sortValue: (row) => row.type },
            { key: 'source', header: 'Source', render: (row) => formatStatus(row.source), sortValue: (row) => row.source },
            { key: 'symbol', header: 'Symbol', render: (row) => row.symbol ?? '-' },
            { key: 'title', header: 'Title', render: (row) => row.title },
            { key: 'assigned', header: 'Assigned', render: (row) => row.assignedTo ?? '-' },
            { key: 'entity', header: 'Entity', render: (row) => `${row.entityType ?? '-'} ${formatIdShort(row.entityId)}` },
            { key: 'updated', header: 'Updated', render: (row) => formatUtc(row.updatedAtUtc), sortValue: (row) => row.updatedAtUtc }
          ]}
        />
      </div>
      {selected && (
        <div className="panel wide">
          <SectionHeader title="Selected Case" eyebrow={`${selected.title} (${formatIdShort(selected.id)})`} />
          <div className="detail-grid">
            <div>
              <p>{selected.description}</p>
              <p><strong>Status:</strong> {selected.status} <strong>Severity:</strong> {selected.severity}</p>
              <p><strong>Assigned:</strong> {selected.assignedTo ?? '-'} <strong>Entity:</strong> {selected.entityType ?? '-'} {formatIdShort(selected.entityId)}</p>
              <div className="button-row">
                <CommandButton tone="info" onClick={() => void runAction('Acknowledge', (reason) => apiClient.acknowledgeExceptionCase(selected.id, reason))}>Acknowledge</CommandButton>
                <CommandButton onClick={() => {
                  const assignedTo = window.prompt('Assign to');
                  if (assignedTo?.trim()) void apiClient.assignExceptionCase(selected.id, assignedTo.trim()).then(actions.refreshAll);
                }}>Assign</CommandButton>
                <CommandButton tone="warning" onClick={() => void runAction('Investigate', (reason) => apiClient.investigateExceptionCase(selected.id, reason))}>Investigating</CommandButton>
                <CommandButton tone="ok" onClick={() => void runAction('Resolve', (reason) => apiClient.resolveExceptionCase(selected.id, reason ?? ''), true)}>Resolve</CommandButton>
                <CommandButton tone="warning" onClick={() => void runAction('False Positive', (reason) => apiClient.falsePositiveExceptionCase(selected.id, reason ?? ''), true)}>False Positive</CommandButton>
                <CommandButton tone={['Blocking', 'Critical'].includes(selected.severity) ? 'danger' : 'warning'} onClick={() => void runAction('Waive', (reason) => apiClient.waiveExceptionCase(selected.id, reason ?? ''), true)}>Waive</CommandButton>
                <CommandButton onClick={() => void runAction('Reopen', (reason) => apiClient.reopenExceptionCase(selected.id, reason))}>Reopen</CommandButton>
                <CommandButton onClick={() => {
                  const note = window.prompt('Add note');
                  if (note?.trim()) void apiClient.addExceptionCaseNote(selected.id, note.trim()).then(() => loadDetail(selected));
                }}>Add Note</CommandButton>
              </div>
            </div>
            <div>
              <SectionHeader title="Action Timeline" />
              <Timeline items={caseActions.map((item) => ({ label: `${formatStatus(item.actionType)} by ${item.actorDisplayName}`, time: item.occurredAtUtc, tone: toneForStatus(item.toStatus ?? item.actionType), detail: item.reason ?? item.note ?? undefined }))} />
              <SectionHeader title="Notes" />
              {notes.length === 0 ? <div className="empty-state">No notes</div> : notes.map((note) => <div className="note-row" key={note.id}><strong>{note.createdBy}</strong> {formatUtc(note.createdAtUtc)}<br />{note.note}</div>)}
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

function LmaxEodPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void> } }) {
  return (
    <section className="workspace-page">
      <SectionHeader title="LMAX EOD" eyebrow="Report import, rollup control, wallet/PnL, EOD recon" />
      <div className="info-box">individual-trades.csv is the execution source of truth. trades.csv is a summary/rollup control. currency-wallets.csv is wallet/cash/PnL, not instrument positions.</div>
      <div className="metric-grid">
        <MetricCard label="Total Wallet USD" value={formatUsd(dashboard.eodPnlSummary?.totalWalletBalanceUsd)} tone="neutral" />
        <MetricCard label="Total P&L USD" value={formatUsd(dashboard.eodPnlSummary?.totalProfitLossUsd)} tone="neutral" />
        <MetricCard label="Total Commission USD" value={formatUsd(dashboard.eodPnlSummary?.totalCommissionUsd)} tone="neutral" />
        <MetricCard label="Total Financing USD" value={formatUsd(dashboard.eodPnlSummary?.totalFinancingUsd)} tone="neutral" />
        <MetricCard label="Total Net PnL USD" value={formatUsd(dashboard.eodPnlSummary?.totalNetPnlUsd)} tone="neutral" />
      </div>
      <LmaxEodReportsPanel
        importRuns={dashboard.lmaxImportRuns}
        validationIssues={dashboard.lmaxValidationIssues}
        individualTrades={dashboard.lmaxIndividualTrades}
        tradeSummaries={dashboard.lmaxTradeSummaries}
        currencyWallets={dashboard.lmaxCurrencyWallets}
        pnlSummary={dashboard.eodPnlSummary}
        reconciliationRuns={dashboard.eodReconciliationRuns}
        eodBreaks={dashboard.eodReconciliationBreaks}
        onGenerateFake={async (request) => {
          const result = await apiClient.generateFakeLmaxEod(request);
          await actions.refreshAll();
          return result;
        }}
        onImportGenerated={async (request) => {
          const result = await apiClient.importGeneratedLmaxEod(request);
          await actions.refreshAll();
          return result;
        }}
        onRunReconciliation={async (request) => {
          const result = await apiClient.runEodReconciliation(request);
          await actions.refreshAll();
          return result;
        }}
        onLoadPnl={async (reportDate, venueName, brokerAccountCode) => {
          const eodPnlSummary = await apiClient.getEodPnlSummary(reportDate, venueName, brokerAccountCode);
          void eodPnlSummary;
          await actions.refreshAll();
        }}
      />
    </section>
  );
}

function RiskAdminPage({ dashboard, health, integrity, actions }: { dashboard: DashboardState; health?: HealthDto; integrity?: ReferenceDataIntegrityDto; actions: { refreshAll: () => Promise<void> } }) {
  return (
    <section className="workspace-page">
      <SectionHeader title="Risk & Admin" eyebrow="Kill switch, reference data, instruments, venues" />
      <div className="page-grid two">
        <SafetyPanel health={health} />
        <ReferenceDataPanel integrity={integrity} />
      </div>
      <AdminPanel
        killSwitch={dashboard.killSwitch}
        instruments={dashboard.instruments}
        venues={dashboard.venues}
        onActivateKillSwitch={async (reason) => {
          await apiClient.activateKillSwitch(reason);
          await actions.refreshAll();
        }}
        onClearKillSwitch={async () => {
          await apiClient.clearKillSwitch();
          await actions.refreshAll();
        }}
      />
    </section>
  );
}

function ConnectivityLabPage() {
  return (
    <section className="workspace-page">
      <SectionHeader title="Connectivity Lab" eyebrow="Isolated manual Demo/UAT investigation" actions={<StatusChip label="Isolated Lab" tone="info" />} />
      <div className="critical-box">This page is read-only. The lab is not connected to this runtime, API, Worker, or execution workflow.</div>
      <div className="panel wide">
        <h3>Manual Commands</h3>
        <div className="command-list">
          <code>.\scripts\lmax-lab-print-config.ps1</code>
          <code>.\scripts\lmax-lab-fix-order-logon-smoke.ps1 -AllowExternalConnections</code>
          <code>.\scripts\lmax-lab-fix-marketdata-logon-smoke.ps1 -AllowExternalConnections</code>
          <code>.\scripts\lmax-lab-fix-marketdata-snapshot-smoke.ps1 -AllowExternalConnections -Instrument EURUSD -LmaxInstrumentId 4001 -SlashSymbol "EUR/USD"</code>
        </div>
        <div className="info-box">No credential forms, live trading controls, or order submission buttons are exposed in the cockpit. Demo market data smoke remains read-only and does not persist to LocalDB.</div>
      </div>
    </section>
  );
}

function AuditJournalPage({ events, onSelect }: { events: OperatorAuditEventDto[]; onSelect: (item: unknown) => void }) {
  const [severity, setSeverity] = useState('');
  const [eventType, setEventType] = useState('');
  const [entityType, setEntityType] = useState('');
  const [correlationId, setCorrelationId] = useState('');
  const [text, setText] = useState('');

  const filtered = events.filter((event) => {
    if (severity && event.severity !== severity) return false;
    if (eventType && !event.eventType.toLowerCase().includes(eventType.toLowerCase())) return false;
    if (entityType && !String(event.entityType ?? '').toLowerCase().includes(entityType.toLowerCase())) return false;
    if (correlationId && !String(event.correlationId ?? '').toLowerCase().includes(correlationId.toLowerCase())) return false;
    if (text) {
      const haystack = `${event.description} ${event.reason ?? ''} ${event.actorDisplayName} ${event.entityId ?? ''} ${event.metadataJson ?? ''}`.toLowerCase();
      if (!haystack.includes(text.toLowerCase())) return false;
    }
    return true;
  });

  return (
    <section className="workspace-page">
      <SectionHeader title="Audit Journal" eyebrow="Append-only operator and system event trail" />
      <div className="info-box">Local operator headers provide attribution only; this is not authentication. Metadata is sanitized before persistence.</div>
      <div className="filter-row">
        <select value={severity} onChange={(event) => setSeverity(event.target.value)} aria-label="Severity filter">
          <option value="">All severities</option>
          <option value="Info">Info</option>
          <option value="Warning">Warning</option>
          <option value="Critical">Critical</option>
        </select>
        <input value={eventType} onChange={(event) => setEventType(event.target.value)} placeholder="Event type" />
        <input value={entityType} onChange={(event) => setEntityType(event.target.value)} placeholder="Entity type" />
        <input value={correlationId} onChange={(event) => setCorrelationId(event.target.value)} placeholder="Correlation ID" />
        <input value={text} onChange={(event) => setText(event.target.value)} placeholder="Search" />
      </div>
      <DataTable rows={filtered} getRowKey={(row) => row.id} onRowClick={onSelect} columns={[
        { key: 'time', header: 'Occurred UTC', render: (row) => formatUtc(row.occurredAtUtc), sortValue: (row) => row.occurredAtUtc },
        { key: 'severity', header: 'Severity', render: (row) => <SeverityBadge value={row.severity} />, sortValue: (row) => row.severity },
        { key: 'event', header: 'Event', render: (row) => formatStatus(row.eventType), sortValue: (row) => row.eventType },
        { key: 'actor', header: 'Actor', render: (row) => `${row.actorDisplayName} (${row.actorType})`, sortValue: (row) => row.actorDisplayName },
        { key: 'entity', header: 'Entity', render: (row) => row.entityType ? `${row.entityType} ${formatIdShort(row.entityId)}` : '-', sortValue: (row) => row.entityType ?? '' },
        { key: 'result', header: 'Result', render: (row) => <StatusChip label={formatStatus(row.result)} tone={toneForStatus(row.result)} />, sortValue: (row) => row.result },
        { key: 'description', header: 'Description', render: (row) => row.description },
        { key: 'correlation', header: 'Correlation', render: (row) => formatIdShort(row.correlationId), sortValue: (row) => row.correlationId ?? '' }
      ]} />
    </section>
  );
}

function RecentModelRuns({ rows, onSelect }: { rows: ModelRunDto[]; onSelect: (row: unknown) => void }) {
  return (
    <div className="panel wide">
      <SectionHeader title="Latest Model Runs" />
      <DataTable rows={rows.slice(0, 8)} getRowKey={(row) => row.id} onRowClick={onSelect} columns={[
        { key: 'id', header: 'ID', render: (row) => formatIdShort(row.id), sortValue: (row) => row.id },
        { key: 'model', header: 'Model', render: (row) => row.modelName },
        { key: 'asOf', header: 'As Of', render: (row) => formatUtc(row.asOfUtc), sortValue: (row) => row.asOfUtc },
        { key: 'nav', header: 'NAV', render: (row) => formatUsd(row.navUsd), sortValue: (row) => row.navUsd },
        { key: 'status', header: 'Status', render: (row) => <StatusChip label={formatStatus(row.status)} tone={toneForStatus(row.status)} /> }
      ]} />
    </div>
  );
}

function RecentOrders({ orders, onSelect }: { orders?: OrdersDto; onSelect: (row: unknown) => void }) {
  return (
    <div className="panel wide">
      <SectionHeader title="Child Order Blotter" />
      <DataTable rows={(orders?.childOrders ?? []).slice(0, 10)} getRowKey={(row) => row.id} onRowClick={onSelect} columns={[
        { key: 'id', header: 'ID', render: (row) => formatIdShort(row.id), sortValue: (row) => row.id },
        { key: 'client', header: 'Client Order ID', render: (row) => formatIdShort(row.clientOrderId) },
        { key: 'side', header: 'Side', render: (row) => row.side },
        { key: 'qty', header: 'Base Qty', render: (row) => formatQuantity(row.baseQuantity), sortValue: (row) => row.baseQuantity },
        { key: 'status', header: 'Status', render: (row) => <StatusChip label={formatStatus(row.status)} tone={toneForStatus(row.status)} /> }
      ]} />
    </div>
  );
}

function WalletSummary({ dashboard }: { dashboard: DashboardState }) {
  return (
    <div className="panel wide">
      <SectionHeader title="Wallet / PnL Summary" />
      <div className="metric-grid">
        <MetricCard label="Wallet Balance USD" value={formatUsd(dashboard.eodPnlSummary?.totalWalletBalanceUsd)} />
        <MetricCard label="Profit/Loss USD" value={formatUsd(dashboard.eodPnlSummary?.totalProfitLossUsd)} />
        <MetricCard label="Commission USD" value={formatUsd(dashboard.eodPnlSummary?.totalCommissionUsd)} />
        <MetricCard label="Net PnL USD" value={formatUsd(dashboard.eodPnlSummary?.totalNetPnlUsd)} />
      </div>
      <DataTable rows={dashboard.lmaxCurrencyWallets} getRowKey={(row) => row.id} emptyLabel="No currency wallets loaded" columns={[
        { key: 'ccy', header: 'CCY', render: (row) => row.currency },
        { key: 'wallet', header: 'Wallet', render: (row) => formatQuantity(row.walletBalance), sortValue: (row) => row.walletBalance },
        { key: 'rate', header: 'Rate', render: (row) => formatPrice(row.rateToBaseCcy), sortValue: (row) => row.rateToBaseCcy },
        { key: 'walletUsd', header: 'Wallet USD', render: (row) => formatUsd(row.walletBalanceBaseUsd), sortValue: (row) => row.walletBalanceBaseUsd },
        { key: 'pnlUsd', header: 'P/L USD', render: (row) => formatUsd(row.profitLossBaseUsd), sortValue: (row) => row.profitLossBaseUsd }
      ]} />
    </div>
  );
}

function EodBreakTable({ rows }: { rows: EodReconciliationBreakDto[] }) {
  return (
    <div className="panel wide">
      <SectionHeader title="EOD Breaks" />
      <DataTable rows={rows} getRowKey={(row) => row.id} columns={[
        { key: 'severity', header: 'Severity', render: (row) => <SeverityBadge value={row.severity} />, sortValue: (row) => row.severity },
        { key: 'type', header: 'Type', render: (row) => row.type },
        { key: 'status', header: 'Status', render: (row) => row.status },
        { key: 'instrument', header: 'Instrument', render: (row) => row.instrumentId ?? '-' },
        { key: 'desc', header: 'Description', render: (row) => row.description },
        { key: 'created', header: 'Created', render: (row) => formatUtc(row.createdAtUtc), sortValue: (row) => row.createdAtUtc }
      ]} />
    </div>
  );
}

function AuditStrip({ health, dashboard }: { health?: HealthDto; dashboard: DashboardState }) {
  const latestAudit = dashboard.auditEvents.slice(0, 10).map((event) => ({
    label: `${formatStatus(event.eventType)}: ${event.description}`,
    time: event.occurredAtUtc,
    tone: (event.severity === 'Critical' ? 'danger' : event.severity === 'Warning' ? 'warning' : 'info') as Tone
  }));
  return (
    <footer className="audit-strip">
      <Timeline items={[
        ...latestAudit,
        { label: `Server ${health?.environment ?? 'unknown'}`, time: health?.utcServerTime, tone: 'info' },
        { label: `${dashboard.modelRuns.length} model runs`, tone: 'neutral' },
        { label: `${dashboard.fills.length} fills`, tone: 'neutral' },
        { label: `${dashboard.eodReconciliationBreaks.length} EOD breaks`, tone: dashboard.eodReconciliationBreaks.some((x) => x.severity === 'Blocking') ? 'danger' : 'ok' },
        { label: `${dashboard.exceptionCases.filter((x) => !['Resolved', 'FalsePositive', 'Waived', 'Closed'].includes(x.status)).length} open exceptions`, tone: dashboard.exceptionCases.some((x) => !['Resolved', 'FalsePositive', 'Waived', 'Closed'].includes(x.status) && ['Blocking', 'Critical'].includes(x.severity)) ? 'danger' : 'neutral' }
      ]} />
    </footer>
  );
}

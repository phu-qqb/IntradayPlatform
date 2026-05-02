import { useCallback, useEffect, useMemo, useState } from 'react';
import { Activity, Archive, BarChart3, Database, FileSearch, Gauge, GitBranch, Landmark, RadioTower, ShieldAlert, WalletCards } from 'lucide-react';
import { apiClient } from './api/apiClient';
import type {
  DriftSnapshotDto,
  EodPnlSummaryDto,
  EodReconciliationBreakDto,
  EodReconciliationRunDto,
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
};

type PageId = 'command' | 'pms' | 'weights' | 'oms' | 'ems' | 'market' | 'recon' | 'lmax-eod' | 'risk-admin' | 'connectivity';

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
  eodReconciliationBreaks: []
};

const navItems: Array<{ id: PageId; label: string; icon: typeof Activity }> = [
  { id: 'command', label: 'Command Center', icon: Gauge },
  { id: 'pms', label: 'PMS', icon: Landmark },
  { id: 'weights', label: 'Model Weights', icon: GitBranch },
  { id: 'oms', label: 'OMS', icon: Archive },
  { id: 'ems', label: 'EMS', icon: Activity },
  { id: 'market', label: 'Market Data', icon: BarChart3 },
  { id: 'recon', label: 'Reconciliation', icon: FileSearch },
  { id: 'lmax-eod', label: 'LMAX EOD', icon: WalletCards },
  { id: 'risk-admin', label: 'Risk & Admin', icon: ShieldAlert },
  { id: 'connectivity', label: 'Connectivity Lab', icon: RadioTower }
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
    const [modelRuns, modelWeightBatches, targets, drifts, internalPositions, brokerPositions, reconciliationBreaks, tradeIntents, riskDecisions, orders, fills, snapshots, bars, killSwitch, instruments, venues, lmaxImportRuns, lmaxValidationIssues, lmaxIndividualTrades, lmaxTradeSummaries, lmaxCurrencyWallets, eodReconciliationRuns, eodReconciliationBreaks] = await Promise.all([
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
      apiClient.getEodReconciliationBreaks()
    ]);

    setDashboard((current) => ({ ...current, modelRuns, modelWeightBatches, targets, drifts, internalPositions, brokerPositions, reconciliationBreaks, tradeIntents, riskDecisions, orders, fills, snapshots, bars, killSwitch, instruments, venues, lmaxImportRuns, lmaxValidationIssues, lmaxIndividualTrades, lmaxTradeSummaries, lmaxCurrencyWallets, eodReconciliationRuns, eodReconciliationBreaks }));
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
      {navItems.map((item) => {
        const Icon = item.icon;
        return (
          <button key={item.id} className={activePage === item.id ? 'active' : undefined} onClick={() => onSelect(item.id)}>
            <Icon size={16} /> {item.label}
          </button>
        );
      })}
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
    case 'recon':
      return <ReconPage dashboard={dashboard} />;
    case 'lmax-eod':
      return <LmaxEodPage dashboard={dashboard} actions={actions} />;
    case 'risk-admin':
      return <RiskAdminPage dashboard={dashboard} health={health} integrity={integrity} actions={actions} />;
    case 'connectivity':
      return <ConnectivityLabPage />;
    case 'command':
    default:
      return <CommandCenter dashboard={dashboard} health={health} integrity={integrity} actions={actions} />;
  }
}

function CommandCenter({ dashboard, health, integrity, actions }: { dashboard: DashboardState; health?: HealthDto; integrity?: ReferenceDataIntegrityDto; actions: { setSelected: (item: unknown) => void } }) {
  const openOrders = (dashboard.orders?.childOrders ?? []).filter((order) => !['Filled', 'Cancelled', 'Rejected', 'Expired'].includes(order.status)).length;
  const blockingBreaks = dashboard.eodReconciliationBreaks.filter((item) => item.severity === 'Blocking').length;
  return (
    <section className="workspace-page">
      <SectionHeader title="Command Center" eyebrow="Operational overview" />
      <div className="metric-grid">
        <MetricCard label="Safety" value={health?.executionGateway === 'FakeLmaxGateway' && !health.liveTradingEnabled ? 'Local Only' : 'Attention'} tone={health?.executionGateway === 'FakeLmaxGateway' && !health.liveTradingEnabled ? 'ok' : 'danger'} />
        <MetricCard label="Reference Integrity" value={`${integrity?.blockingIssueCount ?? '?'} blocking`} tone={(integrity?.blockingIssueCount ?? 1) === 0 ? 'ok' : 'danger'} />
        <MetricCard label="Latest Model Run" value={dashboard.modelRuns[0]?.status ?? '-'} sublabel={formatIdShort(dashboard.modelRuns[0]?.id)} tone={toneForStatus(dashboard.modelRuns[0]?.status)} />
        <MetricCard label="Latest Weight Batch" value={dashboard.modelWeightBatches[0]?.status ?? '-'} sublabel={formatIdShort(dashboard.modelWeightBatches[0]?.id)} tone={toneForStatus(dashboard.modelWeightBatches[0]?.status)} />
        <MetricCard label="Open Orders" value={openOrders} tone={openOrders === 0 ? 'neutral' : 'warning'} />
        <MetricCard label="Fills" value={dashboard.fills.length} tone="info" />
        <MetricCard label="EOD Blocking Breaks" value={blockingBreaks} tone={blockingBreaks === 0 ? 'ok' : 'danger'} />
        <MetricCard label="Net Wallet PnL" value={formatUsd(dashboard.eodPnlSummary?.totalNetPnlUsd)} tone="neutral" />
      </div>
      <div className="page-grid two">
        <HealthPanel health={health} />
        <ReferenceDataPanel integrity={integrity} />
        <RecentModelRuns rows={dashboard.modelRuns} onSelect={actions.setSelected} />
        <RecentOrders orders={dashboard.orders} onSelect={actions.setSelected} />
      </div>
    </section>
  );
}

function PmsPage({ dashboard }: { dashboard: DashboardState }) {
  return (
    <section className="workspace-page">
      <SectionHeader title="PMS" eyebrow="Portfolio, positions, targets, drift, wallets, PnL" />
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
  return (
    <section className="workspace-page">
      <SectionHeader title="OMS" eyebrow="Model runs, intents, risk decisions, orders, fills" />
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

function LmaxEodPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void> } }) {
  return (
    <section className="workspace-page">
      <SectionHeader title="LMAX EOD" eyebrow="Report import, rollup control, wallet/PnL, EOD recon" />
      <div className="info-box">individual-trades.csv is the execution source of truth. trades.csv is a summary/rollup control. currency-wallets.csv is wallet/cash/PnL, not instrument positions.</div>
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
      <SectionHeader title="Connectivity Lab" eyebrow="Isolated manual Demo/UAT investigation" />
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
  return (
    <footer className="audit-strip">
      <Timeline items={[
        { label: `Server ${health?.environment ?? 'unknown'}`, time: health?.utcServerTime, tone: 'info' },
        { label: `${dashboard.modelRuns.length} model runs`, tone: 'neutral' },
        { label: `${dashboard.fills.length} fills`, tone: 'neutral' },
        { label: `${dashboard.eodReconciliationBreaks.length} EOD breaks`, tone: dashboard.eodReconciliationBreaks.some((x) => x.severity === 'Blocking') ? 'danger' : 'ok' }
      ]} />
    </footer>
  );
}

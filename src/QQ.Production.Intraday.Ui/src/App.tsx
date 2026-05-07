import { useCallback, useEffect, useMemo, useState } from 'react';
import { Activity, Archive, BarChart3, CalendarClock, ClipboardList, FileSearch, Gauge, GitBranch, Landmark, RadioTower, ShieldAlert, UserCheck, WalletCards } from 'lucide-react';
import { apiClient, getSelectedOperatorId, setSelectedOperatorId } from './api/apiClient';
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
  LmaxShadowObservationDto,
  LmaxShadowReaderRunResultDto,
  LmaxShadowReplayRunDto,
  LmaxTradeSummaryDto,
  OrdersDto,
  OperatorAuditEventDto,
  OperatorUserDto,
  ApprovalRequestDto,
  ApprovalDecisionDto,
  GovernedActionResultDto,
  PositionDto,
  ReconciliationBreakDto,
  ReferenceDataIntegrityDto,
  RiskDecisionDto,
  RiskInstrumentDto,
  RiskLimitDto,
  RiskLimitSetDto,
  RiskVenueDto,
  InstrumentRiskLimitDto,
  TargetPositionDto,
  TradeIntentDto,
  TradingWindowDto,
  VenueRiskLimitDto,
  VenueDto,
  OperationalJobDefinitionDto,
  OperationalJobRunDto,
  DailyOperationsSummaryDto,
  DailyChecklistItemDto,
  OperationalRunbookDefinitionDto,
  OperationalRunbookRunDto,
  OperationalScheduleDefinitionDto
} from './api/types';
import { AdminPanel } from './components/AdminPanel';
import { ActionButton, ActionToast, useAsyncAction } from './components/ActionFeedback';
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
  activeRiskLimitSet?: RiskLimitSetDto;
  riskLimitSets: RiskLimitSetDto[];
  riskLimits: RiskLimitDto[];
  instrumentRiskLimits: InstrumentRiskLimitDto[];
  venueRiskLimits: VenueRiskLimitDto[];
  tradingWindows: TradingWindowDto[];
  riskInstruments: RiskInstrumentDto[];
  riskVenues: RiskVenueDto[];
  operators: OperatorUserDto[];
  currentOperator?: OperatorUserDto;
  approvalRequests: ApprovalRequestDto[];
  opsJobDefinitions: OperationalJobDefinitionDto[];
  opsJobRuns: OperationalJobRunDto[];
  runbookDefinitions: OperationalRunbookDefinitionDto[];
  runbookRuns: OperationalRunbookRunDto[];
  schedules: OperationalScheduleDefinitionDto[];
  schedulerEnabled: boolean;
  dailyOpsSummary?: DailyOperationsSummaryDto;
  dailyOpsChecklist: DailyChecklistItemDto[];
  lmaxShadowReplayRuns: LmaxShadowReplayRunDto[];
  lmaxShadowObservations: LmaxShadowObservationDto[];
  lmaxShadowReaderStatus?: LmaxShadowReaderRunResultDto;
};

type PageId = 'command' | 'daily-ops' | 'pms' | 'weights' | 'oms' | 'ems' | 'market' | 'exceptions' | 'recon' | 'lmax-eod' | 'lmax-shadow' | 'risk-admin' | 'governance' | 'audit' | 'connectivity';

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
  auditEvents: [],
  riskLimitSets: [],
  riskLimits: [],
  instrumentRiskLimits: [],
  venueRiskLimits: [],
  tradingWindows: [],
  riskInstruments: [],
  riskVenues: [],
  operators: [],
  approvalRequests: [],
  opsJobDefinitions: [],
  opsJobRuns: [],
  runbookDefinitions: [],
  runbookRuns: [],
  schedules: [],
  schedulerEnabled: false,
  dailyOpsChecklist: [],
  lmaxShadowReplayRuns: [],
  lmaxShadowObservations: [],
  lmaxShadowReaderStatus: undefined
};

const navSections: Array<{ label: string; items: Array<{ id: PageId; label: string; icon: typeof Activity }> }> = [
  {
    label: 'Operations',
    items: [
      { id: 'command', label: 'Command Center', icon: Gauge },
      { id: 'daily-ops', label: 'Daily Operations', icon: CalendarClock },
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
      { id: 'lmax-eod', label: 'LMAX EOD', icon: WalletCards },
      { id: 'lmax-shadow', label: 'LMAX Shadow', icon: FileSearch }
    ]
  },
  {
    label: 'Control',
    items: [
      { id: 'risk-admin', label: 'Risk & Admin', icon: ShieldAlert },
      { id: 'governance', label: 'Governance', icon: UserCheck },
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
  const { action: latestAction, elapsedSeconds, runAction, clearAction } = useAsyncAction();

  const loadHealth = useCallback(async () => setHealth(await apiClient.getHealth()), []);
  const loadIntegrity = useCallback(async () => setIntegrity(await apiClient.getReferenceDataIntegrity()), []);

  const loadDashboard = useCallback(async () => {
    const [modelRuns, modelWeightBatches, targets, drifts, internalPositions, brokerPositions, reconciliationBreaks, tradeIntents, riskDecisions, orders, fills, snapshots, bars, killSwitch, instruments, venues, lmaxImportRuns, lmaxValidationIssues, lmaxIndividualTrades, lmaxTradeSummaries, lmaxCurrencyWallets, eodReconciliationRuns, eodReconciliationBreaks, exceptionCases, auditEvents, riskLimitSets, activeRiskLimitSet, tradingWindows, riskInstruments, riskVenues, operators, currentOperator, approvalRequests, opsJobDefinitions, opsJobRuns, runbookDefinitions, runbookRuns, scheduleList, dailyOpsSummary, dailyOpsChecklist, lmaxShadowReplayRuns, lmaxShadowObservations, lmaxShadowReaderStatus] = await Promise.all([
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
      apiClient.getAuditEvents({ limit: 100 }),
      apiClient.getRiskLimitSets(),
      apiClient.getActiveRiskLimitSet().catch(() => undefined),
      apiClient.getTradingWindows(),
      apiClient.getRiskInstruments(),
      apiClient.getRiskVenues(),
      apiClient.getOperators(),
      apiClient.getCurrentOperator().catch(() => undefined),
      apiClient.getApprovals(),
      apiClient.getOpsJobDefinitions(),
      apiClient.getOpsJobRuns(),
      apiClient.getRunbookDefinitions(),
      apiClient.getRunbookRuns(),
      apiClient.getSchedules(),
      apiClient.getDailyOpsSummary(),
      apiClient.getDailyOpsChecklist(),
      apiClient.getLmaxShadowReplayRuns(),
      apiClient.getLmaxShadowObservations(),
      apiClient.getLmaxShadowReaderStatus()
    ]);

    const [riskLimits, instrumentRiskLimits, venueRiskLimits] = activeRiskLimitSet
      ? await Promise.all([
          apiClient.getRiskLimits(activeRiskLimitSet.id),
          apiClient.getInstrumentRiskLimits(activeRiskLimitSet.id),
          apiClient.getVenueRiskLimits(activeRiskLimitSet.id)
        ])
      : [[], [], []];

    setDashboard((current) => ({ ...current, modelRuns, modelWeightBatches, targets, drifts, internalPositions, brokerPositions, reconciliationBreaks, tradeIntents, riskDecisions, orders, fills, snapshots, bars, killSwitch, instruments, venues, lmaxImportRuns, lmaxValidationIssues, lmaxIndividualTrades, lmaxTradeSummaries, lmaxCurrencyWallets, eodReconciliationRuns, eodReconciliationBreaks, exceptionCases, auditEvents, riskLimitSets, activeRiskLimitSet, riskLimits, instrumentRiskLimits, venueRiskLimits, tradingWindows, riskInstruments, riskVenues, operators, currentOperator, approvalRequests, opsJobDefinitions, opsJobRuns, runbookDefinitions, runbookRuns, schedules: scheduleList.value ?? [], schedulerEnabled: scheduleList.schedulerEnabled, dailyOpsSummary, dailyOpsChecklist, lmaxShadowReplayRuns, lmaxShadowObservations, lmaxShadowReaderStatus }));
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
    runOperation: runAction,
    setSelected,
    onCreateModelRun: async (request: Parameters<typeof apiClient.createModelRun>[0]) => {
      await runAction('Creating local model run', () => apiClient.createModelRun(request), (run) => `Created model run ${formatIdShort(run.id)}.`);
      await refreshAll();
    },
    onProcessModelRun: async (id: string) => {
      const result = await runAction('Processing model run', () => apiClient.processModelRun(id), (processed) => `Process result: ${formatStatus(processed.status)}${processed.blockedReason ? ` (${formatStatus(processed.blockedReason)})` : ''}.`);
      setSelected(result);
      await refreshAll();
      return result;
    }
  }), [refreshAll, runAction]);

  const page = renderPage(activePage, dashboard, health, integrity, actions);

  return (
    <div className="operator-shell">
      <TopStatusBar health={health} integrity={integrity} onRefresh={refreshAll} />
      <div className="operator-context-strip">
        <span>Local operator context only — not production authentication.</span>
        <label>
          Operator
          <select value={dashboard.currentOperator?.operatorId ?? getSelectedOperatorId()} onChange={(event) => {
            setSelectedOperatorId(event.target.value);
            void refreshAll();
          }}>
            {dashboard.operators.map((operator) => (
              <option key={operator.operatorId} value={operator.operatorId}>{operator.displayName} ({operator.operatorId})</option>
            ))}
          </select>
        </label>
        <span className="chip neutral">Roles: {dashboard.currentOperator?.roles.join(', ') ?? 'unknown'}</span>
      </div>
      <div className="operator-body">
        <LeftNavigation activePage={activePage} onSelect={setActivePage} />
        <main className="main-workspace">
          {loading && <LoadingState label="Loading cockpit data" />}
          {error && <ErrorState message={error} />}
          {page}
        </main>
        <DetailDrawer item={selected} onClose={() => setSelected(undefined)} />
      </div>
      <ActionToast action={latestAction} elapsedSeconds={elapsedSeconds} onClear={clearAction} />
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

function renderPage(page: PageId, dashboard: DashboardState, health: HealthDto | undefined, integrity: ReferenceDataIntegrityDto | undefined, actions: { refreshAll: () => Promise<void>; runOperation: <T>(label: string, work: () => Promise<T>, successMessage?: (result: T) => string | undefined) => Promise<T>; setSelected: (item: unknown) => void; onCreateModelRun: (request: Parameters<typeof apiClient.createModelRun>[0]) => Promise<void>; onProcessModelRun: (id: string) => Promise<Awaited<ReturnType<typeof apiClient.processModelRun>>> }) {
  switch (page) {
    case 'daily-ops':
      return <DailyOperationsPage dashboard={dashboard} actions={actions} />;
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
    case 'lmax-shadow':
      return <LmaxShadowPage dashboard={dashboard} actions={actions} />;
    case 'risk-admin':
      return <RiskAdminPage dashboard={dashboard} health={health} integrity={integrity} actions={actions} />;
    case 'governance':
      return <GovernancePage dashboard={dashboard} actions={actions} />;
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

function isGovernedActionResult(value: unknown): value is GovernedActionResultDto {
  return Boolean(value && typeof value === 'object' && 'approvalRequired' in value && 'executed' in value);
}

function approvalMessage(result: GovernedActionResultDto) {
  return result.approvalRequired ? `Approval request created: ${formatIdShort(result.approvalRequestId)}.` : result.message;
}

function GovernancePage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void>; runOperation: <T>(label: string, work: () => Promise<T>, successMessage?: (result: T) => string | undefined) => Promise<T>; setSelected: (item: unknown) => void } }) {
  const pending = dashboard.approvalRequests.filter((item) => item.status === 'Pending');
  const [status, setStatus] = useState('');
  const filtered = dashboard.approvalRequests.filter((item) => !status || item.status === status);

  const decide = async (label: string, approval: ApprovalRequestDto, fn: (reason: string) => Promise<ApprovalRequestDto>) => {
    const reason = window.prompt(`${label} approval request. Enter reason:`);
    if (!reason?.trim()) return;
    await actions.runOperation(`${label} approval request`, () => fn(reason.trim()), (updated) => `${label}: ${formatStatus(updated.status)}.`);
    await actions.refreshAll();
  };

  const execute = async (approval: ApprovalRequestDto) => {
    await actions.runOperation('Executing approved request', () => apiClient.executeApproval(approval.id), (result) => result.message);
    await actions.refreshAll();
  };

  return (
    <section className="workspace-page">
      <SectionHeader title="Governance" eyebrow="Local identity, roles, permissions, and four-eyes approvals" />
      <div className="metric-grid">
        <MetricCard label="Current Operator" value={dashboard.currentOperator?.displayName ?? getSelectedOperatorId()} sublabel="Local development operator context only" tone="info" />
        <MetricCard label="Roles" value={dashboard.currentOperator?.roles.join(', ') || '-'} sublabel="Header-based, not authentication" tone="neutral" />
        <MetricCard label="Pending Approvals" value={pending.length} sublabel="Sensitive actions waiting for checker" tone={pending.length ? 'warning' : 'ok'} />
        <MetricCard label="My Permission Count" value={dashboard.currentOperator?.permissions.length ?? 0} tone="neutral" />
      </div>
      <div className="page-grid two">
        <div className="panel">
          <SectionHeader title="Local Operators" />
          <DataTable rows={dashboard.operators} getRowKey={(row) => row.id} onRowClick={actions.setSelected} columns={[
            { key: 'operator', header: 'Operator', render: (row) => row.displayName },
            { key: 'id', header: 'Operator ID', render: (row) => <code>{row.operatorId}</code> },
            { key: 'roles', header: 'Roles', render: (row) => row.roles.join(', ') },
            { key: 'enabled', header: 'Enabled', render: (row) => <StatusChip label={String(row.isEnabled)} tone={row.isEnabled ? 'ok' : 'danger'} /> }
          ]} />
        </div>
        <div className="panel">
          <SectionHeader title="Current Permissions" />
          <div className="chip-list">
            {(dashboard.currentOperator?.permissions ?? []).map((permission) => <span key={permission} className="chip neutral">{formatStatus(permission)}</span>)}
          </div>
        </div>
      </div>
      <div className="panel wide">
        <SectionHeader title="Approval Requests" actions={(
          <select value={status} onChange={(event) => setStatus(event.target.value)} aria-label="Approval status filter">
            <option value="">All statuses</option>
            {['Pending', 'Approved', 'Rejected', 'Cancelled', 'Expired', 'Executed'].map((value) => <option key={value}>{value}</option>)}
          </select>
        )} />
        <DataTable rows={filtered} getRowKey={(row) => row.id} onRowClick={actions.setSelected} columns={[
          { key: 'requestedAt', header: 'Requested', render: (row) => formatUtc(row.requestedAtUtc) },
          { key: 'type', header: 'Type', render: (row) => formatStatus(row.type) },
          { key: 'status', header: 'Status', render: (row) => <StatusChip label={row.status} tone={toneForStatus(row.status)} /> },
          { key: 'requestedBy', header: 'Requested By', render: (row) => row.requestedByDisplayName },
          { key: 'role', header: 'Required Role', render: (row) => row.requiredApproverRole },
          { key: 'entity', header: 'Entity', render: (row) => `${row.entityType} ${formatIdShort(row.entityId)}` },
          { key: 'reason', header: 'Reason', render: (row) => row.reason },
          { key: 'expires', header: 'Expires', render: (row) => formatUtc(row.expiresAtUtc) },
          { key: 'actions', header: 'Actions', render: (row) => (
            <div className="button-row compact">
              <ActionButton idleLabel="Approve" runningLabel="Approving..." disabled={row.status !== 'Pending'} onClick={(event) => event.stopPropagation()} onAction={() => decide('Approve', row, (reason) => apiClient.approveApproval(row.id, reason))} />
              <ActionButton className="command-button warning" idleLabel="Reject" runningLabel="Rejecting..." disabled={row.status !== 'Pending'} onClick={(event) => event.stopPropagation()} onAction={() => decide('Reject', row, (reason) => apiClient.rejectApproval(row.id, reason))} />
              <ActionButton className="command-button danger" idleLabel="Execute" runningLabel="Executing..." disabled={row.status !== 'Approved'} onClick={(event) => event.stopPropagation()} onAction={() => execute(row)} />
            </div>
          ) }
        ]} />
      </div>
    </section>
  );
}

function CommandCenter({ dashboard, health, integrity, actions }: { dashboard: DashboardState; health?: HealthDto; integrity?: ReferenceDataIntegrityDto; actions: { setSelected: (item: unknown) => void } }) {
  const openOrders = (dashboard.orders?.childOrders ?? []).filter((order) => !['Filled', 'Cancelled', 'Rejected', 'Expired'].includes(order.status)).length;
  const blockingBreaks = dashboard.eodReconciliationBreaks.filter((item) => item.severity === 'Blocking').length;
  const openExceptions = dashboard.exceptionCases.filter((item) => !['Resolved', 'FalsePositive', 'Waived', 'Closed'].includes(item.status));
  const blockingExceptions = openExceptions.filter((item) => ['Blocking', 'Critical'].includes(item.severity)).length;
  const mismatchCount = obviousPositionMismatchCount(dashboard.internalPositions, dashboard.brokerPositions);
  const latestEod = dashboard.eodReconciliationRuns[0];
  const recentRiskBlocks = dashboard.riskDecisions.filter((item) => ['Rejected', 'Blocked'].includes(item.status));
  const activeWindow = dashboard.tradingWindows.find((item) => item.isActive && item.tradingEnabled);
  const pendingApprovals = dashboard.approvalRequests.filter((item) => item.status === 'Pending');
  const latestJob = dashboard.opsJobRuns[0];
  const checklistComplete = dashboard.dailyOpsChecklist.filter((item) => item.status === 'Complete').length;
  const openShadowBlocking = dashboard.lmaxShadowObservations.filter((item) => item.status === 'Open' && item.severity === 'Blocking').length;
  const openShadowWarnings = dashboard.lmaxShadowObservations.filter((item) => item.status === 'Open' && item.severity === 'Warning').length;
  const latestShadowReplay = dashboard.lmaxShadowReplayRuns[0];
  return (
    <section className="workspace-page">
      <SectionHeader title="Command Center" eyebrow="Operational overview" actions={<CommandButton tone="info" onClick={() => actions.setSelected(dashboard.auditEvents[0])}>Latest Event</CommandButton>} />
      <div className="metric-grid">
        <MetricCard label="Runtime Safety" value={health?.executionGateway === 'FakeLmaxGateway' && !health.liveTradingEnabled && !health.externalConnectionsEnabled ? 'Safe Local' : 'Attention'} sublabel="FakeLmax-only runtime boundary" tone={health?.executionGateway === 'FakeLmaxGateway' && !health.liveTradingEnabled && !health.externalConnectionsEnabled ? 'ok' : 'danger'} />
        <MetricCard label="Active Risk Set" value={dashboard.activeRiskLimitSet ? `v${dashboard.activeRiskLimitSet.version}` : '-'} sublabel={dashboard.activeRiskLimitSet?.name ?? 'No active profile loaded'} tone={dashboard.activeRiskLimitSet?.isActive ? 'ok' : 'danger'} />
        <MetricCard label="Trading Window" value={activeWindow ? `${activeWindow.openTime}-${activeWindow.closeTime}` : '-'} sublabel={activeWindow ? `No new orders after ${activeWindow.noNewOrdersAfter} ${activeWindow.timeZoneId}` : 'No active window loaded'} tone={activeWindow ? 'info' : 'warning'} />
        <MetricCard label="Risk Blocks" value={recentRiskBlocks.length} sublabel={recentRiskBlocks[0]?.rejectReason ? `Latest: ${formatStatus(recentRiskBlocks[0].rejectReason)}` : 'Recent decisions loaded'} tone={recentRiskBlocks.length ? 'warning' : 'ok'} />
        <MetricCard label="Latest Weight Batch" value={dashboard.modelWeightBatches[0]?.status ?? '-'} sublabel={formatIdShort(dashboard.modelWeightBatches[0]?.id)} tone={toneForStatus(dashboard.modelWeightBatches[0]?.status)} />
        <MetricCard label="Latest Model Run" value={dashboard.modelRuns[0]?.status ?? '-'} sublabel={formatIdShort(dashboard.modelRuns[0]?.id)} tone={toneForStatus(dashboard.modelRuns[0]?.status)} />
        <MetricCard label="Open Exceptions" value={openExceptions.length} sublabel={`${blockingExceptions} blocking/critical`} tone={blockingExceptions ? 'danger' : openExceptions.length ? 'warning' : 'ok'} />
        <MetricCard label="Pending Approvals" value={pendingApprovals.length} sublabel={pendingApprovals[0] ? `${formatStatus(pendingApprovals[0].type)} requested by ${pendingApprovals[0].requestedByOperatorId}` : 'No maker/checker items pending'} tone={pendingApprovals.length ? 'warning' : 'ok'} />
        <MetricCard label="LMAX Shadow" value={latestShadowReplay?.status ?? 'Not run'} sublabel={`${openShadowBlocking} blocking, ${openShadowWarnings} warnings`} tone={openShadowBlocking ? 'danger' : openShadowWarnings ? 'warning' : latestShadowReplay ? 'ok' : 'neutral'} />
        <MetricCard label="Daily Checklist" value={`${checklistComplete}/${dashboard.dailyOpsChecklist.length || '-'}`} sublabel={latestJob ? `Latest job ${formatStatus(latestJob.status)}` : 'No operational jobs yet'} tone={dashboard.dailyOpsSummary?.failedJobCount ? 'danger' : checklistComplete ? 'info' : 'neutral'} />
        <MetricCard label="Failed Jobs Today" value={dashboard.dailyOpsSummary?.failedJobCount ?? 0} sublabel={latestJob ? `${formatStatus(latestJob.jobType)} at ${formatUtc(latestJob.startedAtUtc)}` : 'No job history loaded'} tone={dashboard.dailyOpsSummary?.failedJobCount ? 'danger' : 'ok'} />
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

function DailyOperationsPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void>; runOperation: <T>(label: string, work: () => Promise<T>, successMessage?: (result: T) => string | undefined) => Promise<T>; setSelected: (item: unknown) => void } }) {
  const [reason, setReason] = useState('Daily operations manual run');
  const summarizeJson = (json?: string | null) => {
    if (!json) return '-';
    try {
      const parsed = JSON.parse(json) as Record<string, unknown>;
      const keys = ['status', 'blockingIssueCount', 'warningIssueCount', 'processedCount', 'blockedCount', 'failedCount', 'breakCount', 'blockingBreakCount', 'totalNetPnlUsd', 'currencyCount'];
      const parts = keys.filter((key) => parsed[key] !== undefined).map((key) => `${formatStatus(key)}: ${String(parsed[key])}`);
      return parts.length ? parts.join(' | ') : json;
    } catch {
      return json;
    }
  };
  const runJob = async (jobType: string) => {
    if (!reason.trim()) {
      window.alert('A reason is required to run an operational job.');
      return;
    }
    await actions.runOperation(
      `Running ${formatStatus(jobType)}`,
      () => apiClient.runOpsJob({ jobType, reason }),
      (job) => `${formatStatus(job.jobType)} finished with ${formatStatus(job.status)}.`
    );
    await actions.refreshAll();
  };
  const retryJob = async (job: OperationalJobRunDto) => {
    if (!reason.trim()) {
      window.alert('A reason is required to retry an operational job.');
      return;
    }
    await actions.runOperation(
      `Retrying ${formatStatus(job.jobType)}`,
      () => apiClient.retryOpsJob(job.id, reason),
      (result) => `Retry created job ${formatIdShort(result.id)} with ${formatStatus(result.status)}.`
    );
    await actions.refreshAll();
  };
  const runRunbook = async (runbookType: string) => {
    if (!reason.trim()) {
      window.alert('A reason is required to run an operational runbook.');
      return;
    }
    await actions.runOperation(
      `Running ${formatStatus(runbookType)} runbook`,
      () => apiClient.runRunbook({ runbookType, reason }),
      (runbook) => `${formatStatus(runbook.runbookType)} is ${formatStatus(runbook.status)}.`
    );
    await actions.refreshAll();
  };
  const retryRunbook = async (runbook: OperationalRunbookRunDto) => {
    if (!reason.trim()) {
      window.alert('A reason is required to retry an operational runbook.');
      return;
    }
    await actions.runOperation(
      `Retrying ${formatStatus(runbook.runbookType)} runbook`,
      () => apiClient.retryRunbook(runbook.id, reason),
      (result) => `Retry created runbook ${formatIdShort(result.id)}.`
    );
    await actions.refreshAll();
  };
  const completeWaitingGate = async (runbook: OperationalRunbookRunDto) => {
    if (!reason.trim()) {
      window.alert('A reason is required to complete a manual runbook gate.');
      return;
    }
    const steps = await apiClient.getRunbookSteps(runbook.id);
    const waiting = steps.find((step) => step.status === 'WaitingForOperator');
    if (!waiting) {
      window.alert('No waiting manual gate was found for this runbook.');
      return;
    }
    await actions.runOperation(
      `Completing ${formatStatus(runbook.runbookType)} manual gate`,
      () => apiClient.completeRunbookManualStep(runbook.id, waiting.id, reason),
      (result) => `${formatStatus(result.runbookType)} is ${formatStatus(result.status)}.`
    );
    await actions.refreshAll();
  };

  const summary = dashboard.dailyOpsSummary;
  const jobButtons = [
    'ReferenceDataIntegrityCheck',
    'BuildMarketDataBars',
    'PromoteReadyWeightBatches',
    'ProcessPendingModelRuns',
    'RunEodReconciliation',
    'CalculateEodPnlSummary'
  ];

  return (
    <section className="workspace-page">
      <SectionHeader title="Daily Operations" eyebrow="Persistent local job runs, checklist, audit, and safe reruns" />
      <div className="metric-grid">
        <MetricCard label="Start of Day" value={dashboard.runbookRuns.find((run) => run.runbookType === 'StartOfDay')?.status ?? 'Not Started'} sublabel="Local readiness runbook" tone={toneForStatus(dashboard.runbookRuns.find((run) => run.runbookType === 'StartOfDay')?.status)} />
        <MetricCard label="Intraday Cycle" value={dashboard.runbookRuns.find((run) => run.runbookType === 'IntradayCycle')?.status ?? 'Not Started'} sublabel="Promote/process/build loop" tone={toneForStatus(dashboard.runbookRuns.find((run) => run.runbookType === 'IntradayCycle')?.status)} />
        <MetricCard label="End of Day" value={dashboard.runbookRuns.find((run) => run.runbookType === 'EndOfDay')?.status ?? 'Not Started'} sublabel="Fake local EOD flow only" tone={toneForStatus(dashboard.runbookRuns.find((run) => run.runbookType === 'EndOfDay')?.status)} />
        <MetricCard label="Manual Gates" value={dashboard.runbookRuns.filter((run) => run.status === 'WaitingForOperator').length} sublabel="Waiting for operator confirmation" tone={dashboard.runbookRuns.some((run) => run.status === 'WaitingForOperator') ? 'warning' : 'ok'} />
        <MetricCard label="Local Scheduler" value={dashboard.schedulerEnabled ? 'Enabled' : 'Disabled'} sublabel="Disabled by default; local-only schedules" tone={dashboard.schedulerEnabled ? 'warning' : 'ok'} />
        <MetricCard label="Reference Data" value={summary?.latestReferenceIntegrity?.status ?? 'Not Started'} sublabel={summary?.latestReferenceIntegrity ? formatUtc(summary.latestReferenceIntegrity.startedAtUtc) : 'Run the integrity check'} tone={toneForStatus(summary?.latestReferenceIntegrity?.status)} />
        <MetricCard label="Market Data Bars" value={summary?.latestMarketDataBars?.status ?? 'Not Started'} sublabel="Latest 15-minute bar build" tone={toneForStatus(summary?.latestMarketDataBars?.status)} />
        <MetricCard label="Weight Promotion" value={summary?.latestWeightPromotion?.status ?? 'Not Started'} sublabel="Ready DB batches to ModelRuns" tone={toneForStatus(summary?.latestWeightPromotion?.status)} />
        <MetricCard label="Model Processing" value={summary?.latestModelRunProcessing?.status ?? 'Not Started'} sublabel="Explicit FakeLmax-only processing" tone={toneForStatus(summary?.latestModelRunProcessing?.status)} />
        <MetricCard label="EOD Reconciliation" value={summary?.latestEodReconciliation?.status ?? 'Not Started'} sublabel="Imported local LMAX EOD only" tone={toneForStatus(summary?.latestEodReconciliation?.status)} />
        <MetricCard label="PnL Summary" value={summary?.latestPnlSummary?.status ?? 'Not Started'} sublabel="Wallet/cash/PnL USD summary" tone={toneForStatus(summary?.latestPnlSummary?.status)} />
        <MetricCard label="Open Exceptions" value={summary?.openExceptionCount ?? 0} sublabel={`${summary?.openBlockingExceptionCount ?? 0} blocking/critical`} tone={summary?.openBlockingExceptionCount ? 'danger' : summary?.openExceptionCount ? 'warning' : 'ok'} />
        <MetricCard label="Pending Approvals" value={summary?.pendingApprovalCount ?? 0} tone={summary?.pendingApprovalCount ? 'warning' : 'ok'} />
        <MetricCard label="Failed Jobs Today" value={summary?.failedJobCount ?? 0} tone={summary?.failedJobCount ? 'danger' : 'ok'} />
      </div>

      <div className="panel wide">
        <SectionHeader title="Runbook Runner" eyebrow="Start-of-day, intraday, and end-of-day orchestration. Existing local jobs remain the executable units." />
        <div className="button-row">
          {['StartOfDay', 'IntradayCycle', 'EndOfDay'].map((runbookType) => (
            <ActionButton key={runbookType} idleLabel={`Run ${formatStatus(runbookType)}`} runningLabel="Running..." onAction={() => runRunbook(runbookType)} />
          ))}
        </div>
        <div className="info-box">Runbooks are local-only. They do not call real LMAX, submit orders, persist live market data, or enable external connections.</div>
      </div>

      <div className="page-grid two">
        <div className="panel wide">
          <SectionHeader title="Runbook Definitions" eyebrow="Seeded institutional operating flows." />
          <DataTable rows={dashboard.runbookDefinitions} getRowKey={(row) => row.id} onRowClick={(row) => actions.setSelected(row)} emptyLabel="No runbook definitions loaded" columns={[
            { key: 'name', header: 'Name', render: (row) => row.name, sortValue: (row) => row.name },
            { key: 'type', header: 'Type', render: (row) => formatStatus(row.runbookType), sortValue: (row) => row.runbookType },
            { key: 'enabled', header: 'Enabled', render: (row) => <StatusChip label={row.isEnabled ? 'Enabled' : 'Disabled'} tone={row.isEnabled ? 'ok' : 'warning'} /> },
            { key: 'retry', header: 'Rerun', render: (row) => row.isRerunnable ? 'Yes' : 'No' },
            { key: 'description', header: 'Description', render: (row) => row.description }
          ]} />
        </div>
        <div className="panel wide">
          <SectionHeader title="Local Scheduler" eyebrow="Foundation only. Disabled by default and local-only." />
          <div className="metric-grid compact">
            <MetricCard label="Scheduler" value={dashboard.schedulerEnabled ? 'Enabled' : 'Disabled'} sublabel="LocalScheduler:Enabled default is false" tone={dashboard.schedulerEnabled ? 'warning' : 'ok'} />
            <MetricCard label="Schedules" value={dashboard.schedules.length} sublabel="Only enabled schedules can trigger runbooks" tone="neutral" />
          </div>
          <DataTable rows={dashboard.schedules} getRowKey={(row) => row.id} emptyLabel="No local schedules are configured. Scheduler remains disabled by default." columns={[
            { key: 'name', header: 'Name', render: (row) => row.name },
            { key: 'enabled', header: 'Enabled', render: (row) => <StatusChip label={row.isEnabled ? 'Enabled' : 'Disabled'} tone={row.isEnabled ? 'warning' : 'ok'} /> },
            { key: 'next', header: 'Next Run', render: (row) => formatUtc(row.nextRunAtUtc) },
            { key: 'last', header: 'Last Run', render: (row) => formatUtc(row.lastRunAtUtc) }
          ]} />
        </div>
      </div>

      <div className="panel wide">
        <SectionHeader title="Runbook Runs" eyebrow="Runbook history with linked job runs and manual gates." />
        <DataTable rows={dashboard.runbookRuns} getRowKey={(row) => row.id} onRowClick={(row) => actions.setSelected(row)} emptyLabel="No operational runbooks have run yet" columns={[
          { key: 'started', header: 'Started', render: (row) => formatUtc(row.startedAtUtc), sortValue: (row) => row.startedAtUtc },
          { key: 'type', header: 'Runbook', render: (row) => formatStatus(row.runbookType), sortValue: (row) => row.runbookType },
          { key: 'status', header: 'Status', render: (row) => <StatusChip label={row.status} tone={toneForStatus(row.status)} />, sortValue: (row) => row.status },
          { key: 'duration', header: 'Duration', render: (row) => row.durationMs == null ? '-' : `${Math.round(row.durationMs / 1000)}s`, className: 'numeric' },
          { key: 'reason', header: 'Reason', render: (row) => row.reason ?? '-' },
          { key: 'retryOf', header: 'Retry Of', render: (row) => row.retryOfRunbookRunId ? <code title={row.retryOfRunbookRunId}>{formatIdShort(row.retryOfRunbookRunId)}</code> : '-' },
          { key: 'manual', header: 'Manual Gate', render: (row) => row.status === 'WaitingForOperator' ? <ActionButton idleLabel="Complete" runningLabel="Completing..." onClick={(event) => event.stopPropagation()} onAction={() => completeWaitingGate(row)} /> : '-' },
          { key: 'retry', header: 'Retry', render: (row) => row.canRetry ? <ActionButton idleLabel="Retry" runningLabel="Retrying..." onClick={(event) => event.stopPropagation()} onAction={() => retryRunbook(row)} /> : '-' }
        ]} />
      </div>

      <div className="panel wide">
        <SectionHeader title="Job Runner" eyebrow="Local-only wrappers around existing operational services. A reason is required." />
        <label className="form-field">
          <span>Reason</span>
          <input value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Reason for this operational run" />
        </label>
        <div className="button-row">
          {jobButtons.map((jobType) => (
            <ActionButton key={jobType} idleLabel={formatStatus(jobType)} runningLabel="Running..." onAction={() => runJob(jobType)} />
          ))}
        </div>
        <div className="info-box">No job here calls real LMAX, enables live trading, or changes external connection safety. Business blocks are recorded as job output rather than hidden.</div>
      </div>

      <div className="page-grid two">
        <div className="panel wide">
          <SectionHeader title="Daily Checklist" eyebrow="Status is derived from today's operational job history and open controls." />
          <DataTable rows={dashboard.dailyOpsChecklist} getRowKey={(row, index) => `${row.name}-${index}`} onRowClick={(row) => actions.setSelected(row)} emptyLabel="No checklist items loaded" columns={[
            { key: 'name', header: 'Item', render: (row) => row.name, sortValue: (row) => row.name },
            { key: 'status', header: 'Status', render: (row) => <StatusChip label={row.status} tone={toneForStatus(row.status)} />, sortValue: (row) => row.status },
            { key: 'message', header: 'Message', render: (row) => row.message },
            { key: 'entity', header: 'Related', render: (row) => row.relatedEntityType ? `${row.relatedEntityType} ${formatIdShort(row.relatedEntityId)}` : '-' }
          ]} />
        </div>
        <div className="panel wide">
          <SectionHeader title="Job Definitions" eyebrow="Enabled local workflows and retry policy." />
          <DataTable rows={dashboard.opsJobDefinitions} getRowKey={(row) => row.id} emptyLabel="No job definitions loaded" columns={[
            { key: 'name', header: 'Name', render: (row) => row.name, sortValue: (row) => row.name },
            { key: 'jobType', header: 'Type', render: (row) => formatStatus(row.jobType), sortValue: (row) => row.jobType },
            { key: 'enabled', header: 'Enabled', render: (row) => <StatusChip label={row.isEnabled ? 'Enabled' : 'Disabled'} tone={row.isEnabled ? 'ok' : 'warning'} /> },
            { key: 'retry', header: 'Rerun', render: (row) => row.isRerunnable ? 'Yes' : 'No' },
            { key: 'severity', header: 'Severity', render: (row) => <SeverityBadge value={row.severity} /> }
          ]} />
        </div>
      </div>

      <div className="panel wide">
        <SectionHeader title="Job Runs" eyebrow="Audited, persistent run history with step/event details in the drawer." />
        <DataTable rows={dashboard.opsJobRuns} getRowKey={(row) => row.id} onRowClick={(row) => actions.setSelected(row)} emptyLabel="No operational job runs loaded" columns={[
          { key: 'started', header: 'Started', render: (row) => formatUtc(row.startedAtUtc), sortValue: (row) => row.startedAtUtc },
          { key: 'jobType', header: 'Job', render: (row) => formatStatus(row.jobType), sortValue: (row) => row.jobType },
          { key: 'status', header: 'Status', render: (row) => <StatusChip label={row.status} tone={toneForStatus(row.status)} />, sortValue: (row) => row.status },
          { key: 'duration', header: 'Duration', render: (row) => row.durationMs == null ? '-' : `${Math.round(row.durationMs / 1000)}s`, sortValue: (row) => row.durationMs ?? 0, className: 'numeric' },
          { key: 'operator', header: 'Triggered By', render: (row) => row.triggeredByDisplayName ?? row.triggeredByOperatorId ?? row.triggeredByActorType },
          { key: 'output', header: 'Output Summary', render: (row) => row.errorMessage ? <span className="danger-text">{row.errorMessage}</span> : summarizeJson(row.outputJson) },
          { key: 'retryOf', header: 'Retry Of', render: (row) => row.retryOfJobRunId ? <code title={row.retryOfJobRunId}>{formatIdShort(row.retryOfJobRunId)}</code> : '-' },
          { key: 'exception', header: 'Exception', render: (row) => row.exceptionCaseId ? <code title={row.exceptionCaseId}>{formatIdShort(row.exceptionCaseId)}</code> : '-' },
          { key: 'correlation', header: 'Correlation', render: (row) => row.correlationId ?? '-' },
          { key: 'retry', header: 'Retry', render: (row) => row.canRetry ? <ActionButton idleLabel="Retry" runningLabel="Retrying..." onClick={(event) => event.stopPropagation()} onAction={() => retryJob(row)} /> : '-' }
        ]} />
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

function WeightsPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void>; runOperation: <T>(label: string, work: () => Promise<T>, successMessage?: (result: T) => string | undefined) => Promise<T> } }) {
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
          await actions.runOperation('Creating fake weight batch', () => apiClient.createFakeModelWeightBatch(request), (batch) => `Created fake weight batch ${formatIdShort(batch.id)}.`);
          await actions.refreshAll();
        }}
        onValidate={async (id) => {
          const result = await actions.runOperation('Validating weight batch', () => apiClient.validateModelWeightBatch(id), (validated) => validated.message);
          await actions.refreshAll();
          return result;
        }}
        onPromote={async (id) => {
          const result = await actions.runOperation('Promoting weight batch', () => apiClient.promoteModelWeightBatch(id), (promoted) => promoted.modelRunId ? `Promoted to model run ${formatIdShort(promoted.modelRunId)}.` : promoted.message);
          await actions.refreshAll();
          return result;
        }}
        onPromoteReady={async () => {
          const result = await actions.runOperation('Promoting ready weight batches', () => apiClient.promoteReadyModelWeightBatches(), (promoted) => `Promote-ready completed for ${promoted.length} batch result(s).`);
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

function MarketPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void>; runOperation: <T>(label: string, work: () => Promise<T>, successMessage?: (result: T) => string | undefined) => Promise<T> } }) {
  return (
    <section className="workspace-page">
      <SectionHeader title="Market Data" eyebrow="Fake/local snapshots and derived 15-minute bars" />
      <MarketDataPanel
        snapshots={dashboard.snapshots}
        bars={dashboard.bars}
        onCreateFakeSnapshots={async (request) => {
          const result = await actions.runOperation('Creating fake snapshots', () => apiClient.createFakeSnapshots(request), (created) => `Created ${created.created} local fake snapshots.`);
          await actions.refreshAll();
          return `Created ${result.created} local fake snapshots.`;
        }}
        onBuildBars={async (request) => {
          const result = await actions.runOperation('Building 15-minute bars', () => apiClient.buildBars(request), (built) => `Bar build ${built.status}: created ${built.barsCreated}, updated ${built.barsUpdated}.`);
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

function ExceptionsPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void>; runOperation: <T>(label: string, work: () => Promise<T>, successMessage?: (result: T) => string | undefined) => Promise<T>; setSelected: (item: unknown) => void } }) {
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
    await actions.runOperation(`${label} exception case`, () => callback(reason ?? undefined), (result) => isGovernedActionResult(result) ? approvalMessage(result) : `${label} completed.`);
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
                  if (assignedTo?.trim()) void actions.runOperation('Assigning exception case', () => apiClient.assignExceptionCase(selected.id, assignedTo.trim()), () => 'Exception case assigned.').then(actions.refreshAll);
                }}>Assign</CommandButton>
                <CommandButton tone="warning" onClick={() => void runAction('Investigate', (reason) => apiClient.investigateExceptionCase(selected.id, reason))}>Investigating</CommandButton>
                <CommandButton tone="ok" onClick={() => void runAction('Resolve', (reason) => apiClient.resolveExceptionCase(selected.id, reason ?? ''), true)}>Resolve</CommandButton>
                <CommandButton tone="warning" onClick={() => void runAction('False Positive', (reason) => apiClient.falsePositiveExceptionCase(selected.id, reason ?? ''), true)}>False Positive</CommandButton>
                <CommandButton tone={['Blocking', 'Critical'].includes(selected.severity) ? 'danger' : 'warning'} onClick={() => void runAction('Waive', (reason) => apiClient.waiveExceptionCase(selected.id, reason ?? ''), true)}>Waive</CommandButton>
                <CommandButton onClick={() => void runAction('Reopen', (reason) => apiClient.reopenExceptionCase(selected.id, reason))}>Reopen</CommandButton>
                <CommandButton onClick={() => {
                  const note = window.prompt('Add note');
                  if (note?.trim()) void actions.runOperation('Adding exception note', () => apiClient.addExceptionCaseNote(selected.id, note.trim()), () => 'Exception note added.').then(() => loadDetail(selected));
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

function LmaxEodPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void>; runOperation: <T>(label: string, work: () => Promise<T>, successMessage?: (result: T) => string | undefined) => Promise<T> } }) {
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
          const result = await actions.runOperation('Generating fake LMAX EOD reports', () => apiClient.generateFakeLmaxEod(request), (generated) => `Generated ${generated.individualTradeCount} individual trades, ${generated.tradeSummaryCount} summaries, ${generated.currencyWalletCount} wallets.`);
          await actions.refreshAll();
          return result;
        }}
        onImportGenerated={async (request) => {
          const result = await actions.runOperation('Importing generated LMAX EOD reports', () => apiClient.importGeneratedLmaxEod(request), (imported) => `Import ${imported.status}: ${imported.rowCount} rows, ${imported.blockingIssueCount} blocking issues.`);
          await actions.refreshAll();
          return result;
        }}
        onRunReconciliation={async (request) => {
          const result = await actions.runOperation('Running EOD reconciliation', () => apiClient.runEodReconciliation(request), (run) => `EOD reconciliation: ${run.breakCount} breaks, ${run.blockingBreakCount} blocking.`);
          await actions.refreshAll();
          return result;
        }}
        onLoadPnl={async (reportDate, venueName, brokerAccountCode) => {
          const eodPnlSummary = await actions.runOperation('Loading EOD PnL summary', () => apiClient.getEodPnlSummary(reportDate, venueName, brokerAccountCode), (summary) => `Loaded EOD net PnL ${formatUsd(summary.totalNetPnlUsd)}.`);
          void eodPnlSummary;
          await actions.refreshAll();
        }}
      />
    </section>
  );
}

function LmaxShadowPage({ dashboard, actions }: { dashboard: DashboardState; actions: { refreshAll: () => Promise<void>; runOperation: <T>(label: string, work: () => Promise<T>, successMessage?: (result: T) => string | undefined) => Promise<T>; setSelected: (item: unknown) => void } }) {
  const [reason, setReason] = useState('Local shadow replay inspection');
  const [status, setStatus] = useState('');
  const [severity, setSeverity] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [replayFilter, setReplayFilter] = useState('');
  const [symbolFilter, setSymbolFilter] = useState('');
  const [fingerprintFilter, setFingerprintFilter] = useState('');
  const [textFilter, setTextFilter] = useState('');
  const openBlocking = dashboard.lmaxShadowObservations.filter((item) => item.status === 'Open' && item.severity === 'Blocking');
  const openWarnings = dashboard.lmaxShadowObservations.filter((item) => item.status === 'Open' && item.severity === 'Warning');
  const matches = dashboard.lmaxShadowObservations.filter((item) => item.severity === 'Info');
  const filtered = dashboard.lmaxShadowObservations.filter((item) => {
    const haystack = [
      item.type,
      item.status,
      item.severity,
      item.symbol,
      item.brokerExecutionId,
      item.brokerOrderId,
      item.clientOrderId,
      item.fingerprint,
      item.policyCode,
      item.evidenceMode,
      item.sourceEventType,
      item.rationale,
      item.suggestedOperatorAction,
      item.description
    ].filter(Boolean).join(' ').toLowerCase();
    return (!status || item.status === status)
      && (!severity || item.severity === severity)
      && (!typeFilter || item.type === typeFilter)
      && (!replayFilter || item.replayRunId?.toLowerCase().includes(replayFilter.toLowerCase()))
      && (!symbolFilter || item.symbol?.toLowerCase().includes(symbolFilter.toLowerCase()))
      && (!fingerprintFilter || item.fingerprint.toLowerCase().includes(fingerprintFilter.toLowerCase()))
      && (!textFilter || haystack.includes(textFilter.toLowerCase()));
  });
  const transition = async (label: string, work: () => Promise<LmaxShadowObservationDto>) => {
    await actions.runOperation(label, work, () => `${label} completed.`);
    await actions.refreshAll();
  };

  return (
    <section className="workspace-page">
      <SectionHeader
        title="LMAX Shadow"
        eyebrow="Replay normalized LMAX-like evidence without live connections or runtime mutation"
        actions={<StatusChip label="Replay only" tone="info" />}
      />
      <div className="critical-box">Shadow mode compares normalized evidence to internal state and writes observations only. It does not mutate orders, fills, positions, risk decisions, or reconciliation state.</div>
      <div className="operator-note">Replay is local API processing only. It does not call LMAX, open FIX sessions, submit orders, or use credentials.</div>
      <div className="panel">
        <SectionHeader
          title="Live Shadow Reader Skeleton"
          actions={<StatusChip label={formatStatus(dashboard.lmaxShadowReaderStatus?.status ?? 'Disabled')} tone={toneForStatus(dashboard.lmaxShadowReaderStatus?.status)} />}
        />
        <div className="operator-note">Disabled read-only skeleton. It opens no FIX sessions, uses no credentials, submits no orders, and writes nothing to trading tables.</div>
        <div className="critical-box">{dashboard.lmaxShadowReaderStatus?.blockedReason ?? 'Blocked by default until a future explicit quality gate.'}</div>
        <div className="metric-grid">
          <MetricCard label="Executed" value={dashboard.lmaxShadowReaderStatus?.executed ? 'Yes' : 'No'} sublabel={dashboard.lmaxShadowReaderStatus?.message ?? 'Reader disabled by default'} tone={dashboard.lmaxShadowReaderStatus?.executed ? 'warning' : 'ok'} />
          <MetricCard label="Connected" value={dashboard.lmaxShadowReaderStatus?.connected ? 'Yes' : 'No'} sublabel="No sockets are opened by this skeleton" tone={dashboard.lmaxShadowReaderStatus?.connected ? 'danger' : 'ok'} />
          <MetricCard label="Orders" value={dashboard.lmaxShadowReaderStatus?.ordersSubmitted ? 'Submitted' : 'None'} sublabel="Order submission is forbidden" tone={dashboard.lmaxShadowReaderStatus?.ordersSubmitted ? 'danger' : 'ok'} />
          <MetricCard label="Trading Writes" value={dashboard.lmaxShadowReaderStatus?.persistedToTradingTables ? 'Yes' : 'No'} sublabel="No order/fill/position mutation" tone={dashboard.lmaxShadowReaderStatus?.persistedToTradingTables ? 'danger' : 'ok'} />
        </div>
        <DataTable rows={dashboard.lmaxShadowReaderStatus?.safetyChecks ?? []} getRowKey={(row) => row.gate} columns={[
          { key: 'gate', header: 'Gate', render: (row) => row.gate },
          { key: 'status', header: 'Status', render: (row) => <StatusChip label={formatStatus(row.status)} tone={row.status === 'Failed' ? 'warning' : row.status === 'Warning' ? 'warning' : 'ok'} /> },
          { key: 'observedValue', header: 'Observed', render: (row) => row.observedValue },
          { key: 'expectedValue', header: 'Expected', render: (row) => row.expectedValue },
          { key: 'message', header: 'Message', render: (row) => row.message }
        ]} />
        <div className="form-grid compact">
          <ActionButton
            className="command-button"
            idleLabel="Check Reader Run"
            runningLabel="Checking..."
            disabled={!reason.trim()}
            onAction={async () => {
              const result = await actions.runOperation('Checking LMAX shadow reader', () => apiClient.runLmaxShadowReader(reason), (response) => response.message);
              await actions.refreshAll();
              actions.setSelected(result);
            }}
          />
        </div>
      </div>
      <div className="metric-grid">
        <MetricCard label="Observations" value={dashboard.lmaxShadowObservations.length} sublabel="Stored local shadow observations" tone="neutral" />
        <MetricCard label="Blocking" value={openBlocking.length} sublabel="Creates exception cases when produced" tone={openBlocking.length ? 'danger' : 'ok'} />
        <MetricCard label="Warnings" value={openWarnings.length} sublabel="Operator review items" tone={openWarnings.length ? 'warning' : 'ok'} />
        <MetricCard label="Matches" value={matches.length} sublabel="Info observations" tone="ok" />
        <MetricCard label="Replay Source" value={formatStatus(dashboard.lmaxShadowReplayRuns[0]?.inputSource ?? 'None')} sublabel={dashboard.lmaxShadowReplayRuns[0]?.id ? `Replay ${formatIdShort(dashboard.lmaxShadowReplayRuns[0].id)}` : 'No replay id'} tone="neutral" />
        <MetricCard label="Latest Replay" value={dashboard.lmaxShadowReplayRuns[0]?.status ?? 'Not run'} sublabel={dashboard.lmaxShadowReplayRuns[0]?.message ?? 'No replay history'} tone={toneForStatus(dashboard.lmaxShadowReplayRuns[0]?.status)} />
      </div>
      <div className="form-grid compact">
        <label>
          Reason
          <input value={reason} onChange={(event) => setReason(event.target.value)} />
        </label>
        <ActionButton
          className="command-button info"
          idleLabel="Run Empty Replay"
          runningLabel="Replaying..."
          disabled={!reason.trim()}
          onAction={async () => {
            await actions.runOperation('Running LMAX shadow replay', () => apiClient.runLmaxShadowReplay({ inputSource: 'SyntheticFixture', reason, executionReports: [], tradeCaptureReports: [], orderStatuses: [], protocolRejects: [] }), () => 'Shadow replay completed.');
            await actions.refreshAll();
          }}
        />
      </div>
      <div className="page-grid two">
        <div className="panel">
          <SectionHeader title="Replay Runs" />
          <DataTable rows={dashboard.lmaxShadowReplayRuns} getRowKey={(row) => row.id} onRowClick={actions.setSelected} columns={[
            { key: 'startedAtUtc', header: 'Started', render: (row) => formatUtc(row.startedAtUtc), sortValue: (row) => row.startedAtUtc },
            { key: 'id', header: 'Replay ID', render: (row) => formatIdShort(row.id), sortValue: (row) => row.id },
            { key: 'inputSource', header: 'Source', render: (row) => formatStatus(row.inputSource), sortValue: (row) => row.inputSource },
            { key: 'status', header: 'Status', render: (row) => <StatusChip label={formatStatus(row.status)} tone={toneForStatus(row.status)} />, sortValue: (row) => row.status },
            { key: 'inputEventCount', header: 'Input', render: (row) => String(row.inputEventCount), sortValue: (row) => row.inputEventCount, className: 'numeric' },
            { key: 'uniqueEventCount', header: 'Unique', render: (row) => String(row.uniqueEventCount), sortValue: (row) => row.uniqueEventCount, className: 'numeric' },
            { key: 'duplicateEventCount', header: 'Dupes', render: (row) => String(row.duplicateEventCount), sortValue: (row) => row.duplicateEventCount, className: 'numeric' },
            { key: 'observationCount', header: 'Obs', render: (row) => String(row.observationCount), sortValue: (row) => row.observationCount, className: 'numeric' },
            { key: 'blockingObservationCount', header: 'Blocking', render: (row) => String(row.blockingObservationCount), sortValue: (row) => row.blockingObservationCount, className: 'numeric' },
            { key: 'warningObservationCount', header: 'Warnings', render: (row) => String(row.warningObservationCount), sortValue: (row) => row.warningObservationCount, className: 'numeric' },
            { key: 'message', header: 'Message', render: (row) => row.message ?? '-' }
          ]} />
        </div>
        <div className="panel">
          <SectionHeader title="Observation Controls" />
          <div className="form-grid compact">
            <label>
              Status filter
              <select value={status} onChange={(event) => setStatus(event.target.value)}>
                <option value="">All statuses</option>
                <option value="Open">Open</option>
                <option value="Acknowledged">Acknowledged</option>
                <option value="Resolved">Resolved</option>
                <option value="Ignored">Ignored</option>
              </select>
            </label>
            <label>
              Severity
              <select value={severity} onChange={(event) => setSeverity(event.target.value)}>
                <option value="">All severities</option>
                <option value="Info">Info</option>
                <option value="Warning">Warning</option>
                <option value="Blocking">Blocking</option>
              </select>
            </label>
            <label>
              Type
              <input value={typeFilter} onChange={(event) => setTypeFilter(event.target.value)} placeholder="Exact observation type" />
            </label>
            <label>
              Replay ID
              <input value={replayFilter} onChange={(event) => setReplayFilter(event.target.value)} placeholder="Replay id" />
            </label>
            <label>
              Symbol
              <input value={symbolFilter} onChange={(event) => setSymbolFilter(event.target.value)} placeholder="EURUSD" />
            </label>
            <label>
              Fingerprint
              <input value={fingerprintFilter} onChange={(event) => setFingerprintFilter(event.target.value)} placeholder="Fingerprint" />
            </label>
            <label>
              Search
              <input value={textFilter} onChange={(event) => setTextFilter(event.target.value)} placeholder="Exec id, order id, description" />
            </label>
          </div>
          <DataTable rows={filtered} getRowKey={(row) => row.id} onRowClick={actions.setSelected} columns={[
            { key: 'createdAtUtc', header: 'Created', render: (row) => formatUtc(row.createdAtUtc), sortValue: (row) => row.createdAtUtc },
            { key: 'severity', header: 'Severity', render: (row) => <SeverityBadge value={row.severity} />, sortValue: (row) => row.severity },
            { key: 'type', header: 'Type', render: (row) => formatStatus(row.type), sortValue: (row) => row.type },
            { key: 'policyCode', header: 'Policy', render: (row) => row.policyCode ? formatStatus(row.policyCode) : '-', sortValue: (row) => row.policyCode ?? '' },
            { key: 'evidenceMode', header: 'Mode', render: (row) => row.evidenceMode ? formatStatus(row.evidenceMode) : '-', sortValue: (row) => row.evidenceMode ?? '' },
            { key: 'status', header: 'Status', render: (row) => <StatusChip label={formatStatus(row.status)} tone={toneForStatus(row.status)} />, sortValue: (row) => row.status },
            { key: 'symbol', header: 'Symbol', render: (row) => row.symbol ?? '-' },
            { key: 'brokerExecutionId', header: 'Broker Exec', render: (row) => row.brokerExecutionId ?? '-' },
            { key: 'clientOrderId', header: 'Client Order', render: (row) => row.clientOrderId ?? '-' },
            { key: 'fingerprint', header: 'Fingerprint', render: (row) => formatIdShort(row.fingerprint), sortValue: (row) => row.fingerprint },
            { key: 'replayRunId', header: 'Replay', render: (row) => row.replayRunId ? formatIdShort(row.replayRunId) : '-' },
            { key: 'description', header: 'Description', render: (row) => row.description },
            { key: 'suggestedOperatorAction', header: 'Suggested Action', render: (row) => row.suggestedOperatorAction ?? '-' },
            { key: 'actions', header: 'Actions', render: (row) => (
              <div className="row-actions">
                <ActionButton className="command-button" idleLabel="Ack" runningLabel="Ack..." disabled={!reason.trim() || row.status !== 'Open'} onClick={(event) => event.stopPropagation()} onAction={() => transition('Acknowledging shadow observation', () => apiClient.acknowledgeLmaxShadowObservation(row.id, reason))} />
                <ActionButton className="command-button ok" idleLabel="Resolve" runningLabel="Resolving..." disabled={!reason.trim() || row.status === 'Resolved'} onClick={(event) => event.stopPropagation()} onAction={() => transition('Resolving shadow observation', () => apiClient.resolveLmaxShadowObservation(row.id, reason))} />
                <ActionButton className="command-button" idleLabel="Ignore" runningLabel="Ignoring..." disabled={!reason.trim() || row.status === 'Ignored'} onClick={(event) => event.stopPropagation()} onAction={() => transition('Ignoring shadow observation', () => apiClient.ignoreLmaxShadowObservation(row.id, reason))} />
              </div>
            ) }
          ]} />
        </div>
      </div>
    </section>
  );
}

function RiskAdminPage({ dashboard, health, integrity, actions }: { dashboard: DashboardState; health?: HealthDto; integrity?: ReferenceDataIntegrityDto; actions: { refreshAll: () => Promise<void>; runOperation: <T>(label: string, work: () => Promise<T>, successMessage?: (result: T) => string | undefined) => Promise<T> } }) {
  const [selectedSetId, setSelectedSetId] = useState<string | undefined>(dashboard.activeRiskLimitSet?.id);
  const selectedSet = dashboard.riskLimitSets.find((item) => item.id === selectedSetId) ?? dashboard.activeRiskLimitSet ?? dashboard.riskLimitSets[0];
  const riskLimits = selectedSet?.id === dashboard.activeRiskLimitSet?.id ? dashboard.riskLimits : [];
  const instrumentLimits = selectedSet?.id === dashboard.activeRiskLimitSet?.id ? dashboard.instrumentRiskLimits : [];
  const venueLimits = selectedSet?.id === dashboard.activeRiskLimitSet?.id ? dashboard.venueRiskLimits : [];
  const blockedRiskDecisions = dashboard.riskDecisions.filter((item) => ['Rejected', 'Blocked'].includes(item.status));

  const requireReason = (label: string) => {
    const reason = window.prompt(`${label} reason`);
    return reason?.trim() ? reason.trim() : undefined;
  };

  const cloneSet = async (set: RiskLimitSetDto) => {
    const reason = requireReason('Clone risk limit set');
    if (!reason) return;
    const cloned = await actions.runOperation('Cloning risk set', () => apiClient.cloneRiskLimitSet(set.id, reason), (draft) => `Cloned draft risk set ${draft.name} v${draft.version}.`);
    setSelectedSetId(cloned.id);
    await actions.refreshAll();
  };

  const activateSet = async (set: RiskLimitSetDto) => {
    const reason = requireReason('Activate draft risk limit set');
    if (!reason || !window.confirm(`Activate ${set.name} v${set.version} and retire the prior active profile?`)) return;
    const activated = await actions.runOperation('Activating risk set', () => apiClient.activateRiskLimitSet(set.id, reason), (active) => isGovernedActionResult(active) ? approvalMessage(active) : `Activated risk set ${active.name} v${active.version}.`);
    if (!isGovernedActionResult(activated)) setSelectedSetId(activated.id);
    await actions.refreshAll();
  };

  const retireSet = async (set: RiskLimitSetDto) => {
    const reason = requireReason('Retire risk limit set');
    if (!reason || !window.confirm(`Retire ${set.name} v${set.version}?`)) return;
    await actions.runOperation('Retiring risk set', () => apiClient.retireRiskLimitSet(set.id, reason), (retired) => isGovernedActionResult(retired) ? approvalMessage(retired) : `Retired risk set ${retired.name} v${retired.version}.`);
    await actions.refreshAll();
  };

  const updateInstrumentControl = async (row: RiskInstrumentDto, field: 'isTradingEnabled' | 'isReportImportEnabled' | 'isMarketDataEnabled') => {
    const reason = requireReason(`Update ${row.instrument.symbol} ${field}`);
    if (!reason) return;
    await actions.runOperation('Updating instrument controls', () => apiClient.updateRiskInstrumentControls(row.instrument.id, { [field]: !row.instrument[field], reason }), () => `Updated ${row.instrument.symbol} controls.`);
    await actions.refreshAll();
  };

  const updateVenueControl = async (row: RiskVenueDto, field: 'isTradingEnabled' | 'isReportImportEnabled' | 'isMarketDataEnabled') => {
    const reason = requireReason(`Update ${row.venue.name} ${field}`);
    if (!reason) return;
    await actions.runOperation('Updating venue controls', () => apiClient.updateRiskVenueControls(row.venue.id, { [field]: !row.venue[field], reason }), () => `Updated ${row.venue.name} controls.`);
    await actions.refreshAll();
  };

  return (
    <section className="workspace-page">
      <SectionHeader title="Risk Control Center" eyebrow="Versioned risk profile, trading windows, controls, decisions" />
      <div className="metric-grid">
        <MetricCard label="Active Risk Profile" value={dashboard.activeRiskLimitSet ? `v${dashboard.activeRiskLimitSet.version}` : 'Missing'} sublabel={dashboard.activeRiskLimitSet?.name ?? 'No active set loaded'} tone={dashboard.activeRiskLimitSet ? 'ok' : 'danger'} />
        <MetricCard label="Risk Sets" value={dashboard.riskLimitSets.length} sublabel={`${dashboard.riskLimitSets.filter((item) => item.status === 'Draft').length} draft`} tone="neutral" />
        <MetricCard label="Trading Windows" value={dashboard.tradingWindows.filter((item) => item.isActive).length} sublabel="Active schedule rows" tone={dashboard.tradingWindows.some((item) => item.isActive) ? 'info' : 'warning'} />
        <MetricCard label="Kill Switch" value={dashboard.killSwitch?.isActive ? 'Active' : 'Clear'} sublabel={dashboard.killSwitch?.reason ?? 'Local safety control'} tone={dashboard.killSwitch?.isActive ? 'danger' : 'ok'} />
        <MetricCard label="Blocked Risk Decisions" value={blockedRiskDecisions.length} sublabel={blockedRiskDecisions[0]?.rejectReason ? formatStatus(blockedRiskDecisions[0].rejectReason) : 'Recent decisions'} tone={blockedRiskDecisions.length ? 'warning' : 'ok'} />
      </div>
      <div className="page-grid two">
        <SafetyPanel health={health} />
        <ReferenceDataPanel integrity={integrity} />
      </div>
      <div className="panel wide">
        <SectionHeader title="Active Risk Profile" eyebrow="Active set selection controls simulated/FakeLmax processing only." actions={dashboard.activeRiskLimitSet && <StatusChip label={dashboard.activeRiskLimitSet.status} tone={toneForStatus(dashboard.activeRiskLimitSet.status)} />} />
        <div className="detail-grid">
          <div>
            <p><strong>Name:</strong> {dashboard.activeRiskLimitSet?.name ?? '-'}</p>
            <p><strong>Fund:</strong> {formatIdShort(dashboard.activeRiskLimitSet?.fundId)} <strong>Model:</strong> {dashboard.activeRiskLimitSet?.modelName ?? '-'}</p>
            <p><strong>Version:</strong> {dashboard.activeRiskLimitSet?.version ?? '-'} <strong>Activated:</strong> {formatUtc(dashboard.activeRiskLimitSet?.activatedAtUtc)}</p>
            <p><strong>Description:</strong> {dashboard.activeRiskLimitSet?.description ?? '-'}</p>
          </div>
          <div>
            <p><strong>Max Gross Exposure:</strong> {formatUsd(dashboard.activeRiskLimitSet?.maxGrossExposureUsd)}</p>
            <p><strong>Model Staleness:</strong> {dashboard.activeRiskLimitSet?.maxModelRunAgeSeconds ?? '-'} seconds</p>
            <p><strong>Market Data Staleness:</strong> {dashboard.activeRiskLimitSet?.maxMarketDataAgeSeconds ?? '-'} seconds</p>
            <p><strong>Position Tolerance:</strong> {formatQuantity(dashboard.activeRiskLimitSet?.positionToleranceBaseQuantity)}</p>
          </div>
        </div>
      </div>
      <div className="panel wide">
        <SectionHeader title="Risk Limit Sets" eyebrow="Draft / active / retired lifecycle; activation and retirement require a reason." />
        <DataTable rows={dashboard.riskLimitSets} getRowKey={(row) => row.id} onRowClick={(row) => setSelectedSetId(row.id)} emptyLabel="No risk limit sets loaded" columns={[
          { key: 'name', header: 'Name', render: (row) => row.name, sortValue: (row) => row.name },
          { key: 'version', header: 'Version', render: (row) => `v${row.version}`, sortValue: (row) => row.version },
          { key: 'status', header: 'Status', render: (row) => <StatusChip label={row.status} tone={toneForStatus(row.status)} />, sortValue: (row) => row.status },
          { key: 'active', header: 'Active', render: (row) => row.isActive ? <StatusChip label="Active" tone="ok" /> : <StatusChip label="Inactive" tone="neutral" />, sortValue: (row) => row.isActive },
          { key: 'model', header: 'Model', render: (row) => row.modelName ?? '-' },
          { key: 'activated', header: 'Activated', render: (row) => formatUtc(row.activatedAtUtc), sortValue: (row) => row.activatedAtUtc ?? '' },
          { key: 'actions', header: 'Actions', render: (row) => (
            <div className="button-row compact">
              <ActionButton idleLabel="Clone" runningLabel="Cloning..." onClick={(event) => event.stopPropagation()} onAction={() => cloneSet(row)} />
              <ActionButton className="command-button warning" idleLabel="Activate" runningLabel="Activating..." disabled={row.status !== 'Draft'} onClick={(event) => event.stopPropagation()} onAction={() => activateSet(row)} />
              <ActionButton className="command-button danger" idleLabel="Retire" runningLabel="Retiring..." disabled={row.status === 'Retired' || row.status === 'Archived'} onClick={(event) => event.stopPropagation()} onAction={() => retireSet(row)} />
            </div>
          ) }
        ]} />
      </div>
      <div className="info-box">Active and archived risk sets are read-only from the operator cockpit. Clone an active set to a draft before changing limits, then activate the draft with a reason. No endpoint here can enable live trading or external connections.</div>
      <div className="page-grid two">
        <div className="panel wide">
          <SectionHeader title="Global Limits" eyebrow={selectedSet ? `${selectedSet.name} v${selectedSet.version}` : 'Select a risk set'} />
          <DataTable rows={riskLimits} getRowKey={(row) => row.id} emptyLabel={selectedSet?.id === dashboard.activeRiskLimitSet?.id ? 'No global limits' : 'Limit rows are loaded for the active set only'} columns={[
            { key: 'name', header: 'Limit', render: (row) => formatStatus(row.name), sortValue: (row) => row.name },
            { key: 'scope', header: 'Scope', render: (row) => row.scope },
            { key: 'value', header: 'Value', render: (row) => formatQuantity(row.value), sortValue: (row) => row.value, className: 'numeric' },
            { key: 'unit', header: 'Unit', render: (row) => row.unit },
            { key: 'enabled', header: 'Enabled', render: (row) => <StatusChip label={row.isEnabled ? 'Enabled' : 'Disabled'} tone={row.isEnabled ? 'ok' : 'warning'} /> }
          ]} />
        </div>
        <div className="panel wide">
          <SectionHeader title="Trading Windows" eyebrow="No-new-orders and cutoff visibility" />
          <DataTable rows={dashboard.tradingWindows} getRowKey={(row) => row.id} emptyLabel="No trading windows loaded" columns={[
            { key: 'day', header: 'Day', render: (row) => row.dayOfWeek, sortValue: (row) => row.dayOfWeek },
            { key: 'schedule', header: 'Schedule', render: (row) => row.scheduleName },
            { key: 'tz', header: 'Time Zone', render: (row) => row.timeZoneId },
            { key: 'open', header: 'Open', render: (row) => row.openTime },
            { key: 'close', header: 'Close', render: (row) => row.closeTime },
            { key: 'cutoff', header: 'No New Orders', render: (row) => row.noNewOrdersAfter },
            { key: 'enabled', header: 'Enabled', render: (row) => <StatusChip label={row.tradingEnabled ? 'Enabled' : 'Disabled'} tone={row.tradingEnabled ? 'ok' : 'warning'} /> }
          ]} />
        </div>
      </div>
      <div className="page-grid two">
        <div className="panel wide">
          <SectionHeader title="Instrument Limits" eyebrow="Trading-enabled is separate from report-import-enabled." />
          <DataTable rows={instrumentLimits} getRowKey={(row) => row.id} emptyLabel="Instrument limits are loaded for the active set only" columns={[
            { key: 'symbol', header: 'Symbol', render: (row) => row.symbol ?? formatIdShort(row.instrumentId), sortValue: (row) => row.symbol ?? row.instrumentId },
            { key: 'trade', header: 'Max Trade USD', render: (row) => formatUsd(row.maxTradeNotionalUsd), sortValue: (row) => row.maxTradeNotionalUsd, className: 'numeric' },
            { key: 'position', header: 'Max Position USD', render: (row) => formatUsd(row.maxPositionUsd), sortValue: (row) => row.maxPositionUsd, className: 'numeric' },
            { key: 'minQty', header: 'Min Qty', render: (row) => formatQuantity(row.minTradeQuantity), sortValue: (row) => row.minTradeQuantity, className: 'numeric' },
            { key: 'enabled', header: 'Trading', render: (row) => <StatusChip label={row.isTradingEnabled ? 'Enabled' : 'Disabled'} tone={row.isTradingEnabled ? 'ok' : 'warning'} /> }
          ]} />
        </div>
        <div className="panel wide">
          <SectionHeader title="Venue Limits" eyebrow="Venue controls affect future local/FakeLmax processing only." />
          <DataTable rows={venueLimits} getRowKey={(row) => row.id} emptyLabel="Venue limits are loaded for the active set only" columns={[
            { key: 'venue', header: 'Venue', render: (row) => row.venueName ?? formatIdShort(row.venueId), sortValue: (row) => row.venueName ?? row.venueId },
            { key: 'trade', header: 'Max Trade USD', render: (row) => formatUsd(row.maxTradeNotionalUsd), sortValue: (row) => row.maxTradeNotionalUsd, className: 'numeric' },
            { key: 'turnover', header: 'Daily Turnover USD', render: (row) => formatUsd(row.maxDailyTurnoverUsd), sortValue: (row) => row.maxDailyTurnoverUsd, className: 'numeric' },
            { key: 'rate', header: 'Orders / Min', render: (row) => row.maxOrdersPerMinute, sortValue: (row) => row.maxOrdersPerMinute, className: 'numeric' },
            { key: 'enabled', header: 'Venue', render: (row) => <StatusChip label={row.isVenueEnabled ? 'Enabled' : 'Disabled'} tone={row.isVenueEnabled ? 'ok' : 'warning'} /> }
          ]} />
        </div>
      </div>
      <div className="page-grid two">
        <div className="panel wide">
          <SectionHeader title="Instrument Controls" eyebrow="Known report aliases may import even when trading is disabled." />
          <DataTable rows={dashboard.riskInstruments} getRowKey={(row) => row.instrument.id} emptyLabel="No instruments loaded" columns={[
            { key: 'symbol', header: 'Symbol', render: (row) => row.instrument.symbol, sortValue: (row) => row.instrument.symbol },
            { key: 'trading', header: 'Trading', render: (row) => <button className="link-button" onClick={(event) => { event.stopPropagation(); void updateInstrumentControl(row, 'isTradingEnabled'); }}>{row.instrument.isTradingEnabled ? 'Enabled' : 'Disabled'}</button> },
            { key: 'report', header: 'Report Import', render: (row) => <button className="link-button" onClick={(event) => { event.stopPropagation(); void updateInstrumentControl(row, 'isReportImportEnabled'); }}>{row.instrument.isReportImportEnabled ? 'Enabled' : 'Disabled'}</button> },
            { key: 'market', header: 'Market Data', render: (row) => <button className="link-button" onClick={(event) => { event.stopPropagation(); void updateInstrumentControl(row, 'isMarketDataEnabled'); }}>{row.instrument.isMarketDataEnabled ? 'Enabled' : 'Disabled'}</button> },
            { key: 'aliases', header: 'Aliases', render: (row) => row.aliases.map((alias) => alias.externalSymbol).join(', ') || '-' }
          ]} />
        </div>
        <div className="panel wide">
          <SectionHeader title="Venue Controls" eyebrow="No real LMAX runtime controls are exposed." />
          <DataTable rows={dashboard.riskVenues} getRowKey={(row) => row.venue.id} emptyLabel="No venues loaded" columns={[
            { key: 'venue', header: 'Venue', render: (row) => row.venue.name, sortValue: (row) => row.venue.name },
            { key: 'trading', header: 'Trading', render: (row) => <button className="link-button" onClick={(event) => { event.stopPropagation(); void updateVenueControl(row, 'isTradingEnabled'); }}>{row.venue.isTradingEnabled ? 'Enabled' : 'Disabled'}</button> },
            { key: 'report', header: 'Report Import', render: (row) => <button className="link-button" onClick={(event) => { event.stopPropagation(); void updateVenueControl(row, 'isReportImportEnabled'); }}>{row.venue.isReportImportEnabled ? 'Enabled' : 'Disabled'}</button> },
            { key: 'market', header: 'Market Data', render: (row) => <button className="link-button" onClick={(event) => { event.stopPropagation(); void updateVenueControl(row, 'isMarketDataEnabled'); }}>{row.venue.isMarketDataEnabled ? 'Enabled' : 'Disabled'}</button> }
          ]} />
        </div>
      </div>
      <div className="panel wide">
        <SectionHeader title="Recent Risk Decisions" eyebrow="Observed values, limits, and check-level explanations" />
        <DataTable rows={dashboard.riskDecisions.slice(0, 25)} getRowKey={(row) => row.id} emptyLabel="No risk decisions loaded" columns={[
          { key: 'created', header: 'Created', render: (row) => formatUtc(row.createdAtUtc), sortValue: (row) => row.createdAtUtc },
          { key: 'status', header: 'Status', render: (row) => <StatusChip label={formatStatus(row.status)} tone={toneForStatus(row.status)} />, sortValue: (row) => row.status },
          { key: 'reason', header: 'Reason', render: (row) => row.rejectReason ? formatStatus(row.rejectReason) : '-' },
          { key: 'symbol', header: 'Symbol', render: (row) => row.symbol ?? formatIdShort(row.instrumentId) },
          { key: 'riskSet', header: 'Risk Set', render: (row) => row.riskLimitSetName ? `${row.riskLimitSetName} v${row.riskLimitSetVersion ?? '-'}` : formatIdShort(row.riskLimitSetId) },
          { key: 'check', header: 'Key Check', render: (row) => {
            const failed = row.details?.find((item) => ['Failed', 'Blocked'].includes(item.status));
            const detail = failed ?? row.details?.[0];
            return detail ? `${formatStatus(detail.checkName)}: ${detail.message}` : row.message;
          } }
        ]} />
      </div>
      <AdminPanel
        killSwitch={dashboard.killSwitch}
        instruments={dashboard.instruments}
        venues={dashboard.venues}
        onActivateKillSwitch={async (reason) => {
          await actions.runOperation('Activating kill switch', () => apiClient.activateKillSwitch(reason), () => 'Kill switch activated.');
          await actions.refreshAll();
        }}
        onClearKillSwitch={async (reason) => {
          await actions.runOperation('Clearing kill switch', () => apiClient.clearKillSwitch(reason), (result) => isGovernedActionResult(result) ? approvalMessage(result) : 'Kill switch cleared.');
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

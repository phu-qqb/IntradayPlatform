import { useCallback, useEffect, useState } from 'react';
import { apiClient } from './api/apiClient';
import type {
  DriftSnapshotDto,
  FillDto,
  HealthDto,
  InstrumentDto,
  KillSwitchDto,
  MarketDataBarDto,
  MarketDataSnapshotDto,
  ModelRunDto,
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
import { DriftPanel } from './components/DriftPanel';
import { ErrorState } from './components/ErrorState';
import { FillsPanel } from './components/FillsPanel';
import { HealthPanel } from './components/HealthPanel';
import { LoadingState } from './components/LoadingState';
import { MarketDataPanel } from './components/MarketDataPanel';
import { ModelRunsPanel } from './components/ModelRunsPanel';
import { OrdersPanel } from './components/OrdersPanel';
import { PositionsPanel } from './components/PositionsPanel';
import { ReconciliationPanel } from './components/ReconciliationPanel';
import { ReferenceDataPanel } from './components/ReferenceDataPanel';
import { RiskPanel } from './components/RiskPanel';
import { SafetyPanel } from './components/SafetyPanel';
import { StatusBanner } from './components/StatusBanner';

type DashboardState = {
  modelRuns: ModelRunDto[];
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
};

const emptyDashboard: DashboardState = {
  modelRuns: [],
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
  venues: []
};

export default function App() {
  const [health, setHealth] = useState<HealthDto>();
  const [integrity, setIntegrity] = useState<ReferenceDataIntegrityDto>();
  const [dashboard, setDashboard] = useState<DashboardState>(emptyDashboard);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  const loadHealth = useCallback(async () => {
    setHealth(await apiClient.getHealth());
  }, []);

  const loadIntegrity = useCallback(async () => {
    setIntegrity(await apiClient.getReferenceDataIntegrity());
  }, []);

  const loadDashboard = useCallback(async () => {
    const [
      modelRuns,
      targets,
      drifts,
      internalPositions,
      brokerPositions,
      reconciliationBreaks,
      tradeIntents,
      riskDecisions,
      orders,
      fills,
      snapshots,
      bars,
      killSwitch,
      instruments,
      venues
    ] = await Promise.all([
      apiClient.getModelRuns(),
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
      apiClient.getVenues()
    ]);

    setDashboard({ modelRuns, targets, drifts, internalPositions, brokerPositions, reconciliationBreaks, tradeIntents, riskDecisions, orders, fills, snapshots, bars, killSwitch, instruments, venues });
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

  return (
    <div className="app-shell">
      <StatusBanner health={health} integrity={integrity} onRefresh={refreshAll} />
      {loading && <LoadingState label="Loading cockpit data" />}
      {error && <ErrorState message={error} />}
      <main className="dashboard-grid">
        <HealthPanel health={health} />
        <SafetyPanel health={health} />
        <ReferenceDataPanel integrity={integrity} />
        <MarketDataPanel
          snapshots={dashboard.snapshots}
          bars={dashboard.bars}
          onCreateFakeSnapshots={async (request) => {
            const result = await apiClient.createFakeSnapshots(request);
            await refreshAll();
            return `Created ${result.created} local fake snapshots.`;
          }}
          onBuildBars={async (request) => {
            const result = await apiClient.buildBars(request);
            await refreshAll();
            return `Bar build ${result.status}: created ${result.barsCreated}, updated ${result.barsUpdated}.`;
          }}
        />
        <ModelRunsPanel
          modelRuns={dashboard.modelRuns}
          onCreate={async (request) => {
            await apiClient.createModelRun(request);
            await refreshAll();
          }}
          onProcess={async (id) => {
            const result = await apiClient.processModelRun(id);
            await refreshAll();
            return result;
          }}
        />
        <PositionsPanel internalPositions={dashboard.internalPositions} brokerPositions={dashboard.brokerPositions} />
        <DriftPanel targets={dashboard.targets} drifts={dashboard.drifts} />
        <RiskPanel tradeIntents={dashboard.tradeIntents} riskDecisions={dashboard.riskDecisions} />
        <OrdersPanel orders={dashboard.orders} />
        <FillsPanel fills={dashboard.fills} />
        <ReconciliationPanel breaks={dashboard.reconciliationBreaks} />
        <AdminPanel
          killSwitch={dashboard.killSwitch}
          instruments={dashboard.instruments}
          venues={dashboard.venues}
          onActivateKillSwitch={async (reason) => {
            await apiClient.activateKillSwitch(reason);
            await refreshAll();
          }}
          onClearKillSwitch={async () => {
            await apiClient.clearKillSwitch();
            await refreshAll();
          }}
        />
      </main>
    </div>
  );
}

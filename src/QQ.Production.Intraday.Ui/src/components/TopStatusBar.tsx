import { RefreshCw } from 'lucide-react';
import type { HealthDto, ReferenceDataIntegrityDto } from '../api/types';
import { formatUtc } from '../utils/format';
import { CommandButton, StatusChip } from './primitives';

function ok(ok: boolean) {
  return ok ? 'ok' : 'danger';
}

export function TopStatusBar({ health, integrity, onRefresh }: { health?: HealthDto; integrity?: ReferenceDataIntegrityDto; onRefresh: () => void }) {
  const critical =
    !health ||
    health.executionGateway !== 'FakeLmaxGateway' ||
    health.liveTradingEnabled ||
    health.externalConnectionsEnabled ||
    !health.databaseReachable ||
    health.pendingMigrationsCount > 0 ||
    (integrity?.blockingIssueCount ?? 0) > 0;

  return (
    <header className={`top-status-bar ${critical ? 'critical' : 'safe'}`}>
      <div className="brand-block">
        <h1>QQ Production Intraday</h1>
        <p>{critical ? 'Critical local safety condition requires attention' : 'Local simulator boundary is intact'}</p>
      </div>
      <div className="top-status-grid">
        <StatusChip tone="neutral" label={`Env: ${health?.environment ?? 'Unknown'}`} />
        <StatusChip tone="neutral" label={`Persistence: ${health?.persistenceProvider ?? 'Unknown'}`} />
        <StatusChip tone={ok(health?.databaseReachable === true)} label={`DB: ${health?.databaseReachable ? 'reachable' : 'unreachable'}`} />
        <StatusChip tone={ok((health?.pendingMigrationsCount ?? 1) === 0)} label={`Migrations: ${health?.pendingMigrationsCount ?? '?'}`} />
        <StatusChip tone={ok(health?.executionGateway === 'FakeLmaxGateway')} label={`Execution: ${health?.executionGateway ?? 'Unknown'}`} />
        <StatusChip tone={ok(health?.marketDataMode === 'FakeMarketDataProvider')} label={`Market data: ${health?.marketDataMode ?? 'Unknown'}`} />
        <StatusChip tone={ok(health?.liveTradingEnabled === false)} label={`Live trading: ${String(health?.liveTradingEnabled ?? 'unknown')}`} />
        <StatusChip tone={ok(health?.externalConnectionsEnabled === false)} label={`External connections: ${String(health?.externalConnectionsEnabled ?? 'unknown')}`} />
        <StatusChip tone={ok((integrity?.blockingIssueCount ?? 1) === 0)} label={`Reference integrity: ${integrity ? `${integrity.blockingIssueCount} blocking` : 'unknown'}`} />
        <StatusChip tone="info" label={`UTC: ${formatUtc(health?.utcServerTime)}`} />
      </div>
      <CommandButton tone="info" onClick={onRefresh} title="Refresh all cockpit data">
        <RefreshCw size={15} /> Refresh
      </CommandButton>
    </header>
  );
}

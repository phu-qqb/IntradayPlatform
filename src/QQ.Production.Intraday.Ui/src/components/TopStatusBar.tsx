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
  const safeLocal = !critical && health?.executionGateway === 'FakeLmaxGateway' && health.liveTradingEnabled === false && health.externalConnectionsEnabled === false;

  const groups = [
    {
      label: 'Runtime',
      chips: [
        <StatusChip key="env" tone="neutral" label={`Env: ${health?.environment ?? 'Unknown'}`} />,
        <StatusChip key="persistence" tone="neutral" label={`Persistence: ${health?.persistenceProvider ?? 'Unknown'}`} />,
        <StatusChip key="utc" tone="info" label={`UTC: ${formatUtc(health?.utcServerTime)}`} />
      ]
    },
    {
      label: 'Safety',
      chips: [
        <StatusChip key="execution" tone={ok(health?.executionGateway === 'FakeLmaxGateway')} label={`Execution: ${health?.executionGateway ?? 'Unknown'}`} />,
        <StatusChip key="live" tone={ok(health?.liveTradingEnabled === false)} label={`Live trading: ${String(health?.liveTradingEnabled ?? 'unknown')}`} />,
        <StatusChip key="external" tone={ok(health?.externalConnectionsEnabled === false)} label={`External: ${String(health?.externalConnectionsEnabled ?? 'unknown')}`} />
      ]
    },
    {
      label: 'Data',
      chips: [
        <StatusChip key="db" tone={ok(health?.databaseReachable === true)} label={`DB: ${health?.databaseReachable ? 'reachable' : 'unreachable'}`} />,
        <StatusChip key="migrations" tone={ok((health?.pendingMigrationsCount ?? 1) === 0)} label={`Migrations: ${health?.pendingMigrationsCount ?? '?'}`} />,
        <StatusChip key="market" tone={ok(health?.marketDataMode === 'FakeMarketDataProvider')} label={`Market data: ${health?.marketDataMode ?? 'Unknown'}`} />
      ]
    },
    {
      label: 'Reference',
      chips: [
        <StatusChip key="reference" tone={ok((integrity?.blockingIssueCount ?? 1) === 0)} label={`Integrity: ${integrity ? `${integrity.blockingIssueCount} blocking` : 'unknown'}`} />
      ]
    }
  ];

  return (
    <header className={`top-status-bar ${critical ? 'critical' : 'safe'}`}>
      <div className="brand-block">
        {safeLocal && <span className="safe-local-badge">SAFE LOCAL</span>}
        <h1>QQ Production Intraday</h1>
        <p>{critical ? 'Critical local safety condition requires attention' : 'Local simulator boundary is intact'}</p>
      </div>
      <div className="top-status-groups">
        {groups.map((group) => (
          <div className="status-group" key={group.label}>
            <span className="status-group-title">{group.label}</span>
            <div className="status-group-chips">{group.chips}</div>
          </div>
        ))}
      </div>
      <CommandButton tone="info" onClick={onRefresh} title="Refresh all cockpit data">
        <RefreshCw size={15} /> Refresh
      </CommandButton>
    </header>
  );
}

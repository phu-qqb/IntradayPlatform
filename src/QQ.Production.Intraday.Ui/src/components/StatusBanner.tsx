import type { HealthDto, ReferenceDataIntegrityDto } from '../api/types';

function statusClass(ok: boolean) {
  return ok ? 'chip ok' : 'chip danger';
}

export function StatusBanner({ health, integrity, onRefresh }: { health?: HealthDto; integrity?: ReferenceDataIntegrityDto; onRefresh: () => void }) {
  const critical =
    !health ||
    health.executionGateway !== 'FakeLmaxGateway' ||
    health.liveTradingEnabled ||
    health.externalConnectionsEnabled ||
    !health.databaseReachable ||
    health.pendingMigrationsCount > 0 ||
    (integrity?.blockingIssueCount ?? 0) > 0;

  return (
    <header className={`status-banner ${critical ? 'critical' : 'safe'}`}>
      <div>
        <h1>QQ Production Intraday</h1>
        <p>{critical ? 'Critical local safety condition requires attention' : 'Local simulator boundary is intact'}</p>
      </div>
      <div className="banner-grid">
        <span className="chip neutral">{health?.environment ?? 'Unknown'}</span>
        <span className={statusClass(health?.executionGateway === 'FakeLmaxGateway')}>Execution: {health?.executionGateway ?? 'Unknown'}</span>
        <span className={statusClass(health?.marketDataMode === 'FakeMarketDataProvider')}>Market data: {health?.marketDataMode ?? 'Unknown'}</span>
        <span className={statusClass(health?.liveTradingEnabled === false)}>Live trading: {String(health?.liveTradingEnabled ?? 'unknown')}</span>
        <span className={statusClass(health?.externalConnectionsEnabled === false)}>External connections: {String(health?.externalConnectionsEnabled ?? 'unknown')}</span>
        <span className={statusClass(health?.databaseReachable === true)}>DB: {health?.databaseReachable ? 'reachable' : 'unreachable'}</span>
        <span className={statusClass((health?.pendingMigrationsCount ?? 1) === 0)}>Pending migrations: {health?.pendingMigrationsCount ?? '?'}</span>
        <span className={statusClass((integrity?.blockingIssueCount ?? 1) === 0)}>Reference integrity: {integrity ? `${integrity.blockingIssueCount} blocking` : 'unknown'}</span>
      </div>
      <button className="primary" onClick={onRefresh}>Refresh All</button>
    </header>
  );
}

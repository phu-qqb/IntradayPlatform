import type { HealthDto } from '../api/types';

export function SafetyPanel({ health }: { health?: HealthDto }) {
  const warnings = [
    health && health.executionGateway !== 'FakeLmaxGateway' ? 'Execution gateway is not FakeLmaxGateway.' : null,
    health?.liveTradingEnabled ? 'Live trading is enabled.' : null,
    health?.externalConnectionsEnabled ? 'External connections are enabled.' : null,
    health && !health.databaseReachable ? 'Database is not reachable.' : null,
    health && health.pendingMigrationsCount > 0 ? 'Database has pending migrations.' : null
  ].filter(Boolean);

  return (
    <section className="panel">
      <h2>Safety</h2>
      <div className="safety-row">
        <span className={health?.executionGateway === 'FakeLmaxGateway' ? 'chip ok' : 'chip danger'}>{health?.executionGateway ?? 'Unknown'}</span>
        <span className={health?.marketDataMode === 'FakeMarketDataProvider' ? 'chip ok' : 'chip danger'}>{health?.marketDataMode ?? 'Unknown'}</span>
        <span className={health?.liveTradingEnabled === false ? 'chip ok' : 'chip danger'}>Live trading: {String(health?.liveTradingEnabled ?? 'unknown')}</span>
        <span className={health?.externalConnectionsEnabled === false ? 'chip ok' : 'chip danger'}>External: {String(health?.externalConnectionsEnabled ?? 'unknown')}</span>
      </div>
      {warnings.length > 0 ? <div className="critical-box">{warnings.map((warning) => <div key={warning}>{warning}</div>)}</div> : <p className="muted">FakeLmax-only local safety boundary is intact.</p>}
    </section>
  );
}

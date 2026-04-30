import type { HealthDto } from '../api/types';

export function HealthPanel({ health }: { health?: HealthDto }) {
  return (
    <section className="panel">
      <h2>Health</h2>
      <div className="kv-grid">
        <span>Application</span><strong>{health?.application ?? '-'}</strong>
        <span>Environment</span><strong>{health?.environment ?? '-'}</strong>
        <span>Persistence</span><strong>{health?.persistenceProvider ?? '-'}</strong>
        <span>Database target</span><strong>{health?.databaseTarget ?? '-'}</strong>
        <span>Database reachable</span><strong>{String(health?.databaseReachable ?? '-')}</strong>
        <span>Pending migrations</span><strong>{health?.pendingMigrationsCount ?? '-'}</strong>
        <span>UTC server time</span><strong>{health?.utcServerTime ?? '-'}</strong>
      </div>
    </section>
  );
}

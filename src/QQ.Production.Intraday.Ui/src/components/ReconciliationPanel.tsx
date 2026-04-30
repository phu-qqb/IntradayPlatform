import type { ReconciliationBreakDto } from '../api/types';
import { DataTable } from './DataTable';

export function ReconciliationPanel({ breaks }: { breaks: ReconciliationBreakDto[] }) {
  return (
    <section className="panel wide">
      <h2>Reconciliation</h2>
      <DataTable rows={breaks} getRowKey={(row) => row.id} columns={[
        { key: 'phase', header: 'Phase', render: (row) => row.phase ?? '-' },
        { key: 'type', header: 'Break Type', render: (row) => row.type },
        { key: 'severity', header: 'Severity', render: (row) => <span className={row.severity === 'Blocking' ? 'chip danger' : 'chip warning'}>{row.severity}</span> },
        { key: 'status', header: 'Status', render: (row) => row.status },
        { key: 'instrument', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId ?? '-' },
        { key: 'description', header: 'Description', render: (row) => row.description },
        { key: 'created', header: 'Created', render: (row) => row.createdAtUtc ?? '-' }
      ]} />
    </section>
  );
}

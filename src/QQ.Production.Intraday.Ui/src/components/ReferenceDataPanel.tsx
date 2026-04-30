import type { ReferenceDataIntegrityDto } from '../api/types';
import { DataTable } from './DataTable';

export function ReferenceDataPanel({ integrity }: { integrity?: ReferenceDataIntegrityDto }) {
  return (
    <section className="panel wide">
      <h2>Reference Data Integrity</h2>
      <div className="safety-row">
        <span className={(integrity?.blockingIssueCount ?? 1) === 0 ? 'chip ok' : 'chip danger'}>Blocking: {integrity?.blockingIssueCount ?? '?'}</span>
        <span className={(integrity?.warningIssueCount ?? 0) === 0 ? 'chip ok' : 'chip warning'}>Warnings: {integrity?.warningIssueCount ?? '?'}</span>
        <span className="chip neutral">Checked: {integrity?.checkedAtUtc ?? '-'}</span>
      </div>
      {(integrity?.blockingIssueCount ?? 0) > 0 && <div className="critical-box">Reference data is ambiguous or invalid. Trading should remain blocked.</div>}
      <DataTable
        rows={integrity?.issues ?? []}
        getRowKey={(row) => row.id}
        emptyLabel="No reference data integrity issues"
        columns={[
          { key: 'severity', header: 'Severity', render: (row) => <span className={row.severity === 'Blocking' ? 'chip danger' : 'chip warning'}>{row.severity}</span> },
          { key: 'type', header: 'Issue Type', render: (row) => row.type },
          { key: 'status', header: 'Status', render: (row) => row.status },
          { key: 'key', header: 'Key', render: (row) => row.key },
          { key: 'description', header: 'Description', render: (row) => row.description }
        ]}
      />
    </section>
  );
}

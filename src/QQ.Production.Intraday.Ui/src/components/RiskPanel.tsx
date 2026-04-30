import type { RiskDecisionDto, TradeIntentDto } from '../api/types';
import { DataTable } from './DataTable';

function statusClass(status: string) {
  if (status === 'Approved') return 'chip ok';
  if (status === 'RequiresManualApproval') return 'chip warning';
  return 'chip danger';
}

export function RiskPanel({ tradeIntents, riskDecisions }: { tradeIntents: TradeIntentDto[]; riskDecisions: RiskDecisionDto[] }) {
  return (
    <section className="panel wide">
      <h2>Risk</h2>
      <DataTable rows={riskDecisions} getRowKey={(row) => row.id} columns={[
        { key: 'id', header: 'ID', render: (row) => <code>{row.id}</code> },
        { key: 'tradeIntentId', header: 'Intent', render: (row) => <code>{row.tradeIntentId}</code> },
        { key: 'status', header: 'Status', render: (row) => <span className={statusClass(row.status)}>{row.status}</span> },
        { key: 'reason', header: 'Reject Reason', render: (row) => row.rejectReason },
        { key: 'instrument', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId ?? '-' },
        { key: 'message', header: 'Message', render: (row) => row.explanation },
        { key: 'created', header: 'Created', render: (row) => row.createdAtUtc }
      ]} />
      <h3>Trade Intents</h3>
      <DataTable rows={tradeIntents} getRowKey={(row) => row.id} columns={[
        { key: 'id', header: 'ID', render: (row) => <code>{row.id}</code> },
        { key: 'instrument', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId },
        { key: 'side', header: 'Side', render: (row) => row.side },
        { key: 'base', header: 'Base Qty', render: (row) => row.requestedBaseQuantity },
        { key: 'venue', header: 'Venue Qty', render: (row) => row.requestedVenueQuantity },
        { key: 'status', header: 'Status', render: (row) => row.status },
        { key: 'created', header: 'Created', render: (row) => row.createdAtUtc }
      ]} />
    </section>
  );
}

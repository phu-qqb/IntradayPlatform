import type { FillDto } from '../api/types';
import { DataTable } from './DataTable';

export function FillsPanel({ fills }: { fills: FillDto[] }) {
  return (
    <section className="panel wide">
      <h2>Fills</h2>
      <DataTable rows={fills} getRowKey={(row) => row.id} columns={[
        { key: 'id', header: 'ID', render: (row) => <code>{row.id}</code> },
        { key: 'brokerExecutionId', header: 'Broker Exec ID', render: (row) => row.brokerExecutionId },
        { key: 'child', header: 'Child Order', render: (row) => <code>{row.childOrderId}</code> },
        { key: 'instrument', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId },
        { key: 'venue', header: 'Venue', render: (row) => row.venueName ?? row.venueId },
        { key: 'side', header: 'Side', render: (row) => row.side },
        { key: 'base', header: 'Base Qty', render: (row) => row.baseQuantity },
        { key: 'venueQty', header: 'Venue Qty', render: (row) => row.venueQuantity },
        { key: 'price', header: 'Price', render: (row) => row.price },
        { key: 'received', header: 'Received', render: (row) => row.receivedAtUtc }
      ]} />
    </section>
  );
}

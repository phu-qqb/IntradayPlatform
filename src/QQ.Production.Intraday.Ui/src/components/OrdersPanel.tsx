import type { OrdersDto } from '../api/types';
import { DataTable } from './DataTable';

export function OrdersPanel({ orders }: { orders?: OrdersDto }) {
  return (
    <section className="panel wide">
      <h2>Orders</h2>
      <h3>Parent Orders</h3>
      <DataTable rows={orders?.parentOrders ?? []} getRowKey={(row) => row.id} columns={[
        { key: 'id', header: 'ID', render: (row) => <code>{row.id}</code> },
        { key: 'intent', header: 'Intent', render: (row) => <code>{row.tradeIntentId}</code> },
        { key: 'instrument', header: 'Instrument', render: (row) => row.instrumentId ?? '-' },
        { key: 'client', header: 'Client Order ID', render: (row) => row.clientOrderId },
        { key: 'side', header: 'Side', render: (row) => row.side },
        { key: 'base', header: 'Base Qty', render: (row) => row.baseQuantity },
        { key: 'algo', header: 'Algo', render: (row) => row.algo },
        { key: 'status', header: 'Status', render: (row) => row.status },
        { key: 'created', header: 'Created', render: (row) => row.createdAtUtc }
      ]} />
      <h3>Child Orders</h3>
      <DataTable rows={orders?.childOrders ?? []} getRowKey={(row) => row.id} columns={[
        { key: 'id', header: 'ID', render: (row) => <code>{row.id}</code> },
        { key: 'parent', header: 'Parent', render: (row) => <code>{row.parentOrderId}</code> },
        { key: 'venue', header: 'Venue', render: (row) => row.venueId },
        { key: 'instrument', header: 'Instrument', render: (row) => row.instrumentId ?? '-' },
        { key: 'client', header: 'Client Order ID', render: (row) => row.clientOrderId },
        { key: 'broker', header: 'Broker Order ID', render: (row) => row.brokerOrderId ?? '-' },
        { key: 'side', header: 'Side', render: (row) => row.side },
        { key: 'type', header: 'Type', render: (row) => row.orderType },
        { key: 'tif', header: 'TIF', render: (row) => row.timeInForce },
        { key: 'venueQty', header: 'Venue Qty', render: (row) => row.venueQuantity },
        { key: 'status', header: 'Status', render: (row) => row.status }
      ]} />
    </section>
  );
}

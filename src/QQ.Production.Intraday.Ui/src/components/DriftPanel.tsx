import type { DriftSnapshotDto, TargetPositionDto } from '../api/types';
import { DataTable } from './DataTable';

export function DriftPanel({ targets, drifts }: { targets: TargetPositionDto[]; drifts: DriftSnapshotDto[] }) {
  return (
    <section className="panel wide">
      <h2>Drift</h2>
      <DataTable rows={drifts} getRowKey={(row, index) => `${row.modelRunId}-${row.instrumentId}-${index}`} columns={[
        { key: 'modelRunId', header: 'Model Run', render: (row) => <code>{row.modelRunId}</code> },
        { key: 'instrument', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId },
        { key: 'targetBase', header: 'Target Base', render: (row) => row.targetBaseQuantity },
        { key: 'currentBase', header: 'Current Base', render: (row) => row.currentBaseQuantity },
        { key: 'driftBase', header: 'Drift Base', render: (row) => row.driftBaseQuantity },
        { key: 'targetVenue', header: 'Target Venue', render: (row) => row.targetVenueQuantity },
        { key: 'currentVenue', header: 'Current Venue', render: (row) => row.currentVenueQuantity },
        { key: 'driftVenue', header: 'Drift Venue', render: (row) => row.driftVenueQuantity }
      ]} />
      <h3>Target Positions</h3>
      <DataTable rows={targets} getRowKey={(row, index) => `${row.modelRunId}-${row.instrumentId}-${index}`} columns={[
        { key: 'modelRunId', header: 'Model Run', render: (row) => <code>{row.modelRunId}</code> },
        { key: 'instrument', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId },
        { key: 'notional', header: 'Notional USD', render: (row) => row.targetNotionalUsd },
        { key: 'base', header: 'Base Qty', render: (row) => row.targetBaseQuantity },
        { key: 'venue', header: 'Venue Qty', render: (row) => row.targetVenueQuantity },
        { key: 'mode', header: 'Mode', render: (row) => row.targetQuantityMode }
      ]} />
    </section>
  );
}

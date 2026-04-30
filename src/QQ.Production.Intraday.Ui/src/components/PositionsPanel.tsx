import type { PositionDto } from '../api/types';
import { DataTable } from './DataTable';

export function PositionsPanel({ internalPositions, brokerPositions }: { internalPositions: PositionDto[]; brokerPositions: PositionDto[] }) {
  const brokerByInstrument = new Map(brokerPositions.map((position) => [position.instrumentId, position]));
  const internalByInstrument = new Map(internalPositions.map((position) => [position.instrumentId, position]));

  return (
    <section className="panel wide">
      <h2>Positions</h2>
      <div className="split">
        <div>
          <h3>Internal</h3>
          <DataTable
            rows={internalPositions}
            getRowKey={(row) => row.instrumentId}
            columns={[
              { key: 'symbol', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId },
              { key: 'quantity', header: 'Base Quantity', render: (row) => row.baseQuantity },
              { key: 'match', header: 'Broker Match', render: (row) => brokerByInstrument.get(row.instrumentId)?.baseQuantity === row.baseQuantity ? <span className="chip ok">Match</span> : <span className="chip warning">Check</span> }
            ]}
          />
        </div>
        <div>
          <h3>Broker</h3>
          <DataTable
            rows={brokerPositions}
            getRowKey={(row) => row.instrumentId}
            columns={[
              { key: 'symbol', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId },
              { key: 'quantity', header: 'Base Quantity', render: (row) => row.baseQuantity },
              { key: 'asOf', header: 'As Of', render: (row) => row.asOfUtc ?? '-' },
              { key: 'match', header: 'Internal Match', render: (row) => internalByInstrument.get(row.instrumentId)?.baseQuantity === row.baseQuantity ? <span className="chip ok">Match</span> : <span className="chip warning">Check</span> }
            ]}
          />
        </div>
      </div>
    </section>
  );
}

import { useState } from 'react';
import type { InstrumentDto, KillSwitchDto, VenueDto } from '../api/types';
import { DataTable } from './DataTable';

export function AdminPanel({
  killSwitch,
  instruments,
  venues,
  onActivateKillSwitch,
  onClearKillSwitch
}: {
  killSwitch?: KillSwitchDto;
  instruments: InstrumentDto[];
  venues: VenueDto[];
  onActivateKillSwitch: (reason: string) => Promise<void>;
  onClearKillSwitch: () => Promise<void>;
}) {
  const [reason, setReason] = useState('Local operator activation');

  return (
    <section className="panel wide">
      <h2>Admin</h2>
      <div className="safety-row">
        <span className={killSwitch?.isActive ? 'chip danger' : 'chip ok'}>Kill switch: {killSwitch?.isActive ? 'Active' : 'Clear'}</span>
        <span className="chip neutral">Updated: {killSwitch?.updatedAtUtc ?? '-'}</span>
      </div>
      <div className="form-grid">
        <label>Reason<input value={reason} onChange={(event) => setReason(event.target.value)} /></label>
        <button onClick={() => onActivateKillSwitch(reason)}>Activate Kill Switch</button>
        <button className="primary" onClick={() => window.confirm('Clear the local kill switch?') && onClearKillSwitch()}>Clear Kill Switch</button>
      </div>
      <h3>Instruments</h3>
      <DataTable rows={instruments} getRowKey={(row) => row.id} columns={[
        { key: 'symbol', header: 'Symbol', render: (row) => row.symbol },
        { key: 'asset', header: 'Asset Class', render: (row) => row.assetClass },
        { key: 'base', header: 'Base', render: (row) => row.baseCurrency },
        { key: 'quote', header: 'Quote', render: (row) => row.quoteCurrency },
        { key: 'enabled', header: 'Enabled', render: (row) => String(row.isEnabled) }
      ]} />
      <h3>Venues</h3>
      <DataTable rows={venues} getRowKey={(row) => row.id} columns={[
        { key: 'name', header: 'Name', render: (row) => row.name },
        { key: 'type', header: 'Type', render: (row) => row.venueType },
        { key: 'enabled', header: 'Enabled', render: (row) => String(row.isEnabled) }
      ]} />
    </section>
  );
}

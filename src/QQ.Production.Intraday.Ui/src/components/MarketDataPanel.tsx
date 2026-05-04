import { useMemo, useState } from 'react';
import type { BuildBarsRequest, FakeSnapshotsRequest, MarketDataBarDto, MarketDataSnapshotDto } from '../api/types';
import { ActionButton } from './ActionFeedback';
import { DataTable } from './DataTable';

function previousCompletedBar() {
  const now = new Date();
  const floor = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), now.getUTCHours(), Math.floor(now.getUTCMinutes() / 15) * 15, 0));
  const start = new Date(floor.getTime() - 15 * 60_000);
  return { startUtc: start.toISOString(), endUtc: floor.toISOString() };
}

export function MarketDataPanel({
  snapshots,
  bars,
  onCreateFakeSnapshots,
  onBuildBars
}: {
  snapshots: MarketDataSnapshotDto[];
  bars: MarketDataBarDto[];
  onCreateFakeSnapshots: (request: FakeSnapshotsRequest) => Promise<string>;
  onBuildBars: (request: BuildBarsRequest) => Promise<string>;
}) {
  const defaults = useMemo(previousCompletedBar, []);
  const [snapshotForm, setSnapshotForm] = useState<FakeSnapshotsRequest>({
    instrumentSymbol: 'EURUSD',
    venueName: 'LMAX',
    startUtc: defaults.startUtc,
    intervalSeconds: 60,
    count: 15,
    bid: 1.1,
    ask: 1.1002,
    bidStep: 0.00001,
    askStep: 0.00001
  });
  const [barForm, setBarForm] = useState<BuildBarsRequest>({ venueName: 'LMAX', timeframe: 'FifteenMinutes', startUtc: defaults.startUtc, endUtc: defaults.endUtc });
  const [message, setMessage] = useState<string>();

  return (
    <section className="panel wide">
      <h2>Market Data</h2>
      {message && <div className="notice">{message}</div>}
      <div className="form-grid">
        <label>Instrument<input value={snapshotForm.instrumentSymbol} onChange={(event) => setSnapshotForm({ ...snapshotForm, instrumentSymbol: event.target.value })} /></label>
        <label>Venue<input value={snapshotForm.venueName} onChange={(event) => setSnapshotForm({ ...snapshotForm, venueName: event.target.value })} /></label>
        <label>Start UTC<input value={snapshotForm.startUtc} onChange={(event) => setSnapshotForm({ ...snapshotForm, startUtc: event.target.value })} /></label>
        <label>Interval Sec<input type="number" value={snapshotForm.intervalSeconds} onChange={(event) => setSnapshotForm({ ...snapshotForm, intervalSeconds: Number(event.target.value) })} /></label>
        <label>Count<input type="number" value={snapshotForm.count} onChange={(event) => setSnapshotForm({ ...snapshotForm, count: Number(event.target.value) })} /></label>
        <label>Bid<input type="number" step="0.00001" value={snapshotForm.bid} onChange={(event) => setSnapshotForm({ ...snapshotForm, bid: Number(event.target.value) })} /></label>
        <label>Ask<input type="number" step="0.00001" value={snapshotForm.ask} onChange={(event) => setSnapshotForm({ ...snapshotForm, ask: Number(event.target.value) })} /></label>
        <ActionButton idleLabel="Create Fake Snapshots" runningLabel="Creating..." onAction={async () => setMessage(await onCreateFakeSnapshots(snapshotForm))} />
      </div>
      <div className="form-grid">
        <label>Bar Start UTC<input value={barForm.startUtc} onChange={(event) => setBarForm({ ...barForm, startUtc: event.target.value })} /></label>
        <label>Bar End UTC<input value={barForm.endUtc} onChange={(event) => setBarForm({ ...barForm, endUtc: event.target.value })} /></label>
        <label>Timeframe<input value={barForm.timeframe} onChange={(event) => setBarForm({ ...barForm, timeframe: event.target.value })} /></label>
        <ActionButton idleLabel="Build 15m Bars" runningLabel="Building..." onAction={async () => setMessage(await onBuildBars(barForm))} />
      </div>
      <h3>Latest Snapshots</h3>
      <DataTable rows={snapshots} getRowKey={(row) => row.id} columns={[
        { key: 'instrument', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId },
        { key: 'venue', header: 'Venue', render: (row) => row.venueName ?? row.venueId },
        { key: 'bid', header: 'Bid', render: (row) => row.bid },
        { key: 'ask', header: 'Ask', render: (row) => row.ask },
        { key: 'mid', header: 'Mid', render: (row) => row.mid },
        { key: 'spread', header: 'Spread', render: (row) => row.spread },
        { key: 'received', header: 'Received', render: (row) => row.receivedAtUtc }
      ]} />
      <h3>15-Minute Bars</h3>
      <DataTable rows={bars} getRowKey={(row) => row.id} columns={[
        { key: 'bar', header: 'Bar Start', render: (row) => row.barStartUtc },
        { key: 'instrument', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId },
        { key: 'bid', header: 'Bid O/H/L/C', render: (row) => `${row.bidOpen} / ${row.bidHigh} / ${row.bidLow} / ${row.bidClose}` },
        { key: 'mid', header: 'Mid O/H/L/C', render: (row) => `${row.midOpen} / ${row.midHigh} / ${row.midLow} / ${row.midClose}` },
        { key: 'spread', header: 'Avg Spread', render: (row) => row.spreadAverage },
        { key: 'obs', header: 'Obs', render: (row) => row.observationCount },
        { key: 'quality', header: 'Quality', render: (row) => <span className={row.qualityStatus === 'Complete' ? 'chip ok' : 'chip warning'}>{row.qualityStatus}</span> }
      ]} />
    </section>
  );
}

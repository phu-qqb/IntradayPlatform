import { MetricCard, SectionHeader, StatusChip } from './primitives';

const instrumentRows = [
  {
    symbol: 'GBPUSD',
    slashSymbol: 'GBP/USD',
    securityId: '4002',
    status: 'Completed',
    evidence: 'MarketData evidence available',
    detail: 'Initial success and post-remediation known-good control recovered',
    tone: 'ok' as const
  },
  {
    symbol: 'EURGBP',
    slashSymbol: 'EUR/GBP',
    securityId: '4003',
    status: 'Completed',
    evidence: 'MarketDataOnly evidence and local replay available',
    detail: 'Market-hours snapshot, evidence preview, and local replay completed',
    tone: 'ok' as const
  },
  {
    symbol: 'AUDUSD',
    slashSymbol: 'AUD/USD',
    securityId: '4007',
    status: 'Completed',
    evidence: 'MarketDataOnly evidence available',
    detail: 'Post-remediation read-only snapshot and evidence preview recovered',
    tone: 'ok' as const
  },
  {
    symbol: 'USDJPY',
    slashSymbol: 'USD/JPY',
    securityId: '4004',
    status: 'ParkedSeparateTroubleshootingRail',
    evidence: 'Parked separately',
    detail: 'Repeated pre-logon failures; no MarketDataRequest reject and no SecurityID issue proven',
    tone: 'warning' as const
  }
];

const artifactLinks = [
  'artifacts/readiness/phase7k16-final-operator-signoff.json',
  'artifacts/readiness/phase7k16-final-readiness-documentation-update-summary.json',
  'artifacts/readiness/phase7k16-final-operator-signoff-note.md',
  'artifacts/readiness/phase7k15-final-additional-instrument-readonly-evidence-pack.json',
  'artifacts/readiness/phase7k15-final-additional-instrument-day-closure-gate.json'
];

export function LmaxReadOnlyFinalStatusPanel() {
  return (
    <div className="panel wide" data-testid="lmax-readonly-final-status">
      <SectionHeader
        title="LMAX Read-Only Final Evidence Status"
        eyebrow="Display-only Phase 7K16 / 7L closure state"
        actions={<StatusChip label="NoExternalAttemptsAllowed" tone="ok" />}
      />
      <div className="operator-note">
        Final operator signoff is recorded and the additional-instrument evidence cycle is closed. This panel is read-only status display and has no external-run, snapshot, replay, scheduler, credential, gateway, or order controls.
      </div>
      <div className="metric-grid">
        <MetricCard label="Operational State" value="NoExternalAttemptsAllowed" sublabel="No direct run authorization" tone="ok" />
        <MetricCard label="Evidence Cycle" value="Closed" sublabel="Final operator signoff recorded" tone="ok" />
        <MetricCard label="Successful Evidence" value="GBPUSD / EURGBP / AUDUSD" sublabel="MarketData read-only evidence set" tone="ok" />
        <MetricCard label="Parked Instrument" value="USDJPY" sublabel="Separate troubleshooting rail" tone="warning" />
        <MetricCard label="API / Worker" value="FakeLmaxGateway only" sublabel="No real gateway registration" tone="ok" />
        <MetricCard label="Known Local Issue" value="Optional replay health timeout" sublabel="Not an LMAX evidence failure" tone="info" />
      </div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Symbol</th>
              <th>Slash</th>
              <th>SecurityID</th>
              <th>Status</th>
              <th>Evidence</th>
              <th>External Run</th>
              <th>Detail</th>
            </tr>
          </thead>
          <tbody>
            {instrumentRows.map((row) => (
              <tr key={row.symbol}>
                <td>{row.symbol}</td>
                <td>{row.slashSymbol}</td>
                <td>{row.securityId}</td>
                <td><StatusChip label={row.status} tone={row.tone} /></td>
                <td>{row.evidence}</td>
                <td>false</td>
                <td>{row.detail}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="page-grid two">
        <div className="info-box">
          <strong>Artifact references</strong>
          <div className="chip-list">
            {artifactLinks.map((path) => <code className="chip neutral" key={path}>{path}</code>)}
          </div>
        </div>
        <div className="critical-box">
          <strong>What remains blocked</strong>
          <div className="chip-list">
            {[
              'External attempts',
              'Snapshots',
              'Replay triggers',
              'Scheduler or polling',
              'Runtime shadow replay submit',
              'Order path',
              'Gateway registration',
              'Trading mutation',
              'Retry, batch, or loop'
            ].map((item) => <span className="chip warning" key={item}>{item}</span>)}
          </div>
        </div>
      </div>
    </div>
  );
}

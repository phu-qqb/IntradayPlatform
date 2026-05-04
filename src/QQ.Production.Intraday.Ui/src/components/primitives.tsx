import React from 'react';
import { X } from 'lucide-react';

export type Tone = 'ok' | 'info' | 'warning' | 'danger' | 'neutral';

export function toneForStatus(status?: string | null): Tone {
  const normalized = (status ?? '').toLowerCase();
  if (['approved', 'processed', 'promoted', 'imported', 'completed', 'filled', 'acked', 'ok', 'complete', 'resolved'].includes(normalized)) return 'ok';
  if (['failed', 'rejected', 'riskrejected', 'critical', 'blocking', 'positionmismatch', 'brokerfillmissinginternally', 'internalfillmissinginbrokerreport', 'killswitchactive', 'referencedatainvalid'].includes(normalized)) return 'danger';
  if (['blocked', 'requiresmanualapproval', 'warning', 'sparsedata', 'incomplete', 'waived', 'acknowledged', 'investigating', 'open'].includes(normalized)) return 'warning';
  if (['noactionrequired', 'nodrift', 'alreadyprocessed', 'received', 'created', 'draft', 'ready', 'started', 'info', 'skipped'].includes(normalized)) return 'info';
  if (['falsepositive', 'closed'].includes(normalized)) return 'neutral';
  return 'neutral';
}

export function processResultTone(status?: string | null, blockedReason?: string | null): Tone {
  if (status === 'Processed') return 'ok';
  if (status === 'NoActionRequired' || status === 'AlreadyProcessed') return 'info';
  if (status === 'Failed') return 'danger';
  if (status === 'Blocked') {
    return blockedReason?.includes('ReferenceData') || blockedReason === 'PositionMismatch' ? 'danger' : 'warning';
  }
  return toneForStatus(status);
}

export function StatusChip({ label, tone = 'neutral' }: { label: React.ReactNode; tone?: Tone }) {
  return <span className={`chip ${tone}`}>{label}</span>;
}

export function SeverityBadge({ value }: { value?: string | null }) {
  const tone = value === 'Blocking' || value === 'Critical' ? 'danger' : value === 'Warning' ? 'warning' : value === 'Info' ? 'info' : toneForStatus(value);
  return <StatusChip label={value ?? '-'} tone={tone} />;
}

export function MetricCard({ label, value, tone = 'neutral', sublabel }: { label: string; value: React.ReactNode; tone?: Tone; sublabel?: React.ReactNode }) {
  return (
    <div className={`metric-card ${tone}`}>
      <span>{label}</span>
      <strong>{value}</strong>
      {sublabel && <small>{sublabel}</small>}
    </div>
  );
}

export function SectionHeader({ title, eyebrow, actions }: { title: string; eyebrow?: string; actions?: React.ReactNode }) {
  return (
    <div className="section-header">
      <div>
        {eyebrow && <span className="eyebrow">{eyebrow}</span>}
        <h2>{title}</h2>
      </div>
      {actions && <div className="section-actions">{actions}</div>}
    </div>
  );
}

export function CommandButton({ children, tone = 'neutral', ...props }: React.ButtonHTMLAttributes<HTMLButtonElement> & { tone?: Tone }) {
  return <button className={`command-button ${tone}`} {...props}>{children}</button>;
}

export function DetailDrawer({ item, onClose }: { item?: unknown; onClose: () => void }) {
  const [showRawJson, setShowRawJson] = React.useState(false);
  const record = item && typeof item === 'object' ? item as Record<string, unknown> : undefined;
  const entries = record ? Object.entries(record).filter(([, value]) => value !== undefined && value !== null && value !== '') : [];
  const idEntries = entries.filter(([key]) => key.toLowerCase().includes('id'));
  const timeEntries = entries.filter(([key]) => key.toLowerCase().includes('utc') || key.toLowerCase().endsWith('at'));
  const summaryKeys = ['status', 'severity', 'type', 'source', 'symbol', 'title', 'modelName', 'description', 'message', 'result'];
  const summaryEntries = entries.filter(([key]) => summaryKeys.some((summaryKey) => summaryKey.toLowerCase() === key.toLowerCase()));
  const usedKeys = new Set([...idEntries, ...timeEntries, ...summaryEntries].map(([key]) => key));
  const detailEntries = entries.filter(([key]) => !usedKeys.has(key) && typeof record?.[key] !== 'object').slice(0, 16);

  const renderEntries = (items: Array<[string, unknown]>) => (
    <dl className="drawer-field-list">
      {items.map(([key, value]) => (
        <React.Fragment key={key}>
          <dt>{key}</dt>
          <dd title={String(value)}>{String(value)}</dd>
        </React.Fragment>
      ))}
    </dl>
  );

  return (
    <aside className={`detail-drawer ${item ? 'open' : ''}`} aria-label="Detail drawer">
      <div className="drawer-head">
        <div>
          <span className="eyebrow">Selected record</span>
          <h2>Details</h2>
        </div>
        <button aria-label="Close details" onClick={onClose}><X size={16} /></button>
      </div>
      {item ? (
        <>
          {summaryEntries.length > 0 && <section className="drawer-section"><h3>Summary</h3>{renderEntries(summaryEntries)}</section>}
          {idEntries.length > 0 && <section className="drawer-section"><h3>IDs</h3>{renderEntries(idEntries)}</section>}
          {timeEntries.length > 0 && <section className="drawer-section"><h3>Timestamps</h3>{renderEntries(timeEntries)}</section>}
          {detailEntries.length > 0 && <section className="drawer-section"><h3>Details</h3>{renderEntries(detailEntries)}</section>}
          <section className="drawer-section">
            <button className="raw-json-toggle" onClick={() => setShowRawJson((value) => !value)}>
              {showRawJson ? 'Hide raw JSON' : 'Show raw JSON'}
            </button>
            {showRawJson && <pre>{JSON.stringify(item, null, 2)}</pre>}
          </section>
        </>
      ) : (
        <div className="empty-state">Select a row to inspect its fields.</div>
      )}
    </aside>
  );
}

export function Timeline({ items }: { items: Array<{ label: string; time?: string | null; tone?: Tone; detail?: React.ReactNode }> }) {
  return (
    <div className="timeline">
      {items.map((item, index) => (
        <div className={`timeline-item ${item.tone ?? 'neutral'}`} key={`${item.label}-${index}`}>
          <span>{item.label}</span>
          <small>{item.time ?? '-'}</small>
          {item.detail && <em>{item.detail}</em>}
        </div>
      ))}
    </div>
  );
}

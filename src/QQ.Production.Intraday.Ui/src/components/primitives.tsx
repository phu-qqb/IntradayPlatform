import type React from 'react';
import { X } from 'lucide-react';

export type Tone = 'ok' | 'info' | 'warning' | 'danger' | 'neutral';

export function toneForStatus(status?: string | null): Tone {
  const normalized = (status ?? '').toLowerCase();
  if (['approved', 'processed', 'promoted', 'imported', 'completed', 'filled', 'acked', 'ok', 'complete'].includes(normalized)) return 'ok';
  if (['blocked', 'rejected', 'riskrejected', 'failed', 'critical'].includes(normalized)) return 'danger';
  if (['requiresmanualapproval', 'warning', 'sparsedata', 'incomplete'].includes(normalized)) return 'warning';
  if (['noactionrequired', 'alreadyprocessed', 'received', 'created', 'draft', 'started'].includes(normalized)) return 'info';
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
        <pre>{JSON.stringify(item, null, 2)}</pre>
      ) : (
        <div className="empty-state">Select a row to inspect its fields.</div>
      )}
    </aside>
  );
}

export function Timeline({ items }: { items: Array<{ label: string; time?: string | null; tone?: Tone }> }) {
  return (
    <div className="timeline">
      {items.map((item, index) => (
        <div className={`timeline-item ${item.tone ?? 'neutral'}`} key={`${item.label}-${index}`}>
          <span>{item.label}</span>
          <small>{item.time ?? '-'}</small>
        </div>
      ))}
    </div>
  );
}

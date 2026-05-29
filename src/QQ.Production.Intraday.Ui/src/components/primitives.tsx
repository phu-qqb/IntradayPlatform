import React from 'react';
import { X } from 'lucide-react';

export type Tone = 'ok' | 'info' | 'warning' | 'danger' | 'neutral';

export function toneForStatus(status?: string | null): Tone {
  const normalized = (status ?? '').toLowerCase();
  if (['approved', 'processed', 'promoted', 'imported', 'completed', 'filled', 'acked', 'ok', 'complete', 'resolved', 'active'].includes(normalized)) return 'ok';
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

function parseJsonObject(value: unknown): Record<string, unknown> | undefined {
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    return value as Record<string, unknown>;
  }

  if (typeof value !== 'string') return undefined;

  const trimmed = value.trim();
  if (!trimmed.startsWith('{') || !trimmed.endsWith('}')) return undefined;

  try {
    const parsed = JSON.parse(trimmed);
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as Record<string, unknown> : undefined;
  } catch {
    return undefined;
  }
}

function flattenObject(value: Record<string, unknown>, prefix = ''): Array<[string, unknown]> {
  return Object.entries(value).flatMap(([key, entry]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    const parsed = parseJsonObject(entry);
    if (parsed) return [[path, entry] as [string, unknown], ...flattenObject(parsed, path)];
    return [[path, entry] as [string, unknown]];
  });
}

function formatLabel(value: string): string {
  return value
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[._-]+/g, ' ')
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function collectWorkflowLinks(record?: Record<string, unknown>): Array<[string, unknown]> {
  if (!record) return [];

  const workflowKeys = new Set([
    'replayrunid',
    'observationid',
    'shadowobservationid',
    'exceptioncaseid',
    'approvalrequestid',
    'riskdecisionid',
    'jobrunid',
    'runbookrunid',
    'operationaljobrunid',
    'operationalrunbookrunid',
    'correlationid',
    'fingerprint',
    'policycode',
    'evidencemode',
    'sourceeventtype',
    'entityid',
    'entitytype'
  ]);

  const allEntries = flattenObject(record);
  const seen = new Set<string>();

  return allEntries
    .filter(([key, value]) => {
      if (value === undefined || value === null || value === '') return false;
      const normalizedKey = key.split('.').at(-1)?.toLowerCase() ?? key.toLowerCase();
      return workflowKeys.has(normalizedKey);
    })
    .filter(([key, value]) => {
      const dedupeKey = `${key}:${String(value)}`;
      if (seen.has(dedupeKey)) return false;
      seen.add(dedupeKey);
      return true;
    })
    .slice(0, 20)
    .map(([key, value]) => [formatLabel(key), value]);
}

function hasTruthyValue(record: Record<string, unknown>, keys: string[]): boolean {
  return keys.some((key) => record[key] === true || String(record[key]).toLowerCase() === 'true');
}

function getOperatorGuidance(record?: Record<string, unknown>): { tone: Tone; lines: string[] } | undefined {
  if (!record) return undefined;

  const severity = String(record.severity ?? record.status ?? '').toLowerCase();
  const status = String(record.status ?? '').toLowerCase();
  const policyCode = String(record.policyCode ?? '').toUpperCase();
  const evidenceMode = String(record.evidenceMode ?? '');
  const sourceEventType = String(record.sourceEventType ?? record.execType ?? record.executionType ?? '').toLowerCase();
  const lines: string[] = [];

  if (severity === 'blocking' || severity === 'critical' || status === 'blocked' || status === 'failed') {
    lines.push('Blocking or failed item: do not proceed until the linked evidence, exception, or runbook step is reviewed.');
  }

  if (hasTruthyValue(record, ['createsExceptionCase', 'requiresApproval', 'approvalRequired'])) {
    lines.push('This item is linked to an exception or approval workflow; complete the required reason and maker/checker steps before closure.');
  }

  if (policyCode.includes('TC_MISSING_INTERNAL_FILL')) {
    lines.push('TradeCapture evidence is recovery evidence. In lab/read-only mode this is usually expected for lab-created trades, but the observation still needs review.');
  }

  if (policyCode.includes('PROTOCOL_REJECT')) {
    lines.push('Protocol rejects need context: read-only rejects are usually review items, while order-path rejects are blocking.');
  }

  if (sourceEventType.includes('orderstatus') || policyCode.includes('ORDER_STATUS') || String(record.execType ?? '').toUpperCase() === 'I') {
    lines.push('ExecType=I / OrderStatus is status-only and must not be treated as fill evidence.');
  }

  if (evidenceMode === 'MarketDataOnly' || evidenceMode === 'EmptyReadOnly') {
    lines.push('This evidence mode is context-only; no trading-state change is expected.');
  }

  if (String(record.correlationId ?? '')) {
    lines.push('Use the correlation ID to follow related audit, job, runbook, exception, and approval records.');
  }

  if (String(record.retryOfJobRunId ?? record.retryOfRunbookRunId ?? '')) {
    lines.push('This is part of a retry chain; compare the original run and retry before deciding the next action.');
  }

  if (lines.length === 0) return undefined;

  return { tone: lines.some((line) => line.startsWith('Blocking')) ? 'danger' : 'info', lines };
}

export function DetailDrawer({ item, onClose }: { item?: unknown; onClose: () => void }) {
  const [showRawJson, setShowRawJson] = React.useState(false);
  const record = item && typeof item === 'object' ? item as Record<string, unknown> : undefined;
  const entries = record ? Object.entries(record).filter(([, value]) => value !== undefined && value !== null && value !== '') : [];
  const idEntries = entries.filter(([key]) => key.toLowerCase().includes('id'));
  const timeEntries = entries.filter(([key]) => key.toLowerCase().includes('utc') || key.toLowerCase().endsWith('at'));
  const summaryKeys = ['status', 'severity', 'type', 'source', 'symbol', 'title', 'modelName', 'description', 'message', 'result', 'policyCode', 'evidenceMode', 'sourceEventType', 'rationale', 'suggestedOperatorAction'];
  const summaryEntries = entries.filter(([key]) => summaryKeys.some((summaryKey) => summaryKey.toLowerCase() === key.toLowerCase()));
  const workflowEntries = collectWorkflowLinks(record);
  const guidance = getOperatorGuidance(record);
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
          {guidance && (
            <section className="drawer-section">
              <h3>Operator Guidance</h3>
              <div className={guidance.tone === 'danger' ? 'critical-box' : 'operator-note'}>
                {guidance.lines.map((line) => <p key={line}>{line}</p>)}
              </div>
            </section>
          )}
          {summaryEntries.length > 0 && <section className="drawer-section"><h3>Summary</h3>{renderEntries(summaryEntries)}</section>}
          {workflowEntries.length > 0 && <section className="drawer-section"><h3>Workflow Links</h3>{renderEntries(workflowEntries)}</section>}
          {idEntries.length > 0 && <section className="drawer-section"><h3>IDs</h3>{renderEntries(idEntries)}</section>}
          {timeEntries.length > 0 && <section className="drawer-section"><h3>Timestamps</h3>{renderEntries(timeEntries)}</section>}
          {detailEntries.length > 0 && <section className="drawer-section"><h3>Details</h3>{renderEntries(detailEntries)}</section>}
          <section className="drawer-section">
            <button className="raw-json-toggle" onClick={() => setShowRawJson((value) => !value)}>
              {showRawJson ? 'Hide advanced raw JSON' : 'Show advanced raw JSON'}
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

import type { RiskDecisionDto, TradeIntentDto } from '../api/types';
import { formatIdShort, formatQuantity, formatStatus, formatUtc } from '../utils/format';
import { DataTable } from './DataTable';
import { StatusChip, toneForStatus } from './primitives';
import { useState } from 'react';

export function RiskPanel({ tradeIntents, riskDecisions }: { tradeIntents: TradeIntentDto[]; riskDecisions: RiskDecisionDto[] }) {
  const [selectedDecision, setSelectedDecision] = useState<RiskDecisionDto>();

  return (
    <section className="panel wide">
      <h2>Risk</h2>
      <DataTable rows={riskDecisions} getRowKey={(row) => row.id} onRowClick={setSelectedDecision} columns={[
        { key: 'time', header: 'Time', render: (row) => formatUtc(row.createdAtUtc), sortValue: (row) => row.createdAtUtc },
        { key: 'symbol', header: 'Symbol', render: (row) => row.symbol ?? formatIdShort(row.instrumentId) },
        { key: 'tradeIntentId', header: 'Intent', render: (row) => formatIdShort(row.tradeIntentId) },
        { key: 'status', header: 'Status', render: (row) => <StatusChip label={formatStatus(row.status)} tone={toneForStatus(row.status)} /> },
        { key: 'reason', header: 'Reason', render: (row) => row.rejectReason ? formatStatus(row.rejectReason) : '-' },
        { key: 'riskSet', header: 'Risk Set', render: (row) => row.riskLimitSetName ? `${row.riskLimitSetName} v${row.riskLimitSetVersion ?? '-'}` : formatIdShort(row.riskLimitSetId) },
        { key: 'check', header: 'Key Check', render: (row) => row.summaryCheckName ? formatStatus(row.summaryCheckName) : 'All checks' },
        { key: 'observed', header: 'Observed', render: (row) => formatQuantity(row.summaryObservedValue), sortValue: (row) => row.summaryObservedValue ?? '' },
        { key: 'limit', header: 'Limit', render: (row) => formatQuantity(row.summaryLimitValue), sortValue: (row) => row.summaryLimitValue ?? '' },
        { key: 'unit', header: 'Unit', render: (row) => row.summaryUnit ?? '-' },
        { key: 'message', header: 'Message', render: (row) => row.message || 'All configured risk checks passed.' }
      ]} />
      {selectedDecision && (
        <div className="info-box">
          <strong>Risk decision details:</strong> {formatIdShort(selectedDecision.id)}
          {selectedDecision.details?.length ? (
            <DataTable rows={selectedDecision.details} getRowKey={(row) => row.id} columns={[
              { key: 'check', header: 'Check', render: (row) => formatStatus(row.checkName), sortValue: (row) => row.checkName },
              { key: 'status', header: 'Status', render: (row) => <StatusChip label={formatStatus(row.status)} tone={toneForStatus(row.status)} />, sortValue: (row) => row.status },
              { key: 'observed', header: 'Observed', render: (row) => formatQuantity(row.observedValue), sortValue: (row) => row.observedValue ?? '' },
              { key: 'limit', header: 'Limit', render: (row) => formatQuantity(row.limitValue), sortValue: (row) => row.limitValue ?? '' },
              { key: 'unit', header: 'Unit', render: (row) => row.unit ?? '-' },
              { key: 'message', header: 'Message', render: (row) => row.message }
            ]} />
          ) : (
            <div className="empty-state">No check details available for this historical decision.</div>
          )}
        </div>
      )}
      <h3>Trade Intents</h3>
      <DataTable rows={tradeIntents} getRowKey={(row) => row.id} columns={[
        { key: 'id', header: 'ID', render: (row) => <code>{row.id}</code> },
        { key: 'instrument', header: 'Instrument', render: (row) => row.symbol ?? row.instrumentId },
        { key: 'side', header: 'Side', render: (row) => row.side },
        { key: 'base', header: 'Base Qty', render: (row) => row.requestedBaseQuantity },
        { key: 'venue', header: 'Venue Qty', render: (row) => row.requestedVenueQuantity },
        { key: 'status', header: 'Status', render: (row) => row.status },
        { key: 'created', header: 'Created', render: (row) => row.createdAtUtc }
      ]} />
    </section>
  );
}

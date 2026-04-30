import { useState } from 'react';
import type { CreateModelRunRequest, ModelRunDto, ProcessModelRunResult } from '../api/types';
import { DataTable } from './DataTable';

type WeightRow = { symbol: string; weight: number; rawSecurityId: string };

const criticalBlockedReasons = new Set([
  'ReferenceDataAmbiguous',
  'ReferenceDataInvalid',
  'PositionMismatch',
  'UnknownCurrentPosition',
  'KillSwitchActive'
]);

function getProcessResultClass(result: ProcessModelRunResult) {
  if (result.status === 'Processed') {
    return 'notice';
  }

  if (result.status === 'NoActionRequired' || result.status === 'AlreadyProcessed') {
    return 'info-box';
  }

  if (result.status === 'Blocked') {
    return result.blockedReason && criticalBlockedReasons.has(result.blockedReason) ? 'critical-box' : 'warning-box';
  }

  if (result.status === 'Failed') {
    return 'critical-box';
  }

  return 'info-box';
}

function getProcessResultMessage(result: ProcessModelRunResult) {
  if (result.status === 'NoActionRequired' && result.blockedReason === 'NoDrift') {
    return 'No order was created because target and current position are already within the configured rebalance threshold.';
  }

  return result.message ?? (result.status === 'Processed' ? 'Processed' : 'No further action required.');
}

export function ModelRunsPanel({
  modelRuns,
  onCreate,
  onProcess
}: {
  modelRuns: ModelRunDto[];
  onCreate: (request: CreateModelRunRequest) => Promise<void>;
  onProcess: (id: string) => Promise<ProcessModelRunResult>;
}) {
  const now = new Date().toISOString();
  const [weights, setWeights] = useState<WeightRow[]>([{ symbol: 'EURUSD', weight: -0.1, rawSecurityId: 'EURUSD' }]);
  const [form, setForm] = useState({ modelName: 'IntradayFxModel', asOfUtc: now, effectiveAtUtc: now, navUsd: 1_000_000, frequencyMinutes: 15, targetQuantityMode: 'PortfolioBaseCurrencyNotional' });
  const [result, setResult] = useState<ProcessModelRunResult>();

  const submit = async () => {
    await onCreate({ ...form, weights });
  };

  return (
    <section className="panel wide">
      <h2>Model Runs</h2>
      {result && (
        <div className={getProcessResultClass(result)}>
          Process result: {result.status} {result.blockedReason ? `(${result.blockedReason})` : ''} - {getProcessResultMessage(result)}
        </div>
      )}
      <div className="form-grid">
        <label>Model<input value={form.modelName} onChange={(event) => setForm({ ...form, modelName: event.target.value })} /></label>
        <label>As Of UTC<input value={form.asOfUtc} onChange={(event) => setForm({ ...form, asOfUtc: event.target.value })} /></label>
        <label>NAV USD<input type="number" value={form.navUsd} onChange={(event) => setForm({ ...form, navUsd: Number(event.target.value) })} /></label>
        <label>Frequency<input type="number" value={form.frequencyMinutes} onChange={(event) => setForm({ ...form, frequencyMinutes: Number(event.target.value) })} /></label>
      </div>
      {weights.map((weight, index) => (
        <div className="form-grid" key={index}>
          <label>Symbol<input value={weight.symbol} onChange={(event) => setWeights(weights.map((row, i) => i === index ? { ...row, symbol: event.target.value } : row))} /></label>
          <label>Weight<input type="number" step="0.01" value={weight.weight} onChange={(event) => setWeights(weights.map((row, i) => i === index ? { ...row, weight: Number(event.target.value) } : row))} /></label>
          <label>Raw ID<input value={weight.rawSecurityId} onChange={(event) => setWeights(weights.map((row, i) => i === index ? { ...row, rawSecurityId: event.target.value } : row))} /></label>
          <button onClick={() => setWeights(weights.filter((_, i) => i !== index))}>Remove</button>
        </div>
      ))}
      <div className="button-row">
        <button onClick={() => setWeights([...weights, { symbol: 'EURUSD', weight: 0, rawSecurityId: 'EURUSD' }])}>Add Weight</button>
        <button className="primary" onClick={submit}>Create Local Model Run</button>
      </div>
      <DataTable rows={modelRuns} getRowKey={(row) => row.id} columns={[
        { key: 'id', header: 'ID', render: (row) => <code>{row.id}</code> },
        { key: 'model', header: 'Model', render: (row) => row.modelName },
        { key: 'asOf', header: 'As Of', render: (row) => row.asOfUtc },
        { key: 'nav', header: 'NAV', render: (row) => row.navUsd },
        { key: 'mode', header: 'Mode', render: (row) => row.targetQuantityMode },
        { key: 'status', header: 'Status', render: (row) => row.status },
        { key: 'processed', header: 'Processed', render: (row) => String(row.isProcessed) },
        { key: 'action', header: 'Action', render: (row) => <button onClick={() => onProcess(row.id).then(setResult)}>Process</button> }
      ]} />
    </section>
  );
}

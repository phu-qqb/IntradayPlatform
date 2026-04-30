import { useState } from 'react';
import type { CreateFakeModelWeightBatchRequest, ModelWeightBatchDto, ModelWeightPromotionResultDto, ModelWeightRowDto, ModelWeightValidationIssueDto } from '../api/types';
import { DataTable } from './DataTable';

type WeightRow = { rawSecurityId: string; symbol: string; weight: number };

export function ModelWeightsPanel({
  batches,
  rows,
  issues,
  onSelectBatch,
  onCreateFake,
  onValidate,
  onPromote,
  onPromoteReady
}: {
  batches: ModelWeightBatchDto[];
  rows: ModelWeightRowDto[];
  issues: ModelWeightValidationIssueDto[];
  onSelectBatch: (id: string) => Promise<void>;
  onCreateFake: (request: CreateFakeModelWeightBatchRequest) => Promise<void>;
  onValidate: (id: string) => Promise<ModelWeightPromotionResultDto>;
  onPromote: (id: string) => Promise<ModelWeightPromotionResultDto>;
  onPromoteReady: () => Promise<ModelWeightPromotionResultDto[]>;
}) {
  const now = new Date().toISOString();
  const [selectedBatchId, setSelectedBatchId] = useState<string>();
  const [weights, setWeights] = useState<WeightRow[]>([{ rawSecurityId: 'EURUSD', symbol: 'EURUSD', weight: -0.1 }]);
  const [form, setForm] = useState({ externalBatchId: '', sourceSystem: 'Fake', fundCode: 'QQ_MASTER', modelName: 'IntradayFxModel', asOfUtc: now, effectiveAtUtc: now, frequencyMinutes: 15, navUsd: 1_000_000, targetQuantityMode: 'PortfolioBaseCurrencyNotional', status: 'Ready' });
  const [result, setResult] = useState<ModelWeightPromotionResultDto | ModelWeightPromotionResultDto[]>();

  const selectBatch = async (id: string) => {
    setSelectedBatchId(id);
    await onSelectBatch(id);
  };

  const createFake = async () => {
    await onCreateFake({
      ...form,
      externalBatchId: form.externalBatchId || undefined,
      weights
    });
  };

  const renderResult = () => {
    if (!result) return null;
    const results = Array.isArray(result) ? result : [result];
    return (
      <div className={results.every((item) => item.succeeded) ? 'notice' : 'critical-box'}>
        {results.map((item, index) => (
          <div key={`${item.batchId ?? index}`}>
            {item.status ?? 'Unknown'}: {item.message} {item.modelRunId ? `ModelRun ${item.modelRunId}` : ''}
          </div>
        ))}
      </div>
    );
  };

  return (
    <section className="panel wide">
      <h2>Model Weight Batches</h2>
      {renderResult()}
      <div className="form-grid">
        <label>Source<input value={form.sourceSystem} onChange={(event) => setForm({ ...form, sourceSystem: event.target.value })} /></label>
        <label>Fund Code<input value={form.fundCode} onChange={(event) => setForm({ ...form, fundCode: event.target.value })} /></label>
        <label>Model<input value={form.modelName} onChange={(event) => setForm({ ...form, modelName: event.target.value })} /></label>
        <label>External Batch ID<input value={form.externalBatchId} onChange={(event) => setForm({ ...form, externalBatchId: event.target.value })} /></label>
        <label>As Of UTC<input value={form.asOfUtc} onChange={(event) => setForm({ ...form, asOfUtc: event.target.value })} /></label>
        <label>Effective UTC<input value={form.effectiveAtUtc} onChange={(event) => setForm({ ...form, effectiveAtUtc: event.target.value })} /></label>
        <label>NAV USD<input type="number" value={form.navUsd} onChange={(event) => setForm({ ...form, navUsd: Number(event.target.value) })} /></label>
        <label>Frequency<input type="number" value={form.frequencyMinutes} onChange={(event) => setForm({ ...form, frequencyMinutes: Number(event.target.value) })} /></label>
      </div>
      {weights.map((weight, index) => (
        <div className="form-grid" key={index}>
          <label>Raw ID<input value={weight.rawSecurityId} onChange={(event) => setWeights(weights.map((row, i) => i === index ? { ...row, rawSecurityId: event.target.value } : row))} /></label>
          <label>Symbol<input value={weight.symbol} onChange={(event) => setWeights(weights.map((row, i) => i === index ? { ...row, symbol: event.target.value } : row))} /></label>
          <label>Weight<input type="number" step="0.01" value={weight.weight} onChange={(event) => setWeights(weights.map((row, i) => i === index ? { ...row, weight: Number(event.target.value) } : row))} /></label>
          <button onClick={() => setWeights(weights.filter((_, i) => i !== index))}>Remove</button>
        </div>
      ))}
      <div className="button-row">
        <button onClick={() => setWeights([...weights, { rawSecurityId: 'EURUSD', symbol: 'EURUSD', weight: 0 }])}>Add Row</button>
        <button className="primary" onClick={createFake}>Create Fake Weight Batch</button>
        <button onClick={() => onPromoteReady().then(setResult)}>Promote Ready</button>
      </div>

      <DataTable rows={batches} getRowKey={(row) => row.id} columns={[
        { key: 'id', header: 'ID', render: (row) => <code>{row.id}</code> },
        { key: 'external', header: 'External Batch', render: (row) => row.externalBatchId },
        { key: 'source', header: 'Source', render: (row) => row.sourceSystem },
        { key: 'model', header: 'Model', render: (row) => row.modelName },
        { key: 'asOf', header: 'As Of', render: (row) => row.asOfUtc },
        { key: 'status', header: 'Status', render: (row) => row.status },
        { key: 'promoted', header: 'Promoted ModelRun', render: (row) => row.promotedModelRunId ? <code>{row.promotedModelRunId}</code> : '' },
        { key: 'actions', header: 'Actions', render: (row) => (
          <div className="button-row">
            <button onClick={() => void selectBatch(row.id)}>Rows</button>
            <button onClick={() => onValidate(row.id).then(setResult)}>Validate</button>
            <button onClick={() => onPromote(row.id).then(setResult)}>Promote</button>
          </div>
        ) }
      ]} />

      <div className="split">
        <div>
          <h3>Rows {selectedBatchId ? <code>{selectedBatchId}</code> : ''}</h3>
          <DataTable rows={rows} getRowKey={(row) => row.id} columns={[
            { key: 'raw', header: 'Raw ID', render: (row) => row.rawSecurityId },
            { key: 'symbol', header: 'Symbol', render: (row) => row.symbol },
            { key: 'instrument', header: 'Instrument ID', render: (row) => row.instrumentId ? <code>{row.instrumentId}</code> : '' },
            { key: 'weight', header: 'Weight', render: (row) => row.weight }
          ]} />
        </div>
        <div>
          <h3>Validation Issues</h3>
          <DataTable rows={issues} getRowKey={(row) => row.id} columns={[
            { key: 'type', header: 'Type', render: (row) => row.issueType },
            { key: 'severity', header: 'Severity', render: (row) => row.severity },
            { key: 'row', header: 'Row', render: (row) => row.rowNumber ?? '' },
            { key: 'message', header: 'Message', render: (row) => row.message }
          ]} />
        </div>
      </div>
    </section>
  );
}

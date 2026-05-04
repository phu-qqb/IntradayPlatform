import { useState } from 'react';
import type {
  EodPnlSummaryDto,
  EodReconciliationBreakDto,
  EodReconciliationRunDto,
  FakeLmaxEodReportGenerationDto,
  LmaxCurrencyWalletDto,
  LmaxIndividualTradeDto,
  LmaxReportImportResultDto,
  LmaxReportImportRunDto,
  LmaxReportValidationIssueDto,
  LmaxTradeSummaryDto
} from '../api/types';
import { ActionButton } from './ActionFeedback';
import { DataTable } from './DataTable';

const mutationModes = [
  'None',
  'DropOneExecution',
  'AddUnknownExecution',
  'ChangeExecutionQuantity',
  'ChangeExecutionPrice',
  'ChangeExecutionSide',
  'DropOneSummaryRow',
  'ChangeSummaryCommission',
  'ChangeSummaryNotional',
  'ChangeWalletBalance',
  'ChangeWalletRate',
  'DropCurrencyWallet'
];

export function LmaxEodReportsPanel({
  importRuns,
  validationIssues,
  individualTrades,
  tradeSummaries,
  currencyWallets,
  pnlSummary,
  reconciliationRuns,
  eodBreaks,
  onGenerateFake,
  onImportGenerated,
  onRunReconciliation,
  onLoadPnl
}: {
  importRuns: LmaxReportImportRunDto[];
  validationIssues: LmaxReportValidationIssueDto[];
  individualTrades: LmaxIndividualTradeDto[];
  tradeSummaries: LmaxTradeSummaryDto[];
  currencyWallets: LmaxCurrencyWalletDto[];
  pnlSummary?: EodPnlSummaryDto;
  reconciliationRuns: EodReconciliationRunDto[];
  eodBreaks: EodReconciliationBreakDto[];
  onGenerateFake: (request: { reportDate: string; venueName: string; brokerAccountCode: string; mutationMode: string }) => Promise<FakeLmaxEodReportGenerationDto>;
  onImportGenerated: (request: { reportDate: string; venueName: string; brokerAccountCode: string }) => Promise<LmaxReportImportResultDto>;
  onRunReconciliation: (request: { reportDate: string; venueName: string; brokerAccountCode: string }) => Promise<{ breakCount: number; blockingBreakCount: number }>;
  onLoadPnl: (reportDate: string, venueName: string, brokerAccountCode: string) => Promise<void>;
}) {
  const [form, setForm] = useState({
    reportDate: new Date().toISOString().slice(0, 10),
    venueName: 'LMAX',
    brokerAccountCode: 'LMAX_DEMO_LOCAL',
    mutationMode: 'None'
  });
  const [message, setMessage] = useState<string>();

  return (
    <section className="panel wide">
      <h2>LMAX EOD Reports</h2>
      <p className="muted">Local-only import of actual LMAX EOD report schemas. No LMAX connection is used.</p>
      {message && <div className="info-box">{message}</div>}
      <div className="form-grid">
        <label>Report Date<input value={form.reportDate} onChange={(event) => setForm({ ...form, reportDate: event.target.value })} /></label>
        <label>Venue<input value={form.venueName} onChange={(event) => setForm({ ...form, venueName: event.target.value })} /></label>
        <label>Broker Account<input value={form.brokerAccountCode} onChange={(event) => setForm({ ...form, brokerAccountCode: event.target.value })} /></label>
        <label>Mutation<select value={form.mutationMode} onChange={(event) => setForm({ ...form, mutationMode: event.target.value })}>{mutationModes.map((mode) => <option key={mode}>{mode}</option>)}</select></label>
      </div>
      <div className="button-row">
        <ActionButton idleLabel="Generate Fake LMAX EOD Reports" runningLabel="Generating..." onAction={async () => {
          const result = await onGenerateFake(form);
          setMessage(`Generated ${result.individualTradeCount} individual trades, ${result.tradeSummaryCount} summaries, ${result.currencyWalletCount} wallets.`);
        }} />
        <ActionButton idleLabel="Import Generated Reports" runningLabel="Importing..." onAction={async () => {
          const result = await onImportGenerated(form);
          setMessage(`Import ${result.status}: ${result.rowCount} rows, ${result.blockingIssueCount} blocking issues.`);
        }} />
        <ActionButton idleLabel="Run EOD Reconciliation" runningLabel="Reconciling..." onAction={async () => {
          const result = await onRunReconciliation(form);
          setMessage(`EOD reconciliation: ${result.breakCount} breaks, ${result.blockingBreakCount} blocking.`);
        }} />
        <ActionButton idleLabel="Load PnL Summary" runningLabel="Loading..." onAction={async () => {
          await onLoadPnl(form.reportDate, form.venueName, form.brokerAccountCode);
          setMessage('PnL summary loaded.');
        }} />
      </div>

      {pnlSummary && (
        <div className="kv-grid">
          <span>Total Wallet USD</span><strong>{pnlSummary.totalWalletBalanceUsd}</strong>
          <span>Total P&L USD</span><strong>{pnlSummary.totalProfitLossUsd}</strong>
          <span>Total Commission USD</span><strong>{pnlSummary.totalCommissionUsd}</strong>
          <span>Total Dividends USD</span><strong>{pnlSummary.totalDividendsUsd}</strong>
          <span>Total Financing USD</span><strong>{pnlSummary.totalFinancingUsd}</strong>
          <span>Total Net PnL USD</span><strong>{pnlSummary.totalNetPnlUsd}</strong>
        </div>
      )}

      <h3>Import Runs</h3>
      <DataTable rows={importRuns} getRowKey={(row) => row.id} columns={[
        { key: 'id', header: 'ID', render: (row) => <code>{row.id}</code> },
        { key: 'type', header: 'Type', render: (row) => row.reportType },
        { key: 'date', header: 'Date', render: (row) => row.reportDate },
        { key: 'status', header: 'Status', render: (row) => row.status },
        { key: 'rows', header: 'Rows', render: (row) => row.rowCount ?? '' },
        { key: 'message', header: 'Message', render: (row) => row.message ?? '' }
      ]} />

      <h3>Validation Issues</h3>
      <DataTable rows={validationIssues} getRowKey={(row) => row.id} columns={[
        { key: 'severity', header: 'Severity', render: (row) => <span className={`chip ${row.severity === 'Blocking' ? 'danger' : row.severity === 'Warning' ? 'warning' : 'neutral'}`}>{row.severity}</span> },
        { key: 'type', header: 'Type', render: (row) => row.issueType },
        { key: 'row', header: 'Row', render: (row) => row.rowNumber ?? '' },
        { key: 'message', header: 'Message', render: (row) => row.message }
      ]} />

      <h3>Individual Trades</h3>
      <DataTable rows={individualTrades} getRowKey={(row) => row.id} columns={[
        { key: 'exec', header: 'Execution', render: (row) => row.executionId },
        { key: 'symbol', header: 'Symbol', render: (row) => row.lmaxSymbol },
        { key: 'qty', header: 'Units', render: (row) => row.unitsBoughtSold },
        { key: 'venueQty', header: 'Contracts', render: (row) => row.tradeQuantity },
        { key: 'price', header: 'Price', render: (row) => row.tradePrice },
        { key: 'commission', header: 'Commission', render: (row) => row.totalCommission }
      ]} />

      <h3>Trade Summaries</h3>
      <DataTable rows={tradeSummaries} getRowKey={(row) => row.id} columns={[
        { key: 'symbol', header: 'Symbol', render: (row) => row.lmaxSymbol },
        { key: 'contracts', header: 'Contracts', render: (row) => row.contracts },
        { key: 'avg', header: 'Average Price', render: (row) => row.averagePrice },
        { key: 'notional', header: 'Notional', render: (row) => row.notionalValue },
        { key: 'commission', header: 'Commission', render: (row) => row.commissionFullPrecision }
      ]} />

      <h3>Currency Wallets</h3>
      <DataTable rows={currencyWallets} getRowKey={(row) => row.id} columns={[
        { key: 'ccy', header: 'CCY', render: (row) => row.currency },
        { key: 'wallet', header: 'Wallet', render: (row) => row.walletBalance },
        { key: 'rate', header: 'Rate', render: (row) => row.rateToBaseCcy },
        { key: 'walletUsd', header: 'Wallet USD', render: (row) => row.walletBalanceBaseUsd },
        { key: 'pnl', header: 'P&L', render: (row) => row.profitLoss },
        { key: 'pnlUsd', header: 'P&L USD', render: (row) => row.profitLossBaseUsd },
        { key: 'commissionUsd', header: 'Commission USD', render: (row) => row.commissionBaseUsd },
        { key: 'financingUsd', header: 'Financing USD', render: (row) => row.financingBaseUsd }
      ]} />

      <h3>EOD Reconciliation</h3>
      <DataTable rows={reconciliationRuns} getRowKey={(row) => row.id} columns={[
        { key: 'id', header: 'Run', render: (row) => <code>{row.id}</code> },
        { key: 'date', header: 'Date', render: (row) => row.reportDate },
        { key: 'blocking', header: 'Blocking', render: (row) => String(row.hasBlockingBreaks) }
      ]} />
      <DataTable rows={eodBreaks} getRowKey={(row) => row.id} columns={[
        { key: 'severity', header: 'Severity', render: (row) => <span className={`chip ${row.severity === 'Blocking' ? 'danger' : row.severity === 'Warning' ? 'warning' : 'neutral'}`}>{row.severity}</span> },
        { key: 'type', header: 'Type', render: (row) => row.type },
        { key: 'execution', header: 'Execution', render: (row) => row.brokerExecutionId ?? '' },
        { key: 'description', header: 'Description', render: (row) => row.description }
      ]} />
    </section>
  );
}

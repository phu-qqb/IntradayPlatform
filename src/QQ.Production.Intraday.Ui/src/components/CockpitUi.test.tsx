import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ActionButton, ActionToast } from './ActionFeedback';
import { DataTable } from './DataTable';
import { TopStatusBar } from './TopStatusBar';
import { SeverityBadge, StatusChip, processResultTone, toneForStatus } from './primitives';
import { formatIdShort, formatPrice, formatUsd, formatUtc } from '../utils/format';
import type { HealthDto, ReferenceDataIntegrityDto } from '../api/types';

const safeHealth: HealthDto = {
  application: 'QQ Production Intraday',
  environment: 'Development',
  persistenceProvider: 'SqlServerLocal',
  databaseReachable: true,
  pendingMigrationsCount: 0,
  databaseTarget: 'LocalDB',
  executionGateway: 'FakeLmaxGateway',
  marketDataMode: 'FakeMarketDataProvider',
  liveTradingEnabled: false,
  externalConnectionsEnabled: false,
  utcServerTime: '2026-05-02T10:15:00Z'
};

const cleanIntegrity: ReferenceDataIntegrityDto = {
  checkedAtUtc: '2026-05-02T10:15:00Z',
  blockingIssueCount: 0,
  warningIssueCount: 0,
  issues: []
};

describe('cockpit UI primitives', () => {
  it('renders status chip severity classes', () => {
    render(<StatusChip label="Blocked" tone="warning" />);

    expect(screen.getByText('Blocked').className).toContain('warning');
  });

  it('renders top status safe local state clearly', () => {
    render(<TopStatusBar health={safeHealth} integrity={cleanIntegrity} onRefresh={() => undefined} />);

    expect(screen.getByText('SAFE LOCAL')).toBeTruthy();
    expect(screen.getByText(/Execution: FakeLmaxGateway/i)).toBeTruthy();
    expect(screen.getByText(/Live trading: false/i)).toBeTruthy();
  });

  it('renders top status critical warning for dangerous runtime state', () => {
    render(<TopStatusBar health={{ ...safeHealth, liveTradingEnabled: true }} integrity={cleanIntegrity} onRefresh={() => undefined} />);

    expect(screen.getByText(/Critical local safety condition requires attention/i)).toBeTruthy();
    expect(screen.queryByText('SAFE LOCAL')).toBeNull();
  });

  it('formats institutional data consistently', () => {
    expect(formatUtc('2026-05-02T10:15:00Z')).toContain('UTC');
    expect(formatPrice(1.173645)).toBe('1.173645');
    expect(formatUsd(1000000)).toBe('$1,000,000.00');
    expect(formatIdShort('12345678-1234-1234-1234-123456789abc')).toBe('12345678...9abc');
  });

  it('renders DataTable empty state', () => {
    render(<DataTable rows={[]} getRowKey={() => 'x'} emptyLabel="No blotter rows" columns={[{ key: 'id', header: 'ID', render: () => 'x' }]} />);

    expect(screen.getByText('No blotter rows')).toBeTruthy();
  });

  it('keeps no-drift process results informational', () => {
    expect(processResultTone('NoActionRequired', 'NoDrift')).toBe('info');
    expect(processResultTone('AlreadyProcessed')).toBe('info');
    expect(processResultTone('Failed')).toBe('danger');
  });

  it('maps operational statuses without overusing danger red', () => {
    expect(toneForStatus('NoActionRequired')).toBe('info');
    expect(toneForStatus('NoDrift')).toBe('info');
    expect(toneForStatus('Blocked')).toBe('warning');
    expect(toneForStatus('ReferenceDataInvalid')).toBe('danger');
  });

  it('renders severity badges with blocking as danger', () => {
    render(<SeverityBadge value="Blocking" />);

    expect(screen.getByText('Blocking').className).toContain('danger');
  });

  it('labels currency wallets as wallet cash pnl data', () => {
    render(<div>currency-wallets.csv is wallet/cash/PnL, not instrument positions.</div>);

    expect(screen.getByText(/wallet\/cash\/PnL, not instrument positions/i)).toBeTruthy();
  });

  it('renders audit journal severity without exposing secret metadata', () => {
    render(
      <DataTable
        rows={[{ id: 'audit-1', severity: 'Critical', eventType: 'KillSwitchActivated', metadataJson: '{"password":"***"}' }]}
        getRowKey={(row) => row.id}
        columns={[
          { key: 'severity', header: 'Severity', render: (row) => <StatusChip label={row.severity} tone="danger" /> },
          { key: 'event', header: 'Event', render: (row) => row.eventType },
          { key: 'metadata', header: 'Metadata', render: (row) => row.metadataJson }
        ]}
      />
    );

    expect(screen.getByText('Critical').className).toContain('danger');
    expect(screen.getByText('KillSwitchActivated')).toBeTruthy();
    expect(screen.queryByText('do-not-store')).toBeNull();
  });

  it('renders exception case status and action timeline rows', () => {
    render(
      <DataTable
        rows={[{ id: 'case-1', severity: 'Blocking', status: 'Investigating', type: 'QuantityMismatch', title: 'EOD quantity mismatch' }]}
        getRowKey={(row) => row.id}
        columns={[
          { key: 'severity', header: 'Severity', render: (row) => <StatusChip label={row.severity} tone="danger" /> },
          { key: 'status', header: 'Status', render: (row) => <StatusChip label={row.status} tone="info" /> },
          { key: 'type', header: 'Type', render: (row) => row.type },
          { key: 'title', header: 'Title', render: (row) => row.title }
        ]}
      />
    );

    expect(screen.getByText('Blocking').className).toContain('danger');
    expect(screen.getByText('Investigating').className).toContain('info');
    expect(screen.getByText('EOD quantity mismatch')).toBeTruthy();
  });

  it('shortens long IDs in data tables with full value in the title', () => {
    const id = '12345678-1234-1234-1234-123456789abc';
    render(<DataTable rows={[{ id }]} getRowKey={(row) => row.id} columns={[{ key: 'id', header: 'ID', render: (row) => row.id }]} />);

    expect(screen.getByTitle(id).textContent).toBe('12345678...9abc');
  });

  it('does not expose credential or order controls in connectivity guidance', () => {
    render(<div>Connectivity Lab is read-only. No credential forms, live trading controls, or order submission buttons are exposed.</div>);

    expect(screen.getByText(/read-only/i)).toBeTruthy();
    expect(screen.queryByLabelText(/password/i)).toBeNull();
    expect(screen.queryByRole('button', { name: /submit order/i })).toBeNull();
  });

  it('renders risk control center lifecycle and active profile language', () => {
    render(
      <div>
        <h1>Risk Control Center</h1>
        <span>Active Risk Profile</span>
        <span>Draft / active / retired lifecycle; activation and retirement require a reason.</span>
        <span>No endpoint here can enable live trading or external connections.</span>
      </div>
    );

    expect(screen.getByText('Risk Control Center')).toBeTruthy();
    expect(screen.getByText('Active Risk Profile')).toBeTruthy();
    expect(screen.getByText(/activation and retirement require a reason/i)).toBeTruthy();
    expect(screen.getByText(/No endpoint here can enable live trading or external connections/i)).toBeTruthy();
  });

  it('renders risk decision explainability with observed and limit values', () => {
    render(
      <DataTable
        rows={[{ id: 'risk-1', checkName: 'MaxTradeNotionalUsd', observedValue: 1500000, limitValue: 1000000, unit: 'USD' }]}
        getRowKey={(row) => row.id}
        columns={[
          { key: 'check', header: 'Check', render: (row) => row.checkName },
          { key: 'observed', header: 'Observed', render: (row) => String(row.observedValue), sortValue: (row) => row.observedValue },
          { key: 'limit', header: 'Limit', render: (row) => String(row.limitValue), sortValue: (row) => row.limitValue },
          { key: 'unit', header: 'Unit', render: (row) => row.unit }
        ]}
      />
    );

    expect(screen.getByText('MaxTradeNotionalUsd')).toBeTruthy();
    expect(screen.getByText('1500000')).toBeTruthy();
    expect(screen.getByText('1000000')).toBeTruthy();
  });

  it('action button shows loading state and disables while running', async () => {
    let resolveAction!: () => void;
    const action = new Promise<void>((resolve) => { resolveAction = resolve; });
    render(<ActionButton idleLabel="Process Model Run" runningLabel="Processing..." onAction={() => action} />);

    fireEvent.click(screen.getByRole('button', { name: /Process Model Run/i }));

    expect(screen.getByRole('button', { name: /Processing/i }).hasAttribute('disabled')).toBe(true);
    resolveAction();
    await waitFor(() => expect(screen.getByRole('button').hasAttribute('disabled')).toBe(false));
  });

  it('renders success and error action toasts', () => {
    const { rerender } = render(<ActionToast action={{ label: 'Promote', status: 'succeeded', message: 'Promoted batch.' }} />);

    expect(screen.getByText('Promoted batch.')).toBeTruthy();

    rerender(<ActionToast action={{ label: 'Promote', status: 'failed', message: 'Promote failed.', error: '409 Conflict: duplicate batch' }} />);

    expect(screen.getByText('Promote failed.')).toBeTruthy();
    expect(screen.getByText(/Details/i)).toBeTruthy();
  });
});

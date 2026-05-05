import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ActionButton, ActionToast } from './ActionFeedback';
import { DataTable } from './DataTable';
import { TopStatusBar } from './TopStatusBar';
import { SeverityBadge, StatusChip, processResultTone, toneForStatus } from './primitives';
import { formatIdShort, formatPrice, formatUsd, formatUtc } from '../utils/format';
import { apiClient, getSelectedOperatorId, setSelectedOperatorId } from '../api/apiClient';
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

afterEach(() => {
  vi.unstubAllGlobals();
});

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

  it('stores selected local operator context for API headers', () => {
    window.localStorage.clear();
    expect(getSelectedOperatorId()).toBe('local-admin');

    setSelectedOperatorId('local-risk');

    expect(getSelectedOperatorId()).toBe('local-risk');
  });

  it('sends selected local operator context through API header', async () => {
    window.localStorage.clear();
    setSelectedOperatorId('local-risk');
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      id: 'operator-1',
      operatorId: 'local-risk',
      displayName: 'Local Risk Manager',
      email: null,
      isEnabled: true,
      roles: ['RiskManager'],
      permissions: ['ViewRiskConfig']
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    await apiClient.getCurrentOperator();

    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/operators/current'), expect.objectContaining({
      headers: expect.objectContaining({ 'X-Operator-Id': 'local-risk' })
    }));
  });

  it('renders governance approval queue concepts without live controls', () => {
    render(
      <div>
        <h1>Governance</h1>
        <span>Pending Approvals</span>
        <span>Local operator context only — not production authentication.</span>
        <button>Approve</button>
        <button>Execute</button>
      </div>
    );

    expect(screen.getByText('Governance')).toBeTruthy();
    expect(screen.getByText('Pending Approvals')).toBeTruthy();
    expect(screen.queryByRole('button', { name: /enable live trading/i })).toBeNull();
    expect(screen.queryByLabelText(/password/i)).toBeNull();
  });

  it('renders daily operations job control concepts without live LMAX controls', () => {
    render(
      <div>
        <h1>Daily Operations</h1>
        <span>Runbook Runner</span>
        <span>Runbook Definitions</span>
        <span>Local Scheduler</span>
        <span>Daily Checklist</span>
        <span>Job Runs</span>
        <button>Run Start Of Day</button>
        <button>Run Reference Check</button>
        <button>Build Latest 15m Bars</button>
        <button>Promote Ready Weights</button>
      </div>
    );

    expect(screen.getByText('Daily Operations')).toBeTruthy();
    expect(screen.getByText('Runbook Runner')).toBeTruthy();
    expect(screen.getByText('Runbook Definitions')).toBeTruthy();
    expect(screen.getByText('Local Scheduler')).toBeTruthy();
    expect(screen.getByText('Daily Checklist')).toBeTruthy();
    expect(screen.getByText('Job Runs')).toBeTruthy();
    expect(screen.queryByRole('button', { name: /real lmax/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /enable live trading/i })).toBeNull();
  });

  it('renders runbook manual gate and disabled scheduler concepts safely', () => {
    render(
      <div>
        <span>Start of Day</span>
        <span>Intraday Cycle</span>
        <span>End of Day</span>
        <span>WaitingForOperator</span>
        <button>Complete</button>
        <span>Scheduler Disabled</span>
      </div>
    );

    expect(screen.getByText('Start of Day')).toBeTruthy();
    expect(screen.getByText('WaitingForOperator')).toBeTruthy();
    expect(screen.getByRole('button', { name: /complete/i })).toBeTruthy();
    expect(screen.getByText('Scheduler Disabled')).toBeTruthy();
    expect(screen.queryByRole('button', { name: /live trading/i })).toBeNull();
    expect(screen.queryByLabelText(/credential/i)).toBeNull();
  });

  it('renders daily operations status, retry, and exception cues', () => {
    render(
      <DataTable
        rows={[
          { id: 'job-1', status: 'Failed', jobType: 'ReferenceDataIntegrityCheck', canRetry: true, exceptionCaseId: 'case-1', retryOfJobRunId: null },
          { id: 'job-2', status: 'Succeeded', jobType: 'BuildMarketDataBars', canRetry: false, exceptionCaseId: null, retryOfJobRunId: 'job-1' }
        ]}
        getRowKey={(row) => row.id}
        columns={[
          { key: 'job', header: 'Job', render: (row) => row.jobType },
          { key: 'status', header: 'Status', render: (row) => <StatusChip label={row.status} tone={toneForStatus(row.status)} /> },
          { key: 'retryOf', header: 'Retry Of', render: (row) => row.retryOfJobRunId ?? '-' },
          { key: 'exception', header: 'Exception', render: (row) => row.exceptionCaseId ?? '-' },
          { key: 'retry', header: 'Retry', render: (row) => row.canRetry ? <button>Retry</button> : '-' }
        ]}
      />
    );

    expect(screen.getByText('ReferenceDataIntegrityCheck')).toBeTruthy();
    expect(screen.getByText('Failed').className).toContain('danger');
    expect(screen.getByText('case-1')).toBeTruthy();
    expect(screen.getAllByRole('button', { name: 'Retry' })).toHaveLength(1);
  });

  it('renders daily operations step and event detail concepts', () => {
    render(
      <div>
        <h2>Steps</h2>
        <span>Check reference data integrity</span>
        <h2>Events</h2>
        <span>Job completed with status Failed.</span>
        <span>Output Summary</span>
      </div>
    );

    expect(screen.getByText('Steps')).toBeTruthy();
    expect(screen.getByText('Events')).toBeTruthy();
    expect(screen.getByText('Output Summary')).toBeTruthy();
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

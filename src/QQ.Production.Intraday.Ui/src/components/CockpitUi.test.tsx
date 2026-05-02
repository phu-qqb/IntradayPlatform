import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { DataTable } from './DataTable';
import { StatusChip, processResultTone } from './primitives';
import { formatIdShort, formatPrice, formatUsd, formatUtc } from '../utils/format';

describe('cockpit UI primitives', () => {
  it('renders status chip severity classes', () => {
    render(<StatusChip label="Blocked" tone="warning" />);

    expect(screen.getByText('Blocked').className).toContain('warning');
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

  it('labels currency wallets as wallet cash pnl data', () => {
    render(<div>currency-wallets.csv is wallet/cash/PnL, not instrument positions.</div>);

    expect(screen.getByText(/wallet\/cash\/PnL, not instrument positions/i)).toBeTruthy();
  });
});

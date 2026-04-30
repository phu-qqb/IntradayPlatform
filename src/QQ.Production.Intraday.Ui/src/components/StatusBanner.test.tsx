import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { HealthDto, ReferenceDataIntegrityDto } from '../api/types';
import { StatusBanner } from './StatusBanner';

const safeHealth: HealthDto = {
  application: 'QQ.Production.Intraday.Api',
  environment: 'Development',
  persistenceProvider: 'SqlServerLocal',
  databaseReachable: true,
  pendingMigrationsCount: 0,
  databaseTarget: 'LocalDB',
  executionGateway: 'FakeLmaxGateway',
  marketDataMode: 'FakeMarketDataProvider',
  liveTradingEnabled: false,
  externalConnectionsEnabled: false,
  utcServerTime: '2026-04-30T12:00:00Z'
};

const cleanIntegrity: ReferenceDataIntegrityDto = {
  checkedAtUtc: '2026-04-30T12:00:00Z',
  blockingIssueCount: 0,
  warningIssueCount: 0,
  issues: []
};

describe('StatusBanner', () => {
  it('renders the safe FakeLmax local state', () => {
    render(<StatusBanner health={safeHealth} integrity={cleanIntegrity} onRefresh={() => undefined} />);

    expect(screen.getByText('QQ Production Intraday')).toBeTruthy();
    expect(screen.getByText('Execution: FakeLmaxGateway')).toBeTruthy();
    expect(screen.getByText('Live trading: false')).toBeTruthy();
  });

  it('renders a critical warning when live trading is enabled', () => {
    render(<StatusBanner health={{ ...safeHealth, liveTradingEnabled: true }} integrity={cleanIntegrity} onRefresh={() => undefined} />);

    expect(screen.getByText('Critical local safety condition requires attention')).toBeTruthy();
  });
});

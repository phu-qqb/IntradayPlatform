export function formatUtc(value?: string | null): string {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return `${date.toISOString().replace('.000Z', 'Z')} UTC`;
}

export function formatDate(value?: string | null): string {
  if (!value) return '-';
  return value.slice(0, 10);
}

export function formatDecimal(value?: number | null, maximumFractionDigits = 6): string {
  if (value === undefined || value === null || Number.isNaN(value)) return '-';
  return new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 0,
    maximumFractionDigits
  }).format(value);
}

export function formatQuantity(value?: number | null): string {
  return formatDecimal(value, 4);
}

export function formatPrice(value?: number | null): string {
  return formatDecimal(value, 6);
}

export function formatUsd(value?: number | null): string {
  if (value === undefined || value === null || Number.isNaN(value)) return '-';
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(value);
}

export function formatPercent(value?: number | null): string {
  if (value === undefined || value === null || Number.isNaN(value)) return '-';
  return `${formatDecimal(value * 100, 4)}%`;
}

export function formatIdShort(value?: string | null): string {
  if (!value) return '-';
  return value.length <= 12 ? value : `${value.slice(0, 8)}...${value.slice(-4)}`;
}

export function formatStatus(value?: string | null): string {
  if (!value) return '-';
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}

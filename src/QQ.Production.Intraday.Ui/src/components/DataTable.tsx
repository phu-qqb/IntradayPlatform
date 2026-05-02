import React from 'react';

type Column<T> = {
  key: string;
  header: string;
  render: (row: T) => React.ReactNode;
  sortValue?: (row: T) => string | number | boolean | null | undefined;
  className?: string;
};

type DataTableProps<T> = {
  rows: T[];
  columns: Column<T>[];
  getRowKey: (row: T, index: number) => string;
  emptyLabel?: string;
  loading?: boolean;
  error?: string;
  filterPlaceholder?: string;
  onRowClick?: (row: T) => void;
};

export function DataTable<T>({ rows, columns, getRowKey, emptyLabel = 'No rows', loading, error, filterPlaceholder = 'Filter rows', onRowClick }: DataTableProps<T>) {
  const [filter, setFilter] = React.useState('');
  const [sortKey, setSortKey] = React.useState<string>();
  const [sortDirection, setSortDirection] = React.useState<'asc' | 'desc'>('asc');

  if (loading) {
    return <div className="loading-state">Loading rows</div>;
  }

  if (error) {
    return <div className="error-state">{error}</div>;
  }

  const filtered = filter
    ? rows.filter((row) => JSON.stringify(row).toLowerCase().includes(filter.toLowerCase()))
    : rows;

  const sortColumn = columns.find((column) => column.key === sortKey);
  const displayRows = sortColumn?.sortValue
    ? [...filtered].sort((a, b) => {
        const left = sortColumn.sortValue?.(a);
        const right = sortColumn.sortValue?.(b);
        const compare = String(left ?? '').localeCompare(String(right ?? ''), undefined, { numeric: true });
        return sortDirection === 'asc' ? compare : -compare;
      })
    : filtered;

  return (
    <div className="data-table-shell">
      <input className="table-filter" value={filter} onChange={(event) => setFilter(event.target.value)} placeholder={filterPlaceholder} aria-label={filterPlaceholder} />
      {displayRows.length === 0 ? (
        <div className="empty-state">{emptyLabel}</div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                {columns.map((column) => (
                  <th key={column.key}>
                    {column.sortValue ? (
                      <button
                        className="table-sort"
                        onClick={() => {
                          setSortDirection(sortKey === column.key && sortDirection === 'asc' ? 'desc' : 'asc');
                          setSortKey(column.key);
                        }}
                      >
                        {column.header}{sortKey === column.key ? (sortDirection === 'asc' ? ' ▲' : ' ▼') : ''}
                      </button>
                    ) : column.header}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {displayRows.map((row, index) => (
                <tr key={getRowKey(row, index)} onClick={() => onRowClick?.(row)} className={onRowClick ? 'clickable-row' : undefined}>
                  {columns.map((column) => (
                    <td key={column.key} className={column.className}>
                      {column.render(row)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

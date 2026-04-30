export function EmptyState({ label = 'No data yet' }: { label?: string }) {
  return <div className="empty-state">{label}</div>;
}

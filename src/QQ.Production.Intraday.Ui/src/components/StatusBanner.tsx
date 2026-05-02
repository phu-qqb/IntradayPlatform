import type { HealthDto, ReferenceDataIntegrityDto } from '../api/types';
import { TopStatusBar } from './TopStatusBar';

export function StatusBanner({ health, integrity, onRefresh }: { health?: HealthDto; integrity?: ReferenceDataIntegrityDto; onRefresh: () => void }) {
  return <TopStatusBar health={health} integrity={integrity} onRefresh={onRefresh} />;
}

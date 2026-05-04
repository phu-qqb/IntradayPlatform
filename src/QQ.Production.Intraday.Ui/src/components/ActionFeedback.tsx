import React from 'react';
import type { Tone } from './primitives';

export type ActionStatus = 'idle' | 'running' | 'succeeded' | 'failed' | 'cancelled' | 'timedOut';

export type ActionState = {
  label: string;
  status: ActionStatus;
  startedAt?: number;
  completedAt?: number;
  message?: string;
  error?: string;
};

export function formatActionError(error: unknown) {
  if (error instanceof Error) return error.message;
  return String(error);
}

export function useAsyncAction() {
  const [action, setAction] = React.useState<ActionState>({ label: '', status: 'idle' });
  const [elapsedSeconds, setElapsedSeconds] = React.useState(0);

  React.useEffect(() => {
    if (action.status !== 'running' || !action.startedAt) return undefined;
    const timer = window.setInterval(() => setElapsedSeconds(Math.floor((Date.now() - action.startedAt!) / 1000)), 500);
    return () => window.clearInterval(timer);
  }, [action]);

  const runAction = React.useCallback(async <T,>(label: string, work: () => Promise<T>, successMessage?: (result: T) => string | undefined): Promise<T> => {
    const startedAt = Date.now();
    setElapsedSeconds(0);
    setAction({ label, status: 'running', startedAt, message: `${label}...` });
    try {
      const result = await work();
      const completedAt = Date.now();
      setAction({
        label,
        status: 'succeeded',
        startedAt,
        completedAt,
        message: successMessage?.(result) ?? `${label} succeeded in ${((completedAt - startedAt) / 1000).toFixed(1)}s.`
      });
      return result;
    } catch (error) {
      const completedAt = Date.now();
      setAction({
        label,
        status: 'failed',
        startedAt,
        completedAt,
        message: `${label} failed after ${((completedAt - startedAt) / 1000).toFixed(1)}s.`,
        error: formatActionError(error)
      });
      throw error;
    }
  }, []);

  return { action, elapsedSeconds, runAction, clearAction: () => setAction({ label: '', status: 'idle' }) };
}

export function ActionToast({ action, elapsedSeconds = 0, onClear }: { action: ActionState; elapsedSeconds?: number; onClear?: () => void }) {
  if (action.status === 'idle') return null;
  const tone: Tone = action.status === 'failed' ? 'danger' : action.status === 'running' ? 'info' : 'ok';
  return (
    <div className={`action-toast ${tone}`} aria-live="polite" aria-busy={action.status === 'running'}>
      <span className={action.status === 'running' ? 'spinner' : 'status-dot'} aria-hidden="true" />
      <div>
        <strong>{action.message}</strong>
        {action.status === 'running' && elapsedSeconds >= 2 && <small>Still working... {elapsedSeconds}s elapsed.</small>}
        {action.error && <details><summary>Details</summary><pre>{action.error}</pre></details>}
      </div>
      {action.status !== 'running' && onClear && <button aria-label="Dismiss action status" onClick={onClear}>Dismiss</button>}
    </div>
  );
}

export function ActionButton({
  idleLabel,
  runningLabel,
  onAction,
  className,
  children,
  disabled,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement> & {
  idleLabel?: string;
  runningLabel?: string;
  onAction: () => Promise<unknown>;
}) {
  const [status, setStatus] = React.useState<ActionStatus>('idle');
  const [elapsedSeconds, setElapsedSeconds] = React.useState(0);
  const startedAt = React.useRef<number | undefined>(undefined);

  React.useEffect(() => {
    if (status !== 'running') return undefined;
    const timer = window.setInterval(() => {
      if (startedAt.current) setElapsedSeconds(Math.floor((Date.now() - startedAt.current) / 1000));
    }, 500);
    return () => window.clearInterval(timer);
  }, [status]);

  const label = status === 'running' ? (runningLabel ?? 'Working...') : (idleLabel ?? children);

  return (
    <button
      {...props}
      className={[className, status === 'running' ? 'is-running' : undefined].filter(Boolean).join(' ') || undefined}
      disabled={disabled || status === 'running'}
      aria-busy={status === 'running'}
      title={status === 'running' && elapsedSeconds >= 2 ? `Still working... ${elapsedSeconds}s elapsed.` : props.title}
      onClick={async (event) => {
        props.onClick?.(event);
        if (event.defaultPrevented) return;
        startedAt.current = Date.now();
        setElapsedSeconds(0);
        setStatus('running');
        try {
          await onAction();
          setStatus('succeeded');
        } catch {
          setStatus('failed');
        } finally {
          window.setTimeout(() => setStatus('idle'), 1400);
        }
      }}
    >
      {status === 'running' && <span className="spinner" aria-hidden="true" />}
      <span>{label}</span>
    </button>
  );
}

# M2C1 Operator Read-Only Capture Runbook

Status: prepared, not executed.

Before any M2C1 connection:

1. Obtain lead approval for read-only market-data capture.
2. Verify config contains only market-data endpoint alias, market-data session alias, instruments, output root, quote-age threshold, rotation and flush policies.
3. Verify no Order Entry session alias, no credentials in artifact, no AccountAPI, no Databento API, no DB apply.
4. Start with a short bounded capture window.
5. Abort on any unexpected order/cancel/account/trading-capable path.

M2C0 did not execute this runbook.

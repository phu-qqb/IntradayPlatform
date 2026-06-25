# M2C1 Kill And Stop Runbook

Stop immediately if any of these occur:

- Order entry, cancel, replace, account, or trading-capable method becomes reachable.
- A non-read-only session alias is detected.
- Any external API other than the approved read-only market-data endpoint is touched.
- Recorder health is FAILED.
- Feed state enters FAILED and cannot recover through the read-only state machine.

Stop procedure for M2C1 must close only the read-only market-data capture process. It must not attempt flat/cancel/order/account actions.

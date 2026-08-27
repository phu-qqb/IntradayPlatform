# LMAX ARCH7B first production canary

This runbook is for the first, deliberately tiny production canary only. A successful readiness check does **not** authorize an order. The sole route that can submit an order remains the separately packet-bound `ProductionAuthorizedOnce` ARCH7B lifecycle.

## GATE 0 — local qualification

Confirm the release build, focused readiness tests, ARCH7B regression, ConnectivityLab tests, and `git diff --check` are clean. Do not proceed with a dirty worktree or an unreviewed PR.

## GATE 1 — production dry run

Run the packet-bound `ProductionDryRun` only. Require its explicit zero-I/O proof: **zero network, zero DB, zero order**.

## GATE 2 — production readiness (readiness only)

With the correct fresh request JSON and the explicit `--confirm-production-readiness` flag, run:

```powershell
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- `
  fix-arch7b-production-readiness `
  --request-json "<fresh-production-request.json>" `
  --confirm-production-readiness
```

It permits only read-only PostgreSQL probes, LMAX market-data TCP/TLS/FIX logon, a read-only market-data request and BBO, then logout; and LMAX order-entry TCP/TLS/FIX logon then logout. It never sends an order. Require all readiness phases and `ReadyForProductionCanary=true`.

## GATE 3 — manual operator checks

Before generating the final canary packet, independently verify:

- the correct production account and expected instrument;
- no manual orders, no other bot/gateway/session, and the account is flat;
- no known working order;
- the kill switch is armed; and
- the manual emergency-flatten procedure is ready.

## GATE 4 — fresh final packet

Generate a **new, fresh** final `ProductionAuthorizedOnce` authorization packet after the checks above. Do not reuse the readiness evidence or an old packet.

## GATE 5 — first canary

The first production canary target quantity is **0.1 contract**. This is not the software maximum of 1.0. Use the accepted packet-bound lifecycle and its separate `--confirm-production-known-order` confirmation only after all prior gates pass.

## GATE 6 — no second order

Do not submit a second order unless the first lifecycle has reached a terminal state and the account is reconciled flat.

If status is ambiguous, stop. Do not retry. Resolve the known `ClOrdID` using the existing recovery procedure or broker UI before any further action. If flatten is not positively confirmed, stop automation and have the operator resolve the position manually under the emergency procedure.

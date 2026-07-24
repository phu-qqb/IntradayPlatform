# ARCH7B Demo known-order lifecycle runbook

Status: PREPARED, DISABLED, NOT AUTHORIZED, NOT APPLIED.

## Fixed boundaries

- TEST and LMAX Demo account `1754288005` only.
- Real account `921640160` is rejected in code and database constraints.
- The canonical ARCH7A TradeIntent carries the exact Demo account ID as `AccountScope`
  and the single classification `SHADOW_ONLY`; `TEST_ONLY` and
  `TEST_PMS_SHADOW_ONLY` are not accepted aliases.
- One deterministic GBPUSD ChildOrder (`SecurityID=4002`, `SecurityIDSource=8`).
- Opening quantity `0.1`, one opening `35=D`, one flatten `35=D` only for executed quantity.
- One residual cancel `35=F`, zero replace `35=G`, at most four known-order `35=H`.
- `ExternalOrManualOrderCoverage=UNPROVEN`; exclusivity is a bounded operator declaration plus PostgreSQL advisory lease.

## Preflight order

1. Validate exact authorization packet SHA, Demo endpoint, account and explicit CLI confirmation.
2. Acquire the Demo-account advisory lease.
3. Read the selected ARCH7A ChildOrder and its TradeIntent/RiskDecision/ParentOrder lineage from PostgreSQL.
4. Prove the exact ChildOrder-bound ARCH6F revision and its canonical `intraday_market_data_observations` row by revision, slot, ingestion, market SHA, SecurityID and LMAX lineage; never use a global-latest or legacy market snapshot fallback.
5. Bind a sequence-valid, content-addressed LMAX BBO acquired inside the authorized window to the BUY opening limit at ask and deterministic ClOrdIDs.
6. Persist the qualification run before FIX logon.

## Lifecycle

1. Arm kill switch before logon.
2. Persist each D/F/H send intent before writing bytes.
3. Persist every real `35=8` with raw message SHA, MsgSeqNum and PossDup.
4. On restart, never resend an existing D or F. Query only the known opening/flatten ClOrdID.
5. Cancel opening residual once; after the opening is terminal, require final CumQty to equal the durable sum of unique validated opening Fills.
6. Open a separate `BOUNDED_SNAPSHOT_PLUS_UPDATES_ONE_BBO_THEN_UNSUBSCRIBE` LMAX market-data session, send `263=1`, aggregate one complete sequence-valid bid/ask with no prior book state, and freeze the fresh bid used by the SELL flatten. In `finally`, attempt `263=2` with the same `MDReqID` and logout under one shared cleanup budget capped at 1,000 ms and the absolute lifecycle deadline. The unsubscribe receives at most the first half when logout is also required; timeout or error force-closes the socket before best-effort stream/socket disposal. Final cleanup metadata is immutable, sanitized and cannot invalidate an already valid BBO.
7. Persist the distinct flatten observation SHA with the FLATTEN send intent, then flatten exactly the durable executed opening quantity.
8. Fail closed on unknown ClOrdID/OrderID, account/instrument mismatch, sequence gap, duplicate conflict, budget breach or deadline.
9. Build Fill and PositionLedgerEvent only from valid fill/partial-fill ExecutionReports.
10. Require both known lifecycles terminal, zero known leaves, zero ledger quantity and zero critical break.
11. Persist final reconciliation with authority `LMAX_FIX_EXECUTION_REPORTS_KNOWN_ORDERS`.
12. Attempt FIX Logout on every post-logon exit, including fail-closed exits, then dispose stream/socket and release the lease without masking the original blocker.

## Recovery

Each connection receives a unique persisted FIX-session instance ID. Recovery reuses the qualification run, packet, ClOrdIDs, send ledger, observation IDs, ExecutionReports and durable deduplicated Fills. A stale source cannot authorize a new opening. Existing D/F intents are never resent; only known-order status is queried.


## No fresh flatten BBO

The runner keeps the order-entry lifecycle bounded while attempting at most three separate five-second `SnapshotPlusUpdates` LMAX market-data sessions and never passes the global deadline. Each attempt has a unique `MDReqID`, starts after opening terminality and requires both sides for GBPUSD/4002. Freshness is evaluated against the real evaluation clock (`nowUtc - observedAtUtc <= 5 seconds`), not elapsed time since opening terminality, so attempts two and three remain effective. After a BBO is frozen, unsubscribe and logout share one cancellation budget of at most 1,000 ms and never more than the remaining lifecycle time; when no time remains, no cleanup write starts and the socket is closed immediately. Every started write is awaited, with no fire-and-forget task. If no valid new BBO is obtained, it records `KILL_SWITCH_ACTIVATED_FLATTEN_BBO_UNAVAILABLE`, builds no flatten order, never uses Polygon or the opening observation, and exits fail-closed with the non-flat position explicitly unresolved. Operator recovery must use the same known-order run and broker evidence; it must not create a silent second flatten.

## Current non-actions

No PostgreSQL connection or migration apply, no FIX logon, no secret read, no Demo order, no real-account access and no production mutation were performed while preparing this branch.

## Fresh PMS slot handoff

Before any future market-data capture, start the ARCH7B `prearm-and-import`
mode and require both the per-slot owner lock and `importer.armed.json`. The
capture starter must call `assert-prearmed`; failure means no capture and no FIX.

The canonical slot finalizer calls `publish-ready` immediately after the raw
artifact and `slot_manifest.json` are closed and their indispensable SHA-256
values are available. Publication is temp-file, write-through flush, then
atomic rename. Reports, ZIPs, tests, history scans and evidence copies occur
only after import starts.

The prearmed importer uses a filesystem watcher plus a 100 ms fallback poll
with no initial sleep. Internal SLOs are marker publication within 10 seconds,
detection within 2 seconds and first import PostgreSQL connection within 30
seconds of close. The absolute 300-second boundary is unchanged. There is no
retry and no automatic second capture. A stale, early or conflicting marker
fails closed.

## PostgreSQL target configuration

The same handoff binary supports one explicitly configured PostgreSQL TEST
target per run: local loopback or remote TLS. The connection secret remains
`QQ_PMS_SHADOW_ARCH7B_CONNECTION_STRING`. Required non-secret arguments are
`--target-profile-id`, `--expected-environment`, `--expected-database`,
`--expected-postgres-major`, `--allow-loopback`, and `--require-tls`;
`--expected-schema` defaults to the canonical `pms_shadow`.

Loopback requires `--allow-loopback true`. Remote PostgreSQL requires
`--require-tls true`, `SslMode=VerifyFull`, and
`TrustServerCertificate=false`. There is no local fallback, second target,
dual-write or arbitrary PostgreSQL 16 pin.

`assert-prearmed` and `publish-ready` require the non-secret target profile and
read the fingerprint directly from the canonical `importer.armed.json`. The
requested profile must match the armed profile, and the marker is bound to both
values. Never pass or print the connection string, username, password, token or
private certificate.

## NEXT REQUIRED OPERATION BEFORE LIVE ARCH7B

Before another fresh slot is consumed: inventory the authoritative local
database; verify the provisioned RDS server; select an isolated RDS TEST
database; migrate schema and append-only facts; prove local-to-RDS parity;
perform a historical read-only/no-order dry run; and declare RDS as the one
qualification target. This PR does not connect to AWS or PostgreSQL, migrate,
restore, capture market data, log on to FIX, or send an order.

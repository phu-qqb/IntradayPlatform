# Historical official-report recovery (Demo only)

This deliberately narrow tool recovers one historical three-fill entry plus one
manual closing execution, after matching a pinned authentic Individual Trades
CSV to its previously imported rows, original order chain and intact send journal.
It runs only as Administrator on EC2AMAZ-1QPHTD8 against the existing Demo LocalDB.
It contains no broker, credential, schema migration or session-start operation.

`plan INPUT_JSON` writes an immutable private plan without committing database
changes. The input contains `Request` (the application recovery-request record),
`ReportPath`, `JournalPath` and a new `PlanPath`. Inputs, reports and receipts stay
under `D:\data\lmax-eod`; the journal stays under its existing account directory.
Use the recorded owner authorization in #84, exact original execution/order IDs,
and hashes of genuine files. Never manufacture or refresh an observation.

`verify PLAN_JSON PLAN_SHA256 NEW_RECEIPT_JSON` executes the complete SQL recovery
and reconciliation under an exclusive owner lease and serializable transaction,
then rolls it back. `apply` with the same arguments commits only if the reviewed
plan is still current (less than 15 minutes old), readback matches and the existing
EOD reconciler finds no breaks. Repeating an applied plan verifies its durable
audit and existing records, without inserting duplicates.

For the first qualified run only, `correct-audit` with the same reviewed-plan
arguments recognizes the exact EF before-object mutation defect and appends a
separate correction audit. It cannot change economic rows or replace the original
audit. Any other discrepancy is rejected. Future applies attach only after-state
records and verify the original plan hash again before creating the audit.

The original child retains its internal ID but receives the authentic physical
ClOrdID and Limit/GFD properties from the send journal. The original model is
marked `RecoveredFromOfficialReport` and processed to prevent replay. A separate
terminal `ExternalExecutionBooked` intent and `ExternalManual` parent represent
the already executed portal close; their association with the historical model
is bookkeeping, not a model target, strategy submission or risk approval. Unknown
manual TIF remains `Unknown`. Existing targets and risk decisions are retained.

Four recovered fills and signed position-ledger events use actual execution IDs,
quantities, prices and event times; recovery time and complete before/after state
are separately audited as `OfficialExecutionRecovered`. No FIX ExecutionReports
are synthesized. Matching older open missing-fill breaks receive audited Resolved
status; their historical runs and original evidence remain intact.

**Provenance limitation:** a successful reconciliation after this recovery proves
that internal accounting now matches the official report it was recovered from.
It is not independent FIX confirmation. Keep this distinction in downstream
reports. Journal and owner-lock bytes remain unchanged. The recovery commands alone neither retire the faulted actual-send session nor
prove the account currently flat/no-orders. The separate retirement command below,
qualified FIX and a genuine fresh official account observation remain mandatory
before a new trading session.

## Exact recovered-send retirement

`inspect-retirement PLAN_JSON PLAN_SHA256` verifies the exact durable recovery,
its before/after audit and current internal flat/no-open-child/no-break state under
an exclusive owner lease and read transaction. It does not retire the session.

`retire-session PLAN_JSON PLAN_SHA256 OBSERVATION_JSON` requires that same evidence
plus a retained authentic official UI inspection of Demo account 1754288005, less
than 900 seconds old, explicitly showing flat/no working orders. The JSON bundle
contains `Observation` (the existing `LmaxDemoRetirementObservation` record),
`CapturePath`, `CaptureSha256` and `SourceUrl`. Capture paths stay under
`D:\data\lmax-demo-ui`; origin is `https://web-order.london-demo.lmax.com`.
The capture timestamp must equal the observation timestamp. Never create a positive
capture by copying, changing or retimestamping an old inspection.

The command writes one immutable `.recovered-send-retirement.json` sidecar for the
exact 17 September journal. Journal bytes, owner.lock, economic facts and FIX
reports remain untouched. The sidecar is verified during subsequent ownership
checks; it retires the old incident, not future account-state checks.

## Full native USD reference configuration

`verify-reference OFFICIAL_CSV NEW_RECEIPT_JSON` applies and checks the candidate
inside a serializable transaction, then rolls back. `apply-reference` uses the same
arguments and commits only for the pinned genuine LD4 CSV, exact Demo owner/account,
reconciled internal state and existing unchanged QQ risk policy. It creates missing
native instruments/mappings/report aliases and binds the existing conservative
EURUSD risk template to the other native legs. It never increases risk ceilings.

Published minimum order is 0.1 contracts and contract size is 10,000 for all 14 legs.
QQ deliberately uses one published minimum lot as its conservative order quantum;
the source CSV does not independently publish a quantity-increment column.
USDJPY and USDHUF tick size is 0.001; the others are 0.00001. Source URL/hash and
before/after changes are retained in the audit and immutable operator receipt.
These commands cannot start trading, obtain broker-account observations or change
commercial fee terms.

## Dated owner confirmation — 18 September 2026 only

Philippe supplied current flat/no-working-orders confirmation and requested today's
start in #84 comment 5730268654. The explicit `OWNER_CONFIRMED_ACCOUNT_STATE` source
binds that exact quote, account, reference and fixed declaration recording time
12:52:03.0232487Z to the authentic retained opening report receipt. It is not an
automated UI/broker observation. Existing UI evidence remains a separate path.

The recording time is never refreshed: the same 900-second age check applies at
retirement and startup. Report dates stay unchanged. Fresh recovery/readback,
exclusive ownership, exact journal hash, no internal orders/positions/breaks and
all normal trading controls remain mandatory. This dated owner source is not a
new daily unattended attestation or a generic way to manufacture broker evidence.
The startup journal retains observation source, evidence path and SHA256.

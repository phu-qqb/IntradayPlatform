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
reports. Journal and owner-lock bytes remain unchanged. This tool neither retires
the faulted actual-send session nor proves the account currently flat/no-orders;
session resolution, qualified FIX and a genuine fresh official account observation
remain mandatory before a new trading session.

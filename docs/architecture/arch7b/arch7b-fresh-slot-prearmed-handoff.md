# ARCH7B prearmed fresh-slot import handoff

Status: corrective implementation, TEST-only, no migration, no live operation.

## Root cause

The recurrent failure is classified `ORCHESTRATOR_NOT_PREARMED`, with
`MANUAL_OPERATOR_SEQUENCE` as a contributing cause.

- The 16:30 failure finalized capture at +44.1576621 s, but the connection label
  was not armed and the observed worker action occurred at +332.224858 s.
- The successful 18:00 run started its orchestrator before close, completed the
  stitch at +3.2169945 s and invoked the worker at +3.2199951 s.
- The 12:00 failure finalized the recorder at 11:59:52.0236779Z, before close.
  Worker creation was only requested at 12:03:56.6805892Z and again at
  12:05:02.5611575Z. Both worker logs are empty, so their actual starts are
  unobserved. PostgreSQL classified the slot at +319.257775 s.

Recorder cleanup, essential hashing, process start delay and PostgreSQL
connection delay are not proven primary causes for the 12:00 failure. The
worker, connection and transaction timestamps did not exist on the critical
path because no importer was prearmed.

The source ZIP
`ARCH7B_NO_GO_FRESH_PMS_SOURCE_UNAVAILABLE_20260724T1145Z.zip` was verified as
SHA-256
`f856ec609388c2f1d16f0c1a1518b0aa4aa5e075585902462304b5ab68b97776`.

## Operational contract

The existing `Arch6fEconomicReplay` tool exposes three explicit ARCH7B modes:

1. `prearm-and-import` validates the loopback TEST database using a connection
   string resolved only from `QQ_PMS_SHADOW_ARCH7B_CONNECTION_STRING`, acquires
   the per-slot owner lock, writes `importer.armed.json`, and blocks on a
   `FileSystemWatcher` with a 100 ms fallback poll.
2. `assert-prearmed` is called by the capture starter. It rejects capture unless
   the owner lock and matching armed state exist before close and no ready
   marker already exists.
3. `publish-ready` is called directly by the canonical slot finalizer. It hashes
   the closed artifact and `slot_manifest.json`, writes a temporary marker with
   write-through flush, and atomically renames it to `capture.ready.json`.

The import process is already alive when capture starts. No operator report,
ZIP, historical inventory, test run, or full evidence copy is permitted between
artifact finalization and marker publication. Cleanup that is not required for
artifact integrity runs after publication and cannot block import.

## Ready marker

Contract version: `pms_shadow_fresh_slot_handoff_v1`.

Required fields:

- slot ID and contractual close UTC;
- source session ID;
- logical artifact path;
- artifact and manifest SHA-256;
- creation UTC and creator PID;
- full repository commit;
- `environment=TEST`;
- `no_order=true`.

The marker is slot-specific and validates both files byte-for-byte. An identical
second publish is idempotent. A conflicting marker, stale marker, pre-close
marker, SHA mismatch, source mismatch, commit mismatch, or non-TEST/non-no-order
marker fails closed.

## Ownership and deadlines

- One `orchestrator.owner.lock` per slot, opened with create-new semantics.
- A second importer is rejected before preflight or import.
- `import.completed.json` makes an identical replay canonically idempotent.
- Armed state and owner lock are released on success, failure, or cancellation.
- Ready marker, completion result, and timeline events remain as evidence.
- Absolute scheduler boundary remains exactly 300 seconds after close:
  299.999 s and 300.000 s are inside; values greater than 300 s are rejected.
- Internal SLOs are 10 s for marker publication, 2 s for detection, and 30 s
  for the first import PostgreSQL connection.

No retry and no automatic second capture exist.

## Timeline

Each event is an immutable JSON file under `timeline-events`. Filenames combine
UTC ticks, PID, process sequence and a GUID, so separate publisher and importer
processes append without rewriting a shared file. Every event includes UTC,
process-monotonic elapsed milliseconds, PID, managed thread, slot ID, run ID,
optional artifact SHA, and sanitized detail.

Events cover preflight, watcher prearm, capture start, slot close, artifact
finalization, marker publish/detect, import start, actual PostgreSQL connection
and transaction start, classification, completion/failure, and cleanup.

## Safety

This correction adds no migration and changes no entity or economic model. It
does not open FIX, send an order, create a Fill or PositionLedgerEvent, access a
real account, use Polygon or Databento, or mutate production.

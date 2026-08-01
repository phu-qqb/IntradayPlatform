# ARCH7B offline A-to-K lineage forensic

## Verdict

`NO_GO_ARCH7B_OFFLINE_REHEARSAL_ECONOMIC_LINEAGE_BREAK`

The first broken transition is `E -> F`.

## Proven chain

A through E are integrated by the offline bridge. The real Core final index and bracket contract are read without rewriting, the consumer produces `99/99/0`, the real import planner returns `+1/+99`, and the runtime selector chooses snapshot `c5f4d0d9-1912-c4ef-c8c9-61d0401caef7`.

That selected snapshot binds source session `arch6b-daily-tier1-20260721T130346Z-422530a8` and required-universe SHA `f538619ed5c5f49155aa23ab240f26c9cbf4dbe10352cd7ba68aea7cd783d5b4`. Its universe contains 99 real FX pairs.

## First break

The versioned F/G fixture in `Arch7bSlotBoundedBboSelectionTests.cs` is a standalone slot fixture for `pms-shadow-15m-20260724T1030Z`. It requires `GBPUSD` plus 48 synthetic `X00001` through `X00048` symbols.

It carries none of E's selected snapshot ID, source session ID, required-universe SHA, or bridge evidence SHA. Only `GBPUSD` is common to the two symbol sets. Treating it as E's next economic input would therefore invent a mapping and a lineage edge.

## Consequence

F/G, H, I, J, and K retain their independent versioned coverage. K is independently complete after the exact status inventory correction. They are not presented as one integrated A-to-K economic chain.

The ten full rehearsals are not executed because the packet makes them conditional on complete compatibility. Run count is `0`, not a partial success.

No secret, database, live slot, LMAX, FIX, order, or AWS operation was used.
